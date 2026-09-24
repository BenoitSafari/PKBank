using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using PKBank.Desktop.Services.Slots;
using PKBank.Desktop.Utils;

namespace PKBank.Desktop.Components;

public sealed partial class MainWindow : Window
{
    private readonly SlotDragDropHost _drag;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        _drag = new SlotDragDropHost(this, DragGhostLayer, DragGhostImage)
        {
            FileDropHandler = OnFilesDropped
        };
        _drag.AttachArea(BoxPanelsItems);
        _drag.AttachArea(PartyItems);
        _drag.AttachArea(BankSection.BoxArea);
        _drag.AttachWindow();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    /// <summary>Closing the window (title bar or File &gt; Exit) must not silently drop pending changes.</summary>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_forceClose || ViewModel is not { IsDirty: true } vm)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true; // cancel before awaiting: the close cannot be held open across the dialog
        base.OnClosing(e);

        if (!await MainWindowMenu.ConfirmDiscardAsync(this, vm, "Exit", "Exit", "Exiting will discard them."))
            return;
        _forceClose = true;
        Close();
    }

    /// <summary>Files dropped anywhere in the window: a save replaces the loaded one, entities go into slots.</summary>
    private async Task<bool> OnFilesDropped(IReadOnlyList<string> paths, Point position)
    {
        if (ViewModel is not { } vm)
            return false;

        // Several files dropped: import them into the selected slots in display order.
        if (paths.Count > 1)
        {
            vm.ImportFilesToSelection(paths);
            return true;
        }

        var path = paths[0];

        // A dropped save file replaces the loaded one, wherever it lands.
        if (SaveFileDrop.TryGetSaveFile(path, out var dropped))
        {
            var action = $"Loading “{Path.GetFileName(path)}” will discard them.";
            if (await MainWindowMenu.ConfirmDiscardAsync(this, vm, "Load Save File", "Load", action))
                vm.LoadSaveFromPath(dropped, path);
            return true;
        }

        // External Pokémon file dropped onto a slot: import it there.
        if (_drag.HitTestSlot(position) is not { } slot)
            return false;
        vm.TryImportFileToSlot(path, slot);
        return true;
    }

    // ----- Selected-slot actions -------------------------------------------

    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => ViewModel?.DeleteSelected();

    private async void OnEditSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { CanEditSelected: true, SelectedSlot: { } slot } vm)
            await SlotEditorDialogs.EditSlotAsync(this, vm, slot);
    }

    private void OnCopySelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { CanCopySelected: true, SelectedSlot: { } slot } vm)
            vm.CopySlot(slot);
    }

    private async void OnPasteSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { CanPasteSelected: true, SelectedSlot: { } slot } vm)
            await SlotEditorDialogs.PasteToSlotAsync(this, vm, slot);
    }

    private async void OnImportSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { SelectedSlot: not null } vm)
            await SlotFileDialogs.ImportIntoSlotsAsync(this, vm, vm.GetSelectedSlotsInDisplayOrder());
    }

    private async void OnExportSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
            await SlotFileDialogs.ExportSlotsAsync(this, vm, vm.SelectedSlots);
    }
}
