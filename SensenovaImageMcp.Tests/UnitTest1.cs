using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SensenovaImageMcp.Tools;
using Xunit;

namespace SensenovaImageMcp.Tests;

/// <summary>
/// Tests for <see cref="ImageTools"/> — validates model selection,
/// payload construction, and response parsing against a mock HTTP transport.
/// </summary>
public class ImageToolsTests
{
    private const string ApiKeyEnvVar = "SENSENOVA_IMAGE_MCP_KEY";
    private const string FakeKey = "TEST_KEY_12345";

    // ── helpers ────────────────────────────────────────────────────────

    private static void SetKey() =>
        Environment.SetEnvironmentVariable(ApiKeyEnvVar, FakeKey);

    private static void ClearKey() =>
        Environment.SetEnvironmentVariable(ApiKeyEnvVar, null);

    /// <summary>Builds a valid SenseNova /v1/images/generations response.</summary>
    private static string GenGenResponse(string baseUrl, int n = 1)
    {
        var items = Enumerable.Range(0, n)
            .Select(i => $"{{\"url\":\"{baseUrl}/img_{i}.png\"}}")
            .Aggregate((a, b) => $"{a},{b}");
        return $"{{\"data\":[{items}]}}";
    }

    /// <summary>Builds a valid SenseNova /v1/images/edits response.</summary>
    private static string GenEditResponse(string baseUrl, int n = 1) =>
        GenGenResponse(baseUrl, n);

    /// <summary>Builds an HTTP error response body.</summary>
    private static string GenError(int status, string reason) =>
        $"{{\"error\":{{\"code\":{status},\"message\":\"{reason}\"}}}}";

