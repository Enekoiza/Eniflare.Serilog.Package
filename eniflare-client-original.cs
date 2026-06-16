// =============================================================================
//  Eniflare — drop-in Serilog client
// -----------------------------------------------------------------------------
//  Copy this single file into any .NET app you want to monitor with Eniflare.
//
//  1. Add the NuGet packages:
//       dotnet add package Serilog.AspNetCore
//       dotnet add package Serilog.Sinks.Http
//       dotnet add package Serilog.Formatting.Compact
//
//  2. Configure the logger (one line) — see usage at the bottom of this file.
//
//  It ships logs to Eniflare's POST /ingest endpoint in CLEF batches, sending
//  the application's API key in the X-Api-Key header. Batching/retry is handled
//  by Serilog.Sinks.Http in the background, so it never blocks your app.
// =============================================================================

using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Http;

namespace Eniflare.Client;

/// <summary>
/// Serilog HTTP client that authenticates against Eniflare with an API key header.
/// </summary>
public sealed class EniflareHttpClient(string apiKey) : IHttpClient
{
    private readonly HttpClient _client = new();

    public void Configure(IConfiguration configuration)
    {
        // API key may also come from configuration ("Eniflare:ApiKey") if not passed in.
        var fromConfig = configuration["Eniflare:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(fromConfig))
            _apiKey = fromConfig;
    }

    private string _apiKey = apiKey;

    public async Task<HttpResponseMessage> PostAsync(
        string requestUri, Stream contentStream, CancellationToken cancellationToken)
    {
        using var content = new StreamContent(contentStream);
        content.Headers.ContentType = new("application/json");
        content.Headers.Add("X-Api-Key", _apiKey);
        return await _client.PostAsync(requestUri, content, cancellationToken);
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>Extension methods to wire Eniflare into a Serilog logger configuration.</summary>
public static class EniflareSinkExtensions
{
    /// <summary>
    /// Sends log events to Eniflare. <paramref name="baseUrl"/> is the Eniflare root
    /// (e.g. "https://eniflare.example.com"); "/ingest" is appended automatically.
    /// </summary>
    public static LoggerConfiguration Eniflare(
        this LoggerSinkConfiguration sinkConfiguration,
        string baseUrl,
        string apiKey)
    {
        var ingestUri = $"{baseUrl.TrimEnd('/')}/ingest";

        return sinkConfiguration.Http(
            requestUri: ingestUri,
            queueLimitBytes: null,
            textFormatter: new CompactJsonFormatter(),   // CLEF
            httpClient: new EniflareHttpClient(apiKey));
    }
}

// =============================================================================
//  USAGE
// -----------------------------------------------------------------------------
//
//  // Program.cs (minimal)
//  Log.Logger = new LoggerConfiguration()
//      .Enrich.FromLogContext()
//      .Enrich.WithProperty("App", "my-service")          // optional, useful context
//      .WriteTo.Console()                                  // keep local logs too
//      .WriteTo.Eniflare("https://eniflare.example.com", "enf_your_api_key")
//      .CreateLogger();
//
//  // ASP.NET Core — read URL/key from configuration:
//  builder.Host.UseSerilog((ctx, cfg) => cfg
//      .Enrich.FromLogContext()
//      .WriteTo.Console()
//      .WriteTo.Eniflare(
//          ctx.Configuration["Eniflare:Url"]!,
//          ctx.Configuration["Eniflare:ApiKey"]!));
//
//  // Then just log normally — errors are auto-grouped on the server:
//  Log.Information("User {UserId} signed in", userId);
//  Log.Error(ex, "Failed to process order {OrderId}", orderId);
// =============================================================================