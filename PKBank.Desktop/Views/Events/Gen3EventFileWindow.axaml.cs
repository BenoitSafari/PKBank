using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PKHeX.Core;

namespace PKBank.Desktop.Views.Events;

/// <summary>
/// Import/export window for one kind of Gen 3 event data file, mirroring the
/// WC3 Plugin forms: shows what the save currently holds, validates the file
/// size on import and fixes checksums through the Extensions backend.
/// </summary>
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
        if (_sav is not { } sav || _kind is not { } kind)
            return;

        ExportButton.IsEnabled = kind.Has(sav);
        if (!kind.Has(sav))
        {
            StatusText.Text = "The save file currently holds no data of this kind.";
            return;
        }

        var summary = kind.GetSummary(sav);
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
        if (_kind is not { } kind)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Open {kind.Title} file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(kind.Title) { Patterns = [$"*.{kind.Extension}"] },
                FilePickerFileTypes.All,
            ],
        });
        if (files is [{ } file, ..] && file.TryGetLocalPath() is { } path)
            ImportFile(path);
    }

    private void ImportFile(string path)
    {
        if (_sav is not { } sav || _kind is not { } kind)
            return;

        ErrorText.IsVisible = false;
        try
        {
            var data = File.ReadAllBytes(path);
            var sizes = kind.GetValidSizes(sav);
            if (!sizes.Contains(data.Length))
            {
                ShowError($"Invalid file size: 0x{data.Length:X} bytes (expected {string.Join(" or ", sizes.Select(z => $"0x{z:X}"))}).");
                return;
            }

            kind.Import(sav, data);
            sav.State.Edited = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Unable to read the file: {ex.Message}");
        }
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _kind is not { } kind)
            return;

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Save {kind.Title} file",
            DefaultExtension = kind.Extension,
            SuggestedFileName = $"{PathUtil.CleanFileName(sav.OT)}.{kind.Extension}",
            FileTypeChoices = [new FilePickerFileType(kind.Title) { Patterns = [$"*.{kind.Extension}"] }],
        });
        if (file?.TryGetLocalPath() is not { } path)
            return;

        ErrorText.IsVisible = false;
        try
        {
            File.WriteAllBytes(path, kind.Export(sav));
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Unable to write the file: {ex.Message}");
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            ImportFile(path);
            e.Handled = true;
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e) => Close();
}
