---
name: database-migration-agent
model: inherit
---

# Agente de Migraciones de Base de Datos

## Descripción
Especializado en gestionar cambios en la base de datos mediante Entity Framework Core.

## Comandos Principales

### Crear Migración
```bash
cd c:\proyectos\master
dotnet ef migrations add [NombreDescriptivo]

# Ejemplos: AddUserEmail, CreateProductsTable, UpdateDummyRecordSchema
```

### Aplicar Migración
```bash
dotnet ef database update                    # Aplicar todas pendientes
dotnet ef database update [NombreMigracion]  # Aplicar hasta una específica
```

### Revertir Migración
```bash
dotnet ef database update [MigracionAnterior]  # Revertir a una anterior
dotnet ef migrations remove                     # Eliminar última (si no se aplicó)
```

### Comandos Útiles
```bash
dotnet ef migrations list                              # Ver todas las migraciones
dotnet ef migrations script --idempotent -o script.sql # Generar SQL para producción
dotnet ef dbcontext info                               # Ver info del DbContext
```

## Proceso Recomendado

### 1. Antes de Crear
- ✅ Modificar entidades en `Models/`
- ✅ Actualizar `ApplicationDbContext.cs` si es necesario
- ✅ Verificar que PostgreSQL está corriendo
- ✅ Revisar `appsettings.json` tiene la conexión correcta

### 2. Crear y Revisar
- ✅ Ejecutar `dotnet ef migrations add NombreDescriptivo`
- ✅ Abrir archivo en `Migrations/[Timestamp]_[Nombre].cs`
- ✅ Revisar método `Up()` (qué se aplicará)
- ✅ Revisar método `Down()` (cómo se revierte)
- ✅ Asegurar que no hay pérdida de datos

### 3. Aplicar
- ✅ En desarrollo: `dotnet ef database update`
- ✅ Probar funcionalidad
- ✅ Commit a Git

### 4. Producción
- ✅ Backup de base de datos
- ✅ Generar script SQL idempotente
- ✅ Probar en staging
- ✅ Aplicar en producción
- ✅ Verificar y monitorear

## Casos Especiales

### Columna No Nullable
Agregar con valor por defecto:
```csharp
migrationBuilder.AddColumn<string>("Email", "Users", nullable: false, defaultValue: "");
```

### Renombrar Columna
Usar `RenameColumn` para no perder datos:
```csharp
migrationBuilder.RenameColumn("OldName", "TableName", "NewName");
```

### Migrar Datos
Usar SQL para transformar datos:
```csharp
migrationBuilder.Sql(@"UPDATE ""Users"" SET ""NewColumn"" = ""OldColumn""");
```

### Configurar Relaciones
En `ApplicationDbContext.OnModelCreating`:
```csharp
builder.Entity<Order>()
    .HasOne(o => o.Customer)
    .WithMany(c => c.Orders)
    .HasForeignKey(o => o.CustomerId);
```

## Troubleshooting

| Error | Solución |
|-------|----------|
| "No migrations configuration" | Verificar que estás en `c:\proyectos\master` |
| "Cannot connect to database" | Verificar PostgreSQL corriendo y `appsettings.json` |
| "Pending model changes" | Crear migración: `dotnet ef migrations add SyncChanges` |

## Reglas Importantes

⚠️ **NUNCA** modificar migraciones ya aplicadas en producción  
⚠️ **NUNCA** aplicar migraciones sin backup en producción  
✅ **SIEMPRE** revisar el código generado antes de aplicar  
✅ **SIEMPRE** probar en desarrollo primero  

## Referencias

- [EF Core Migrations](https://learn.microsoft.com/es-es/ef/core/managing-schemas/migrations/)
- [PostgreSQL con EF Core](https://www.npgsql.org/efcore/)
