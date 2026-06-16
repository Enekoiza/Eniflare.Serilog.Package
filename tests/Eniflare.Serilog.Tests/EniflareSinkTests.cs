using System.Net;
using System.Text;
using System.Text.Json;
using Eniflare.Serilog;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Compact;
using Xunit;

namespace Eniflare.Serilog.Tests;

public class EniflareSinkTests
{
    /// <summary>
    /// Captures a single outgoing request so assertions can be made against it without a real server.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static MemoryStream ClefStream(string message)
    {
        // Produce a CLEF line the way the sink does, so the body looks like a real batch.
        var clef = $$"""{"@t":"2026-06-16T00:00:00.0000000Z","@m":"{{message}}","@i":"00000000"}""";
        return new MemoryStream(Encoding.UTF8.GetBytes(clef + "\n"));
    }

    [Fact]
    public async Task PostAsync_posts_to_ingest_with_api_key_header()
    {
        var handler = new CapturingHandler();
        using var client = new EniflareHttpClient("enf_test_key", handler);

        using var response = await client.PostAsync(
            "https://eniflare.example.com/ingest",
            ClefStream("hello"),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://eniflare.example.com/ingest", handler.Request.RequestUri!.ToString());

        Assert.True(handler.Request.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("enf_test_key", Assert.Single(values));
    }

    [Fact]
    public async Task PostAsync_body_is_valid_clef()
    {
        var handler = new CapturingHandler();
        using var client = new EniflareHttpClient("enf_test_key", handler);

        await client.PostAsync(
            "https://eniflare.example.com/ingest",
            ClefStream("user signed in"),
            CancellationToken.None);

        var lines = handler.Body!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(lines);
        foreach (var line in lines)
        {
            using var doc = JsonDocument.Parse(line);
            Assert.True(doc.RootElement.TryGetProperty("@t", out _), "CLEF line must have @t");
            Assert.True(doc.RootElement.TryGetProperty("@m", out _), "CLEF line must have @m");
        }
    }

    private static IConfiguration InMemoryConfig(string key, string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

    [Fact]
    public async Task Configure_then_PostAsync_uses_configured_key()
    {
        var config = InMemoryConfig("Eniflare:ApiKey", "enf_from_config");
        var handler = new CapturingHandler();
        using var client = new EniflareHttpClient("", handler);

        client.Configure(config);
        await client.PostAsync("https://eniflare.example.com/ingest", ClefStream("x"), CancellationToken.None);

        Assert.True(handler.Request!.Headers.TryGetValues("X-Api-Key", out var values));
        Assert.Equal("enf_from_config", Assert.Single(values));
    }

    [Fact]
    public void Eniflare_extension_builds_a_usable_logger()
    {
        using var logger = new LoggerConfiguration()
            .WriteTo.Eniflare("https://eniflare.example.com/", "enf_test_key")
            .CreateLogger();

        // Should not throw when logging.
        logger.Information("User {UserId} signed in", 42);
        Assert.NotNull(logger);
    }
}
