# Agente de Integración con APIs Externas

## Descripción
Especializado en integrar APIs externas usando HttpClient, configuración desde `appsettings.json` y diferentes tipos de autenticación.

## 1. Configuración en appsettings.json

#### Ejemplos con Diferentes Tipos de Autenticación

```json
{
  "ExternalApis": {
    "OpenAI": {
      "BaseUrl": "https://api.openai.com/v1",
      "ApiKey": "sk-changeme",
      "AuthType": "Bearer",
      "Timeout": 60
    },
    "Jira": {
      "BaseUrl": "https://tu-dominio.atlassian.net/rest/api/3",
      "Email": "tu-email@ejemplo.com",
      "ApiToken": "changeme",
      "AuthType": "Basic",
      "Timeout": 30
    },
    "Aha": {
      "BaseUrl": "https://tu-dominio.aha.io/api/v1",
      "ApiKey": "changeme",
      "AuthType": "Bearer",
      "Timeout": 30
    },
    "Auth0": {
      "BaseUrl": "https://tu-dominio.auth0.com/api/v2",
      "Domain": "tu-dominio.auth0.com",
      "ClientId": "changeme",
      "ClientSecret": "changeme",
      "Audience": "https://tu-dominio.auth0.com/api/v2/",
      "AuthType": "OAuth2",
      "Timeout": 30
    },
    "AzureDevOps": {
      "BaseUrl": "https://dev.azure.com/tu-organizacion",
      "PersonalAccessToken": "changeme",
      "AuthType": "Basic",
      "Timeout": 30
    }
  }
}
```

#### Variables de Entorno (Producción)
```bash
# En Azure App Service o variables de entorno
ExternalApis__OpenWeather__ApiKey=sk-real-key-here
ExternalApis__SendGrid__ApiKey=SG.real-key-here
ExternalApis__Stripe__SecretKey=sk_live_real-key
```

## 2. Configurar HttpClient en Program.cs

### Bearer Token (OpenAI, Aha)
```csharp
builder.Services.AddHttpClient("OpenAI", client =>
{
    var config = builder.Configuration.GetSection("ExternalApis:OpenAI");
    client.BaseAddress = new Uri(config["BaseUrl"]!);
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Bearer", config["ApiKey"]);
});
```

### Basic Auth (Jira - email:token)
```csharp
builder.Services.AddHttpClient("Jira", client =>
{
    var config = builder.Configuration.GetSection("ExternalApis:Jira");
    client.BaseAddress = new Uri(config["BaseUrl"]!);
    var credentials = Convert.ToBase64String(
        Encoding.ASCII.GetBytes($"{config["Email"]}:{config["ApiToken"]}"));
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Basic", credentials);
});
```

### Basic Auth (Azure DevOps - PAT)
```csharp
builder.Services.AddHttpClient("AzureDevOps", client =>
{
    var config = builder.Configuration.GetSection("ExternalApis:AzureDevOps");
    client.BaseAddress = new Uri(config["BaseUrl"]!);
    var credentials = Convert.ToBase64String(
        Encoding.ASCII.GetBytes($":{config["PersonalAccessToken"]}"));
    client.DefaultRequestHeaders.Authorization = 
        new AuthenticationHeaderValue("Basic", credentials);
});
```

### OAuth2 Client Credentials (Auth0)
Crear servicio para obtener y cachear token:
```csharp
public class Auth0TokenService
{
    private string? _cachedToken;
    private DateTime _tokenExpiry;

    public async Task<string> GetAccessTokenAsync()
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
            return _cachedToken;
        
        // Obtener token de Auth0
        var tokenResponse = await _httpClient.PostAsJsonAsync(
            $"https://{domain}/oauth/token", 
            new { client_id, client_secret, audience, grant_type = "client_credentials" });
        
        // Cachear token
        _cachedToken = tokenResponse.AccessToken;
        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 300);
        
        return _cachedToken;
    }
}
```

