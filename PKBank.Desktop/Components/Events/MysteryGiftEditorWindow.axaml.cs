using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PKBank.Desktop.Sprites;
using PKBank.Desktop.Components.Common.QRCodeWindow;
using PKHeX.Core;

namespace PKBank.Desktop.Components.Events;

/// <summary>
///     Mystery Gift editor mirroring WinForms' SAV_Wondercard (Gen 4-7): the album
///     slots on the left, the currently viewed card on the right acting as the
///     clipboard for Set/Import/Export/QR, plus the received-flags list.
///     Changes are only written to the save on Save.
/// </summary>
public sealed partial class MysteryGiftEditorWindow : Window
{
    private readonly DataMysteryGift[] _album = [];
    private readonly IMysteryGiftStorage? _cards;
    private readonly IMysteryGiftFlags? _flags;
    private readonly ObservableCollection<string> _received = [];
    private readonly SaveFile? _sav;
    private readonly List<Border> _slotBorders = [];
    private readonly List<Image> _slotImages = [];

    private DataMysteryGift? _current;
    private int _lastTouched = -1;
    private bool _loadingView;

    public MysteryGiftEditorWindow() => InitializeComponent(); // designer

    public MysteryGiftEditorWindow(SaveFile sav) : this()
    {
        _sav = sav;
        _cards = ((IMysteryGiftStorageProvider)sav).MysteryGiftStorage;
        _flags = _cards as IMysteryGiftFlags;
        _album = LoadAlbum(sav, _cards);

        BuildAlbumUi(sav);
        RefreshAlbum();
        LoadReceivedFlags();
        ReceivedList.ItemsSource = _received;

        if (_album is [WR7, ..]) // giftused is not a valid prop
            UsedAllButton.IsVisible = UnusedAllButton.IsVisible = QrButton.IsVisible = UsedCheck.IsVisible = false;

        ViewGift(_album[0], 0);

        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static DataMysteryGift[] LoadAlbum(SaveFile sav, IMysteryGiftStorage cards)
    {
        var count = cards.GiftCountMax;
        var result = new DataMysteryGift[sav is SAV4HGSS ? count + 1 : count];
        for (var i = 0; i < count; i++)
            result[i] = cards.GetMysteryGift(i);
        if (sav is SAV4HGSS hgss)
            result[^1] = hgss.LockCapsuleSlot;
        return result;
    }

    // UI generation, mirroring WinForms' PopulateViewGiftsG4/G567 row layouts.
    private void BuildAlbumUi(SaveFile sav)
    {
        if (sav.Generation == 4)
        {
            AddAlbumRow("PGT 1-6", 0, 6);
            AddAlbumRow("PGT 7-8", 6, 2);
            AddAlbumRow("PCD 1-3", 8, 3);
            if (_album.Length == 12)
                AddAlbumRow(GameInfo.Strings.Item[533], 11, 1); // Lock Capsule
        }
        else
        {
            const int cellsPerRow = 6;
            for (var start = 0; start < _album.Length; start += cellsPerRow)
            {
                var count = Math.Min(cellsPerRow, _album.Length - start);
                AddAlbumRow($"{start + 1}-{start + count}", start, count);
            }
        }
    }

    private void AddAlbumRow(string label, int start, int count)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(new TextBlock
        {
            Text = label,
            Width = 78,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 6, 0),
            Opacity = 0.7,
            FontSize = 12
        });
        for (var i = 0; i < count; i++)
        {
            var index = start + i;
            var image = new Image { Width = 68, Height = 56, Stretch = Stretch.None };
            var border = new Border
            {
                Child = image,
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                CornerRadius = new CornerRadius(4),
                Background = Brushes.Transparent,
                ContextMenu = BuildSlotMenu(index)
            };
            border.PointerPressed += (_, e) =>
            {
                if (e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
                    ViewGift(_album[index], index);
            };
            _slotBorders.Add(border);
            _slotImages.Add(image);
            row.Children.Add(border);
        }

        AlbumPanel.Children.Add(row);
    }

    private ContextMenu BuildSlotMenu(int index)
    {
        var view = new MenuItem { Header = "View" };
        view.Click += (_, _) => ViewGift(_album[index], index);
        var set = new MenuItem { Header = "Set" };
        set.Click += (_, _) => SetGift(index);
        var delete = new MenuItem { Header = "Delete" };
        delete.Click += (_, _) => DeleteGift(index);
        return new ContextMenu { Items = { view, set, delete } };
    }

