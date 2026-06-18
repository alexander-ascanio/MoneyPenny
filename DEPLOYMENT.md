# Guía de Despliegue en Producción

## 📋 Preparación de la Base de Datos

**✅ IMPORTANTE:** La aplicación crea automáticamente la base de datos si no existe, siempre que el usuario tenga permisos `CREATEDB`.

### Opción 1: Creación Automática (Recomendado)

La aplicación creará la base de datos automáticamente al iniciar si:
- El usuario de PostgreSQL tiene permisos `CREATEDB`
- `ApplyMigrationsOnStartup: true` en appsettings.json

**Verificar permisos:**
```bash
psql -U postgres
\du  # Ver permisos de usuarios
```

**Dar permisos CREATEDB:**
```sql
ALTER USER tu_usuario CREATEDB;
```

### Opción 2: Creación Manual (Opcional)

Si prefieres crear la base de datos manualmente antes de ejecutar la aplicación:

#### PostgreSQL Local o en Servidor

#### 1. Crear la Base de Datos Manualmente

**Usando psql:**
```bash
psql -U postgres -h tu-servidor.com
CREATE DATABASE moneypenny_db;
\q
```

**Usando pgAdmin:**
1. Conecta a tu servidor PostgreSQL
2. Click derecho en "Databases" → "Create" → "Database"
3. Nombre: `moneypenny_db`
4. Owner: tu usuario de PostgreSQL
5. Click "Save"

#### 2. Configurar Permisos del Usuario

```sql
-- Crear usuario si no existe
CREATE USER tu_usuario WITH PASSWORD 'tu_password_segura';

-- Dar permisos sobre la base de datos
GRANT ALL PRIVILEGES ON DATABASE moneypenny_db TO tu_usuario;

-- Conectar a la base de datos
\c moneypenny_db

-- Dar permisos sobre el esquema
GRANT ALL ON SCHEMA public TO tu_usuario;
```

### Opción 2: Azure Database for PostgreSQL

#### 1. Crear el Servidor PostgreSQL en Azure

**Desde el Portal de Azure:**
1. Busca "Azure Database for PostgreSQL"
2. Click en "Crear"
3. Selecciona "Servidor flexible" (recomendado)
4. Configura:
   - **Grupo de recursos:** Crea uno nuevo o usa existente
   - **Nombre del servidor:** `tu-app-postgres` (será: tu-app-postgres.postgres.database.azure.com)
   - **Región:** Elige la más cercana a tus usuarios
   - **Versión de PostgreSQL:** 16 (o la más reciente LTS)
   - **Proceso y almacenamiento:** Básico (1-2 vCores para empezar)
   - **Usuario administrador:** `tu_usuario`
   - **Contraseña:** Contraseña segura
5. En "Redes":
   - Habilita "Permitir acceso público desde cualquier servicio de Azure"
   - Añade tu IP actual si necesitas conectarte desde tu máquina
6. Click "Revisar y crear"

#### 2. Crear la Base de Datos

**Opción A: Desde el Portal de Azure**
1. Ve a tu servidor PostgreSQL creado
2. En el menú lateral, selecciona "Bases de datos"
3. Click en "+ Agregar"
4. Nombre: `moneypenny_db`
5. Click "Guardar"

**Opción B: Usando psql**
```bash
psql "host=tu-servidor.postgres.database.azure.com port=5432 dbname=postgres user=tu_usuario password=tu_password sslmode=require"
CREATE DATABASE moneypenny_db;
\q
```

**Opción C: Usando pgAdmin**
1. Conecta usando:
   - Host: `tu-servidor.postgres.database.azure.com`
   - Port: `5432`
   - Database: `postgres`
   - Username: `tu_usuario`
   - Password: `tu_password`
   - SSL Mode: `require`
2. Click derecho en "Databases" → "Create" → "Database"
3. Nombre: `moneypenny_db`

#### 3. Configurar Firewall

En el Portal de Azure:
1. Ve a tu servidor PostgreSQL
2. Menú lateral → "Redes"
3. Añade reglas de firewall:
   - Para desarrollo: Añade tu IP actual
   - Para producción: Añade la IP de tu App Service
   - O habilita "Permitir acceso desde servicios de Azure"

## ⚙️ Configuración de la Aplicación

### 1. Actualizar appsettings.json para Producción

**IMPORTANTE:** No subas contraseñas a Git. Usa variables de entorno o Azure Key Vault.

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
    "ApplyMigrationsOnStartup": false,
    "EnableSeed": false
  }
}
```

### 2. Aplicar Migraciones

**Opción A: Manualmente antes de desplegar**
```bash
# Configura la cadena de conexión
export ConnectionStrings__DefaultConnection="Host=tu-servidor.postgres.database.azure.com;Port=5432;Database=moneypenny_db;Username=tu_usuario;Password=tu_password;SSL Mode=Require"

