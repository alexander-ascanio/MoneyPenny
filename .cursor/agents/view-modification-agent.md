# Agente de Modificación de Vistas

## Descripción
Especializado en crear y modificar vistas Razor, diseñar interfaces de usuario con Bootstrap 5 y crear componentes UI reutilizables.

## 1. Estructura de Vistas en MoneyPenny

### Ubicaciones Principales
```
Views/
├── Shared/
│   ├── _Layout.cshtml          # Layout principal
│   ├── _LoginPartial.cshtml    # Menú de usuario
│   └── _ValidationScriptsPartial.cshtml
├── Home/
│   ├── Index.cshtml            # Página de inicio
│   └── Privacy.cshtml
├── [Controlador]/
│   ├── Index.cshtml            # Lista
│   ├── Create.cshtml           # Crear
│   ├── Edit.cshtml             # Editar
│   ├── Details.cshtml          # Detalles
│   └── Delete.cshtml           # Eliminar
```

## 2. Crear Nueva Vista

### Paso 1: Crear Acción en Controlador
```csharp
public class ProductsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
```

### Paso 2: Crear Archivo de Vista
Crear archivo: `Views/Products/Index.cshtml`

```html
@model IEnumerable<Product>

@{
    ViewData["Title"] = "Productos";
}

<h2>@ViewData["Title"]</h2>

<div class="mb-3">
    <a asp-action="Create" class="btn btn-primary">Nuevo Producto</a>
</div>

<table class="table table-striped">
    <thead>
        <tr>
            <th>Nombre</th>
            <th>Precio</th>
            <th>Acciones</th>
        </tr>
    </thead>
    <tbody>
        @foreach (var item in Model)
        {
            <tr>
                <td>@item.Name</td>
                <td>@item.Price.ToString("C")</td>
                <td>
                    <a asp-action="Edit" asp-route-id="@item.Id" class="btn btn-sm btn-warning">Editar</a>
                    <a asp-action="Delete" asp-route-id="@item.Id" class="btn btn-sm btn-danger">Eliminar</a>
                </td>
            </tr>
        }
    </tbody>
</table>
```

## 3. Componentes UI Comunes

### Tarjetas (Cards)
```html
<div class="card shadow">
    <div class="card-header bg-primary text-white">
        <h5 class="mb-0">Título</h5>
    </div>
    <div class="card-body">
        <p>Contenido de la tarjeta</p>
    </div>
</div>
```

### Formularios
```html
<form asp-action="Create" method="post">
    <div class="mb-3">
        <label asp-for="Name" class="form-label"></label>
        <input asp-for="Name" class="form-control" />
        <span asp-validation-for="Name" class="text-danger"></span>
    </div>
    
    <div class="mb-3">
        <label asp-for="Price" class="form-label"></label>
        <input asp-for="Price" class="form-control" type="number" step="0.01" />
        <span asp-validation-for="Price" class="text-danger"></span>
    </div>
    
    <button type="submit" class="btn btn-primary">Guardar</button>
    <a asp-action="Index" class="btn btn-secondary">Cancelar</a>
</form>

@section Scripts {
    @{await Html.RenderPartialAsync("_ValidationScriptsPartial");}
}
```

### Tablas con Búsqueda y Filtros
```html
<div class="card shadow mb-4">
    <div class="card-body">
        <form method="get" class="row g-3">
            <div class="col-md-6">
                <input type="text" name="search" class="form-control" placeholder="Buscar..." value="@ViewData["SearchQuery"]" />
            </div>
            <div class="col-md-4">
                <select name="filter" class="form-select">
                    <option value="">Todos</option>
                    <option value="active">Activos</option>
                    <option value="inactive">Inactivos</option>
                </select>
            </div>
            <div class="col-md-2">
                <button type="submit" class="btn btn-primary w-100">Buscar</button>
            </div>
        </form>
    </div>
</div>
```

### Alertas y Mensajes
```html
@if (TempData["Success"] != null)
{
    <div class="alert alert-success alert-dismissible fade show">
        <i class="bi bi-check-circle me-2"></i>
        @TempData["Success"]
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    </div>
}

@if (TempData["Error"] != null)
{
    <div class="alert alert-danger alert-dismissible fade show">
        <i class="bi bi-exclamation-triangle me-2"></i>
        @TempData["Error"]
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    </div>
}
```

### Modales
```html
<!-- Botón que abre el modal -->
<button type="button" class="btn btn-primary" data-bs-toggle="modal" data-bs-target="#myModal">
    Abrir Modal
</button>

<!-- Modal -->
<div class="modal fade" id="myModal" tabindex="-1">
    <div class="modal-dialog">
        <div class="modal-content">
            <div class="modal-header">
                <h5 class="modal-title">Título del Modal</h5>
                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
            </div>
            <div class="modal-body">
                <p>Contenido del modal</p>
            </div>
            <div class="modal-footer">
                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cerrar</button>
                <button type="button" class="btn btn-primary">Guardar</button>
            </div>
        </div>
    </div>
</div>
```

## 4. Modificar Layout Principal

### Agregar Ítem al Menú
Editar `Views/Shared/_Layout.cshtml`:

```html
<ul class="navbar-nav flex-grow-1">
    <li class="nav-item">
        <a class="nav-link text-dark" asp-controller="Products" asp-action="Index">Productos</a>
    </li>
</ul>
```

### Agregar Estilos Personalizados
En `wwwroot/css/site.css`:

