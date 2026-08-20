using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using PKBank.Desktop.Services;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKBank.Desktop.ViewModels;

public sealed class MainWindowViewModel(AppSettings settings) : ViewModelBase
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
    public AppSettings Settings { get; } = settings;

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

    private void NotifySlotActionStates()
    {
        OnPropertyChanged(nameof(CanViewSelected));
        OnPropertyChanged(nameof(CanSetToSlot));
        OnPropertyChanged(nameof(CanSetSelected));
        OnPropertyChanged(nameof(CanDeleteSelected));
    }

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PokemonEditorViewModel.HasSpecies))
        {
            OnPropertyChanged(nameof(CanSetToSlot));
            OnPropertyChanged(nameof(CanSetSelected));
        }
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

    /// <summary>Loads the configured blank save at startup when no file argument was given.</summary>
    public void LoadStartupBlank() => NewBlank(Settings.BlankSaveVersion);

    public void SetLanguage(string code)
    {
        if (!GameLanguage.IsLanguageValid(code) || code == Settings.Language)
            return;
        Settings.Language = code;
        Settings.Save();
        GameInfo.CurrentLanguage = code;
        // Actually reload the cached string tables (setting CurrentLanguage alone does not).
        LocalizeUtil.InitializeStrings(code, _sav);
        ReloadCurrentSave();
        StatusMessage = "Game data language changed.";
    }

    public void SetShinySprites(bool value)
    {
        if (value == Settings.ShinySprites)
            return;
        Settings.ShinySprites = value;
        Settings.Save();
        SpriteName.AllowShinySprite = value;
        foreach (var slot in BoxSlots)
            slot.Refresh();
        foreach (var slot in PartySlots)
            slot.Refresh();
        Editor?.RefreshSprite();
    }

    /// <summary>Rebuilds all view-models from the current save (e.g. after a language change), keeping the selection.</summary>
    public void ReloadCurrentSave()
    {
        if (_sav is not { } sav)
            return;
        var box = _currentBox;
        var previous = SelectedSlot;
        LoadSave(sav);
        if (HasBox)
            CurrentBox = box;
        if (previous is not { } prev)
            return;
        var match = prev.IsParty
            ? (prev.Slot < PartySlots.Count ? PartySlots[prev.Slot] : null)
            : (prev.Slot < BoxSlots.Count ? BoxSlots[prev.Slot] : null);
        if (match is not null)
            ViewSlot(match);
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

    // ----- Slot interactions -----------------------------------------------
    // View/Set/Delete act on a target slot. The context menu targets the
    // right-clicked slot; the top action bar targets the selected slot.
    // Both share the same guards (CanView/CanSetToSlot/CanDelete) and actions.

    /// <summary>Set requires an entity in the editor, whatever the target slot is.</summary>
    public bool CanSetToSlot => Editor is { HasSpecies: true };

    public bool CanViewSelected => SelectedSlot is { IsEmpty: false };
    public bool CanSetSelected => SelectedSlot is not null && CanSetToSlot;
    public bool CanDeleteSelected => SelectedSlot is { IsEmpty: false };

    public void ViewSelected()
    {
        if (SelectedSlot is { } slot)
            ViewSlot(slot);
    }

    public void SetSelected()
    {
        if (SelectedSlot is { } slot)
            SetSlotFromEditor(slot);
    }

    public void DeleteSelected()
    {
        if (SelectedSlot is { } slot)
            DeleteSlot(slot);
    }

    /// <summary>Marks the slot as selected without touching the editor (left click).</summary>
    public void SelectSlot(SlotViewModel slot)
    {
        if (SelectedSlot is { } previous)
            previous.IsSelected = false;
        slot.IsSelected = true;
        SelectedSlot = slot;
        NotifySlotActionStates();
    }

    /// <summary>Selects the slot and loads its Pokémon into the editor (View action).</summary>
    public void ViewSlot(SlotViewModel slot)
    {
        SelectSlot(slot);
        LoadEditor(slot);
    }

    private void LoadEditor(SlotViewModel slot)
    {
        if (_sav is not { } sav || _sources is not { } sources)
            return;

        if (Editor is { } old)
            old.PropertyChanged -= OnEditorPropertyChanged;
        var editor = new PokemonEditorViewModel(sav, sources, slot);
        editor.PropertyChanged += OnEditorPropertyChanged;
        Editor = editor;
        NotifySlotActionStates();
    }

    public void DeleteSlot(SlotViewModel slot)
    {
        if (_sav is not { } sav)
            return;
        slot.Write(sav.BlankPKM);
        sav.State.Edited = true;
        if (slot.IsParty)
            RefreshParty();
        NotifySlotActionStates();
    }

    /// <summary>
    /// Drag &amp; drop between slots: moves the Pokémon to the target slot,
    /// swapping when the target is occupied.
    /// </summary>
    public void MoveOrSwapSlots(SlotViewModel source, SlotViewModel target)
    {
        if (_sav is not { } sav || source == target)
            return;
        var src = source.Read();
        if (src.Species == 0)
            return;
        var dst = target.Read();

        if (target.IsParty)
            src.ResetPartyStats();
        src.RefreshChecksum();
        target.Write(src);

        if (dst.Species != 0)
        {
            if (source.IsParty)
                dst.ResetPartyStats();
            dst.RefreshChecksum();
            source.Write(dst);
        }
        else
        {
            source.Write(sav.BlankPKM);
        }

        sav.State.Edited = true;
        if (source.IsParty || target.IsParty)
            RefreshParty();
        NotifySlotActionStates();
        StatusMessage = dst.Species != 0 ? "Slots swapped." : "Pokémon moved.";
    }

    /// <summary>Writes the editor's current entity (with pending edits) into the given slot.</summary>
    public void SetSlotFromEditor(SlotViewModel slot)
    {
        if (_sav is not { } sav || Editor is not { } editor)
            return;
        var pk = editor.GetEntityClone();
        if (slot.IsParty)
            pk.ResetPartyStats();
        pk.RefreshChecksum();
        slot.Write(pk);
        sav.State.Edited = true;
        if (slot.IsParty)
            RefreshParty();
        if (slot == editor.Origin)
            editor.Revert(); // origin slot now holds the freshly written data
        NotifySlotActionStates();
        StatusMessage = "Editor content written to slot.";
    }

    public void NextBox() => CurrentBox = _sav is { } sav && _currentBox >= sav.BoxCount - 1 ? 0 : _currentBox + 1;
    public void PrevBox() => CurrentBox = _sav is { } sav && _currentBox <= 0 ? sav.BoxCount - 1 : _currentBox - 1;

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
                string? name;
                try
                {
                    name = (sav as IBoxDetailNameRead)?.GetBoxName(i);
                }
                catch
                {
                    name = null; // blank saves may lack the underlying blocks
                }
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

        string playTime;
        try
        {
            playTime = sav.PlayTimeString;
        }
        catch
        {
            playTime = "–"; // blank saves may lack the underlying blocks
        }
        TrainerInfo = $"{sav.OT}  ·  TID {sav.DisplayTID}  ·  {GameInfo.GetVersionName(sav.Version)}  ·  {playTime}";
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
                ViewSlot(slot);
                return;
            }
        }
        foreach (var slot in PartySlots)
        {
            if (!slot.IsEmpty)
            {
                ViewSlot(slot);
                return;
            }
        }
        if (BoxSlots.Count > 0)
            ViewSlot(BoxSlots[0]);
        else if (PartySlots.Count > 0)
            ViewSlot(PartySlots[0]);
    }
}
