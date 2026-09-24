using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PKBank.Desktop.Services.Updates.Types;

namespace PKBank.Desktop.Services.Updates;

/// <summary>
///     Looks for a newer release on GitHub and, for an AppImage install, replaces the running image with it.
///     Nothing here runs on its own: the startup check and the Options menu drive it (see UpdateDialogs).
/// </summary>
public static class UpdateService
{
    public const string Repository = "BenoitSafari/PKBank";

    /// <summary>Set by the AppImage runtime to the image that was started; unset for every other launch.</summary>
    private const string AppImageVariable = "APPIMAGE";

    /// <summary>The check is a single small request; a stalled one must not keep the caller waiting.</summary>
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(20);

    private static readonly Uri LatestReleaseApi = new($"https://api.github.com/repos/{Repository}/releases/latest");

    private static readonly HttpClient Http = CreateClient();

    public static string ReleasesPageUrl { get; } = $"https://github.com/{Repository}/releases/latest";

    /// <summary>Version of the running build, as the release tags spell it.</summary>
    public static Version CurrentVersion { get; } = ParseVersion(AppInfo.Version) ?? new Version(0, 0, 0);

    /// <summary>
    ///     Path of the running AppImage, or <c>null</c> when the app was started any other way — a
    ///     <c>dotnet run</c> from the checkout, or a plain published folder. Only an AppImage can be replaced in
    ///     place, so that is also what tells a real install from a development run.
    /// </summary>
    public static string? AppImagePath { get; } = ResolveAppImagePath();

    /// <summary>Whether an update found by <see cref="CheckAsync" /> could be installed without the user's help.</summary>
    public static bool CanSelfUpdate => AppImagePath is not null;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            // Covers the asset download too; the check has its own, much shorter budget.
            Timeout = TimeSpan.FromMinutes(30)
        };
        // GitHub turns away requests without one; a literal keeps a surprising assembly version from
        // making the header unparsable.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PKBank-Updater");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <summary>Asks GitHub for the newest release and compares it with the running version.</summary>
    /// <remarks>Never throws for a network or payload problem: those come back as <see cref="UpdateCheckStatus.Failed" />.</remarks>
    public static async Task<UpdateCheckResult> CheckAsync(CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(CheckTimeout);

        try
        {
            await using var stream = await Http.GetStreamAsync(LatestReleaseApi, timeout.Token);
            var release = await JsonSerializer.DeserializeAsync(
                stream, GitHubJsonContext.Default.GitHubRelease, timeout.Token);

            if (release is null || release.Draft || release.TagName.Length == 0)
                return UpdateCheckResult.Failed("GitHub returned no usable release.");
            if (ParseVersion(release.TagName) is not { } version)
                return UpdateCheckResult.Failed($"Release tag “{release.TagName}” is not a x.y.z version.");
            if (version <= CurrentVersion)
                return UpdateCheckResult.UpToDate();

            var asset = PickAppImage(release);
            var page = release.HtmlUrl.Length != 0 ? release.HtmlUrl : ReleasesPageUrl;
            return UpdateCheckResult.Available(new ReleaseInfo(
                version, release.TagName, (release.Body ?? string.Empty).Trim(), page,
                asset?.BrowserDownloadUrl, asset?.Size ?? 0));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw; // the caller pulled the plug, not the timeout
        }
        catch (OperationCanceledException)
        {
            return UpdateCheckResult.Failed("GitHub did not answer in time.");
        }
        catch (Exception e)
        {
            return UpdateCheckResult.Failed(e.Message);
        }
    }

    /// <summary>
    ///     Downloads the release asset next to the running AppImage and moves it over it. The replacement is a
    ///     rename, so the running process keeps reading the old image and only a restart picks the new one up.
    /// </summary>
    /// <exception cref="InvalidOperationException">The app cannot update itself, or the release has no AppImage.</exception>
    /// <exception cref="UnauthorizedAccessException">The running image sits where the user cannot replace it.</exception>
    public static async Task InstallAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken token)
    {
        if (AppImagePath is not { } target)
            throw new InvalidOperationException($"{AppInfo.Name} was not started from an AppImage.");
        if (release.DownloadUrl is not { } url)
            throw new InvalidOperationException("This release does not ship an AppImage.");

        // Nothing here is worth an 80 MB download the app could not put in place afterwards.
        var folder = Path.GetDirectoryName(target) ?? ".";
        if (!CanWriteTo(folder))
            throw new UnauthorizedAccessException(
                $"“{target}” cannot be replaced from here. Move the AppImage to a folder you own, or " +
                "download the new version from the release page.");

        // Staged in the same folder so the move is a rename and never a half-written copy of the image.
        var staged = $"{target}.{release.Tag}.part";
        try
        {
            await DownloadAsync(url, staged, release.DownloadSize, progress, token);
            File.SetUnixFileMode(staged,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            File.Move(staged, target, true);
        }
        catch
        {
            TryDelete(staged);
            throw;
        }
    }

    private static async Task DownloadAsync(
        string url, string path, long expected, IProgress<double>? progress, CancellationToken token)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? expected;
        var copied = 0L;

        await using (var source = await response.Content.ReadAsStreamAsync(token))
        await using (var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(buffer, token)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), token);
                copied += read;
                if (total > 0)
                    progress?.Report(Math.Min(1d, (double)copied / total));
            }
        }

        // A truncated transfer must never replace a working image.
        if (total > 0 && copied != total)
            throw new IOException($"The download stopped after {copied} of {total} bytes.");
    }

    /// <summary>The image built for this machine, falling back to the only one when the names say nothing.</summary>
    private static GitHubReleaseAsset? PickAppImage(GitHubRelease release)
    {
        var images = release.Assets
            .Where(static a => a.Name.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "aarch64",
            _ => null
        };

        return images.FirstOrDefault(a =>
                   architecture is not null && a.Name.Contains(architecture, StringComparison.OrdinalIgnoreCase))
               ?? (images.Count == 1 ? images[0] : null);
    }

    /// <summary>Reads a <c>x.y.z</c> release tag, tolerating the usual <c>v</c> prefix.</summary>
    private static Version? ParseVersion(string text)
    {
        var trimmed = text.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(trimmed, out var version))
            return null;

        // Tags carry three parts, assembly versions four; compare them on the same footing.
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
    }

    private static string? ResolveAppImagePath()
    {
        if (!OperatingSystem.IsLinux())
            return null;

        var path = Environment.GetEnvironmentVariable(AppImageVariable);
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path) ? Path.GetFullPath(path) : null;
    }

    /// <summary>Whether the folder holding the running image takes a new file (a system-wide install does not).</summary>
    private static bool CanWriteTo(string folder)
    {
        var probe = Path.Combine(folder, $".pkbank-update-{Guid.NewGuid():N}");
        try
        {
            using (File.Create(probe))
            {
            }

            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Leftover staging file: harmless, and the next attempt overwrites it.
        }
    }
}
