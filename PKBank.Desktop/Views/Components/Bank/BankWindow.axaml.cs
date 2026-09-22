using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using PKBank.Desktop.Utils;
using PKBank.Desktop.ViewModels;

namespace PKBank.Desktop.Views.Components.Bank;

/// <summary>
///     A bank box, shown next to the main window. Not modal: entities are moved between the two by
///     dragging across them.
/// </summary>
public sealed partial class BankWindow : Window
{
    private readonly SlotDragDropHost _drag;

    public BankWindow()
    {
        InitializeComponent();
        _drag = new SlotDragDropHost(this, DragGhostLayer, DragGhostImage);
        _drag.AttachArea(BoxHost);
        _drag.AttachWindow();
        DataContextChanged += (_, _) => Subscribe();
    }

    private MainWindowViewModel? _viewModel;

    /// <summary>A bank only makes sense against a loaded save; closing it takes this window with it.</summary>
    private void Subscribe()
    {
        if (_viewModel is { } previous)
            previous.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is { } current)
            current.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.HasSave) && _viewModel is { HasSave: false })
            Close();
    }

    /// <summary>
    ///     Committing a bank means saving the loaded file, and that is normally Ctrl+S on the main window's
    ///     menu. Bank work keeps the focus here, so the same shortcut has to reach it from this window.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e is { Key: Key.S, KeyModifiers: KeyModifiers.Control } && _viewModel is { CanSave: true } vm)
        {
            vm.SaveInPlace();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel is { } vm)
            vm.PropertyChanged -= OnViewModelPropertyChanged;
        base.OnClosed(e);
    }
}
