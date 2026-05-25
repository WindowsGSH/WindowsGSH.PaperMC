using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsGSH.Modules.PaperMC;

public sealed class PaperDownloadClient : IDisposable
{
    private const string Project = "paper";
    private const string BaseUrl = "https://fill.papermc.io/v3";
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public PaperDownloadClient()
        : this(new HttpClient(), ownsClient: true)
    {
    }

    public PaperDownloadClient(HttpClient httpClient, bool ownsClient = false)
    {
        _httpClient = httpClient;
        _ownsClient = ownsClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WindowsGSH", "0.1.0"));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(https://github.com/shenniko/WindowsGSH)"));
        }
    }

    public async Task<PaperBuildInfo> ResolveBuildAsync(
        string minecraftVersion,
        string requestedBuild,
        CancellationToken cancellationToken)
    {
        var builds = await GetBuildsAsync(minecraftVersion, cancellationToken);
        if (builds.Length == 0)
        {
            throw new InvalidOperationException($"No PaperMC builds were returned for Minecraft {minecraftVersion}.");
        }

        var build = string.IsNullOrWhiteSpace(requestedBuild) ||
            string.Equals(requestedBuild, "latest", StringComparison.OrdinalIgnoreCase)
            ? SelectLatestStableBuild(builds)
            : builds.FirstOrDefault(item => string.Equals(item.Id, requestedBuild.Trim(), StringComparison.OrdinalIgnoreCase));

        if (build == null)
        {
            throw new InvalidOperationException($"No matching PaperMC build was found for Minecraft {minecraftVersion}.");
        }

        if (!string.Equals(build.Channel, "STABLE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"PaperMC build {build.Id} for Minecraft {minecraftVersion} is not stable.");
        }

        if (!build.Downloads.TryGetValue("server:default", out var download) ||
            string.IsNullOrWhiteSpace(download.Url))
        {
            throw new InvalidOperationException($"PaperMC build {build.Id} did not include a server download URL.");
        }

        return new PaperBuildInfo(minecraftVersion, build.Id, download.Name ?? $"paper-{minecraftVersion}-{build.Id}.jar", download.Url);
    }

    private static PaperBuild? SelectLatestStableBuild(IEnumerable<PaperBuild> builds)
    {
        var stableBuilds = builds
            .Where(item => string.Equals(item.Channel, "STABLE", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (stableBuilds.Length == 0)
        {
            return null;
        }

        var numericStableBuilds = stableBuilds
            .Select(item => new { Build = item, NumericId = int.TryParse(item.Id, out var id) ? id : (int?)null })
            .Where(item => item.NumericId.HasValue)
            .ToArray();

        return numericStableBuilds.Length > 0
            ? numericStableBuilds.OrderByDescending(item => item.NumericId!.Value).First().Build
            : stableBuilds.First();
    }

    public async Task DownloadAsync(string downloadUrl, string targetPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = targetPath + ".tmp";
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        File.Move(tempPath, targetPath);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<PaperBuild[]> GetBuildsAsync(string minecraftVersion, CancellationToken cancellationToken)
    {
        var url = $"{BaseUrl}/projects/{Project}/versions/{Uri.EscapeDataString(minecraftVersion)}/builds";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<PaperBuild[]>(stream, JsonOptions, cancellationToken) ?? [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new FlexibleStringConverter() }
    };

    private sealed class PaperBuild
    {
        public string Id { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public Dictionary<string, PaperDownload> Downloads { get; set; } = [];
    }

    private sealed class PaperDownload
    {
        public string? Name { get; set; }
        public string Url { get; set; } = string.Empty;
    }

    private sealed class FlexibleStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString() ?? string.Empty,
                JsonTokenType.Number => reader.TryGetInt64(out var longValue)
                    ? longValue.ToString()
                    : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonTokenType.True => bool.TrueString,
                JsonTokenType.False => bool.FalseString,
                _ => string.Empty
            };
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}

public sealed record PaperBuildInfo(string Version, string BuildId, string FileName, string DownloadUrl);
