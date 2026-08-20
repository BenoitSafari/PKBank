using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKBank.Desktop.Services;
using PKBank.Desktop.ViewModels;
using PKHeX.Core;

namespace PKBank.Desktop.Views.PokemonEditor;

public sealed partial class PokemonEditorView : UserControl
{
    public PokemonEditorView()
    {
        InitializeComponent();
        // Dropping a Pokémon file anywhere on the form loads it into the editor.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
        AddHandler(DragDrop.DropEvent, OnFileDrop);
    }

    private MainWindowViewModel? MainViewModel => TopLevel.GetTopLevel(this)?.DataContext as MainWindowViewModel;

    private static bool IsInternalSlotDrag(DragEventArgs e)
        => e.DataTransfer.Contains(SlotDragFormats.Slot) || e.DataTransfer.Contains(SlotDragFormats.Multi);

    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        // Internal slot drags also carry export files; those are not for the editor.
        if (IsInternalSlotDrag(e) || !e.DataTransfer.Contains(DataFormat.File))
            return; // let the window-level handler deal with it
        e.DragEffects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnFileDrop(object? sender, DragEventArgs e)
    {
        if (IsInternalSlotDrag(e) || DataContext is not PokemonEditorViewModel vm)
            return;
        var path = e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath();
        if (path is null)
            return;
        vm.TryLoadEntityFromFile(path, out var message);
        if (MainViewModel is { } main)
            main.StatusMessage = message;
        e.Handled = true;
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PokemonEditorViewModel vm || !vm.HasSpecies)
            return;
        if (TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var pk = vm.GetEntityClone();
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Pokémon File",
            SuggestedFileName = PathUtil.CleanFileName(pk.FileName),
            ShowOverwritePrompt = true,
        });
        var path = file?.TryGetLocalPath();
        if (path is null)
            return;
        try
        {
            PkmFileService.Export(pk, path);
            if (MainViewModel is { } main)
                main.StatusMessage = $"Exported {System.IO.Path.GetFileName(path)}.";
        }
        catch (System.Exception ex)
        {
            if (MainViewModel is { } main)
                main.StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private void OnRerollPidClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PokemonEditorViewModel vm)
            vm.RerollPid();
    }

    private void OnPidLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is PokemonEditorViewModel vm)
            vm.NormalizePidText(); // snap partial input back to the stored 8-digit value
    }
}