    private void RefreshAlbum()
    {
        for (var i = 0; i < _album.Length; i++)
        {
            _slotImages[i].Source = SpriteService.GetMysteryGiftSprite(_album[i]);
            _slotImages[i].Opacity = _album[i] is { IsEmpty: false, GiftUsed: true } ? 0.3 : 1.0;
            _slotBorders[i].BorderBrush = i == _lastTouched
                ? (IBrush?)(this.TryFindResource("SystemControlHighlightAccentBrush", ActualThemeVariant, out var brush)
                    ? brush as IBrush
                    : Brushes.CornflowerBlue) ?? Brushes.CornflowerBlue
                : Brushes.Transparent;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private void ViewGift(DataMysteryGift gift, int index = -1)
    {
        ErrorText.IsVisible = false;
        _loadingView = true;
        try
        {
            PreviewImage.Source = SpriteService.GetMysteryGiftSprite(gift);
            DescriptionLines.ItemsSource = gift.GetDescription().ToArray();
            UsedCheck.IsChecked = !gift.IsEmpty && gift.GiftUsed;
            UsedCheck.IsEnabled = !gift.IsEmpty;
            _current = gift;
            if (index >= 0)
            {
                _lastTouched = index;
                RefreshAlbum();
            }
        }
        // Some user input mystery gifts can have out-of-bounds values.
        catch (Exception ex)
        {
            DescriptionLines.ItemsSource = Array.Empty<string>();
            ShowError($"Unable to parse the mystery gift: {ex.Message}");
        }
        finally
        {
            _loadingView = false;
        }
    }

    private void OnUsedChanged(object? sender, RoutedEventArgs e)
    {
        if (_loadingView || _current is null or { IsEmpty: true })
            return;
        _current.GiftUsed = UsedCheck.IsChecked == true;
        RefreshAlbum();
    }

    private static int GetLastUnfilledByType(DataMysteryGift gift, ReadOnlySpan<DataMysteryGift> album)
    {
        for (var i = 0; i < album.Length; i++)
        {
            var exist = album[i];
            if (!exist.IsEmpty)
                continue;
            if (exist.Type != gift.Type)
                continue;
            return i;
        }

        return -1;
    }

    private void SetGift(int index)
    {
        if (_sav is not { } sav || _current is not { } gift)
            return;

        ErrorText.IsVisible = false;
        if (!gift.IsCardCompatible(sav, out var msg))
        {
            ShowError(msg);
            return;
        }

        // Hijack to the latest unfilled slot if index creates interstitial empty slots.
        var lastUnfilled = GetLastUnfilledByType(gift, _album);
        if (lastUnfilled > -1 && lastUnfilled < index)
            index = lastUnfilled;
        if (gift is PCD { IsLockCapsule: true })
        {
            if (_album.Length != 12)
            {
                ShowError("Lock Capsule gifts require a HeartGold/SoulSilver save.");
                return;
            }

            index = 11;
        }

        var other = _album[index];
        if (gift is PCD { CanConvertToPGT: true } pcd && other is PGT)
        {
            gift = pcd.Gift;
        }
        else if (gift.Type != other.Type)
        {
            ShowError($"Slot type mismatch: {gift.Type} != {other.Type}");
            return;
        }
        else if (gift is PCD g && g is { IsLockCapsule: true } != (index == 11))
        {
            ShowError($"{GameInfo.Strings.Item[533]} slot not valid.");
            return;
        }

        _album[index] = (DataMysteryGift)gift.Clone();
        _lastTouched = index;
        SetCardID(gift.CardID);
        RefreshAlbum();
    }

    private void DeleteGift(int index)
    {
        ErrorText.IsVisible = false;
        _album[index].Clear();

        // Shuffle blank card down
        var i = index;
        while (i < _album.Length - 1)
        {
            if (_album[i + 1].IsEmpty)
                break;
            if (_album[i + 1].Type != _album[i].Type)
                break;
            i++;
            (_album[i - 1], _album[i]) = (_album[i], _album[i - 1]);
        }

        _lastTouched = i;
        RefreshAlbum();
    }

    // Received flags
    private void LoadReceivedFlags()
    {
        _received.Clear();
        if (_flags is not { } flags)
            return;
        var count = flags.MysteryGiftReceivedFlagMax;
        for (var i = 1; i < count; i++)
            if (flags.GetMysteryGiftReceivedFlag(i))
                _received.Add(i.ToString("0000"));
    }

    private void SetCardID(int cardID)
    {
        if (_flags is null || (uint)cardID >= _flags.MysteryGiftReceivedFlagMax)
            return;

        var card = cardID.ToString("0000");
        if (!_received.Contains(card))
            _received.Add(card);
        ReceivedList.SelectedItem = card;
    }

    private void OnRemoveFlagsClicked(object? sender, RoutedEventArgs e)
    {
        foreach (var item in ReceivedList.SelectedItems?.OfType<string>().ToArray() ?? [])
            _received.Remove(item);
    }

    // Mystery Gift IO (.file <-> window)
    private static string[] GetImportPatterns(EntityContext context) => context switch
    {
        EntityContext.Gen4 => ["*.pgt", "*.pcd", "*.wc4"],
        EntityContext.Gen5 => ["*.pgf", "*.wc5full"],
        EntityContext.Gen6 => ["*.wc6", "*.wc6full"],
        EntityContext.Gen7 => ["*.wc7", "*.wc7full"],
        EntityContext.Gen7b => ["*.wr7"],
        _ => ["*"]
    };

    private async void OnImportClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Mystery Gift file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType($"Gen{sav.Generation} Mystery Gift")
                    { Patterns = GetImportPatterns(sav.Context) },
                FilePickerFileTypes.All
            ]
        });
        if (files is [{ } file, ..] && file.TryGetLocalPath() is { } path)
            ImportFile(path);
    }

    private void ImportFile(string path)
    {
        ErrorText.IsVisible = false;
        var info = new FileInfo(path);
        if (!MysteryGift.IsMysteryGift(info.Length))
        {
            ShowError($"Invalid mystery gift file size: {Path.GetFileName(path)}");
            return;
        }

        var gift = MysteryGift.GetMysteryGift(File.ReadAllBytes(path), info.Extension);
        if (gift is null)
        {
            ShowError($"Unable to parse the mystery gift file: {Path.GetFileName(path)}");
            return;
        }

        ViewGift(gift);
    }

    private async void OnExportClicked(object? sender, RoutedEventArgs e)
    {
        if (_current is not { } gift)
            return;
        if (gift.IsEmpty)
        {
            ShowError("No mystery gift data to export.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Mystery Gift file",
            SuggestedFileName = PathUtil.CleanFileName(gift.FileName),
            DefaultExtension = gift.Extension,
            FileTypeChoices = [new FilePickerFileType(gift.Type) { Patterns = [$"*.{gift.Extension}"] }]
        });
        if (file?.TryGetLocalPath() is not { } path)
            return;

        try
        {
            File.WriteAllBytes(path, gift.Write());
        }
        catch (Exception ex)
        {
            ShowError($"Unable to write the file: {ex.Message}");
        }
    }

    private void OnQrClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _current is not { } gift)
            return;
        if (gift.IsEmpty)
        {
            ShowError("No mystery gift data to encode.");
            return;
        }

        if (sav.Generation == 6 && gift is { IsItem: true, ItemID: 726 })
        {
            ShowError("Eon Ticket QR codes are not readable by the games; inject the Eon Ticket directly instead.");
            return;
        }

        new QRCodeWindow(gift).Show(this);
    }

    private void OnUsedAllClicked(object? sender, RoutedEventArgs e) => SetUsedAll(true);

    private void OnUnusedAllClicked(object? sender, RoutedEventArgs e) => SetUsedAll(false);

    private void SetUsedAll(bool used)
    {
        foreach (var gift in _album)
            gift.GiftUsed = used;
        if (_current is { } current)
            ViewGift(current, _lastTouched);
        RefreshAlbum();
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles()?.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            ImportFile(path);
            e.Handled = true;
        }
    }

    // Close window
    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (_sav is not { } sav || _cards is not { } cards)
        {
            Close();
            return;
        }

        SaveReceivedFlags();

        if (cards is MysteryBlock4 s4)
        {
            s4.IsDeliveryManActive = _album.Any(g => !g.IsEmpty);
            MysteryBlock4.UpdateSlotPGT(_album, sav is SAV4HGSS);
            if (sav is SAV4HGSS hgss)
                hgss.LockCapsuleSlot = (PCD)_album[^1];
        }

        var count = cards.GiftCountMax;
        for (var i = 0; i < count; i++)
            cards.SetMysteryGift(i, _album[i]);
        if (cards is MysteryBlock5 s5)
            s5.EndAccess(); // need to encrypt the at-rest data with the seed.

        sav.State.Edited = true;
        Close();
    }

    private void SaveReceivedFlags()
    {
        if (_flags is not { } flags)
            return; // nothing to save

        // Store the list of set flag indexes back to the bitflag array.
        flags.ClearReceivedFlags();
        foreach (var item in _received)
            if (int.TryParse(item, out var index))
                flags.SetMysteryGiftReceivedFlag(index, true);
    }
}
