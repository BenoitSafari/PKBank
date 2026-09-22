using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using PKBank.Core.Configuration;
using PKBank.Desktop.Utils;
using PKHeX.Core;

namespace PKBank.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    public const int MaxOpenBoxes = 20;

    // ----- Selection -------------------------------------------------------

    private readonly List<SlotViewModel> _selectedSlots = [];
    private IReadOnlyList<string> _boxNames = [];
    private PokemonEditorViewModel? _editor;
    private string? _savePath;
    private SlotViewModel? _selectedSlot;
    private FilteredGameDataSource? _sources;
    private string _statusMessage = "Select a save file to edit.";
    private string _trainerInfo = string.Empty;

    public MainWindowViewModel(AppConfigService config)
    {
        Config = config;
        SaveSelection = new SaveSelectionViewModel(config);
        // Adding or removing a save folder (or switching language) changes what the
        // selection screen should list; only rescan while it is the visible screen.
        Config.Changed += (_, _) =>
        {
            if (SAV is null)
                _ = SaveSelection.RefreshAsync();
        };
    }

    public SaveFile? SAV { get; private set; }

    public SaveSelectionViewModel SaveSelection { get; }

    public bool HasSave => SAV is not null;

    /// <summary>
    ///     Deliberately not tied to <see cref="SaveFileState.Edited" />: writing the save is also what commits
    ///     pending bank changes, which leave the save itself untouched.
    /// </summary>
    public bool CanSave => SAV is not null && _savePath is not null;

    /// <summary>What would be lost by closing right now; empty when nothing is pending.</summary>
    public string PendingChangesSummary => SAV is { State.Edited: true }
        ? "The loaded save has unsaved changes"
        : string.Empty;

    public bool IsDirty => PendingChangesSummary.Length != 0;

    public bool CanEditTrainer => SAV is not null;
    public bool CanEditMysteryGift => SAV is IMysteryGiftStorageProvider;
    public bool IsGen3Save => SAV is SAV3;
    public bool IsGen3FRLGE => SAV is SAV3FRLG or SAV3E;
    public bool IsGen3RSE => SAV is SAV3RS or SAV3E;
    public bool CanEditPokedex => SAV?.HasPokeDex == true;
    public bool CanEditInventory => SAV?.Inventory.Pouches.Count > 0;
    public bool CanEditRoamer => SAV is SAV3 or SAV4 or SAV6XY;
    public AppConfigService Config { get; }

    public ObservableCollection<BoxPanelViewModel> OpenBoxes { get; } = [];
    public ObservableCollection<SlotViewModel> PartySlots { get; } = [];

    public IReadOnlyList<string> BoxNames
    {
        get => _boxNames;
        private set => SetField(ref _boxNames, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string TrainerInfo
    {
        get => _trainerInfo;
        private set => SetField(ref _trainerInfo, value);
    }

    public string WindowTitle => SAV is null
        ? AppInfo.Name
        : $"{AppInfo.Name}: {GameInfo.GetVersionName(SAV.Version)} - {Path.GetFileName(_savePath)}";

    public bool HasBox => SAV?.HasBox == true;
    public bool HasParty => SAV?.HasParty == true;

    public bool CanAddBox => HasBox && OpenBoxes.Count < MaxOpenBoxes;
    public bool CanCloseBox => OpenBoxes.Count > 1;

    public SlotViewModel? SelectedSlot
    {
        get => _selectedSlot;
        private set => SetField(ref _selectedSlot, value);
    }

    public PokemonEditorViewModel? Editor
    {
        get => _editor;
        private set => SetField(ref _editor, value);
    }

    // ----- Slot interactions -----------------------------------------------

    public bool CanSetToSlot => Editor is { HasSpecies: true };

    // View/Set are single-slot actions; Import/Delete/Export also work on a multi-selection.
    public bool CanViewSelected => !IsMultiSelection && SelectedSlot is { IsEmpty: false };
    public bool CanSetSelected => !IsMultiSelection && SelectedSlot is not null && CanSetToSlot;
    public bool CanDeleteSelected => _selectedSlots.Exists(static s => !s.IsEmpty);
    public bool CanImportSelected => SelectedSlot is not null;
    public bool CanExportSelected => _selectedSlots.Exists(static s => !s.IsEmpty);

    public IReadOnlyList<SlotViewModel> SelectedSlots => _selectedSlots;
    public bool IsMultiSelection => _selectedSlots.Count > 1;

    /// <summary>Opens another box panel, defaulting to the box after the last open one (wrapping).</summary>
    public void AddBoxPanel()
    {
        if (SAV is not { HasBox: true } sav || OpenBoxes.Count >= MaxOpenBoxes)
            return;
        var box = OpenBoxes.Count == 0 ? sav.CurrentBox : (OpenBoxes[^1].BoxIndex + 1) % sav.BoxCount;
        AddBoxPanel(box);
    }

    private void AddBoxPanel(int box)
    {
        if (SAV is not { } sav)
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
        var selectionChanged = false;
        for (var i = _selectedSlots.Count - 1; i >= 0; i--)
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

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PokemonEditorViewModel.HasSpecies))
        {
            OnPropertyChanged(nameof(CanSetToSlot));
            OnPropertyChanged(nameof(CanSetSelected));
        }
    }

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

    public void CloseSave()
    {
        SAV = null;
        _savePath = null;
        _sources = null;

        SelectedSlot = null;
        _selectedSlots.Clear();
        Editor = null;
        OpenBoxes.Clear();
        PartySlots.Clear();
        BoxNames = [];

        TrainerInfo = string.Empty;
        StatusMessage = "Select a save file to edit.";
        NotifySaveStateChanged();
        NotifySlotActionStates();
        _ = SaveSelection.RefreshAsync();
    }

    public void SetLanguage(string code)
    {
        if (!Config.SetLanguage(code))
            return;
        GameInfo.CurrentLanguage = code;
        // Actually reload the cached string tables (setting CurrentLanguage alone does not).
        LocalizeUtil.InitializeStrings(code, SAV);
        ReloadCurrentSave();
        // The Changed handler already kicked a scan, but with the previous string
        // tables; redo it so the listed game names follow the new language.
        if (SAV is null)
            _ = SaveSelection.RefreshAsync();
        StatusMessage = "Game data language changed.";
    }

    /// <summary>
    ///     Rebuilds all view-models from the current save (e.g. after a language change), keeping the open boxes and
    ///     selection.
    /// </summary>
    public void ReloadCurrentSave()
    {
        if (SAV is not { } sav)
            return;
        var openBoxes = OpenBoxes.Select(p => p.BoxIndex).ToArray();
        var previous = SelectedSlot;
        LoadSave(sav);
        if (HasBox && openBoxes.Length > 0)
        {
            OpenBoxes[0].BoxIndex = openBoxes[0];
            for (var i = 1; i < openBoxes.Length; i++)
                AddBoxPanel(openBoxes[i]);
        }

        if (previous is not { } prev)
            return;
        var match = prev.IsParty
            ? prev.Slot < PartySlots.Count ? PartySlots[prev.Slot] : null
            : OpenBoxes.FirstOrDefault(p => p.BoxIndex == prev.Box) is { } panel && prev.Slot < panel.Slots.Count
                ? panel.Slots[prev.Slot]
                : null;
        if (match is not null)
            ViewSlot(match);
    }

    /// <summary>Writes back to the file the save was loaded from.</summary>
    public bool SaveInPlace() => _savePath is { } path && TrySaveTo(path);

    public bool TrySaveTo(string path)
    {
        if (SAV is not { } sav)
            return false;
        try
        {
            var data = sav.Write();
            File.WriteAllBytes(path, data.Span);
            _savePath = path;
            sav.State.Edited = false;
            StatusMessage = $"Saved to {Path.GetFileName(path)}.";
            OnPropertyChanged(nameof(WindowTitle));
            NotifyPendingChanges();
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    ///     Imports a Pokémon file into the given slot, converting it to the save's
    ///     format when needed (Gen 3 file onto a Gen 4 save, etc.).
    /// </summary>
    public bool TryImportFileToSlot(string path, SlotViewModel slot)
    {
        if (SAV is not { } sav)
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

    /// <summary>Selected slots in display order: the party bar first, then each open box panel.</summary>
    public IReadOnlyList<SlotViewModel> GetSelectedSlotsInDisplayOrder()
    {
        var selected = new HashSet<SlotViewModel>(_selectedSlots);
        var result = new List<SlotViewModel>(_selectedSlots.Count);
        foreach (var slot in PartySlots)
            if (selected.Contains(slot))
                result.Add(slot);
        foreach (var panel in OpenBoxes)
        foreach (var slot in panel.Slots)
            if (selected.Contains(slot))
                result.Add(slot);

        return result;
    }

    /// <summary>Imports files into the current selection in display order (see <see cref="ImportFilesToSlots" />).</summary>
    public void ImportFilesToSelection(IReadOnlyList<string> paths)
        => ImportFilesToSlots(paths, GetSelectedSlotsInDisplayOrder());

    /// <summary>
    ///     Multi-import: the first file goes into the first target slot and so on;
    ///     files beyond the target count are ignored. Two panels showing the same
    ///     box expose the same physical slot twice — it only receives one file.
    /// </summary>
    public void ImportFilesToSlots(IReadOnlyList<string> paths, IReadOnlyList<SlotViewModel> targets)
    {
        if (paths.Count == 0 || targets.Count == 0)
            return;

        var seen = new HashSet<(bool IsParty, int Box, int Slot)>();
        var unique = new List<SlotViewModel>(targets.Count);
        foreach (var target in targets)
            if (seen.Add((target.IsParty, target.Box, target.Slot)))
                unique.Add(target);

        var count = Math.Min(unique.Count, paths.Count);
        var imported = 0;
        for (var i = 0; i < count; i++)
            if (TryImportFileToSlot(paths[i], unique[i]))
                imported++;

        if (paths.Count > 1 || unique.Count > 1)
        {
            var ignored = paths.Count > unique.Count
                ? $" ({paths.Count - unique.Count} extra file(s) ignored)"
                : string.Empty;
            StatusMessage = $"Imported {imported} of {count} file(s) into the selected slots{ignored}.";
        }
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
            if (!slot.IsEmpty)
                DeleteSlot(slot);
    }

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

        // Box ranges span the open panels in display order, so Shift+click can
        // select across boxes; party ranges stay within the party bar.
        var list = slot.IsParty ? PartySlots.ToList() : OpenBoxes.SelectMany(p => p.Slots).ToList();
        var from = list.IndexOf(anchor);
        var to = list.IndexOf(slot);
        if (from < 0 || to < 0)
        {
            SelectSlot(slot);
            return;
        }

        var (start, end) = from <= to ? (from, to) : (to, from);
        for (var i = start; i <= end; i++)
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
    ///     Slots a context-menu action should apply to: the whole selection when the
    ///     clicked slot belongs to it, otherwise just the clicked slot.
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
        if (SAV is not { } sav || _sources is not { } sources)
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
        if (SAV is not { } sav)
            return;
        slot.Write(sav.BlankPKM);
        sav.State.Edited = true;
        RefreshMirrorSlots(slot);
        if (slot.IsParty)
            RefreshParty();
        NotifySlotActionStates();
    }

    /// <summary>
    ///     Drag &amp; drop between slots: moves the Pokémon to the target slot,
    ///     swapping when the target is occupied.
    /// </summary>
    public void MoveOrSwapSlots(SlotViewModel source, SlotViewModel target)
    {
        if (SAV is not { } sav || source == target)
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
        if (SAV is not { } sav || Editor is not { } editor)
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
        SAV = sav;
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
            for (var i = 0; i < names.Length; i++)
            {
                string? name;
                try
                {
                    name = (sav as IBoxDetailNameRead)?.GetBoxName(i);
                }
                catch
                {
                    name = null; // some saves lack the underlying blocks
                }

                name = DisplayText.Sanitize(name ?? string.Empty);
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
            for (var i = 0; i < 6; i++)
                PartySlots.Add(new SlotViewModel(sav, true, -1, i));

        RefreshTrainerInfo();
        StatusMessage = "Save loaded.";
        NotifySaveStateChanged();

        SelectFirstOccupiedSlot();
    }

    /// <summary>Everything that depends on which save (if any) is currently loaded.</summary>
    private void NotifySaveStateChanged()
    {
        OnPropertyChanged(nameof(HasSave));
        OnPropertyChanged(nameof(CanEditTrainer));
        OnPropertyChanged(nameof(CanEditMysteryGift));
        OnPropertyChanged(nameof(IsGen3Save));
        OnPropertyChanged(nameof(IsGen3FRLGE));
        OnPropertyChanged(nameof(IsGen3RSE));
        OnPropertyChanged(nameof(CanEditPokedex));
        OnPropertyChanged(nameof(CanEditInventory));
        OnPropertyChanged(nameof(CanEditRoamer));
        OnPropertyChanged(nameof(HasBox));
        OnPropertyChanged(nameof(HasParty));
        OnPropertyChanged(nameof(CanAddBox));
        OnPropertyChanged(nameof(CanCloseBox));
        OnPropertyChanged(nameof(WindowTitle));
        NotifyPendingChanges();
    }

    /// <summary>Everything that depends on what is still waiting to be written to disk.</summary>
    private void NotifyPendingChanges()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(PendingChangesSummary));
        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>Rebuilds the status-bar trainer summary, e.g. after the trainer editor changed it.</summary>
    public void RefreshTrainerInfo()
    {
        if (SAV is not { } sav)
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
            playTime = "–"; // some saves lack the underlying blocks
        }

        TrainerInfo = $"{sav.OT}  ·  TID {sav.DisplayTID}  ·  {GameInfo.GetVersionName(sav.Version)}  ·  {playTime}";
    }

    private void SelectFirstOccupiedSlot()
    {
        var boxSlots = OpenBoxes.Count > 0 ? OpenBoxes[0].Slots : [];
        foreach (var slot in boxSlots)
            if (!slot.IsEmpty)
            {
                ViewSlot(slot);
                return;
            }

        foreach (var slot in PartySlots)
            if (!slot.IsEmpty)
            {
                ViewSlot(slot);
                return;
            }

        if (boxSlots.Count > 0)
            ViewSlot(boxSlots[0]);
        else if (PartySlots.Count > 0)
            ViewSlot(PartySlots[0]);
    }
}
