using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using PKHeX.Core;

namespace PKBank.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private SaveFile? _sav;
    private FilteredGameDataSource? _sources;
    private string? _savePath;
    private int _currentBox;
    private SlotViewModel? _selectedSlot;
    private PokemonEditorViewModel? _editor;
    private string _statusMessage = "Open a save file to get started (File → Open…).";
    private IReadOnlyList<string> _boxNames = [];
    private string _trainerInfo = string.Empty;

    /// <summary>Game versions offered by the File → New menu.</summary>
    public static readonly IReadOnlyList<(string Label, GameVersion Version)> NewSaveOptions =
    [
        ("Legends: Z-A", GameVersion.ZA),
        ("Scarlet/Violet", GameVersion.SL),
        ("Legends: Arceus", GameVersion.PLA),
        ("Brilliant Diamond/Shining Pearl", GameVersion.BD),
        ("Sword/Shield", GameVersion.SW),
        ("Ultra Sun/Ultra Moon", GameVersion.US),
        ("Omega Ruby/Alpha Sapphire", GameVersion.OR),
        ("Black 2/White 2", GameVersion.B2),
        ("HeartGold/SoulSilver", GameVersion.HG),
        ("Emerald", GameVersion.E),
        ("Crystal", GameVersion.C),
        ("Red", GameVersion.RD),
    ];

    public SaveFile? SAV => _sav;
    public bool HasSave => _sav is not null;

    public ObservableCollection<SlotViewModel> BoxSlots { get; } = [];
    public ObservableCollection<SlotViewModel> PartySlots { get; } = [];

    public IReadOnlyList<string> BoxNames { get => _boxNames; private set => SetField(ref _boxNames, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public string TrainerInfo { get => _trainerInfo; private set => SetField(ref _trainerInfo, value); }

    public string WindowTitle => _sav is null
        ? "PKBank.Desktop"
        : $"PKBank.Desktop — {GameInfo.GetVersionName(_sav.Version)} — {(_savePath is null ? "(new)" : Path.GetFileName(_savePath))}";

    public bool HasBox => _sav?.HasBox == true;
    public bool HasParty => _sav?.HasParty == true;

    public int CurrentBox
    {
        get => _currentBox;
        set
        {
            if (_sav is not { HasBox: true } sav)
                return;
            var box = Math.Clamp(value, 0, sav.BoxCount - 1);
            if (SetField(ref _currentBox, box))
            {
                foreach (var slot in BoxSlots)
                    slot.ChangeBox(box);
            }
            else if (value != box)
            {
                // The view pushed an out-of-range value (e.g. -1 while ItemsSource resets);
                // notify so it re-reads the clamped value.
                OnPropertyChanged();
            }
        }
    }

    public SlotViewModel? SelectedSlot
    {
        get => _selectedSlot;
        private set => SetField(ref _selectedSlot, value);
    }

    public PokemonEditorViewModel? Editor { get => _editor; private set => SetField(ref _editor, value); }

    public void LoadSaveFromPath(string path)
    {
        try
        {
            if (!SaveUtil.TryGetSaveFile(path, out var sav))
            {
                StatusMessage = $"Not a recognized save file: {Path.GetFileName(path)}";
                return;
            }
            _savePath = path;
            LoadSave(sav);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load save: {ex.Message}";
        }
    }

    public void NewBlank(GameVersion version)
    {
        _savePath = null;
        LoadSave(BlankSaveFile.Get(version, _sav));
        StatusMessage = $"Created a blank {GameInfo.GetVersionName(version)} save.";
    }

    public bool TrySaveTo(string path)
    {
        if (_sav is not { } sav)
            return false;
        try
        {
            var data = sav.Write();
            File.WriteAllBytes(path, data.Span);
            _savePath = path;
            sav.State.Edited = false;
            StatusMessage = $"Saved to {Path.GetFileName(path)}.";
            OnPropertyChanged(nameof(WindowTitle));
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
            return false;
        }
    }

    public void SelectSlot(SlotViewModel slot)
    {
        if (_sav is not { } sav || _sources is not { } sources)
            return;

        if (SelectedSlot is { } previous)
            previous.IsSelected = false;
        slot.IsSelected = true;
        SelectedSlot = slot;

        if (Editor is { } old)
            old.Applied -= OnEditorApplied;
        var editor = new PokemonEditorViewModel(sav, sources, slot);
        editor.Applied += OnEditorApplied;
        Editor = editor;
    }

    public void DeleteSelected()
    {
        if (_sav is not { } sav || SelectedSlot is not { } slot)
            return;
        slot.Write(sav.BlankPKM);
        sav.State.Edited = true;
        if (slot.IsParty)
            RefreshParty();
        SelectSlot(slot); // rebuild the editor on the now-empty slot
    }

    public void NextBox() => CurrentBox = _sav is { } sav && _currentBox >= sav.BoxCount - 1 ? 0 : _currentBox + 1;
    public void PrevBox() => CurrentBox = _sav is { } sav && _currentBox <= 0 ? sav.BoxCount - 1 : _currentBox - 1;

    private void OnEditorApplied()
    {
        if (SelectedSlot is { IsParty: true })
            RefreshParty();
        StatusMessage = "Changes written to slot. Use File → Save As… to export the save.";
    }

    private void RefreshParty()
    {
        foreach (var slot in PartySlots)
            slot.Refresh();
    }

    private void LoadSave(SaveFile sav)
    {
        _sav = sav;
        _sources = new FilteredGameDataSource(sav, GameInfo.Sources);
        GameInfo.FilteredSources = _sources;

        SelectedSlot = null;
        Editor = null;

        BoxSlots.Clear();
        PartySlots.Clear();

        if (sav.HasBox)
        {
            var names = new string[sav.BoxCount];
            for (int i = 0; i < names.Length; i++)
            {
                var name = (sav as IBoxDetailNameRead)?.GetBoxName(i);
                names[i] = string.IsNullOrWhiteSpace(name) ? BoxDetailNameExtensions.GetDefaultBoxName(i) : name;
            }
            BoxNames = names;

            var target = Math.Clamp(sav.CurrentBox, 0, sav.BoxCount - 1);
            for (int i = 0; i < sav.BoxSlotCount; i++)
                BoxSlots.Add(new SlotViewModel(sav, isParty: false, target, i));
            // The binding deduplicates writes: the ComboBox dropped the initial index while its
            // ItemsSource was still empty, so force a real value transition now that items exist.
            _currentBox = -1;
            CurrentBox = target;
        }
        else
        {
            BoxNames = [];
        }

        if (sav.HasParty)
        {
            for (int i = 0; i < 6; i++)
                PartySlots.Add(new SlotViewModel(sav, isParty: true, -1, i));
        }

        TrainerInfo = $"{sav.OT}  ·  TID {sav.DisplayTID}  ·  {GameInfo.GetVersionName(sav.Version)}  ·  {sav.PlayTimeString}";
        StatusMessage = "Save loaded.";
        OnPropertyChanged(nameof(HasSave));
        OnPropertyChanged(nameof(HasBox));
        OnPropertyChanged(nameof(HasParty));
        OnPropertyChanged(nameof(WindowTitle));

        SelectFirstOccupiedSlot();
    }

    private void SelectFirstOccupiedSlot()
    {
        foreach (var slot in BoxSlots)
        {
            if (!slot.IsEmpty)
            {
                SelectSlot(slot);
                return;
            }
        }
        foreach (var slot in PartySlots)
        {
            if (!slot.IsEmpty)
            {
                SelectSlot(slot);
                return;
            }
        }
        if (BoxSlots.Count > 0)
            SelectSlot(BoxSlots[0]);
        else if (PartySlots.Count > 0)
            SelectSlot(PartySlots[0]);
    }
}