### API Key en Header
```csharp
builder.Services.AddHttpClient("CustomApi", client =>
{
    client.DefaultRequestHeaders.Add("X-API-Key", config["ApiKey"]);
    // O: client.DefaultRequestHeaders.Add("api-key", config["ApiKey"]);
});
```

## 3. Crear Servicio y Usar API

### Estructura Básica
```csharp
public class [Nombre]Service
{
    private readonly IHttpClientFactory _httpClientFactory;

    public [Nombre]Service(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<T> MetodoAsync()
    {
        var client = _httpClientFactory.CreateClient("[NombreHttpClient]");
        
        // GET
        var response = await client.GetAsync("/endpoint");
        return await response.Content.ReadFromJsonAsync<T>();
        
        // POST
        var response = await client.PostAsJsonAsync("/endpoint", data);
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
```

### Ejemplos Rápidos

**OpenAI - Generar texto:**
```csharp
var client = _httpClientFactory.CreateClient("OpenAI");
var request = new { model = "gpt-4", messages = new[] { new { role = "user", content = prompt } } };
var response = await client.PostAsJsonAsync("/chat/completions", request);
```

**Jira - Crear issue:**
```csharp
var client = _httpClientFactory.CreateClient("Jira");
var issue = new { fields = new { project = new { key = "PROJ" }, summary = "Título", issuetype = new { name = "Task" } } };
var response = await client.PostAsJsonAsync("/issue", issue);
```

**Aha - Obtener features:**
```csharp
var client = _httpClientFactory.CreateClient("Aha");
var response = await client.GetAsync($"/products/{productId}/features");
var features = await response.Content.ReadFromJsonAsync<List<Feature>>();
```

**Auth0 - Listar usuarios:**
```csharp
var client = _httpClientFactory.CreateClient("Auth0");
var token = await _tokenService.GetAccessTokenAsync(); // Obtener token OAuth2
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var response = await client.GetAsync("/users");
```

## 4. Manejo de Errores y Retry

### Retry Automático con Polly
```bash
dotnet add package Microsoft.Extensions.Http.Polly
```

```csharp
// En Program.cs
builder.Services.AddHttpClient("OpenAI", client => { /* config */ })
    .AddTransientHttpErrorPolicy(policy => 
        policy.WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));
```

### Manejo de Errores
```csharp
try
{
    var response = await client.GetAsync("/endpoint");
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<T>();
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Error calling API");
    throw;
}
```

## 5. Extras Opcionales

### Caché de Respuestas
```csharp
// En Program.cs
builder.Services.AddMemoryCache();

// En el servicio
if (_cache.TryGetValue<T>(cacheKey, out var cached))
    return cached;

var result = await GetAsync<T>(endpoint);
_cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
```

### Timeout Personalizado
```csharp
builder.Services.AddHttpClient("API", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
```

## Checklist de Integración

- [ ] Agregar configuración en `appsettings.json`
- [ ] Registrar HttpClient en `Program.cs` con autenticación
- [ ] Crear servicio con IHttpClientFactory
- [ ] Implementar métodos GET/POST según necesidad
- [ ] Agregar manejo de errores y logging
- [ ] Configurar retry policy (opcional)
- [ ] Probar en desarrollo
- [ ] Configurar variables de entorno para producción

## Buenas Prácticas

⚠️ **NUNCA** commitear API keys en Git  
⚠️ **SIEMPRE** usar variables de entorno en producción  
✅ Usar IHttpClientFactory (no crear HttpClient manualmente)  
✅ Implementar retry logic para errores transitorios  
✅ Loggear errores con contexto  
✅ Configurar timeouts apropiados  

## Referencias

- [HttpClient Best Practices](https://learn.microsoft.com/es-es/dotnet/fundamentals/networking/http/httpclient-guidelines)
- [IHttpClientFactory](https://learn.microsoft.com/es-es/dotnet/core/extensions/httpclient-factory)
