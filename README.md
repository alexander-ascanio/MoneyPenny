# MoneyPenny - Aplicación ASP.NET Core MVC con PostgreSQL

Aplicación ASP.NET Core MVC con PostgreSQL, Entity Framework Core y ASP.NET Identity.

## Stack Tecnológico

- **.NET 10.0** (LTS)
- **ASP.NET Core MVC**
- **Entity Framework Core 10.0.3**
- **PostgreSQL** con Npgsql 10.0.0
- **ASP.NET Core Identity 10.0.3**
- **Bootstrap** (frontend)

## Características

- ✅ Autenticación y autorización con ASP.NET Identity
- ✅ Sistema de roles (Admin, User)
- ✅ Gestión completa de usuarios (solo administradores)
- ✅ Gestión de roles personalizada
- ✅ Usuario administrador creado automáticamente
- ✅ Registro cerrado (solo administradores pueden crear usuarios)
- ✅ Gestión de usuarios con campos personalizados (DisplayName, CreatedAt, LastLoginAt)
- ✅ CRUD completo para entidad DummyRecord
- ✅ Migraciones automáticas al iniciar la aplicación
- ✅ Seed de datos inicial configurable
- ✅ Configuración por atributos en appsettings.json

## Estructura del Proyecto

```
MoneyPenny/
├── Controllers/
│   ├── AccountController.cs       # Autenticación (Login, Register, Logout)
│   ├── DummyRecordsController.cs  # CRUD para DummyRecord
│   └── HomeController.cs
├── Data/
│   ├── ApplicationDbContext.cs    # DbContext con Identity
│   └── DbSeeder.cs                # Seed de datos inicial
├── Models/
│   ├── ApplicationUser.cs         # Usuario extendido de Identity
│   ├── DummyRecord.cs             # Entidad de prueba
│   ├── LoginViewModel.cs
│   └── RegisterViewModel.cs
├── Views/
│   ├── Account/                   # Vistas de autenticación
│   ├── DummyRecords/              # Vistas CRUD
│   └── Shared/
├── Migrations/                    # Migraciones de EF Core
├── appsettings.json
├── appsettings.Development.json
└── Program.cs

## Configuración

### Base de Datos (appsettings.json)

```json
{
  "Database": {
    "Host": "localhost",
    "Port": 5432,
    "Name": "moneypenny_db",
    "User": "postgres",
    "Password": "postgres",
    "SslMode": "Disable",
    "TrustServerCertificate": true,
    "ApplyMigrationsOnStartup": true,
    "EnableSeed": true
  }
}
```

### Identity (appsettings.json)

```json
{
  "Identity": {
    "Password": {
      "RequireDigit": true,
      "RequiredLength": 6,
      "RequireNonAlphanumeric": false,
      "RequireUppercase": false,
      "RequireLowercase": false
    },
    "Lockout": {
      "DefaultLockoutTimeSpan": "00:05:00",
      "MaxFailedAccessAttempts": 5
    }
  }
}
```

## Requisitos Previos

1. **.NET 10 SDK** instalado
2. **PostgreSQL** instalado y ejecutándose

### Base de Datos

**✅ La base de datos se crea automáticamente** al ejecutar la aplicación por primera vez.

**Requisitos:**
- El usuario de PostgreSQL debe tener permisos para crear bases de datos
- Por defecto, el usuario `postgres` tiene estos permisos
- Si usas otro usuario, asegúrate de que tenga el privilegio `CREATEDB`

**Verificar permisos del usuario:**
```bash
psql -U postgres
\du  # Lista usuarios y sus permisos
```

**Dar permisos CREATEDB a un usuario:**
```sql
ALTER USER tu_usuario CREATEDB;
```

**Nota:** Si prefieres crear la base de datos manualmente, puedes hacerlo antes de ejecutar la aplicación:
```bash
createdb -U postgres moneypenny_db
```

## Comandos

### Restaurar dependencias
```bash
dotnet restore
```

### Compilar el proyecto
```bash
dotnet build
```

### Ejecutar la aplicación
```bash
dotnet run
```

La aplicación estará disponible en:
- HTTPS: https://localhost:5001
- HTTP: http://localhost:5000

### Migraciones

**Automáticas (por defecto):**
Las migraciones se aplican automáticamente al iniciar la aplicación si `ApplyMigrationsOnStartup: true` en appsettings.json.

El sistema:
1. ✅ Verifica si la base de datos existe
2. ✅ Crea la base de datos si no existe (requiere permisos CREATEDB)
3. ✅ Aplica todas las migraciones pendientes
4. ✅ Crea el usuario administrador y roles automáticamente

**Comandos de Migraciones (para desarrollo):**
```bash
# Crear nueva migración
dotnet ef migrations add NombreMigracion