# Aplica las migraciones
dotnet ef database update
```

**Opción B: Automáticamente en el primer arranque**
1. Configura `ApplyMigrationsOnStartup: true` temporalmente
2. Despliega y ejecuta la aplicación
3. Una vez aplicadas, cambia a `false` y redespliega

### 3. Variables de Entorno en Azure App Service

En el Portal de Azure:
1. Ve a tu App Service
2. Menú lateral → "Configuración" → "Configuración de la aplicación"
3. Añade las siguientes variables:
   ```
   Database__Host = tu-servidor.postgres.database.azure.com
   Database__Port = 5432
   Database__Name = moneypenny_db
   Database__User = tu_usuario
   Database__Password = tu_password_segura
   Database__SslMode = Require
   Database__TrustServerCertificate = false
   Database__ApplyMigrationsOnStartup = false
   Database__EnableSeed = false
   ```

## 🚀 Despliegue

### Desde Visual Studio

1. Click derecho en el proyecto → "Publicar"
2. Selecciona "Azure" → "Azure App Service (Windows o Linux)"
3. Selecciona tu suscripción y App Service
4. Click "Publicar"

### Desde Visual Studio Code

1. Instala la extensión "Azure App Service"
2. Click en el icono de Azure en la barra lateral
3. Click derecho en tu App Service → "Deploy to Web App"
4. Selecciona la carpeta del proyecto

### Usando GitHub Actions

Crea `.github/workflows/deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v2
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: '10.0.x'
    
    - name: Build
      run: dotnet build --configuration Release
    
    - name: Publish
      run: dotnet publish -c Release -o ${{env.DOTNET_ROOT}}/myapp
    
    - name: Deploy to Azure Web App
      uses: azure/webapps-deploy@v2
      with:
        app-name: 'tu-app-name'
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: ${{env.DOTNET_ROOT}}/myapp
```

## ✅ Verificación Post-Despliegue

### 1. Verificar la Base de Datos

```bash
# Conecta a la base de datos
psql "host=tu-servidor.postgres.database.azure.com port=5432 dbname=moneypenny_db user=tu_usuario password=tu_password sslmode=require"

# Verifica las tablas
\dt

# Verifica el usuario admin
SELECT * FROM "AspNetUsers" WHERE "Email" = 'admin@MoneyPenny.com';

# Verifica los roles
SELECT * FROM "AspNetRoles";
```

### 2. Verificar la Aplicación

1. Accede a tu URL: `https://tu-app.azurewebsites.net`
2. Intenta iniciar sesión con el usuario admin
3. Verifica que puedes acceder a la gestión de usuarios
4. Crea un registro de prueba

## 🔒 Seguridad en Producción

### 1. Cambiar Contraseña del Admin

1. Inicia sesión como admin
2. Ve a "Usuarios"
3. Edita el usuario admin
4. Cambia la contraseña por una segura

### 2. Configurar SSL/TLS

- Azure App Service incluye HTTPS por defecto
- Configura `SslMode: Require` en la conexión a PostgreSQL
- Considera usar un certificado personalizado para tu dominio

### 3. Configurar Azure Key Vault (Recomendado)

1. Crea un Azure Key Vault
2. Almacena las contraseñas y secrets
3. Configura tu App Service para usar Key Vault
4. Referencia los secrets en la configuración

### 4. Habilitar Logs y Monitoreo

En Azure App Service:
1. Menú lateral → "Registros de App Service"
2. Habilita "Registro de aplicaciones"
3. Configura Application Insights para monitoreo

## 🔄 Actualizaciones Futuras

### Aplicar Nuevas Migraciones

```bash
# 1. Crear la migración localmente
dotnet ef migrations add NombreMigracion

# 2. Aplicar en producción (opción manual)
dotnet ef database update --connection "tu_cadena_de_conexion"

# O configurar ApplyMigrationsOnStartup: true temporalmente
```

## 📊 Monitoreo

### Métricas Importantes

- **App Service:** CPU, Memoria, Tiempo de respuesta
- **PostgreSQL:** Conexiones activas, Uso de CPU, Espacio en disco
- **Application Insights:** Excepciones, Solicitudes fallidas, Dependencias

### Configurar Alertas

1. En el Portal de Azure, ve a tu recurso
2. Menú lateral → "Alertas"
3. Crea reglas de alerta para:
   - CPU > 80%
   - Memoria > 80%
   - Errores HTTP 500
   - Tiempo de respuesta > 5 segundos

## 🆘 Solución de Problemas

### Error: "No se puede conectar a la base de datos"

1. Verifica que la base de datos existe
2. Verifica las credenciales en la configuración
3. Verifica las reglas de firewall en Azure
4. Verifica que `SslMode` esté configurado correctamente

### Error: "Migraciones no aplicadas"

1. Verifica que `ApplyMigrationsOnStartup: true` o aplica manualmente
2. Verifica que el usuario tiene permisos para crear tablas
3. Revisa los logs de la aplicación

### Error: "No se puede iniciar sesión como admin"

1. Verifica que el seeder se ejecutó correctamente
2. Consulta la base de datos para verificar que el usuario existe
3. Intenta resetear la contraseña desde la base de datos

## 📝 Checklist de Despliegue

- [ ] Base de datos creada en PostgreSQL
- [ ] Usuario de base de datos con permisos adecuados
- [ ] Firewall configurado para permitir conexiones
- [ ] Variables de entorno configuradas en App Service
- [ ] Migraciones aplicadas
- [ ] Usuario administrador creado
- [ ] Contraseña del admin cambiada
- [ ] SSL/TLS habilitado
- [ ] Logs y monitoreo configurados
- [ ] Alertas configuradas
- [ ] Backup de base de datos programado
- [ ] Documentación actualizada
