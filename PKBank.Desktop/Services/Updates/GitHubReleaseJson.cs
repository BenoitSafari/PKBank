using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PKBank.Desktop.Services.Updates;

/// <summary>Subset of the GitHub "latest release" payload the updater reads.</summary>
internal sealed class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("body")] public string? Body { get; set; }

    [JsonPropertyName("draft")] public bool Draft { get; set; }

    [JsonPropertyName("assets")] public List<GitHubReleaseAsset> Assets { get; set; } = [];
}

internal sealed class GitHubReleaseAsset
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")] public long Size { get; set; }
}

[JsonSerializable(typeof(GitHubRelease))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
