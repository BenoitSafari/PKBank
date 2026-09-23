using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using PKBank.Desktop.Utils;
using PKBank.Desktop.Components;
using PKBank.Desktop.Components.Box.ViewModels;
using PKHeX.Core;

namespace PKBank.Desktop.Services.Slots;

/// <summary>
///     File pickers behind the slot Import/Export actions, shared by the slot context menu and the
///     selected-slot action bar.
/// </summary>
public static class SlotFileDialogs
{
    public static async Task ImportIntoSlotsAsync(
        TopLevel top, MainWindowViewModel vm, IReadOnlyList<SlotViewModel> targets)
    {
        if (vm.SAV is not { } sav || targets.Count == 0)
            return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = targets.Count > 1
                ? $"Import Pokémon Files ({targets.Count} slots selected)"
                : "Import Pokémon File",
            AllowMultiple = targets.Count > 1,
            FileTypeFilter = PkmFileService.GetPickerFileTypes(sav)
        });

        var paths = files.Select(f => f.TryGetLocalPath()).OfType<string>().ToArray();
        if (paths.Length == 0)
            return;
        if (targets.Count == 1)
            vm.TryImportFileToSlot(paths[0], targets[0]);
        else
            vm.ImportFilesToSlots(paths, targets);
    }

    public static async Task ExportSlotsAsync(
        TopLevel top, MainWindowViewModel vm, IReadOnlyList<SlotViewModel> slots)
    {
        var occupied = slots.Where(static s => !s.IsEmpty).ToList();
        if (occupied.Count == 0)
            return;

        if (occupied.Count == 1)
        {
            await ExportSingleAsync(top, vm, occupied[0]);
            return;
        }

        // Multiple Pokémon: pick a folder and write one file per occupied slot.
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = $"Export {occupied.Count} Pokémon To Folder",
            AllowMultiple = false
        });
        var dir = folders.FirstOrDefault()?.TryGetLocalPath();
        if (dir is null)
            return;

        var exported = 0;
        try
        {
            foreach (var slot in occupied)
            {
                var pk = slot.Read();
                PkmFileService.Export(pk, Path.Combine(dir, PathUtil.CleanFileName(pk.FileName)));
                exported++;
            }

            vm.StatusMessage = $"Exported {exported} Pokémon to {Path.GetFileName(dir)}.";
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Export failed after {exported} file(s): {ex.Message}";
        }
    }

    private static async Task ExportSingleAsync(TopLevel top, MainWindowViewModel vm, SlotViewModel slot)
    {
        var pk = slot.Read();
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Pokémon File",
            SuggestedFileName = PathUtil.CleanFileName(pk.FileName),
            ShowOverwritePrompt = true
        });
        var path = file?.TryGetLocalPath();
        if (path is null)
            return;
        try
        {
            PkmFileService.Export(pk, path);
            vm.StatusMessage = $"Exported {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"Export failed: {ex.Message}";
        }
    }
}
