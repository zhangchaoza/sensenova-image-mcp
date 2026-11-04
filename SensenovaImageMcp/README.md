# SenseNova Image MCP

An MCP server (C#/.NET) that exposes SenseTime **SenseNova** image models through tools:

- `generate-image` — text-to-image generation.
- `edit-image` — edit an existing image (URL or base64 data URI) from a text instruction.

Both tools accept a `fastMode` parameter: when `true` the faster **SenseNova U1.5 Fast**
(`sensenova-u1.5-fast`) model is used; by default the higher-quality **SenseNova U1.5 Lite**
(`sensenova-u1.5-lite`) model is used. Results are returned as temporary image URLs valid for
24 hours.

## Requirements

- [.NET SDK 11](https://dotnet.microsoft.com/download) (the project targets `net11.0`).
- A [SenseNova API key](https://platform.sensenova.cn/) provided via the
  `SENSENOVA_IMAGE_MCP_KEY` environment variable.

Optionally set `SENSENOVA_IMAGE_MCP_BASE_URL` to override the API base URL
(defaults to `https://token.sensenova.cn/v1`).

## Example configuration

```json
{
  "servers": {
    "sensenova-image-mcp": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "<PATH TO PROJECT DIRECTORY>"],
      "env": {
        "SENSENOVA_IMAGE_MCP_KEY": "<your api key>"
      }
    }
  }
}
```

## Tools

### generate-image

Generates an image from a text description.

| Parameter      | Type   | Default      | Notes |
|----------------|--------|--------------|-------|
| `prompt`       | string | (required)   | Detailed text description of the image. |
| `fastMode`     | bool   | `false`      | Use `sensenova-u1.5-fast` when true, `sensenova-u1.5-lite` otherwise. |
| `size`         | string | `1024x1024`  | e.g. `1024x1024`, `2752x1536`, `1536x2752`. Sizes are multiples of 32, max ratio 3:1. |
| `n`            | int    | `1`          | Number of images to generate. |
| `watermark`    | bool   | `false`      | Whether to add a watermark. |
| `promptExtend` | bool   | `true`       | Whether to auto-extend/optimize the prompt. |

### edit-image

Edits an existing image from a text instruction.

| Parameter      | Type   | Default      | Notes |
|----------------|--------|--------------|-------|
| `prompt`       | string | (required)   | Instruction describing how to change the image. |
| `image`        | string | (required)   | The input image: an `https`/`http` URL or a base64 data URI (`data:image/png;base64,...`). |
| `fastMode`     | bool   | `false`      | Use `sensenova-u1.5-fast` when true, `sensenova-u1.5-lite` otherwise. |
| `n`            | int    | `1`          | Number of images to produce. |
| `watermark`    | bool   | `false`      | Whether to add a watermark. |
| `promptExtend` | bool   | `true`       | Whether to auto-extend/optimize the prompt. |

## Testing the MCP Server

Once configured, you can ask your MCP client to generate or edit an image, for example
`Generate an image of a red apple on a white background` or
`Change the background of the image to a mountain scene`. The client should prompt you to use
the `generate-image` / `edit-image` tool on the `sensenova-image-mcp` MCP server.

## Publishing to NuGet.org

1. Run `dotnet pack -c Release` to create the NuGet package
2. Publish to NuGet.org with `dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json`

## Using the MCP Server from NuGet.org

Once the MCP server package is published to NuGet.org, you can configure it in your preferred IDE. Both VS Code and Visual Studio use the `dnx` command to download and install the MCP server package from NuGet.org.

- **VS Code**: Create a `<WORKSPACE DIRECTORY>/.vscode/mcp.json` file
- **Visual Studio**: Create a `<SOLUTION DIRECTORY>\.mcp.json` file

For both VS Code and Visual Studio, the configuration file uses the following server definition:

```json
{
  "servers": {
    "sensenova-image-mcp": {
      "type": "stdio",
      "command": "dnx",
      "args": [
        "<your package ID here>",
        "--version",
        "<your package version here>",
        "--yes"
      ]
    }
  }
}
```

## Running Tests

The project includes xUnit tests that verify the image generation and editing logic without requiring a live API connection:

```bash
# Run all tests
dotnet test sensenova-image-mcp.slnx

# Run specific test class
dotnet test --filter "FullyQualifiedName~ImageToolsTests"
```

## More information

.NET MCP servers use the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) C# SDK. For more information about MCP:

- [Official Documentation](https://modelcontextprotocol.io/)
- [Protocol Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Organization](https://github.com/modelcontextprotocol)
- [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)
