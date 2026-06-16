using System.Net;
using System.Text;
using System.Text.Json;
using Serilog;
using Xunit;

namespace Eniflare.Serilog.Tests;

/// <summary>
/// End-to-end tests that exercise the real <c>WriteTo.Eniflare(...)</c> pipeline — including
/// the <c>Serilog.Sinks.Http</c> batch formatter — against a local HTTP listener, so the body
/// asserted here is exactly what ships over the wire (a JSON array of CLEF objects).
/// </summary>
public class EniflareSinkIntegrationTests
{
    private sealed record Captured(string Method, string Path, string? ApiKey, string? ContentType, string Body);

    private static async Task<Captured> ShipOneEventAsync(int port)
    {
        var baseUrl = $"http://localhost:{port}";
        var tcs = new TaskCompletionSource<Captured>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var listener = new HttpListener();
        listener.Prefixes.Add(baseUrl + "/");
        listener.Start();

        _ = Task.Run(async () =>
        {
            try
            {
                var ctx = await listener.GetContextAsync();
                using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                var captured = new Captured(
                    ctx.Request.HttpMethod,
                    ctx.Request.Url!.AbsolutePath,
                    ctx.Request.Headers["X-Api-Key"],
                    ctx.Request.ContentType,
                    body);
                ctx.Response.StatusCode = 200;
                ctx.Response.Close();
                tcs.TrySetResult(captured);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        using (var logger = new LoggerConfiguration()
                   .Enrich.FromLogContext()
                   .WriteTo.Eniflare(baseUrl, "enf_test", period: TimeSpan.FromMilliseconds(200))
                   .CreateLogger())
        {
            logger.Information("User {UserId} signed in", 42);
            logger.Error(new InvalidOperationException("boom"), "Failed order {OrderId}", 7);
        } // Dispose flushes pending batches.

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.True(completed == tcs.Task, "No request was received within 15 seconds.");
        return await tcs.Task;
    }

    [Fact]
    public async Task Shipped_request_posts_clef_array_to_ingest_with_api_key()
    {
        var captured = await ShipOneEventAsync(port: 5098);

        Assert.Equal("POST", captured.Method);
        Assert.Equal("/ingest", captured.Path);
        Assert.Equal("enf_test", captured.ApiKey);
        Assert.StartsWith("application/json", captured.ContentType);

        using var doc = JsonDocument.Parse(captured.Body);

        // Serilog.Sinks.Http's default batch formatter wraps events in a JSON array.
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() >= 1);

        foreach (var evt in doc.RootElement.EnumerateArray())
        {
            Assert.True(evt.TryGetProperty("@t", out _), "CLEF event must have @t");
            var hasMessage = evt.TryGetProperty("@mt", out _) || evt.TryGetProperty("@m", out _);
            Assert.True(hasMessage, "CLEF event must have @mt or @m");
        }
    }
}
