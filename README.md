# Eniflare.Serilog

A [Serilog](https://serilog.net/) sink that ships your application's log events to
[Eniflare](https://github.com/Enekoiza/Eniflare.Serilog.Package) in the background.

- Posts batches to `POST {baseUrl}/ingest` using the **CLEF** format (`CompactJsonFormatter`).
- Authenticates with your API key via the `X-Api-Key` request header.
- Batching, buffering and retries are handled by `Serilog.Sinks.Http` — logging never blocks your app.
- Targets `net8.0`, `net9.0` and `net10.0`.

This package contains **only the sink**. Your application provides `Serilog.AspNetCore`
(or plain `Serilog`) for the rest of the logging pipeline.

## Installation

```bash
dotnet add package Eniflare.Serilog
```

The package is published to **[NuGet.org](https://www.nuget.org/packages/Eniflare.Serilog)**,
so no authentication is required to install it.

> 📖 See the full step-by-step guide in [docs/USAGE.md](docs/USAGE.md).

## Usage

### Configure in code

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Eniflare("https://eniflare.example.com", "enf_your_api_key")
    .CreateLogger();
```

### Configure via `appsettings.json`

Requires the [`Serilog.Settings.Configuration`](https://www.nuget.org/packages/Serilog.Settings.Configuration)
package in your application. The `Args` keys map directly to the extension's parameter names.

```json
{
  "Serilog": {
    "Using": [ "Eniflare.Serilog" ],
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "Eniflare",
        "Args": {
          "baseUrl": "https://eniflare.example.com",
          "apiKey": "enf_your_api_key"
        }
      }
    ]
  }
}
```

You can omit `apiKey` from `Args` and instead provide it as `Eniflare:ApiKey` in
configuration (for example via an environment variable or user secret) — the sink
resolves it at runtime.

### Options

| Parameter                  | Default            | Description                                                  |
|----------------------------|--------------------|--------------------------------------------------------------|
| `baseUrl`                  | _(required)_       | Eniflare root URL; `/ingest` is appended automatically.      |
| `apiKey`                   | `""`               | API key; falls back to `Eniflare:ApiKey` configuration.      |
| `queueLimitBytes`          | `null` (unlimited) | Max bytes buffered in memory before shipping.                |
| `logEventLimitBytes`       | `null` (unlimited) | Max size of a single event; larger events are dropped.       |
| `batchSizeLimit`           | `1000`             | Max number of events per batch.                              |
| `period`                   | `null` (default)   | Interval between batch shipments.                            |
| `restrictedToMinimumLevel` | `Verbose`          | Minimum level passed to the sink.                            |

## Creating a release

Versioning is driven by git tags via [MinVer](https://github.com/adamralph/minver).
Tag the commit you want to publish and push the tag:

```bash
git tag v1.2.3
git push origin v1.2.3
```

The [`release.yml`](.github/workflows/release.yml) workflow builds, tests, packs and
pushes the `.nupkg` and `.snupkg` to GitHub Packages for the `Enekoiza` organization.

## License

MIT
