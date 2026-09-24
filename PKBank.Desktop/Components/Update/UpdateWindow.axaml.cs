using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Services.Updates;
using PKBank.Desktop.Services.Updates.Types;

namespace PKBank.Desktop.Components.Update;

/// <summary>
///     Offers a newer release, downloads it over the running AppImage and asks for the restart that puts it to
///     use. The whole exchange happens in this one window, which walks through <see cref="Stage" />.
/// </summary>
public sealed partial class UpdateWindow : Window
{
    private readonly ReleaseInfo? _release;
    private bool _closed;
    private CancellationTokenSource? _download;
    private bool _restartRequested;
    private Stage _stage = Stage.Offer;

    public UpdateWindow() => InitializeComponent(); // designer

    private UpdateWindow(ReleaseInfo release) : this()
    {
        _release = release;
        Title = $"{AppInfo.Name} Update";
        NotesText.Text = release.Notes;
        Render(Stage.Offer);
    }

    private enum Stage
    {
        /// <summary>A newer release exists; the user decides whether to take it.</summary>
        Offer,

        Downloading,

        /// <summary>The image has been replaced; only a restart is left.</summary>
        Ready,

        Failed
    }

    /// <summary>Whether the app can put the update in place by itself, or only point at the release page.</summary>
    private static bool CanInstall(ReleaseInfo release) => UpdateService.CanSelfUpdate && release.HasAppImage;

    /// <returns><c>true</c> when the user asked to restart into the installed update.</returns>
    public static async Task<bool> ShowAsync(Window owner, ReleaseInfo release)
    {
        var window = new UpdateWindow(release);
        await window.ShowDialog(owner);
        return window._restartRequested;
    }

    /// <summary>A download in flight is dropped when the window goes away; the staged file is cleaned up with it.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _download?.Cancel();
        base.OnClosed(e);
    }

    private void Render(Stage stage, string? message = null)
    {
        if (_release is not { } release)
            return;

        _stage = stage;
        var installable = CanInstall(release);

        HeadlineText.Text = stage switch
        {
            Stage.Downloading => $"Downloading {AppInfo.Name} {release.Tag}…",
            Stage.Ready => $"{AppInfo.Name} {release.Tag} is ready",
            Stage.Failed => "The update could not be installed",
            _ => $"{AppInfo.Name} {release.Tag} is available"
        };

        DetailText.Text = stage switch
        {
            Stage.Downloading => "The new version is written next to the running one and takes over on restart.",
            Stage.Ready => $"Restart {AppInfo.Name} to use it.",
            Stage.Failed => message ?? "Unknown error.",
            _ => OfferDetail(release, installable)
        };

        NotesBox.IsVisible = stage is Stage.Offer && release.Notes.Length != 0;
        ProgressArea.IsVisible = stage is Stage.Downloading;
        ReleasePageButton.IsVisible = stage is Stage.Failed || (stage is Stage.Offer && !installable);

        DismissButton.Content = stage switch
        {
            Stage.Downloading => "Cancel",
            Stage.Failed => "Close",
            Stage.Offer when !installable => "Close",
            _ => "Later"
        };

        ActionButton.IsVisible = stage is Stage.Ready || (stage is Stage.Offer && installable);
        ActionButton.Content = stage is Stage.Ready ? "Restart Now" : "Update";
    }

    private static string OfferDetail(ReleaseInfo release, bool installable)
    {
        var running = $"You are running {UpdateService.CurrentVersion.ToString(3)}.";
        if (installable)
            return release.DownloadSize > 0
                ? $"{running} The update downloads {FormatSize(release.DownloadSize)} and replaces the AppImage you started."
                : $"{running} The update replaces the AppImage you started.";

        return release.HasAppImage
            ? $"{running} {AppInfo.Name} can only update itself when it runs from an AppImage; download the new version from the release page."
            : $"{running} This release ships no AppImage for this system; see the release page.";
    }

    private async void OnActionClicked(object? sender, RoutedEventArgs e)
    {
        if (_release is not { } release)
            return;

        if (_stage is Stage.Ready)
        {
            _restartRequested = true;
            Close();
            return;
        }

        if (_stage is not Stage.Offer || !CanInstall(release))
            return;

        _download = new CancellationTokenSource();
        Render(Stage.Downloading);
        ReportProgress(0);

        try
        {
            await UpdateService.InstallAsync(release, new Progress<double>(ReportProgress), _download.Token);
            Render(Stage.Ready);
        }
        catch (OperationCanceledException)
        {
            if (!_closed)
                Render(Stage.Offer); // cancelled from this window, not by it closing
        }
        catch (Exception error)
        {
            Render(Stage.Failed, error.Message);
        }
        finally
        {
            _download?.Dispose();
            _download = null;
        }
    }

    private void OnDismissClicked(object? sender, RoutedEventArgs e)
    {
        if (_stage is Stage.Downloading)
        {
            _download?.Cancel(); // the download loop restores the offer
            return;
        }

        Close();
    }

    private void OnReleasePageClicked(object? sender, RoutedEventArgs e) =>
        _ = Launcher.LaunchUriAsync(new Uri(_release?.PageUrl ?? UpdateService.ReleasesPageUrl));

    private void ReportProgress(double fraction)
    {
        DownloadProgress.Value = fraction;
        var size = _release is { DownloadSize: > 0 } release
            ? $" of {FormatSize(release.DownloadSize)}"
            : string.Empty;
        ProgressText.Text = $"{fraction:P0}{size}";
    }

    private static string FormatSize(long bytes) => bytes >= 1024 * 1024
        ? $"{bytes / (1024d * 1024d):0.#} MB"
        : $"{bytes / 1024d:0.#} KB";
}