    private class MockHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _factory;
        public MockHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> f) =>
            _factory = f;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage req, CancellationToken ct) => _factory(req, ct);
    }

    /// <summary>Injects a mock HttpClient into ImageTools' static field and runs the action.</summary>
    private static async Task WithMock(HttpClient hc, Func<Task> act)
    {
        var field = typeof(ImageTools).GetField("HttpClient",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static);
        if (field == null)
            throw new InvalidOperationException("Cannot find ImageTools.HttpClient — API may have changed.");
        field.SetValue(null, hc);
        try { await act(); }
        finally { field.SetValue(null, null); }
    }

    /// <summary>Injects a mock HttpClient and returns the result of the func.</summary>
    private static async Task<T> WithMock<T>(HttpClient hc, Func<Task<T>> act)
    {
        var field = typeof(ImageTools).GetField("HttpClient",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static);
        if (field == null)
            throw new InvalidOperationException("Cannot find ImageTools.HttpClient — API may have changed.");
        field.SetValue(null, hc);
        try { return await act(); }
        finally { field.SetValue(null, null); }
    }

    // ── test: missing API key ─────────────────────────────────────────

    [Fact]
    public async Task GenerateImage_ThrowsWhenApiKeyMissing()
    {
        ClearKey();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ImageTools.GenerateImage("test prompt"));
        Assert.Contains(ApiKeyEnvVar, ex.Message);
    }

    [Fact]
    public async Task EditImage_ThrowsWhenApiKeyMissing()
    {
        ClearKey();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ImageTools.EditImage("change bg", "https://example.test/in.png"));
        Assert.Contains(ApiKeyEnvVar, ex.Message);
    }

    // ── test: model selection ────────────────────────────────────────

    [Fact]
    public async Task GenerateImage_DefaultModelUsesLite()
    {
        SetKey();
        bool gotFast = false;
        var h = new MockHandler(async (req, ct) =>
        {
            var body = await req.Content!.ReadAsStringAsync(ct);
            if (body.Contains("\"sensenova-u1.5-fast\"")) gotFast = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GenGenResponse("https://u.test/g"))
            };
        });
        using var hc = new HttpClient(h);
        await WithMock(hc, async () =>
        {
            await ImageTools.GenerateImage("red apple", fastMode: false);
            Assert.False(gotFast);
        });
    }

    [Fact]
    public async Task GenerateImage_FastModeUsesFastModel()
    {
        SetKey();
        bool gotFast = false;
        var h = new MockHandler(async (req, ct) =>
        {
            var body = await req.Content!.ReadAsStringAsync(ct);
            if (body.Contains("\"sensenova-u1.5-fast\"")) gotFast = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GenGenResponse("https://u.test/g"))
            };
        });
        using var hc = new HttpClient(h);
        await WithMock(hc, async () =>
        {
            await ImageTools.GenerateImage("red apple", fastMode: true);
            Assert.True(gotFast);
        });
    }

    [Fact]
    public async Task EditImage_DefaultModelUsesLite()
    {
        SetKey();
        bool gotFast = false;
        var h = new MockHandler(async (req, ct) =>
        {
            var body = await req.Content!.ReadAsStringAsync(ct);
            if (body.Contains("\"sensenova-u1.5-fast\"")) gotFast = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GenEditResponse("https://u.test/e"))
            };
        });
        using var hc = new HttpClient(h);
        await WithMock(hc, async () =>
        {
            await ImageTools.EditImage("mountains", "https://u.test/in.png", fastMode: false);
            Assert.False(gotFast);
        });
    }

    [Fact]
    public async Task EditImage_FastModeUsesFastModel()
    {
        SetKey();
        bool gotFast = false;
        var h = new MockHandler(async (req, ct) =>
        {
            var body = await req.Content!.ReadAsStringAsync(ct);
            if (body.Contains("\"sensenova-u1.5-fast\"")) gotFast = true;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GenEditResponse("https://u.test/e"))
            };
        });
        using var hc = new HttpClient(h);
        await WithMock(hc, async () =>
        {
            await ImageTools.EditImage("mountains", "https://u.test/in.png", fastMode: true);
            Assert.True(gotFast);
        });
    }

    // ── test: payload fields ───────────────────────────────────────────

    [Fact]
    public async Task GenerateImage_PayloadContainsExpectedFields()
    {
        SetKey();
        var captured = new List<string>();
        var h = new MockHandler(async (req, ct) =>
        {
            captured.Add(await req.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GenGenResponse("https://u.test/out"))
            };
        });
        using var hc = new HttpClient(h);
        await WithMock(hc, async () =>
        {
            await ImageTools.GenerateImage("a sunset over the ocean", fastMode: false,
                                size: "1920x1080", n: 2, watermark: true, promptExtend: false);
            var body = captured[0];
            Assert.Contains("\"model\":\"sensenova-u1.5-lite\"", body);
            Assert.Contains("\"prompt\":\"a sunset over the ocean\"", body);
            Assert.Contains("\"size\":\"1920x1080\"", body);
            Assert.Contains("\"n\":2", body);
            Assert.Contains("\"watermark\":true", body);
            Assert.Contains("\"prompt_extend\":false", body);
            Assert.Contains("\"response_format\":\"url\"", body);
            Assert.Contains("\"output_format\":\"png\"", body);
        });
    }

    [Fact]
    public async Task EditImage_PayloadContainsExpectedFields()
    {
        SetKey();
        var captured = new List<string>();
        var h = new MockHandler(async (req, ct) =>
        {
            captured.Add(await req.Content!.ReadAsStringAsync(ct));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GenEditResponse("https://u.test/out"))
            };
        });
        using var hc = new HttpClient(h);
        await WithMock(hc, async () =>
        {
            await ImageTools.EditImage("make it greyscale", "https://u.test/input.jpg",
                            fastMode: false, n: 1, watermark: false, promptExtend: true);
            var body = captured[0];
            Assert.Contains("\"model\":\"sensenova-u1.5-lite\"", body);
            Assert.Contains("\"prompt\":\"make it greyscale\"", body);
            Assert.Contains("\"image_url\":\"https://u.test/input.jpg\"", body);
            Assert.Contains("\"images\":[", body);
            Assert.Contains("\"size\":\"auto\"", body);
            Assert.Contains("\"response_format\":\"url\"", body);
        });
    }

    // ── test: response parsing ─────────────────────────────────────────

    [Fact]
    public async Task GenerateImage_ReturnsUrlList()
    {
        SetKey();
        var h = new MockHandler(async (req, ct) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(GenGenResponse("https://cdn.test", n: 2))
        });
        using var hc = new HttpClient(h);
        var img = await WithMock(hc, () => ImageTools.GenerateImage("test"));
        Assert.Equal(2, img.Urls.Count);
        Assert.EndsWith("img_0.png", img.Urls[0]);
        Assert.EndsWith("img_1.png", img.Urls[1]);
    }

    [Fact]
    public async Task EditImage_ReturnsSingleUrl()
    {
        SetKey();
        var h = new MockHandler(async (req, ct) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(GenEditResponse("https://cdn.test/edit"))
        });
        using var hc = new HttpClient(h);
        var img = await WithMock(hc, () => ImageTools.EditImage("change", "https://u.test/in.png"));
        Assert.Single(img.Urls);
        Assert.EndsWith("img_0.png", img.Urls[0]);
    }

    [Fact]
    public async Task GenerateImage_HttpErrorThrows()
    {
        SetKey();
        var h = new MockHandler(async (req, ct) => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(GenError(429, "rate limit"))
        });
        using var hc = new HttpClient(h);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WithMock(hc, () => ImageTools.GenerateImage("test")));
        Assert.Contains("429", ex.Message);
        Assert.Contains("rate limit", ex.Message);
    }

    [Fact]
    public async Task GenerateImage_NoUrlsThrows()
    {
        SetKey();
        var h = new MockHandler(async (req, ct) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}")
        });
        using var hc = new HttpClient(h);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => WithMock(hc, () => ImageTools.GenerateImage("test")));
        Assert.Contains("did not contain any image URL", ex.Message);
    }

    // ── test: base URL override ────────────────────────────────────────

    [Fact]
    public async Task GenerateImage_UsesCustomBaseUrl()
    {
        SetKey();
        Environment.SetEnvironmentVariable("SENSENOVA_IMAGE_MCP_BASE_URL", "https://custom.test/v1");
        try
        {
            var h = new MockHandler(async (req, ct) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GenGenResponse("https://custom.test/img"))
            });
            using var hc = new HttpClient(h);
            var img = await WithMock(hc, () => ImageTools.GenerateImage("x"));
            Assert.Single(img.Urls);
        }
        finally { Environment.SetEnvironmentVariable("SENSENOVA_IMAGE_MCP_BASE_URL", null); }
    }

    // ── test: headers ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateImage_AuthHeaderSet()
    {
        SetKey();
        bool hasAuth = false;
        string authValue = "";
        var h = new MockHandler(async (req, ct) =>
        {
            if (req.Headers.TryGetValues("Authorization", out var vals))
            {
                hasAuth = true;
                authValue = vals.FirstOrDefault() ?? "";
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GenGenResponse("https://u.test/img"))
            };
        });
        using var hc = new HttpClient(h);
        await WithMock(hc, () => ImageTools.GenerateImage("test"));
        Assert.True(hasAuth);
        Assert.StartsWith("Bearer ", authValue);
        Assert.Equal($"Bearer {FakeKey}", authValue);
    }

    [Fact]
    public async Task GenerateImage_ContentTypeJson()
    {
        SetKey();
        string ct = "";
        var h = new MockHandler(async (req, ct0) =>
        {
            ct = req.Content?.Headers.ContentType?.MediaType ?? "";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(GenGenResponse("https://u.test/img"))
            };
        });
        using var hc = new HttpClient(h);
        await WithMock(hc, () => ImageTools.GenerateImage("test"));
        Assert.Contains("application/json", ct);
    }
}
