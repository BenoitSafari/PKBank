using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PKBank.Desktop.Components;

namespace PKBank.Desktop.Services.SaveFiles;

public static class SaveFileDialogs
{
    public static async Task OpenAsync(TopLevel top, MainWindowViewModel vm)
    {
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Save File",
            AllowMultiple = false
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
            vm.LoadSaveFromPath(path);
    }
}
