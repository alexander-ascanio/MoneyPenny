using Microsoft.EntityFrameworkCore;
using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Data;

/// <summary>
/// Contexto de solo lectura sobre TeamSupport (Azure: tickets, ticket_actions).
/// No usar SaveChanges ni migraciones sobre esta base de datos.
/// Excepción única: TicketIngestController (POST /api/tickets) inserta ticket + acción.
/// </summary>
public class TicketsDbContext : DbContext
{
    public TicketsDbContext(DbContextOptions<TicketsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketAction> TicketActions => Set<TicketAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("tickets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasColumnName("Id");
            entity.Property(t => t.Number).HasColumnName("TicketNumber").HasMaxLength(50);
            entity.Property(t => t.Title).HasColumnName("Subject").HasMaxLength(500);
            entity.Property(t => t.Description).HasColumnName("Description");
            entity.Property(t => t.Status).HasColumnName("Status").HasMaxLength(50);
            entity.Property(t => t.Priority).HasColumnName("Priority").HasMaxLength(50);
            entity.Property(t => t.Type).HasColumnName("Type");
            entity.Property(t => t.Source).HasColumnName("Source");
            entity.Property(t => t.IsClosed).HasColumnName("IsClosed");
            entity.Property(t => t.Customer).HasColumnName("CustomerName");
            entity.Property(t => t.Contacts).HasColumnName("Contacts");
            entity.Property(t => t.TeamSupportId).HasColumnName("TeamSupportId").HasMaxLength(50);
            entity.Property(t => t.CodigoTelegestion).HasColumnName("CodigoTelegestion").HasMaxLength(50);
            entity.Property(t => t.Group).HasColumnName("GroupName");
            entity.Property(t => t.Product).HasColumnName("ProductName");
            entity.Property(t => t.IsKnowledgeBase).HasColumnName("IsKnowledgeBase");
            entity.Property(t => t.Assignee).HasColumnName("AssignedToName");
            entity.Property(t => t.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(t => t.UpdatedAt).HasColumnName("UpdatedAt");
        });

        modelBuilder.Entity<TicketAction>(entity =>
        {
            entity.ToTable("ticket_actions");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).HasColumnName("Id");
            entity.Property(a => a.TeamSupportActionId).HasColumnName("TeamSupportActionId");
            entity.Property(a => a.TicketId).HasColumnName("TicketId");
            entity.Property(a => a.ActionType).HasColumnName("ActionType");
            entity.Property(a => a.Content).HasColumnName("Content");
            entity.Property(a => a.CreatedByName).HasColumnName("CreatedByName");
            entity.Property(a => a.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(a => a.TimeSpentMinutes).HasColumnName("TimeSpentMinutes");
            entity.Property(a => a.TicketStatus).HasColumnName("TicketStatus");
            entity.Property(a => a.Source).HasColumnName("Source");
            entity.Property(a => a.ModifierName).HasColumnName("ModifierName");
            entity.Property(a => a.IsVisible).HasColumnName("IsVisible");
            entity.Property(a => a.AssignedUsername).HasColumnName("AssignedUsername");
            entity.Property(a => a.ModifiedAt).HasColumnName("ModifiedAt");
        });
    }
}
