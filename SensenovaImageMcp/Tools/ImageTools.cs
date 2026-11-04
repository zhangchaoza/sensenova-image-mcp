using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace SensenovaImageMcp.Tools;

/// <summary>
/// SenseNova image generation and editing tools backed by the OpenAI-compatible
/// <c>https://token.sensenova.cn/v1</c> API.
/// </summary>
[McpServerToolType]
public class ImageTools
{
    /// <summary>Model used by default (slower, higher quality).</summary>
    public const string DefaultModel = "sensenova-u1.5-lite";

    /// <summary>Model used when <c>fastMode</c> is enabled.</summary>
    public const string FastModel = "sensenova-u1.5-fast";

    private const string DefaultBaseUrl = "https://token.sensenova.cn/v1";

    /// <summary>Environment variable that provides the SenseNova API key.</summary>
    public const string ApiKeyEnvVar = "SENSENOVA_IMAGE_MCP_KEY";

    /// <summary>HTTP client used for SenseNova API calls. Exposed for unit testing.</summary>
    internal static HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        return new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
    }

    /// <summary>
    /// Synchronously generates one or more images from a text prompt using the SenseNova
    /// U1.5 image models. When <paramref name="fastMode"/> is true the faster
    /// "sensenova-u1.5-fast" model is used, otherwise the default "sensenova-u1.5-lite" model.
    /// The generated image(s) are returned as temporary URLs valid for 24 hours.
    /// </summary>
    [McpServerTool(
        Name = "generate-image",
        Title = "Generate image with SenseNova",
        OpenWorld = true,
        ReadOnly = false)]
    [Description(
        "Generates an image from a text description using SenseNova. Returns the temporary " +
        "image URL(s). Set fastMode=true to use the faster 'SenseNova U1.5 Fast' model; the " +
        "default is the higher-quality 'SenseNova U1.5 Lite' model.")]
    public static async Task<ImageResult> GenerateImage(
        [Description("Detailed text description of the image to generate.")] string prompt,
        [Description(
            "When true, uses the faster 'SenseNova U1.5 Fast' model; when false (default) " +
            "uses the higher-quality 'SenseNova U1.5 Lite' model.")]
        bool fastMode = false,
        [Description("Dimensions of the generated image, e.g. '1024x1024' or '2752x1536'. " +
            "Sizes are multiples of 32 with a max ratio of 3:1.")] string size = "1024x1024",
        [Description("Number of images to generate. Defaults to 1.")] int n = 1,
        [Description("Whether to add a watermark. Weakens free-tier beta quota usage.")] bool watermark = false,
        [Description("Whether to auto-extend/optimize the prompt. Defaults to true.")] bool promptExtend = true,
        CancellationToken cancellationToken = default)
    {
        var model = fastMode ? FastModel : DefaultModel;

        var payload = new JsonObject
        {
            ["model"] = model,
            ["prompt"] = prompt,
            ["size"] = size,
            ["n"] = n,
            ["response_format"] = "url",
            ["output_format"] = "png",
            ["watermark"] = watermark,
            ["prompt_extend"] = promptExtend,
        };

        return await CallSenseNovaAsync("/images/generations", payload, cancellationToken);
    }

    /// <summary>
    /// Synchronously edits an existing image (from an image URL or a base64 data URI) using the
    /// SenseNova U1.5 image models. When <paramref name="fastMode"/> is true the faster
    /// "sensenova-u1.5-fast" model is used, otherwise the default "sensenova-u1.5-lite" model.
    /// The edited image is returned as a temporary URL valid for 24 hours.
    /// </summary>
    [McpServerTool(
        Name = "edit-image",
        Title = "Edit an image with SenseNova",
        OpenWorld = true,
        ReadOnly = false)]
    [Description(
        "Edits an existing image based on a text instruction using SenseNova. Pass the input " +
        "image as an https/http URL or a base64 data URI (data:image/png;base64,...). Returns " +
        "the edited image's temporary URL. Set fastMode=true to use the faster 'SenseNova U1.5 " +
        "Fast' model; the default is the higher-quality 'SenseNova U1.5 Lite' model.")]
    public static async Task<ImageResult> EditImage(
        [Description("Instruction describing how to change the input image, e.g. change the " +
            "background to mountains.")] string prompt,
        [Description("The input image: an https/http URL or a base64 data URI " +
            "(data:image/png;base64,...).")] string image,
        [Description(
            "When true, uses the faster 'SenseNova U1.5 Fast' model; when false (default) " +
            "uses the higher-quality 'SenseNova U1.5 Lite' model.")]
        bool fastMode = false,
        [Description("Number of images to produce. Defaults to 1.")] int n = 1,
        [Description("Whether to add a watermark.")] bool watermark = false,
        [Description("Whether to auto-extend/optimize the prompt. Defaults to true.")] bool promptExtend = true,
        CancellationToken cancellationToken = default)
    {
        var model = fastMode ? FastModel : DefaultModel;

        // The edits API expects the image wrapped as { "images": [ { "image_url": ... } ] }.
        var payload = new JsonObject
        {
            ["model"] = model,
            ["images"] = new JsonArray(
                new JsonObject { ["image_url"] = image }),
            ["prompt"] = prompt,
            ["size"] = "auto",
            ["n"] = n,
            ["response_format"] = "url",
            ["watermark"] = watermark,
            ["prompt_extend"] = promptExtend,
        };

        return await CallSenseNovaAsync("/images/edits", payload, cancellationToken);
    }

    private static async Task<ImageResult> CallSenseNovaAsync(
        string endpoint,
        JsonObject payload,
        CancellationToken cancellationToken)
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvVar);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"The environment variable {ApiKeyEnvVar} is not set. Provide a SenseNova API key.");
        }

        var baseUrl = Environment.GetEnvironmentVariable("SENSENOVA_IMAGE_MCP_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = DefaultBaseUrl;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await HttpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Failed to reach the SenseNova API: {ex.Message}", ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"SenseNova API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}): {Truncate(body, 1000)}");
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var urls = new List<string>();
                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("url", out var url) &&
                            url.ValueKind == JsonValueKind.String &&
                            !string.IsNullOrEmpty(url.GetString()))
                        {
                            urls.Add(url.GetString()!);
                        }
                    }

                    if (urls.Count > 0)
                    {
                        return new ImageResult(urls);
                    }
                }

                throw new InvalidOperationException(
                    "The SenseNova API response did not contain any image URL. Raw response: " + Truncate(body, 1000));
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Invalid JSON in the SenseNova response: {ex.Message}", ex);
            }
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength] + "…";
}

/// <summary>Result returned by the SenseNova image tools.</summary>
public record ImageResult(
    [property: Description("Temporary image URL(s) valid for 24 hours.")] IReadOnlyList<string> Urls);