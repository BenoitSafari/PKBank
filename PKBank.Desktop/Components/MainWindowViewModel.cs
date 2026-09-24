using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using PKBank.Core.Configuration;
using PKBank.Desktop.Services.Slots;
using PKBank.Desktop.Services.Slots.Types;
using PKBank.Desktop.Utils;
using PKHeX.Core;
using PKBank.Desktop.Components.Bank.ViewModels;
using PKBank.Desktop.Components.Box.ViewModels;
using PKBank.Desktop.Components.PokemonEditor.ViewModels;
using PKBank.Desktop.Components.SaveSelection.ViewModels;

namespace PKBank.Desktop.Components;

public sealed class MainWindowViewModel : ViewModelBase
{
    /// <summary>Distinct reasons listed when a batch is refused; enough to act on, short enough to read.</summary>
    private const int MaxReportedRefusals = 5;

    // ----- Selection -------------------------------------------------------

    private readonly List<SlotViewModel> _selectedSlots = [];

    /// <summary>Copied Pokémon, kept in-process: Paste stays available across boxes and banks.</summary>
    private PKM? _clipboard;

    private SaveBoxPanelViewModel? _saveBox;
    private SaveBoxStore? _boxStore;
    private SavePartyStore? _partyStore;
    private string? _savePath;
    private SlotViewModel? _selectedSlot;
    private FilteredGameDataSource? _sources;
    private string _statusMessage = "Select a save file to edit.";
    private string _trainerInfo = string.Empty;

    public MainWindowViewModel(AppConfigService config)
    {
        Config = config;
        SaveSelection = new SaveSelectionViewModel(config);
        Bank = new BankViewModel(config);
        Bank.StatusRaised += message => StatusMessage = message;
        Bank.PropertyChanged += OnBankPropertyChanged;
        Bank.Initialize();
        // Adding or removing a save folder (or switching language) changes what the
        // selection screen should list; only rescan while it is the visible screen.
        Config.Changed += (_, _) =>
        {
            if (SAV is null)
                _ = SaveSelection.RefreshAsync();
        };
    }

    public SaveFile? SAV { get; private set; }

    /// <summary>Localized game lists the editor window needs to build its combos.</summary>
    internal FilteredGameDataSource? Sources => _sources;

    public SaveSelectionViewModel SaveSelection { get; }

    public BankViewModel Bank { get; }

    public bool HasSave => SAV is not null;

    /// <summary>
    ///     Deliberately not tied to <see cref="SaveFileState.Edited" />: writing the save is also what commits
    ///     pending bank changes, which leave the save itself untouched.
    /// </summary>
    public bool CanSave => SAV is not null && _savePath is not null;

    /// <summary>What would be lost by closing right now; empty when nothing is pending.</summary>
    public string PendingChangesSummary => (SAV is { State.Edited: true }, Bank.IsDirty) switch
    {
        (true, true) => "The loaded save and the bank have unsaved changes",
        (true, false) => "The loaded save has unsaved changes",
        (false, true) => "The bank has unsaved changes",
        _ => string.Empty
    };

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

    /// <summary>The box of the loaded save on screen; null when the save has no boxes.</summary>
    public SaveBoxPanelViewModel? SaveBox
    {
        get => _saveBox;
        private set => SetField(ref _saveBox, value);
    }

    private IReadOnlyList<SlotViewModel> SaveBoxSlots => (IReadOnlyList<SlotViewModel>?)SaveBox?.Slots ?? [];
    public ObservableCollection<SlotViewModel> PartySlots { get; } = [];

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


    public SlotViewModel? SelectedSlot
    {
        get => _selectedSlot;
        private set => SetField(ref _selectedSlot, value);
    }

    // ----- Slot interactions -----------------------------------------------

    // Edit/Copy/Paste are single-slot actions; Import/Delete/Export also work on a multi-selection.
    // An empty slot is editable too: that is the "New" case.
    public bool CanEditSelected => !IsMultiSelection && SelectedSlot is { IsCompatible: true };

    /// <summary>The edit action creates when the slot is empty, so the button says so.</summary>
    public string EditSelectedLabel => SelectedSlot is { IsEmpty: false } ? "Edit" : "New";

