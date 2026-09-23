using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKBank.Core.Events.Files;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Events;

public sealed partial class Gen3EventFileWindow : Window
{
    private readonly SAV3? _sav;
    private readonly Gen3EventFileKind? _kind;

    public Gen3EventFileWindow() => InitializeComponent(); // designer

    public Gen3EventFileWindow(SAV3 sav, Gen3EventFileKind kind) : this()
    {
        _sav = sav;
        _kind = kind;
        Title = kind.Title;

        RefreshStatus();

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void RefreshStatus()
    {
        if (_sav is null || _kind is null)
            return;

        ExportButton.IsEnabled = _kind.Has(_sav);
        if (!_kind.Has(_sav))
        {
            StatusText.Text = "The save file currently holds no data of this kind.";
            return;
        }

        var summary = _kind.GetSummary(_sav);
        StatusText.Text = summary.Length == 0
            ? "The save file currently holds data of this kind."
            : $"Current content: “{summary}”";
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private async void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        if (_kind is null)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Open {_kind.Title} file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(_kind.Title) { Patterns = [$"*.{_kind.Extension}"] },
                FilePickerFileTypes.All,
            ],
        });
        if (files is [{ } file, ..] && file.TryGetLocalPath() is { } path)
            ImportFile(path);
    }

    private void ImportFile(string path)
    {
        if (_sav is null || _kind is null)
            return;

        ErrorText.IsVisible = false;
        try
        {
            var data = File.ReadAllBytes(path);
            var sizes = _kind.GetValidSizes(_sav);
            if (!sizes.Contains(data.Length))
            {
                ShowError($"Invalid file size: 0x{data.Length:X} bytes (expected {string.Join(" or ", sizes.Select(z => $"0x{z:X}"))}).");
                return;
            }

            _kind.Import(_sav, data);
            _sav.State.Edited = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Unable to read the file: {ex.Message}");
        }
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is null || _kind is null)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {_kind.Title} file",
            DefaultExtension = _kind.Extension,
            SuggestedFileName = $"{PathUtil.CleanFileName(_sav.OT)}.{_kind.Extension}",
            FileTypeChoices = [new FilePickerFileType(_kind.Title) { Patterns = [$"*.{_kind.Extension}"] }],
        });
        if (file?.TryGetLocalPath() is not { } path)
            return;

        ErrorText.IsVisible = false;
        try
        {
            await File.WriteAllBytesAsync(path, _kind.Export(_sav));
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Unable to write the file: {ex.Message}");
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath() is not { } path)
            return;

        ImportFile(path);
        e.Handled = true;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
