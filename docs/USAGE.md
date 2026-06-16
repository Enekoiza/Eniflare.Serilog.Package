# Guía de uso — Eniflare.Serilog

Esta guía explica, paso a paso, **qué tiene que hacer un usuario** para instalar y usar el
paquete `Eniflare.Serilog` en su propia aplicación .NET.

El paquete se publica en **GitHub Packages** de la organización
[`Enekoiza`](https://github.com/orgs/Enekoiza/packages), no en NuGet.org. Eso implica un
paso extra de **autenticación** que no existe con paquetes públicos de NuGet.org.

---

## Índice

1. [Requisitos](#1-requisitos)
2. [Autenticarse con GitHub Packages](#2-autenticarse-con-github-packages)
3. [Añadir la fuente de paquetes](#3-añadir-la-fuente-de-paquetes)
4. [Instalar el paquete](#4-instalar-el-paquete)
5. [Configurar el logger](#5-configurar-el-logger)
   - [Opción A — por código](#opción-a--por-código)
   - [Opción B — vía appsettings.json](#opción-b--vía-appsettingsjson)
6. [Opciones disponibles](#6-opciones-disponibles)
7. [Comprobar que funciona](#7-comprobar-que-funciona)
8. [Solución de problemas](#8-solución-de-problemas)

---

## 1. Requisitos

- **.NET SDK 8.0, 9.0 o 10.0** (el paquete soporta los tres _target frameworks_).
- Una **cuenta de GitHub** con acceso a la organización `Enekoiza` (al menos permiso de
  lectura sobre los packages de la org).
- Tu **URL de Eniflare** (por ejemplo `https://eniflare.example.com`) y una **API key**
  (algo como `enf_xxx`).

---

## 2. Autenticarse con GitHub Packages

GitHub Packages **requiere autenticación incluso para descargar** (a diferencia de
NuGet.org). Necesitas un **Personal Access Token (classic)** con el scope `read:packages`.

1. Ve a **GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic)**:
   https://github.com/settings/tokens
2. **Generate new token (classic)**.
3. Marca el scope:
   - ✅ `read:packages` — para **instalar** paquetes (suficiente para consumir).
   - (Solo si además vas a **publicar** paquetes a mano necesitarías `write:packages`.)
4. Genera el token y **cópialo** (no se vuelve a mostrar).

> ⚠️ **Trata el token como una contraseña.** No lo subas a ningún repositorio. Más abajo
> se explica cómo mantenerlo fuera del control de versiones.

---

## 3. Añadir la fuente de paquetes

Tienes dos formas. La recomendada es un archivo `nuget.config` **por proyecto/solución**.

### Forma recomendada — `nuget.config` (sin secreto en el archivo)

Crea un `nuget.config` en la raíz de tu solución (junto al `.sln`):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="github-enekoiza"
         value="https://nuget.pkg.github.com/Enekoiza/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <github-enekoiza>
      <add key="Username" value="%GITHUB_USERNAME%" />
      <add key="ClearTextPassword" value="%GITHUB_TOKEN%" />
    </github-enekoiza>
  </packageSourceCredentials>
</configuration>
```

Las credenciales se leen de **variables de entorno**, así que el token **no** queda escrito
en el archivo y puedes subir este `nuget.config` al repo sin riesgo.

Define las variables en tu máquina antes de restaurar:

**PowerShell (Windows):**
```powershell
$env:GITHUB_USERNAME = "tu-usuario-github"
$env:GITHUB_TOKEN    = "ghp_tuTokenAqui"
```

**bash / zsh (Linux/macOS):**
```bash
export GITHUB_USERNAME="tu-usuario-github"
export GITHUB_TOKEN="ghp_tuTokenAqui"
```

> Si prefieres no usar variables, puedes poner el token directamente en
> `ClearTextPassword`, pero entonces **añade `nuget.config` a tu `.gitignore`** para no
> filtrarlo.

### Forma alternativa — `dotnet nuget add source` (global)

Registra la fuente una sola vez en tu máquina (el token se guarda en la config global de
NuGet de tu usuario):

```bash
dotnet nuget add source "https://nuget.pkg.github.com/Enekoiza/index.json" \
  --name github-enekoiza \
  --username tu-usuario-github \
  --password ghp_tuTokenAqui \
  --store-password-in-clear-text
```

---

## 4. Instalar el paquete

Con la fuente y las credenciales listas:

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

## 5. Configurar el logger

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

## 6. Opciones disponibles

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

## 7. Comprobar que funciona

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

## 8. Solución de problemas

**`error NU1301: Unable to load the service index` o `401 Unauthorized` al restaurar**
- Falta autenticación o el token no tiene `read:packages`. Revisa el paso
  [2](#2-autenticarse-con-github-packages) y que `GITHUB_USERNAME` / `GITHUB_TOKEN` estén
  definidos en la terminal donde ejecutas `dotnet restore`.

**`Unable to find package Eniflare.Serilog`**
- La fuente `github-enekoiza` no está registrada o apunta mal. Comprueba con:
  ```bash
  dotnet nuget list source
  ```

**Los logs no aparecen en Eniflare**
- Asegúrate de llamar a `Log.CloseAndFlush()` antes de que el proceso termine (en apps de
  consola/cortas), o los últimos lotes pueden no enviarse.
- Verifica que `baseUrl` es la raíz correcta (sin `/ingest`, se añade solo) y que la
  `apiKey` es válida.

**Quiero migrar a NuGet.org en el futuro**
- El repositorio incluye en `.github/workflows/release.yml` un bloque comentado para
  publicar en NuGet.org usando un secret `NUGET_API_KEY`. Los consumidores ya no
  necesitarían autenticarse para instalar.
