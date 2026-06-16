# Guía de uso — Eniflare.Serilog

Esta guía explica, paso a paso, **qué tiene que hacer un usuario** para instalar y usar el
paquete `Eniflare.Serilog` en su propia aplicación .NET.

El paquete se publica en **[NuGet.org](https://www.nuget.org/packages/Eniflare.Serilog)**,
el repositorio público oficial de .NET. Eso significa que **se instala sin autenticación**:
basta con `dotnet add package`.

---

## Índice

1. [Requisitos](#1-requisitos)
2. [Instalar el paquete](#2-instalar-el-paquete)
3. [Configurar el logger](#3-configurar-el-logger)
   - [Opción A — por código](#opción-a--por-código)
   - [Opción B — vía appsettings.json](#opción-b--vía-appsettingsjson)
4. [Opciones disponibles](#4-opciones-disponibles)
5. [Comprobar que funciona](#5-comprobar-que-funciona)
6. [Solución de problemas](#6-solución-de-problemas)

---

## 1. Requisitos

- **.NET SDK 8.0, 9.0 o 10.0** (el paquete soporta los tres _target frameworks_).
- Tu **URL de Eniflare** (por ejemplo `https://eniflare.example.com`) y una **API key**
  (algo como `enf_xxx`).

No necesitas cuenta de GitHub ni token: al estar en NuGet.org, la instalación es pública.

---

## 2. Instalar el paquete

```bash
dotnet add package Eniflare.Serilog
```

Para fijar una versión concreta:

```bash
dotnet add package Eniflare.Serilog --version 1.0.0
```

Si tu aplicación es **ASP.NET Core**, instala además el paquete que aporta la integración
con el host (este paquete **no** lo incluye a propósito):

```bash
dotnet add package Serilog.AspNetCore
```

Y si vas a configurar el sink desde `appsettings.json`:

```bash
dotnet add package Serilog.Settings.Configuration
```

---

## 3. Configurar el logger

### Opción A — por código

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()                       // logs locales (opcional)
    .WriteTo.Eniflare(
        "https://eniflare.example.com",      // baseUrl — se le añade "/ingest"
        "enf_tu_api_key")                    // apiKey
    .CreateLogger();

try
{
    Log.Information("La aplicación ha arrancado");
    // ... tu app ...
}
finally
{
    Log.CloseAndFlush();                     // importante: vacía los logs pendientes al salir
}
```

En **ASP.NET Core** (`Program.cs`), leyendo la URL y la key de configuración:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Eniflare(
        ctx.Configuration["Eniflare:Url"]!,
        ctx.Configuration["Eniflare:ApiKey"]!));

var app = builder.Build();
app.Run();
```

### Opción B — vía `appsettings.json`

Requiere el paquete `Serilog.Settings.Configuration`. Los nombres dentro de `Args`
coinciden exactamente con los parámetros del método de extensión.

```json
{
  "Serilog": {
    "Using": [ "Eniflare.Serilog" ],
    "MinimumLevel": "Information",
    "Enrich": [ "FromLogContext" ],
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "Eniflare",
        "Args": {
          "baseUrl": "https://eniflare.example.com",
          "apiKey": "enf_tu_api_key"
        }
      }
    ]
  }
}
```

Y en `Program.cs`:

```csharp
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

var app = builder.Build();
app.Run();
```

> 🔐 **No pongas la API key en `appsettings.json` si lo subes al repo.** Omite `apiKey`
> del bloque `Args` y entrégala como `Eniflare:ApiKey` mediante una **variable de entorno**
> o **user secrets** — el sink la resuelve automáticamente en tiempo de ejecución:
>
> ```bash
> # variable de entorno (doble guion bajo = separador de sección)
> export Eniflare__ApiKey="enf_tu_api_key"
> ```

---

## 4. Opciones disponibles

| Parámetro                  | Por defecto         | Descripción                                                   |
|----------------------------|---------------------|---------------------------------------------------------------|
| `baseUrl`                  | _(obligatorio)_     | URL raíz de Eniflare; se le añade `/ingest` automáticamente.  |
| `apiKey`                   | `""`                | API key; si está vacía, se usa `Eniflare:ApiKey` de config.   |
| `queueLimitBytes`          | `null` (sin límite) | Máx. de bytes en cola en memoria antes de enviar.             |
| `logEventLimitBytes`       | `null` (sin límite) | Tamaño máx. de un evento; los mayores se descartan.           |
| `batchSizeLimit`           | `1000`              | Nº máximo de eventos por lote.                                |
| `period`                   | `null` (por defecto)| Intervalo entre envíos de lotes.                              |
| `restrictedToMinimumLevel` | `Verbose`           | Nivel mínimo de eventos que pasan al sink.                    |

Ejemplo afinando el batching:

```csharp
.WriteTo.Eniflare(
    baseUrl: "https://eniflare.example.com",
    apiKey: "enf_tu_api_key",
    batchSizeLimit: 200,
    period: TimeSpan.FromSeconds(5))
```

---

## 5. Comprobar que funciona

```csharp
Log.Information("Usuario {UserId} ha iniciado sesión", 42);
Log.Error(new InvalidOperationException("boom"), "Fallo procesando el pedido {OrderId}", 7);
Log.CloseAndFlush();
```

- Los eventos se envían **en segundo plano** en lotes con formato **CLEF**
  (`POST {baseUrl}/ingest`), con la API key en la cabecera `X-Api-Key`.
- El envío **nunca bloquea** tu aplicación: del batching y los reintentos se encarga
  `Serilog.Sinks.Http`.
- Revisa el panel de Eniflare para confirmar que los logs llegan.

---

## 6. Solución de problemas

**`Unable to find package Eniflare.Serilog`**
- Comprueba que tienes conexión a NuGet.org y que el nombre/versión son correctos. Lista
  tus fuentes con `dotnet nuget list source` y asegúrate de que `nuget.org` está habilitada.
- Si acabas de publicar una versión, puede tardar unos minutos en estar indexada y
  disponible para instalar.

**Los logs no aparecen en Eniflare**
- Asegúrate de llamar a `Log.CloseAndFlush()` antes de que el proceso termine (en apps de
  consola/cortas), o los últimos lotes pueden no enviarse.
- Verifica que `baseUrl` es la raíz correcta (sin `/ingest`, se añade solo) y que la
  `apiKey` es válida.

---

## Para mantenedores — cómo publicar una versión

El versionado lo gestiona [MinVer](https://github.com/adamralph/minver) a partir de tags
git. Para lanzar una versión a NuGet.org:

1. Asegúrate de que el secret `NUGET_API_KEY` existe en el repo
   (**Settings → Secrets and variables → Actions**), con una API key de NuGet.org con
   permiso de _Push_.
2. Crea y sube el tag:
   ```bash
   git tag v1.2.3
   git push origin v1.2.3
   ```
3. El workflow [`release.yml`](../.github/workflows/release.yml) construye, prueba,
   empaqueta y publica el `.nupkg` (y el `.snupkg` de símbolos) en NuGet.org.

> Recuerda: en NuGet.org una versión publicada **no se puede borrar ni sobrescribir**
> (solo _unlist_). Para corregir algo, publica una versión superior.
