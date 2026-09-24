using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PKBank.Core.Configuration;
using PKBank.Desktop.Utils;
using PKBank.Desktop.Components;
using PKBank.Desktop.Services.Updates;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKBank.Desktop;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var config = new AppConfigService(new FileConfigStore());
            GameInfo.CurrentLanguage = config.Language;
            LocalizeUtil.InitializeStrings(config.Language);
            SpriteName.AllowShinySprite = true;

            var vm = new MainWindowViewModel(config);
            var window = new MainWindow { DataContext = vm };
            desktop.MainWindow = window;

            // Open the file passed on the command line
            if (desktop.Args is [{ Length: > 0 } path, ..])
                vm.LoadSaveFromPath(path);
            if (vm.SAV is null)
                _ = vm.SaveSelection.RefreshAsync();

#if !DEBUG
            // Released builds look for a newer AppImage once the window is up; a development run never does
            // (UpdateDialogs also skips anything that was not started from an AppImage).
            window.Opened += (_, _) => _ = UpdateDialogs.CheckOnStartupAsync(window);
#endif
        }

        base.OnFrameworkInitializationCompleted();
    }
}