    public bool CanCopySelected => !IsMultiSelection && SelectedSlot is { IsEmpty: false };
    public bool CanPasteSelected => !IsMultiSelection && SelectedSlot is not null && CanPaste;

    /// <summary>A copied Pokémon can go into any slot: the target store converts it on the way.</summary>
    public bool CanPaste => _clipboard is not null && SAV is not null;
    public bool CanDeleteSelected => _selectedSlots.Exists(static s => !s.IsEmpty);
    public bool CanImportSelected => SelectedSlot is not null;
    public bool CanExportSelected => _selectedSlots.Exists(static s => !s.IsEmpty);

    public IReadOnlyList<SlotViewModel> SelectedSlots => _selectedSlots;
    public bool IsMultiSelection => _selectedSlots.Count > 1;

    private void NotifySlotActionStates()
    {
        OnPropertyChanged(nameof(CanEditSelected));
        OnPropertyChanged(nameof(EditSelectedLabel));
        OnPropertyChanged(nameof(CanCopySelected));
        OnPropertyChanged(nameof(CanPasteSelected));
        OnPropertyChanged(nameof(CanPaste));
        OnPropertyChanged(nameof(CanDeleteSelected));
        OnPropertyChanged(nameof(CanImportSelected));
        OnPropertyChanged(nameof(CanExportSelected));
        OnPropertyChanged(nameof(IsMultiSelection));
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
        _boxStore = null;
        _partyStore = null;

        ClearSelection();
        _clipboard = null; // belongs to the save being closed; a language reload keeps it
        SaveBox = null;
        PartySlots.Clear();

        TrainerInfo = string.Empty;
        StatusMessage = "Select a save file to edit.";
        Bank.OnSaveChanged(null, Config.Language);
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
    ///     Rebuilds all view-models from the current save (e.g. after a language change), keeping the box on
    ///     screen and the selection.
    /// </summary>
    public void ReloadCurrentSave()
    {
        if (SAV is not { } sav)
            return;
        var box = SaveBox?.ContainerIndex;
        var previous = SelectedSlot;
        LoadSave(sav);
        if (SaveBox is { } panel && box is { } index)
            panel.ContainerIndex = index;

        // The stores were rebuilt, so the old slot instances are gone: find the same coordinates again.
        if (previous is not { } prev)
            return;
        var match = prev.IsParty
            ? prev.Index < PartySlots.Count ? PartySlots[prev.Index] : null
            : SaveBox is { } saveBox && saveBox.ContainerIndex == prev.Container &&
              prev.Index < saveBox.Slots.Count
                ? saveBox.Slots[prev.Index]
                : null;
        if (match is not null)
            SelectSlot(match);
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
            // Save first, banks second: a failed bank commit leaves its pending file in place to be
            // retried, whereas the reverse would alter a bank for a save that never made it to disk.
            var banks = Bank.CommitPending();
            StatusMessage = $"Saved to {Path.GetFileName(path)}.{banks}";
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
        if (slot.IsParty)
            RefreshParty();
        NotifySlotActionStates();
        return true;
    }

    /// <summary>
    ///     Every slot on screen, in reading order: the party bar, then the save box, then the bank
    ///     box. A selection never spans two of these scopes, so the order between them never matters.
    /// </summary>
    private IEnumerable<SlotViewModel> DisplayedSlots()
    {
        foreach (var slot in PartySlots)
            yield return slot;
        foreach (var slot in SaveBoxSlots)
            yield return slot;
        if (Bank.CurrentBox is { } bankBox)
            foreach (var slot in bankBox.Slots)
                yield return slot;
    }

    /// <summary>Selected slots in display order.</summary>
    public IReadOnlyList<SlotViewModel> GetSelectedSlotsInDisplayOrder()
    {
        var selected = new HashSet<SlotViewModel>(_selectedSlots);
        return DisplayedSlots().Where(selected.Contains).ToArray();
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

        var seen = new HashSet<SlotKey>();
        var unique = new List<SlotViewModel>(targets.Count);
        foreach (var target in targets)
            if (seen.Add(target.Key))
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
        ClearSelection();
        _selectedSlots.Add(slot);
        slot.IsSelected = true;
        SelectedSlot = slot;
        NotifySlotActionStates();
    }

    private void ClearSelection()
    {
        foreach (var previous in _selectedSlots)
            previous.IsSelected = false;
        _selectedSlots.Clear();
        SelectedSlot = null;
    }

    /// <summary>
    ///     A selection never spans two scopes: extending it from a save box into a bank box (or back) starts a
    ///     fresh single selection instead.
    /// </summary>
    private bool LeavesSelectionScope(SlotViewModel slot)
        => SelectedSlot is { } anchor && anchor.Store.Scope != slot.Store.Scope;

    /// <summary>Ctrl+click: adds the slot to the selection (or removes it when already selected).</summary>
    public void ToggleSelectSlot(SlotViewModel slot)
    {
        if (_selectedSlots.Count == 0 || LeavesSelectionScope(slot))
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
        if (SelectedSlot is not { } anchor || anchor.Store != slot.Store)
        {
            SelectSlot(slot); // no same-area anchor: behave like a plain click
            return;
        }

        var list = GetRangeArea(slot);
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
    ///     Slots a Shift+click range may cover: the party bar, the save box or the bank box the slot is in.
    /// </summary>
    private List<SlotViewModel> GetRangeArea(SlotViewModel slot)
    {
        if (slot.Store == _partyStore)
            return PartySlots.ToList();
        if (Bank.CurrentBox is { } bankBox && bankBox.Store == slot.Store)
            return bankBox.Slots.ToList();
        return SaveBoxSlots.Where(s => s.Store == slot.Store).ToList();
    }

    /// <summary>
    ///     Slots a context-menu action should apply to: the whole selection when the
    ///     clicked slot belongs to it, otherwise just the clicked slot.
    /// </summary>
    public IReadOnlyList<SlotViewModel> GetActionTargets(SlotViewModel clicked)
        => IsMultiSelection && _selectedSlots.Contains(clicked) ? _selectedSlots.ToArray() : [clicked];

    public void DeleteSlot(SlotViewModel slot)
    {
        if (SAV is not { } sav)
            return;
        slot.Write(slot.Store.Blank);
        MarkEdited(slot.Store);
        if (slot.IsParty)
            RefreshParty();
        NotifySlotActionStates();
        TrackBankChanges();
    }

    /// <summary>
    ///     Drag &amp; drop between slots: moves the Pokémon to the target slot,
    ///     swapping when the target is occupied. Crossing stores converts on the way.
    /// </summary>
    public void MoveOrSwapSlots(SlotViewModel source, SlotViewModel target)
    {
        if (SAV is not { } sav || source == target || source.Key == target.Key)
            return;
        var src = source.Read();
        if (src.Species == 0)
            return;
        var dst = target.Read();
        var crossStore = source.Store != target.Store;

        if (crossStore)
        {
            // Clone first: conversion returns the same instance when the type already matches, and
            // AdaptToSaveFile mutates it — the origin store must not see that.
            if (target.Store.TryAccept(src.Clone(), out var refused) is not { } accepted)
            {
                StatusMessage = refused;
                return;
            }

            src = accepted;
        }

        if (dst.Species != 0 && crossStore)
        {
            // Swapping both ways: make sure the return trip is possible before writing anything.
            if (source.Store.TryAccept(dst.Clone(), out var refused) is not { } accepted)
            {
                StatusMessage = refused;
                return;
            }

            dst = accepted;
        }

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
            source.Write(source.Store.Blank);
        }

        MarkEdited(source.Store, target.Store);
        if (source.IsParty || target.IsParty)
            RefreshParty();
        NotifySlotActionStates();
        TrackBankChanges();
        StatusMessage = dst.Species != 0 ? "Slots swapped." : "Pokémon moved.";
    }

    /// <summary>
    ///     Dry-run for dropping a whole selection onto one slot: every entity is converted for the
    ///     destination and assigned a free slot, walking forward from the drop target and wrapping around
    ///     to the first container. Returns null with a reason when the batch does not fit or cannot be
    ///     taken — nothing is written either way.
    /// </summary>
    public MultiMovePlan? TryPlanMultiMove(
        IReadOnlyList<SlotViewModel> sources, SlotViewModel target, out string error)
    {
        error = string.Empty;
        var store = target.Store;

        var sourceKeys = new HashSet<SlotKey>();
        var batch = new List<SlotViewModel>(sources.Count);
        foreach (var source in sources)
            if (!source.IsEmpty && sourceKeys.Add(source.Key))
                batch.Add(source);
        if (batch.Count == 0)
            return null;

        // Convert first: a single rejected entity cancels the whole batch, so this must run before
        // anything is placed. Clone, because conversion mutates the entity in place.
        var entities = new List<PKM>(batch.Count);
        var refusals = new List<string>();
        foreach (var source in batch)
        {
            var pk = source.Read();
            if (source.Store == store)
            {
                entities.Add(pk);
                continue;
            }

            if (store.TryAccept(pk.Clone(), out var refused) is { } accepted)
                entities.Add(accepted);
            else
                refusals.Add($"{GameInfo.GetStrings(Config.Language).Species[pk.Species]}: {refused}");
        }

        if (refusals.Count > 0)
        {
            error = $"{refusals.Count} of {batch.Count} Pokémon cannot be added to the loaded save:\n\n"
                    + string.Join('\n', refusals.Distinct().Take(MaxReportedRefusals));
            return null;
        }

        // Slots the batch is vacating are free for it to reuse.
        var slotsPer = store.SlotsPerContainer;
        var total = store.ContainerCount * slotsPer;
        var start = (target.Container * slotsPer) + target.Index;

        var placements = new List<MultiMovePlacement>(entities.Count);
        var next = 0;
        for (var step = 0; step < total && next < entities.Count; step++)
        {
            var position = (start + step) % total;
            var container = position / slotsPer;
            var index = position % slotsPer;
            if (!sourceKeys.Contains(new SlotKey(store, container, index)) &&
                store.Read(container, index).Species != 0)
                continue;
            placements.Add(new MultiMovePlacement(container, index, entities[next++]));
        }

        if (next < entities.Count)
        {
            error = $"Not enough free space for these {entities.Count} Pokémon: "
                    + $"only {placements.Count} slot(s) are free from the drop point onwards.";
            return null;
        }

        return new MultiMovePlan(store, [.. sourceKeys], placements);
    }

    /// <summary>Carries out a plan from <see cref="TryPlanMultiMove" />.</summary>
    public void ApplyMultiMove(MultiMovePlan plan)
    {
        if (SAV is not { } sav)
            return;

        // The entities were read while planning, so the sources can be emptied first even when the
        // source and destination sets overlap.
        foreach (var key in plan.Sources)
            key.Store.Write(key.Container, key.Index, key.Store.Blank);

        foreach (var (container, index, pk) in plan.Placements)
        {
            if (plan.Store.IsParty)
                pk.ResetPartyStats();
            pk.RefreshChecksum();
            plan.Store.Write(container, index, pk);
        }

        MarkEdited([plan.Store, .. plan.Sources.Select(static s => s.Store)]);
        RefreshAllSlots();
        NotifySlotActionStates();
        TrackBankChanges();
        StatusMessage = $"Moved {plan.Placements.Count} Pokémon.";
    }

    private void RefreshAllSlots()
    {
        foreach (var slot in DisplayedSlots())
            slot.Refresh();
    }

    // ----- Bank transfers ---------------------------------------------------

    public bool CanSendToBank(SlotViewModel slot)
        => !slot.IsEmpty && Bank.CurrentStore is { } bank && slot.Store != bank;

    public bool CanSendToSave(SlotViewModel slot)
        => !slot.IsEmpty && slot.IsCompatible && Bank.CurrentStore == slot.Store && _boxStore is not null;

    /// <summary>Sends a slot to the first free bank slot; the entity leaves the save.</summary>
    public void SendToBank(SlotViewModel slot)
    {
        if (Bank is { CurrentStore: { } bank, CurrentBox: { } bankBox } && FindFreeSlot(bank, bankBox.Slots) is { } target)
            MoveOrSwapSlots(slot, target);
        else
            StatusMessage = "No free slot in this bank box.";
    }

    /// <summary>Sends a bank slot to the first free box slot of the loaded save.</summary>
    public void SendToSave(SlotViewModel slot)
    {
        if (FindFreeSlot(_boxStore, SaveBoxSlots) is { } target)
            MoveOrSwapSlots(slot, target);
        else
            StatusMessage = "No free slot in this box.";
    }

    private static SlotViewModel? FindFreeSlot(ISlotStore? store, IEnumerable<SlotViewModel> candidates)
        => store is null ? null : candidates.FirstOrDefault(s => s.Store == store && s.IsEmpty);

    /// <summary>Takes a snapshot of the slot; the source may be edited or deleted afterwards.</summary>
    public void CopySlot(SlotViewModel slot)
    {
        if (slot.IsEmpty)
            return;
        _clipboard = slot.Read().Clone();
        NotifySlotActionStates();
        StatusMessage = "Pokémon copied.";
    }

    /// <summary>
    ///     Writes the copied Pokémon into the slot. Crossing stores converts on the way, exactly as a
    ///     drag between them would; returns false with a reason when the target refuses it.
    /// </summary>
    public bool PasteToSlot(SlotViewModel slot)
    {
        if (_clipboard is null || SAV is null)
            return false;
        var pk = slot.Store.TryAccept(_clipboard.Clone(), out var message);
        if (pk is null)
        {
            StatusMessage = message;
            return false;
        }

        WriteEntityToSlot(slot, pk);
        StatusMessage = "Pokémon pasted.";
        return true;
    }

    /// <summary>Writes an entity the editor produced into the given slot.</summary>
    public void WriteEntityToSlot(SlotViewModel slot, PKM pk)
    {
        if (SAV is null)
            return;
        if (slot.IsParty)
            pk.ResetPartyStats();
        pk.RefreshChecksum();
        slot.Write(pk);
        MarkEdited(slot.Store);
        if (slot.IsParty)
            RefreshParty();
        NotifySlotActionStates();
        TrackBankChanges();
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

        ClearSelection();

        PartySlots.Clear();

        _boxStore = sav.HasBox ? SaveBoxStore.Create(sav) : null;
        _partyStore = sav.HasParty ? new SavePartyStore(sav) : null;

        SaveBox = _boxStore is { } boxes
            ? new SaveBoxPanelViewModel(boxes, Math.Clamp(sav.CurrentBox, 0, sav.BoxCount - 1))
            : null;

        if (_partyStore is { } party)
            for (var i = 0; i < party.SlotsPerContainer; i++)
                PartySlots.Add(new SlotViewModel(party, 0, i));

        // Pending bank changes belong to the save they were made against; a leftover diff would be
        // replayed against the wrong one.
        Bank.OnSaveChanged(sav, Config.Language);

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

    /// <summary>
    ///     Marks the save as edited only when one of the touched stores belongs to it — rearranging a bank
    ///     leaves the save file itself untouched.
    /// </summary>
    private void MarkEdited(params ISlotStore[] stores)
    {
        if (SAV is { } sav && stores.Any(static s => s.Scope == SlotScope.Save))
            sav.State.Edited = true;
    }

    /// <summary>
    ///     The bank box on screen was replaced (another bank picked, or the bank vanished from disk): bank
    ///     slots still selected point at a box that is gone. Pending changes can also be dropped from under
    ///     us when a bank folder disappears.
    /// </summary>
    private void OnBankPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BankViewModel.CurrentBox):
                if (_selectedSlots.Exists(static s => s.Store.Scope == SlotScope.Bank))
                {
                    ClearSelection();
                    NotifySlotActionStates();
                }

                break;
            case nameof(BankViewModel.IsDirty):
                NotifyPendingChanges();
                break;
        }
    }

    /// <summary>
    ///     Mirrors bank changes to their pending files after every edit, so working across several banks
    ///     before saving does not lose the ones not currently on screen.
    /// </summary>
    private void TrackBankChanges()
    {
        if (Bank.IsDirty)
            Bank.SavePending(_savePath ?? string.Empty);
        NotifyPendingChanges();
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
        foreach (var slot in SaveBoxSlots)
            if (!slot.IsEmpty)
            {
                SelectSlot(slot);
                return;
            }

        foreach (var slot in PartySlots)
            if (!slot.IsEmpty)
            {
                SelectSlot(slot);
                return;
            }

        if (SaveBoxSlots.Count > 0)
            SelectSlot(SaveBoxSlots[0]);
        else if (PartySlots.Count > 0)
            SelectSlot(PartySlots[0]);
    }
}
