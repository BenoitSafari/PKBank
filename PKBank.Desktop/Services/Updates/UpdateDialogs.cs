using System;
using System.Threading.Tasks;
using PKBank.Desktop.Components;
using PKBank.Desktop.Components.Common.ConfirmationWindow;
using PKBank.Desktop.Components.Update;
using PKBank.Desktop.Services.Updates.Types;

namespace PKBank.Desktop.Services.Updates;

/// <summary>Ties the release check to the windows the user sees.</summary>
public static class UpdateDialogs
{
    private static string Title => $"{AppInfo.Name} Update";

    /// <summary>
    ///     Startup check: only a real install (an AppImage) looks for a release, and only a newer one ever shows
    ///     up, so a development run, an offline start or a GitHub hiccup stays out of the way.
    /// </summary>
    public static async Task CheckOnStartupAsync(MainWindow window)
    {
        if (!UpdateService.CanSelfUpdate)
            return;

        try
        {
            if (await UpdateService.CheckAsync() is { Status: UpdateCheckStatus.UpdateAvailable, Release: { } release })
                await OfferAsync(window, release);
        }
        catch (Exception)
        {
            // An unattended check is never worth interrupting the user for.
        }
    }

    /// <summary>Options ▸ Check for Updates: the user asked, so every outcome is reported.</summary>
    public static async Task CheckManuallyAsync(MainWindow window)
    {
        var result = await UpdateService.CheckAsync();
        switch (result)
        {
            case { Status: UpdateCheckStatus.UpdateAvailable, Release: { } release }:
                await OfferAsync(window, release);
                break;
            case { Status: UpdateCheckStatus.Failed, Error: var error }:
                await ConfirmationWindow.ShowMessageAsync(window, Title,
                    $"Could not check for updates.\n\n{error}");
                break;
            default:
                await ConfirmationWindow.ShowMessageAsync(window, Title,
                    $"{AppInfo.Name} {AppInfo.Version} is the latest version.");
                break;
        }
    }

    private static async Task OfferAsync(MainWindow window, ReleaseInfo release)
    {
        if (await UpdateWindow.ShowAsync(window, release) && UpdateService.AppImagePath is { } appImage)
            await window.TryRestartIntoAsync(appImage);
    }
}
