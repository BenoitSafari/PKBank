using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace PKBank.Desktop.Views.Components.Common.DirectoryPathList;

public sealed class DirectoryPathEventArgs(string path) : EventArgs
{
    public string Path { get; } = path;
}

public sealed partial class DirectoryPathList : UserControl
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<DirectoryPathList, string>(nameof(Header), "Directories");

    public static readonly StyledProperty<string> ColumnHeaderProperty =
        AvaloniaProperty.Register<DirectoryPathList, string>(nameof(ColumnHeader), "Directory");

    public static readonly StyledProperty<string> PickerTitleProperty =
        AvaloniaProperty.Register<DirectoryPathList, string>(nameof(PickerTitle), "Select Folder");

    public static readonly StyledProperty<double> ListMinHeightProperty =
        AvaloniaProperty.Register<DirectoryPathList, double>(nameof(ListMinHeight), 96d);

    public static readonly StyledProperty<IReadOnlyList<string>?> PathsProperty =
        AvaloniaProperty.Register<DirectoryPathList, IReadOnlyList<string>?>(nameof(Paths));

    public DirectoryPathList() => InitializeComponent();

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string ColumnHeader
    {
        get => GetValue(ColumnHeaderProperty);
        set => SetValue(ColumnHeaderProperty, value);
    }

    public string PickerTitle
    {
        get => GetValue(PickerTitleProperty);
        set => SetValue(PickerTitleProperty, value);
    }

    public double ListMinHeight
    {
        get => GetValue(ListMinHeightProperty);
        set => SetValue(ListMinHeightProperty, value);
    }

    public IReadOnlyList<string>? Paths
    {
        get => GetValue(PathsProperty);
        set => SetValue(PathsProperty, value);
    }

    public event EventHandler<DirectoryPathEventArgs>? PathAdded;

    public event EventHandler<DirectoryPathEventArgs>? PathRemoved;

    private async void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
            return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = PickerTitle,
            AllowMultiple = true
        });
        foreach (var path in folders.Select(f => f.TryGetLocalPath()).OfType<string>())
            PathAdded?.Invoke(this, new DirectoryPathEventArgs(path));
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (PathsList.SelectedItem is string path)
            PathRemoved?.Invoke(this, new DirectoryPathEventArgs(path));
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        RemoveButton.IsEnabled = PathsList.SelectedItem is string;
}
