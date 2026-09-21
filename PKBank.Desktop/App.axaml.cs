using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PKBank.Core.Configuration;
using PKBank.Desktop.Services;
using PKBank.Desktop.ViewModels;
using PKBank.Desktop.Views;
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
            desktop.MainWindow = new MainWindow { DataContext = vm };

            // Open the file passed on the command line, else start on a blank save.
            if (desktop.Args is [{ Length: > 0 } path, ..])
                vm.LoadSaveFromPath(path);
            else
                vm.LoadStartupBlank();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
