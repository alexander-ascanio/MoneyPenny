# Guía de Configuración

## 📝 Archivo appsettings.json

### Configuración de Base de Datos

```json
"Database": {
  "Host": "localhost",              // Servidor PostgreSQL
  "Port": 5432,                     // Puerto (por defecto 5432)
  "Name": "moneypenny_db",              // Nombre de la base de datos
  "User": "postgres",               // Usuario de PostgreSQL
  "Password": "tu_password",        // Contraseña del usuario
  "SslMode": "Disable",             // Modo SSL: Disable, Require, Prefer
  "TrustServerCertificate": true,   // Confiar en certificado del servidor
  "ApplyMigrationsOnStartup": true, // Crear BD y aplicar migraciones automáticamente
  "EnableSeed": true                // Insertar datos de prueba
}
```

### ⚙️ ApplyMigrationsOnStartup

**`true` (Recomendado):**
- ✅ Crea la base de datos automáticamente si no existe
- ✅ Aplica todas las migraciones pendientes al iniciar
- ✅ Crea el usuario administrador y roles automáticamente
- ✅ Ideal para desarrollo y producción

**`false`:**
- ❌ No crea la base de datos automáticamente
- ❌ No aplica migraciones
- ⚠️ Debes aplicar migraciones manualmente con `dotnet ef database update`
- 📝 Útil si quieres control total sobre cuándo se aplican las migraciones

### 🌱 EnableSeed

**`true`:**
- Inserta 3 registros de prueba en la tabla DummyRecords
- Útil para desarrollo y pruebas

**`false`:**
- No inserta datos de prueba
- Recomendado para producción

**Nota:** El usuario administrador y los roles se crean siempre, independientemente de este valor.

## 🔐 Configuración de Identity

```json
"Identity": {
  "Password": {
    "RequireDigit": true,              // Requiere al menos un dígito (0-9)
    "RequiredLength": 6,               // Longitud mínima de la contraseña
    "RequireNonAlphanumeric": false,   // Requiere caracteres especiales (!@#$%)
    "RequireUppercase": false,         // Requiere mayúsculas (A-Z)
    "RequireLowercase": false          // Requiere minúsculas (a-z)
  },
  "Lockout": {
    "DefaultLockoutTimeSpan": "00:05:00",  // Tiempo de bloqueo (5 minutos)
    "MaxFailedAccessAttempts": 5           // Intentos fallidos antes de bloquear
  }
}
```

### Ejemplos de Configuración de Contraseñas

**Desarrollo (Flexible):**
```json
"Password": {
  "RequireDigit": true,
  "RequiredLength": 6,
  "RequireNonAlphanumeric": false,
  "RequireUppercase": false,
  "RequireLowercase": false
}
```

**Producción (Segura):**
```json
"Password": {
  "RequireDigit": true,
  "RequiredLength": 12,
  "RequireNonAlphanumeric": true,
  "RequireUppercase": true,
  "RequireLowercase": true
}
```

## 🌐 Configuración de APIs Externas

```json
"ExternalApis": {
  "ExampleApi": {
    "BaseUrl": "https://api.example.com",  // URL base de la API
    "ApiKey": "tu_api_key_aqui",           // Clave de API
    "TimeoutSeconds": 30,                   // Timeout en segundos
    "RetryCount": 3                         // Número de reintentos
  }
}
```

### Añadir Nuevas APIs

```json
"ExternalApis": {
  "Stripe": {
    "BaseUrl": "https://api.stripe.com",
    "ApiKey": "sk_test_...",
    "TimeoutSeconds": 30,
    "RetryCount": 3
  },
  "SendGrid": {
    "BaseUrl": "https://api.sendgrid.com",
    "ApiKey": "SG...",
    "TimeoutSeconds": 15,
    "RetryCount": 2
  }
}
```

## 🔧 Configuración por Entorno

### appsettings.Development.json

Sobrescribe configuraciones para desarrollo:

```json
{
  "Database": {
    "Name": "moneypenny_db_dev",           // Base de datos separada para desarrollo
    "ApplyMigrationsOnStartup": true,  // Siempre aplicar migraciones en desarrollo
    "EnableSeed": true                 // Datos de prueba en desarrollo
  }
}
```

### appsettings.Production.json

Crea este archivo para producción:

```json
{
  "Database": {
    "Host": "tu-servidor.postgres.database.azure.com",
    "SslMode": "Require",
    "TrustServerCertificate": false,
    "ApplyMigrationsOnStartup": true,  // Crear BD automáticamente en producción
    "EnableSeed": false                // Sin datos de prueba en producción
  },
  "Identity": {
    "Password": {
      "RequireDigit": true,
      "RequiredLength": 12,
      "RequireNonAlphanumeric": true,
      "RequireUppercase": true,
      "RequireLowercase": true
    }
  }
}
```

## 🔐 Variables de Entorno (Recomendado para Producción)

En lugar de poner contraseñas en appsettings.json, usa variables de entorno:

### Windows (PowerShell)
```powershell
$env:Database__Password = "tu_password_segura"
$env:Database__Host = "tu-servidor.postgres.database.azure.com"
```

### Linux/Mac (Bash)
```bash
export Database__Password="tu_password_segura"
export Database__Host="tu-servidor.postgres.database.azure.com"
```

### Azure App Service

En el Portal de Azure:
1. Ve a tu App Service
2. Configuración → Configuración de la aplicación
3. Añade las variables:
   - `Database__Host`
   - `Database__Password`
   - `Database__User`
   - etc.

### Docker

```yaml
environment:
  - Database__Host=postgres
  - Database__Password=tu_password
  - Database__Name=moneypenny_db
```

## 📋 Configuraciones Comunes

### PostgreSQL Local (Desarrollo)

```json
{
  "Database": {
    "Host": "localhost",
    "Port": 5432,
    "Name": "moneypenny_db_dev",
    "User": "postgres",
    "Password": "postgres",
    "SslMode": "Disable",
    "TrustServerCertificate": true,
    "ApplyMigrationsOnStartup": true,
    "EnableSeed": true
  }
}
```

### Azure Database for PostgreSQL (Producción)

```json
{
  "Database": {
    "Host": "tu-servidor.postgres.database.azure.com",
    "Port": 5432,
    "Name": "moneypenny_db",
    "User": "tu_usuario",
    "Password": "usa_variables_de_entorno",
    "SslMode": "Require",
    "TrustServerCertificate": false,
    "ApplyMigrationsOnStartup": true,
    "EnableSeed": false
  }
}
```

### PostgreSQL en Docker

```json
{
  "Database": {
    "Host": "postgres",
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

## ✅ Verificación de Configuración

### 1. Verificar Conexión a PostgreSQL

```bash
psql -h localhost -U postgres -d postgres
# Si conecta, la configuración es correcta
```

### 2. Verificar Permisos del Usuario

```sql
-- Conectar como postgres
psql -U postgres

-- Ver permisos
\du

-- Dar permisos CREATEDB si es necesario
ALTER USER tu_usuario CREATEDB;
```

### 3. Probar la Aplicación

```bash
dotnet run
```

Deberías ver en los logs:
```
info: Program[0]
      Verificando y creando base de datos si no existe...
info: Program[0]
      Base de datos verificada y migraciones aplicadas correctamente.
info: Program[0]
      Inicialización de base de datos completada.
```

## 🆘 Solución de Problemas

### Error: "No se puede conectar a la base de datos"

**Causas comunes:**
- PostgreSQL no está ejecutándose
- Host o puerto incorrectos
- Usuario o contraseña incorrectos
- Firewall bloqueando la conexión

**Solución:**
```bash
# Verificar que PostgreSQL está ejecutándose
# Windows
Get-Service postgresql*

# Linux
sudo systemctl status postgresql

# Verificar conexión
psql -h localhost -U postgres -d postgres
```

### Error: "No se puede crear la base de datos"

**Causa:** El usuario no tiene permisos `CREATEDB`

**Solución:**
```sql
-- Conectar como postgres
psql -U postgres

-- Dar permisos
ALTER USER tu_usuario CREATEDB;
```

### Error: "SSL connection required"

**Causa:** El servidor requiere SSL pero está configurado como `Disable`

**Solución:**
```json
"Database": {
  "SslMode": "Require",
  "TrustServerCertificate": false
}
```

## 📚 Referencias

- [Configuración de ASP.NET Core](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/configuration/)
- [PostgreSQL Connection Strings](https://www.npgsql.org/doc/connection-string-parameters.html)
- [ASP.NET Core Identity](https://learn.microsoft.com/es-es/aspnet/core/security/authentication/identity)
- [Entity Framework Core Migrations](https://learn.microsoft.com/es-es/ef/core/managing-schemas/migrations/)