# Aplicar migraciones manualmente
dotnet ef database update

# Revertir última migración
dotnet ef migrations remove

# Ver migraciones aplicadas
dotnet ef migrations list
```

**Configuración:**
- `ApplyMigrationsOnStartup: true` - Aplica migraciones automáticamente (recomendado)
- `ApplyMigrationsOnStartup: false` - Debes aplicar migraciones manualmente

## Uso

### 1. Iniciar la aplicación

```bash
dotnet run
```

Las migraciones se aplicarán automáticamente y se crearán:
- Usuario administrador por defecto
- Roles del sistema (Admin, User)
- 3 registros de prueba si `EnableSeed: true`

### 2. Usuario Administrador por Defecto

**Credenciales:**
- Email: `admin@MoneyPenny.com`
- Contraseña: `Admin123!`

⚠️ **IMPORTANTE**: Cambia estas credenciales en producción.

### 3. Iniciar sesión

1. Navega a `/Account/Login`
2. Ingresa email y contraseña
3. Haz clic en "Iniciar Sesión"

### 4. Gestión de Usuarios (Solo Administradores)

1. Inicia sesión como administrador
2. Navega a `/UserManagement`
3. Puedes:
   - Ver todos los usuarios y sus roles
   - Crear nuevos usuarios
   - Editar usuarios y asignar roles
   - Eliminar usuarios
   - Gestionar roles del sistema

### 5. Gestionar registros

1. Una vez autenticado, navega a `/DummyRecords`
2. Puedes:
   - Ver todos los registros
   - Crear nuevos registros
   - Editar registros existentes
   - Eliminar registros

## Tablas de Base de Datos

### Tablas de Identity (generadas automáticamente)
- `AspNetUsers` - Usuarios
- `AspNetRoles` - Roles
- `AspNetUserRoles` - Relación usuarios-roles
- `AspNetUserClaims` - Claims de usuarios
- `AspNetUserLogins` - Logins externos
- `AspNetUserTokens` - Tokens de usuarios
- `AspNetRoleClaims` - Claims de roles

### Tablas personalizadas
- `DummyRecords` - Registros de prueba

## Entidades

### ApplicationUser
```csharp
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}
```

### DummyRecord
```csharp
public class DummyRecord
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## Decisiones de Diseño

### Nombre del proyecto
**MoneyPenny**

### Versión de .NET
**.NET 10.0.100** (LTS)

### Puertos
- HTTP: 5000
- HTTPS: 5001

### Base de datos
- **Desarrollo**: `moneypenny_db_dev`
- **Producción**: `moneypenny_db`

### Migraciones automáticas
Las migraciones se aplican automáticamente al iniciar la aplicación si `ApplyMigrationsOnStartup: true`.

### Seed de datos
Se insertan 3 registros de prueba si `EnableSeed: true` y la tabla está vacía.

### Autenticación
- Basada en cookies (no JWT)
- Sesión persistente opcional ("Recordarme")
- Bloqueo de cuenta tras 5 intentos fallidos
- Contraseñas con requisitos configurables

## Seguridad

- ✅ Validación CSRF en formularios
- ✅ Contraseñas hasheadas con Identity
- ✅ Bloqueo de cuenta tras intentos fallidos
- ✅ HTTPS habilitado por defecto
- ✅ Autorización requerida para CRUD de registros
- ✅ Registro público cerrado
- ✅ Control de acceso basado en roles
- ✅ Solo administradores pueden gestionar usuarios

## Próximos Pasos

- [ ] Agregar roles y autorización basada en roles
- [ ] Implementar recuperación de contraseña
- [ ] Agregar confirmación de email
- [ ] Implementar paginación en listados
- [ ] Agregar búsqueda y filtros
- [ ] Implementar logging
- [ ] Agregar tests unitarios
- [ ] Configurar CI/CD

## Licencia

Este proyecto es un esqueleto de aplicación para desarrollo.
