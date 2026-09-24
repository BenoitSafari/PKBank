using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PKBank.Desktop.Services.Slots;
using PKBank.Desktop.Components.Bank.ViewModels;
using PKBank.Desktop.Utils;

namespace PKBank.Desktop.Components;

public sealed partial class MainWindow : Window
{
    /// <summary>Used until the theme's scrollbar has been laid out once.</summary>
    private const double FallbackScrollBarWidth = 16;

    private readonly SlotDragDropHost _drag;
    private double _baseMinWidth;
    private MainWindowViewModel? _boundViewModel;
    private bool _fitted;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        _drag = new SlotDragDropHost(this, DragGhostLayer, DragGhostImage)
        {
            FileDropHandler = OnFilesDropped
        };
        _drag.AttachArea(SaveBoxHost);
        _drag.AttachArea(PartyItems);
        _drag.AttachArea(BankSection.BoxArea);
        _drag.AttachWindow();

        DataContextChanged += (_, _) => BindViewModel();
        StorageScroller.ScrollChanged += (_, _) => UpdateMinWidth();
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

    // ----- Window size ----------------------------------------------------------

    private void BindViewModel()
    {
        if (_boundViewModel is { } previous)
        {
            previous.PropertyChanged -= OnViewModelPropertyChanged;
            previous.Bank.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _boundViewModel = ViewModel;
        if (_boundViewModel is { } current)
        {
            current.PropertyChanged += OnViewModelPropertyChanged;
            current.Bank.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    /// <summary>The save and its bank box both arrive asynchronously; fit once each has been laid out.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_fitted && e.PropertyName is nameof(MainWindowViewModel.HasSave) or nameof(BankViewModel.CurrentBox))
            Dispatcher.UIThread.Post(FitToContent, DispatcherPriority.Loaded);
    }

    /// <summary>
    ///     The first time a save is shown, sizes the window to the storage column (party, box and bank frames)
    ///     and makes that width the minimum. The height is capped to the screen; the window stays resizable.
    ///     A fit made before the bank box is loaded is provisional: it is redone once the box is there.
    /// </summary>
    private void FitToContent()
    {
        if (_fitted || ViewModel is not { HasSave: true } vm)
            return;
        _fitted = vm.Bank.CurrentBox is not null;

        // Everything around the scroll area (menu, toolbar, status bar, margins) stays; the area takes the
        // column. Bounds rather than the viewport: a scrollbar shown right now must not count.
        var content = StorageColumn.Bounds.Size;
        var width = Math.Ceiling(content.Width + ClientSize.Width - StorageScroller.Bounds.Width);
        var height = Math.Ceiling(content.Height + ClientSize.Height - StorageScroller.Bounds.Height);

        if ((Screens.ScreenFromVisual(this) ?? Screens.Primary) is { } screen)
        {
            var area = screen.WorkingArea.Size.ToSize(screen.Scaling);
            var decorations = FrameSize is { } frame ? frame - ClientSize : default;
            height = Math.Min(height, area.Height - decorations.Height);
        }

        // The scrollbar state follows once laid out at this size (see UpdateMinWidth).
        _baseMinWidth = width;
        MinWidth = width;
        Height = height;
        Width = width;
    }

    /// <summary>
    ///     The vertical scrollbar takes room from the column while it is shown, so the minimum width grows by
    ///     its width meanwhile (widening the window if needed) and the frames are never clipped.
    /// </summary>
    private void UpdateMinWidth()
    {
        if (_baseMinWidth <= 0)
            return;

        var scrolls = StorageScroller.Extent.Height > StorageScroller.Viewport.Height + 0.5;
        MinWidth = _baseMinWidth + (scrolls ? VerticalScrollBarWidth() : 0);
        if (ClientSize.Width < MinWidth)
            Width = MinWidth; // not every platform grows the window to a raised minimum by itself
    }

    /// <summary>Width of the storage column's vertical scrollbar, as the theme draws it.</summary>
    private double VerticalScrollBarWidth()
    {
        var bar = StorageScroller.GetVisualDescendants().OfType<ScrollBar>()
            .FirstOrDefault(static b => b.Orientation == Orientation.Vertical);
        return bar is { Bounds.Width: > 0 } ? bar.Bounds.Width : FallbackScrollBarWidth;
    }

    // ----- Selected-slot actions -------------------------------------------

    private void OnDeleteClicked(object? sender, RoutedEventArgs e) => ViewModel?.DeleteSelected();

    private async void OnEditSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { CanEditSelected: true, SelectedSlot: { } slot } vm)
            await SlotEditorDialogs.EditSlotAsync(this, vm, slot);
    }

    private void OnCutSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { CanCutSelected: true } vm)
            vm.CutSlots(vm.GetSelectedSlotsInDisplayOrder());
    }

    private void OnCopySelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { CanCopySelected: true } vm)
            vm.CopySlots(vm.GetSelectedSlotsInDisplayOrder());
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
