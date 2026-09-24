using System;

namespace PKBank.Desktop.Services.Updates.Types;

/// <summary>A published GitHub release, reduced to what the updater needs.</summary>
/// <param name="Version">Version parsed from <paramref name="Tag" />.</param>
/// <param name="Tag">Tag as published, in <c>x.y.z</c> form.</param>
/// <param name="Notes">Release body, shown in the update dialog; may be empty.</param>
/// <param name="PageUrl">Release page, offered whenever the app cannot install the update itself.</param>
/// <param name="DownloadUrl">AppImage asset, or <c>null</c> when the release ships none.</param>
/// <param name="DownloadSize">Asset size in bytes, <c>0</c> when unknown.</param>
public sealed record ReleaseInfo(
    Version Version,
    string Tag,
    string Notes,
    string PageUrl,
    string? DownloadUrl,
    long DownloadSize)
{
    /// <summary>An update can only be applied in place when it actually carries an AppImage.</summary>
    public bool HasAppImage => DownloadUrl is not null;
}

public enum UpdateCheckStatus
{
    /// <summary>The newest release is the running version (or older).</summary>
    UpToDate,

    UpdateAvailable,

    /// <summary>GitHub could not be reached, or answered something unusable; see <see cref="UpdateCheckResult.Error" />.</summary>
    Failed
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, ReleaseInfo? Release = null, string? Error = null)
{
    public static UpdateCheckResult UpToDate() => new(UpdateCheckStatus.UpToDate);

    public static UpdateCheckResult Available(ReleaseInfo release) => new(UpdateCheckStatus.UpdateAvailable, release);

    public static UpdateCheckResult Failed(string error) => new(UpdateCheckStatus.Failed, Error: error);
}
