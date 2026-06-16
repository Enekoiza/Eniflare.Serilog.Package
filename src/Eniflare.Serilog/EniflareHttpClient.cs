using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace Eniflare.Serilog;

/// <summary>
/// A <see cref="IHttpClient"/> implementation that ships Serilog batches to Eniflare,
/// authenticating each request with the API key in the <c>X-Api-Key</c> request header.
/// </summary>
/// <remarks>
/// The API key is taken from the constructor argument. If that value is empty and the
/// sink is configured through <c>appsettings.json</c>, the key is resolved from the
/// <c>Eniflare:ApiKey</c> configuration value via <see cref="Configure"/>.
/// </remarks>
public sealed class EniflareHttpClient : IHttpClient
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    private readonly HttpClient _client;
    private string? _apiKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="EniflareHttpClient"/> class.
    /// </summary>
    /// <param name="apiKey">
    /// The Eniflare API key sent in the <c>X-Api-Key</c> header. May be <see langword="null"/>
    /// or empty when the key is supplied through configuration instead (see <see cref="Configure"/>).
    /// </param>
    public EniflareHttpClient(string? apiKey)
    {
        _apiKey = apiKey;
        _client = new HttpClient();
    }

    /// <summary>
    /// Test-only constructor that allows a custom <see cref="HttpMessageHandler"/> to be
    /// injected, avoiding a real network connection.
    /// </summary>
    internal EniflareHttpClient(string? apiKey, HttpMessageHandler handler)
    {
        _apiKey = apiKey;
        _client = new HttpClient(handler);
    }

    /// <summary>
    /// Resolves the API key from configuration when it was not supplied to the constructor.
    /// Called by Serilog when the sink is configured via <c>Serilog.Settings.Configuration</c>.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    public void Configure(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            return;
        }

        var fromConfig = configuration["Eniflare:ApiKey"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            _apiKey = fromConfig;
        }
    }

    /// <summary>
    /// Sends a batch of log events to the Eniflare ingest endpoint.
    /// </summary>
    /// <param name="requestUri">The absolute ingest URI to post to.</param>
    /// <param name="contentStream">The CLEF-formatted batch payload.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The HTTP response returned by Eniflare.</returns>
    public async Task<HttpResponseMessage> PostAsync(
        string requestUri,
        Stream contentStream,
        CancellationToken cancellationToken)
    {
        using var content = new StreamContent(contentStream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = content,
        };

        // X-Api-Key is a request header, not a content header.
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, _apiKey);
        }

        return await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();
}
