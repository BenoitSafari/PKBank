using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using PKBank.Desktop.Services;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKBank.Desktop.ViewModels;

public sealed class MainWindowViewModel(AppSettings settings) : ViewModelBase
{
    private SaveFile? _sav;
    private FilteredGameDataSource? _sources;
    private string? _savePath;
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
    public bool CanEditTrainer => _sav is not null;
    public bool CanEditMysteryGift => _sav is IMysteryGiftStorageProvider;
    public bool IsGen3Save => _sav is SAV3;
    public bool IsGen3FRLGE => _sav is SAV3FRLG or SAV3E;
    public bool IsGen3RSE => _sav is SAV3RS or SAV3E;
    public bool CanEditPokedex => _sav?.HasPokeDex == true;
    public bool CanEditInventory => _sav?.Inventory.Pouches.Count > 0;
    public AppSettings Settings { get; } = settings;

    /// <summary>At most this many box panels can be open side by side.</summary>
    public const int MaxOpenBoxes = 20;

    public ObservableCollection<BoxPanelViewModel> OpenBoxes { get; } = [];
    public ObservableCollection<SlotViewModel> PartySlots { get; } = [];

    public IReadOnlyList<string> BoxNames { get => _boxNames; private set => SetField(ref _boxNames, value); }
    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }
    public string TrainerInfo { get => _trainerInfo; private set => SetField(ref _trainerInfo, value); }

    public string WindowTitle => _sav is null
        ? "PKBank.Desktop"
        : $"PKBank.Desktop — {GameInfo.GetVersionName(_sav.Version)} — {(_savePath is null ? "(new)" : Path.GetFileName(_savePath))}";

    public bool HasBox => _sav?.HasBox == true;
    public bool HasParty => _sav?.HasParty == true;

    public bool CanAddBox => HasBox && OpenBoxes.Count < MaxOpenBoxes;
    public bool CanCloseBox => OpenBoxes.Count > 1;

    /// <summary>Opens another box panel, defaulting to the box after the last open one (wrapping).</summary>
    public void AddBoxPanel()
    {
        if (_sav is not { HasBox: true } sav || OpenBoxes.Count >= MaxOpenBoxes)
            return;
        var box = OpenBoxes.Count == 0 ? sav.CurrentBox : (OpenBoxes[^1].BoxIndex + 1) % sav.BoxCount;
        AddBoxPanel(box);
    }

    private void AddBoxPanel(int box)
    {
        if (_sav is not { } sav)
            return;
        OpenBoxes.Add(new BoxPanelViewModel(sav, box));
        OnPropertyChanged(nameof(CanAddBox));
        OnPropertyChanged(nameof(CanCloseBox));
    }

    public void CloseBoxPanel(BoxPanelViewModel panel)
    {
        if (OpenBoxes.Count <= 1 || !OpenBoxes.Remove(panel))
            return;

        // Drop the panel's slots from the selection so no ghost selection remains.
        bool selectionChanged = false;
        for (int i = _selectedSlots.Count - 1; i >= 0; i--)
        {
            if (!panel.Slots.Contains(_selectedSlots[i]))
                continue;
            _selectedSlots[i].IsSelected = false;
            _selectedSlots.RemoveAt(i);
            selectionChanged = true;
        }
        if (SelectedSlot is { } selected && panel.Slots.Contains(selected))
            SelectedSlot = _selectedSlots.Count > 0 ? _selectedSlots[^1] : null;
        if (selectionChanged)
            NotifySlotActionStates();

        OnPropertyChanged(nameof(CanAddBox));
        OnPropertyChanged(nameof(CanCloseBox));
    }

    /// <summary>A write through one panel's slot must also show in other panels viewing the same box.</summary>
    private void RefreshMirrorSlots(SlotViewModel written)
    {
        if (written.IsParty)
            return;
        foreach (var panel in OpenBoxes)
        {
            if (panel.BoxIndex != written.Box || written.Slot >= panel.Slots.Count)
                continue;
            var mirror = panel.Slots[written.Slot];
            if (mirror != written)
                mirror.Refresh();
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
        OnPropertyChanged(nameof(CanImportSelected));
        OnPropertyChanged(nameof(CanExportSelected));
        OnPropertyChanged(nameof(IsMultiSelection));
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
            LoadSaveFromPath(sav, path);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load save: {ex.Message}";
        }
    }

    /// <summary>Loads an already-parsed save file, remembering where it came from.</summary>
    public void LoadSaveFromPath(SaveFile sav, string path)
    {
        _savePath = path;
        LoadSave(sav);
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
        foreach (var panel in OpenBoxes)
            panel.RefreshSlots();
        foreach (var slot in PartySlots)
            slot.Refresh();
        Editor?.RefreshSprite();
    }

    /// <summary>Rebuilds all view-models from the current save (e.g. after a language change), keeping the open boxes and selection.</summary>
    public void ReloadCurrentSave()
    {
        if (_sav is not { } sav)
            return;
        var openBoxes = OpenBoxes.Select(p => p.BoxIndex).ToArray();
        var previous = SelectedSlot;
        LoadSave(sav);
        if (HasBox && openBoxes.Length > 0)
        {
            OpenBoxes[0].BoxIndex = openBoxes[0];
            for (int i = 1; i < openBoxes.Length; i++)
                AddBoxPanel(openBoxes[i]);
        }
        if (previous is not { } prev)
            return;
        var match = prev.IsParty
            ? (prev.Slot < PartySlots.Count ? PartySlots[prev.Slot] : null)
            : OpenBoxes.FirstOrDefault(p => p.BoxIndex == prev.Box) is { } panel && prev.Slot < panel.Slots.Count ? panel.Slots[prev.Slot] : null;
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

    // View/Set/Import are single-slot actions; Delete/Export also work on a multi-selection.
    public bool CanViewSelected => !IsMultiSelection && SelectedSlot is { IsEmpty: false };
    public bool CanSetSelected => !IsMultiSelection && SelectedSlot is not null && CanSetToSlot;
    public bool CanDeleteSelected => _selectedSlots.Exists(static s => !s.IsEmpty);
    public bool CanImportSelected => !IsMultiSelection && SelectedSlot is not null;
    public bool CanExportSelected => _selectedSlots.Exists(static s => !s.IsEmpty);

    /// <summary>
    /// Imports a Pokémon file into the given slot, converting it to the save's
    /// format when needed (Gen 3 file onto a Gen 4 save, etc.).
    /// </summary>
    public bool TryImportFileToSlot(string path, SlotViewModel slot)
    {
        if (_sav is not { } sav)
            return false;
        var pk = PkmFileService.TryLoadCompatible(path, sav, out var message);
        StatusMessage = message;
        if (pk is null)
            return false;

        if (slot.IsParty)
            pk.ResetPartyStats();
        pk.RefreshChecksum();
        slot.Write(pk);
        sav.State.Edited = true;
        RefreshMirrorSlots(slot);
        if (slot.IsParty)
            RefreshParty();
        if (Editor?.Origin == slot)
            Editor.Revert(); // the displayed entity's slot changed underneath it
        NotifySlotActionStates();
        return true;
    }

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

    public void DeleteSelected() => DeleteSlots(_selectedSlots.ToArray());

    public void DeleteSlots(IReadOnlyList<SlotViewModel> slots)
    {
        foreach (var slot in slots)
        {
            if (!slot.IsEmpty)
                DeleteSlot(slot);
        }
    }

    // ----- Selection -------------------------------------------------------
    // SelectedSlot is the primary slot (anchor for ranges, target of the
    // editor-centric actions); _selectedSlots holds the full multi-selection.

    private readonly List<SlotViewModel> _selectedSlots = [];

    public IReadOnlyList<SlotViewModel> SelectedSlots => _selectedSlots;
    public bool IsMultiSelection => _selectedSlots.Count > 1;

    /// <summary>Plain left click: collapses any multi-selection back to a single slot.</summary>
    public void SelectSlot(SlotViewModel slot)
    {
        foreach (var previous in _selectedSlots)
            previous.IsSelected = false;
        _selectedSlots.Clear();
        _selectedSlots.Add(slot);
        slot.IsSelected = true;
        SelectedSlot = slot;
        NotifySlotActionStates();
    }

    /// <summary>Ctrl+click: adds the slot to the selection (or removes it when already selected).</summary>
    public void ToggleSelectSlot(SlotViewModel slot)
    {
        if (_selectedSlots.Count == 0)
        {
            SelectSlot(slot);
            return;
        }
        if (_selectedSlots.Contains(slot))
        {
            if (_selectedSlots.Count == 1)
                return; // never empty the selection entirely
            _selectedSlots.Remove(slot);
            slot.IsSelected = false;
            if (SelectedSlot == slot)
                SelectedSlot = _selectedSlots[^1];
        }
        else
        {
            _selectedSlots.Add(slot);
            slot.IsSelected = true;
            SelectedSlot = slot; // the anchor follows the last addition
        }
        NotifySlotActionStates();
    }

    /// <summary>Shift+click: adds every slot between the anchor and the clicked slot (inclusive).</summary>
    public void RangeSelectSlot(SlotViewModel slot)
    {
        if (SelectedSlot is not { } anchor || anchor.IsParty != slot.IsParty)
        {
            SelectSlot(slot); // no same-area anchor: behave like a plain click
            return;
        }

        // Ranges only span a single area: the party bar or one box panel.
        var list = slot.IsParty ? PartySlots : OpenBoxes.FirstOrDefault(p => p.Slots.Contains(slot))?.Slots;
        if (list is null)
        {
            SelectSlot(slot);
            return;
        }
        int from = list.IndexOf(anchor);
        int to = list.IndexOf(slot);
        if (from < 0 || to < 0)
        {
            SelectSlot(slot);
            return;
        }

        var (start, end) = from <= to ? (from, to) : (to, from);
        for (int i = start; i <= end; i++)
        {
            var member = list[i];
            if (_selectedSlots.Contains(member))
                continue;
            _selectedSlots.Add(member);
            member.IsSelected = true;
        }
        NotifySlotActionStates();
    }

    /// <summary>
    /// Slots a context-menu action should apply to: the whole selection when the
    /// clicked slot belongs to it, otherwise just the clicked slot.
    /// </summary>
    public IReadOnlyList<SlotViewModel> GetActionTargets(SlotViewModel clicked)
        => IsMultiSelection && _selectedSlots.Contains(clicked) ? _selectedSlots.ToArray() : [clicked];

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
        RefreshMirrorSlots(slot);
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
        RefreshMirrorSlots(source);
        RefreshMirrorSlots(target);
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
        RefreshMirrorSlots(slot);
        if (slot.IsParty)
            RefreshParty();
        if (slot == editor.Origin)
            editor.Revert(); // origin slot now holds the freshly written data
        NotifySlotActionStates();
        StatusMessage = "Editor content written to slot.";
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
        _selectedSlots.Clear();
        Editor = null;

        OpenBoxes.Clear();
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
            AddBoxPanel(Math.Clamp(sav.CurrentBox, 0, sav.BoxCount - 1));
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

        RefreshTrainerInfo();
        StatusMessage = "Save loaded.";
        OnPropertyChanged(nameof(HasSave));
        OnPropertyChanged(nameof(CanEditTrainer));
        OnPropertyChanged(nameof(CanEditMysteryGift));
        OnPropertyChanged(nameof(IsGen3Save));
        OnPropertyChanged(nameof(IsGen3FRLGE));
        OnPropertyChanged(nameof(IsGen3RSE));
        OnPropertyChanged(nameof(CanEditPokedex));
        OnPropertyChanged(nameof(CanEditInventory));
        OnPropertyChanged(nameof(HasBox));
        OnPropertyChanged(nameof(HasParty));
        OnPropertyChanged(nameof(CanAddBox));
        OnPropertyChanged(nameof(CanCloseBox));
        OnPropertyChanged(nameof(WindowTitle));

        SelectFirstOccupiedSlot();
    }

    /// <summary>Rebuilds the status-bar trainer summary, e.g. after the trainer editor changed it.</summary>
    public void RefreshTrainerInfo()
    {
        if (_sav is not { } sav)
        {
            TrainerInfo = string.Empty;
            return;
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
    }

    private void SelectFirstOccupiedSlot()
    {
        var boxSlots = OpenBoxes.Count > 0 ? OpenBoxes[0].Slots : [];
        foreach (var slot in boxSlots)
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
        if (boxSlots.Count > 0)
            ViewSlot(boxSlots[0]);
        else if (PartySlots.Count > 0)
            ViewSlot(PartySlots[0]);
    }
}
