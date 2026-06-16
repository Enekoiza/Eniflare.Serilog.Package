using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Eniflare.Serilog;

/// <summary>
/// Extension methods that wire the Eniflare sink into a Serilog logger configuration.
/// </summary>
public static class EniflareSinkExtensions
{
    /// <summary>
    /// Sends log events to Eniflare in CLEF batches over HTTP.
    /// </summary>
    /// <param name="sinkConfiguration">The logger sink configuration being extended.</param>
    /// <param name="baseUrl">
    /// The Eniflare root URL (for example <c>https://eniflare.example.com</c>). The
    /// <c>/ingest</c> path is appended automatically and any trailing slash is normalized.
    /// </param>
    /// <param name="apiKey">
    /// The Eniflare API key sent in the <c>X-Api-Key</c> header. If left empty, the key is
    /// resolved from the <c>Eniflare:ApiKey</c> configuration value at runtime.
    /// </param>
    /// <param name="queueLimitBytes">
    /// The maximum size, in bytes, of events queued in memory while waiting to be shipped.
    /// <see langword="null"/> (the default) means no limit.
    /// </param>
    /// <param name="logEventLimitBytes">
    /// The maximum size, in bytes, of a single serialized log event. Events larger than this
    /// are dropped. <see langword="null"/> (the default) means no limit.
    /// </param>
    /// <param name="batchSizeLimit">The maximum number of events sent in a single batch.</param>
    /// <param name="period">The interval between batch shipments.</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed to the sink.</param>
    /// <returns>The logger configuration, to allow method chaining.</returns>
    /// <example>
    /// Configure the sink in code:
    /// <code>
    /// Log.Logger = new LoggerConfiguration()
    ///     .Enrich.FromLogContext()
    ///     .WriteTo.Console()
    ///     .WriteTo.Eniflare("https://eniflare.example.com", "enf_your_api_key")
    ///     .CreateLogger();
    /// </code>
    /// </example>
    /// <example>
    /// Configure the sink via <c>appsettings.json</c> (requires <c>Serilog.Settings.Configuration</c>):
    /// <code>
    /// {
    ///   "Serilog": {
    ///     "Using": [ "Eniflare.Serilog" ],
    ///     "WriteTo": [
    ///       { "Name": "Console" },
    ///       { "Name": "Eniflare", "Args": { "baseUrl": "https://eniflare.example.com", "apiKey": "enf_your_api_key" } }
    ///     ]
    ///   }
    /// }
    /// </code>
    /// </example>
    public static LoggerConfiguration Eniflare(
        this LoggerSinkConfiguration sinkConfiguration,
        string baseUrl,
        string apiKey = "",
        long? queueLimitBytes = null,
        int? logEventLimitBytes = null,
        int batchSizeLimit = 1000,
        TimeSpan? period = null,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var ingestUri = $"{baseUrl.TrimEnd('/')}/ingest";

        return sinkConfiguration.Http(
            requestUri: ingestUri,
            queueLimitBytes: queueLimitBytes,
            logEventLimitBytes: logEventLimitBytes,
            logEventsInBatchLimit: batchSizeLimit,
            period: period,
            textFormatter: new CompactJsonFormatter(),
            restrictedToMinimumLevel: restrictedToMinimumLevel,
            httpClient: new EniflareHttpClient(apiKey));
    }
}
