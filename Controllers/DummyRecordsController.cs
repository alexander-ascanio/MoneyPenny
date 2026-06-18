using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Models;

namespace MoneyPenny.Controllers;

[Authorize]
public class DummyRecordsController : Controller
{
    private readonly ApplicationDbContext _context;

    public DummyRecordsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string search, string filter)
    {
        var query = _context.DummyRecords.AsQueryable();

        // Aplicar búsqueda
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r => r.Name.Contains(search));
            ViewData["SearchQuery"] = search;
        }

        // Aplicar filtros
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var now = DateTime.UtcNow;
            query = filter switch
            {
                "recent" => query.Where(r => r.CreatedAt >= now.AddDays(-7)),
                "old" => query.Where(r => r.CreatedAt < now.AddMonths(-1)),
                "intermediate" => query.Where(r => r.CreatedAt < now.AddDays(-7) && r.CreatedAt >= now.AddMonths(-1)),
                _ => query
            };
        }

        var records = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return View(records);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name")] DummyRecord record)
    {
        if (ModelState.IsValid)
        {
            record.CreatedAt = DateTime.UtcNow;
            _context.Add(record);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(record);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var record = await _context.DummyRecords.FindAsync(id);
        if (record == null)
        {
            return NotFound();
        }
        return View(record);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,CreatedAt")] DummyRecord record)
    {
        if (id != record.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(record);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DummyRecordExists(record.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(record);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var record = await _context.DummyRecords.FirstOrDefaultAsync(m => m.Id == id);
        if (record == null)
        {
            return NotFound();
        }

        return View(record);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var record = await _context.DummyRecords.FindAsync(id);
        if (record != null)
        {
            _context.DummyRecords.Remove(record);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private bool DummyRecordExists(int id)
    {
        return _context.DummyRecords.Any(e => e.Id == id);
    }
}