```css
.custom-card {
    border-radius: 10px;
    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
}

.btn-custom {
    background-color: #3498db;
    color: white;
}
```

### Agregar Scripts Personalizados
En `wwwroot/js/site.js`:

```javascript
// Confirmar eliminación
function confirmDelete(id, name) {
    if (confirm(`¿Estás seguro de eliminar ${name}?`)) {
        document.getElementById(`deleteForm-${id}`).submit();
    }
}
```

## 5. ViewModels para Vistas

### Crear ViewModel
```csharp
public class ProductIndexViewModel
{
    public List<Product> Products { get; set; }
    public string SearchQuery { get; set; }
    public string FilterType { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}
```

### Usar en Vista
```html
@model ProductIndexViewModel

<h2>Productos (@Model.TotalCount)</h2>

@foreach (var product in Model.Products)
{
    <div>@product.Name</div>
}
```

## 6. Tag Helpers Útiles

### Enlaces
```html
<!-- Enlace a acción -->
<a asp-controller="Products" asp-action="Edit" asp-route-id="@item.Id">Editar</a>

<!-- Enlace con clase activa -->
<a asp-controller="Home" asp-action="Index" class="nav-link @(ViewContext.RouteData.Values["controller"]?.ToString() == "Home" ? "active" : "")">Home</a>
```

### Formularios
```html
<!-- Form con antiforgery automático -->
<form asp-action="Create" method="post">
    <input asp-for="Name" class="form-control" />
    <span asp-validation-for="Name" class="text-danger"></span>
</form>
```

### Imágenes
```html
<!-- Imagen desde wwwroot -->
<img src="~/images/logo.png" alt="Logo" class="img-fluid" />
```

## 7. Bootstrap 5 - Componentes Clave

### Grid System
```html
<div class="row">
    <div class="col-md-6">Columna 1</div>
    <div class="col-md-6">Columna 2</div>
</div>
```

### Botones
```html
<button class="btn btn-primary">Primario</button>
<button class="btn btn-success">Éxito</button>
<button class="btn btn-danger">Peligro</button>
<button class="btn btn-warning">Advertencia</button>
<button class="btn btn-info">Info</button>
<button class="btn btn-outline-primary">Outline</button>
```

### Badges
```html
<span class="badge bg-primary">Nuevo</span>
<span class="badge bg-success">Activo</span>
<span class="badge bg-danger">Inactivo</span>
```

### Iconos Bootstrap Icons
```html
<i class="bi bi-plus-circle"></i> Agregar
<i class="bi bi-pencil"></i> Editar
<i class="bi bi-trash"></i> Eliminar
<i class="bi bi-eye"></i> Ver
```

## 8. Paginación

### En el Controlador
```csharp
public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 10)
{
    var products = await _context.Products
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();
    
    ViewBag.PageNumber = pageNumber;
    ViewBag.TotalPages = (int)Math.Ceiling(await _context.Products.CountAsync() / (double)pageSize);
    
    return View(products);
}
```

### En la Vista
```html
<nav>
    <ul class="pagination">
        @for (int i = 1; i <= ViewBag.TotalPages; i++)
        {
            <li class="page-item @(i == ViewBag.PageNumber ? "active" : "")">
                <a class="page-link" asp-action="Index" asp-route-pageNumber="@i">@i</a>
            </li>
        }
    </ul>
</nav>
```

## 9. Validación del Lado del Cliente

### Incluir Scripts de Validación
```html
@section Scripts {
    @{await Html.RenderPartialAsync("_ValidationScriptsPartial");}
}
```

### Validación en Tiempo Real
```html
<input asp-for="Email" class="form-control" />
<span asp-validation-for="Email" class="text-danger"></span>
```

## 10. Partial Views (Vistas Parciales)

### Crear Partial View
Crear `Views/Shared/_ProductCard.cshtml`:

```html
@model Product

<div class="card">
    <div class="card-body">
        <h5 class="card-title">@Model.Name</h5>
        <p class="card-text">@Model.Price.ToString("C")</p>
    </div>
</div>
```

### Usar Partial View
```html
@foreach (var product in Model)
{
    <partial name="_ProductCard" model="product" />
}
```

## Checklist de Modificación de Vistas

- [ ] Crear acción en el controlador
- [ ] Crear archivo de vista en la carpeta correcta
- [ ] Definir el modelo con `@model`
- [ ] Establecer el título con `ViewData["Title"]`
- [ ] Usar componentes Bootstrap 5 para el diseño
- [ ] Agregar validación si es un formulario
- [ ] Incluir mensajes de éxito/error con TempData
- [ ] Agregar iconos Bootstrap Icons
- [ ] Probar en diferentes tamaños de pantalla (responsive)
- [ ] Agregar al menú si es necesario

## Buenas Prácticas

✅ Usar ViewModels para vistas complejas  
✅ Mantener la lógica fuera de las vistas  
✅ Usar partial views para componentes reutilizables  
✅ Seguir el diseño existente de MoneyPenny  
✅ Usar Bootstrap 5 para consistencia  
✅ Agregar validación del lado del cliente  
✅ Usar Tag Helpers en lugar de HTML puro  
✅ Hacer las vistas responsive (mobile-first)  

## Referencias

- [Razor Syntax](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/razor)
- [Bootstrap 5 Docs](https://getbootstrap.com/docs/5.3/)
- [Bootstrap Icons](https://icons.getbootstrap.com/)
- [Tag Helpers](https://learn.microsoft.com/es-es/aspnet/core/mvc/views/tag-helpers/intro)
