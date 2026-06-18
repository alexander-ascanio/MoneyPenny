# Agentes Especializados de MoneyPenny

Esta carpeta contiene agentes especializados que te ayudarán con tareas específicas del proyecto.

## 📋 Agentes Disponibles

Los agentes están diseñados para las **3 tareas más comunes** en el desarrollo del portal:

### 1. Database Migration Agent 🔴
**Archivo:** `database-migration-agent.md`

**Para:** Cambios en el diccionario de datos (base de datos)

**Cuándo usar:**
- Crear nuevas migraciones de base de datos
- Modificar el esquema (agregar/modificar tablas o columnas)
- Agregar/modificar entidades del modelo
- Revertir migraciones
- Aplicar migraciones en producción

**Comandos clave:**
```bash
dotnet ef migrations add NombreMigracion
dotnet ef database update
dotnet ef migrations remove
dotnet ef migrations list
```

### 2. API Integration Agent 🟢
**Archivo:** `api-integration-agent.md`

**Para:** Conexiones a APIs de terceros

**Cuándo usar:**
- Integrar APIs externas (OpenAI, Jira, Auth0, Stripe, etc.)
- Configurar servicios HTTP con diferentes autenticaciones
- Manejar autenticación (Bearer, Basic, OAuth2)
- Implementar retry logic y timeouts
- Cachear respuestas de APIs

**Tareas principales:**
- Configuración en `appsettings.json`
- Creación de servicios HTTP
- Manejo de errores y timeouts
- Implementación de caché

### 3. View Modification Agent 🔵
**Archivo:** `view-modification-agent.md`

**Para:** Modificaciones en vistas e interfaces de usuario

**Cuándo usar:**
- Crear nuevas vistas Razor
- Diseñar formularios y tablas
- Crear componentes UI reutilizables
- Modificar el layout principal
- Agregar modales, alertas y cards
- Implementar paginación y búsqueda

**Tareas principales:**
- Creación de vistas con Razor
- Uso de Bootstrap 5 y componentes
- ViewModels y Tag Helpers
- Partial Views y layouts

## 🚀 Cómo Usar los Agentes

### Opción 1: En Cursor IDE (Recomendado)

1. Abre el chat de Cursor (Ctrl/Cmd + L)
2. Menciona el agente que necesitas:
   ```
   @database-migration-agent.md necesito agregar una nueva columna Email a la tabla Users
   ```
   o
   ```
   @api-integration-agent.md necesito integrar la API de SendGrid para enviar emails
   ```

3. El agente te guiará paso a paso en la tarea

### Opción 2: Lectura Manual

1. Abre el archivo del agente correspondiente
2. Lee la sección relevante para tu tarea
3. Sigue los pasos y ejemplos proporcionados

## 📚 Estructura de los Agentes

Cada agente incluye:

- **Descripción**: Qué hace el agente
- **Responsabilidades**: Tareas específicas que maneja
- **Ejemplos de Código**: Código listo para usar
- **Buenas Prácticas**: Recomendaciones y tips
- **Troubleshooting**: Solución a problemas comunes
- **Checklist**: Lista de verificación para la tarea
- **Referencias**: Enlaces a documentación oficial

## 🎯 Ejemplos de Uso

### Ejemplo 1: Agregar Nueva Tabla

```
Usuario: @database-migration-agent.md necesito crear una tabla Products con 
columnas: Id, Name, Price, Description, CreatedAt

Agente: Te ayudaré a crear la entidad y la migración...
```

### Ejemplo 2: Integrar API de Clima

```
Usuario: @api-integration-agent.md quiero integrar OpenWeatherMap API 
usando la configuración de appsettings.json

Agente: Perfecto, vamos a configurar el servicio paso a paso...
```

### Ejemplo 3: Modificar Columna Existente

```
Usuario: @database-migration-agent.md necesito cambiar la columna Name 
de DummyRecord para que sea obligatoria con máximo 200 caracteres

Agente: Vamos a crear una migración para modificar esa columna...
```

### Ejemplo 4: Crear Vista con Formulario

```
Usuario: @view-modification-agent.md necesito crear una vista para 
registrar productos con nombre, precio y descripción

Agente: Vamos a crear la vista con un formulario Bootstrap 5...
```

## 🔧 Personalización

Puedes crear tus propios agentes especializados:

1. Crea un nuevo archivo `.md` en esta carpeta
2. Sigue la estructura de los agentes existentes
3. Documenta claramente las responsabilidades y ejemplos
4. Úsalo con `@nombre-agente.md` en Cursor

## 📝 Plantilla para Nuevos Agentes

```markdown
# Nombre del Agente

## Descripción
[Descripción breve del propósito del agente]

## Responsabilidades
- Tarea 1
- Tarea 2
- Tarea 3

## Ejemplos de Código
[Código de ejemplo]

## Buenas Prácticas
1. Práctica 1
2. Práctica 2

## Troubleshooting
[Problemas comunes y soluciones]

## Referencias
- [Enlace a documentación]
```

## 🤝 Contribuir

Si creas un nuevo agente útil o mejoras uno existente:

1. Documenta claramente su propósito
2. Incluye ejemplos prácticos
3. Agrega referencias a documentación oficial
4. Actualiza este README

## 📞 Soporte

Si tienes dudas sobre cómo usar los agentes:

1. Lee la documentación del agente específico
2. Consulta la sección de Troubleshooting
3. Revisa los ejemplos de código
4. Pregunta en el chat de Cursor mencionando el agente

---

**Nota:** Estos agentes son guías especializadas que te ayudan a realizar tareas específicas de manera más eficiente. No son código ejecutable, sino documentación estructurada que Cursor puede usar para asistirte mejor.
