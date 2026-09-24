using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKBank.Core.Configuration;
using PKBank.Desktop.Services.Banks;
using PKBank.Desktop.Services.Banks.Types;
using PKBank.Desktop.Services.Slots;
using PKHeX.Core;
using PKBank.Desktop.Components;

namespace PKBank.Desktop.Components.Bank.ViewModels;

/// <summary>
/// The banks and the box currently on screen. There is always at least one bank.
/// Two kinds of change meet here. Changes made on disk (a folder created, renamed or deleted by hand)
/// are followed as they happen. Changes made in the app (a bank created, renamed or deleted, entities
/// moved) are pending, like any save edit: they reach the folders only when the save is written, and are
/// dropped when another save is opened. Meanwhile they are mirrored in pending files next to the banks.
/// </summary>
public sealed class BankViewModel : ViewModelBase
{
    private readonly AppConfigService _config;

    /// <summary>Every known bank, those deleted in the app but not saved yet included.</summary>
    private readonly List<BankEntryViewModel> _entries = [];

    private readonly BankFolderWatcher _watcher = new();
    private BankBoxPanelViewModel? _currentBox;
    private bool _isLoading;
    private string _language = GameLanguage.DefaultLanguage;
    private BankLibrary _library;
    private CancellationTokenSource? _load;
    private int _openVersion;
    private SaveFile? _sav;
    private BankEntryViewModel? _selectedBank;

    public BankViewModel(AppConfigService config)
    {
        _config = config;
        _library = new BankLibrary(BankLibrary.ResolveRoot(config.BanksPath));
        _watcher.Changed += (_, _) => Reconcile();
    }

    /// <summary>Messages for the status bar: a bank vanished from disk, a file operation failed…</summary>
    public event Action<string>? StatusRaised;

    /// <summary>The banks shown in the picker, sorted by name: deleted ones are left out.</summary>
    public ObservableCollection<BankEntryViewModel> Banks { get; } = [];

    /// <summary>The banks folder actually in use: the configured one, or the default when that is unusable.</summary>
    public string Root => _library.Root;

    public BankEntryViewModel? SelectedBank
    {
        get => _selectedBank;
        set
        {
            // The picker pushes null while its items are being rearranged; a bank is always selected.
            if (value is null)
                return;
            if (SetField(ref _selectedBank, value))
                _ = OpenSelectedAsync();
        }
    }

    public BankBoxPanelViewModel? CurrentBox
    {
        get => _currentBox;
        private set => SetField(ref _currentBox, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    /// <summary>The last bank cannot be deleted.</summary>
    public bool CanDelete => Banks.Count > 1;

    /// <summary>The store behind the box on screen, used to tell bank slots from save slots.</summary>
    public ISlotStore? CurrentStore => CurrentBox?.Store;

    public bool IsDirty => _entries.Exists(static e => e.HasPendingChange || e.Session is { IsDirty: true });

    /// <summary>Called once at startup: makes sure a bank exists and starts following the folder.</summary>
    public void Initialize()
    {
        Reconcile();
        _watcher.Watch(Root);
    }

    // ----- Bank management (pending until the save is written) -----------------

    /// <summary>Why <paramref name="name" /> cannot be used for a new bank, or to rename the selected one.</summary>
    public string? ValidateName(string name, bool forRename)
    {
        if (BankLibrary.ValidateFormat(name) is { } error)
            return error;
        var self = forRename ? SelectedBank : null;
        return Banks.Any(b => b != self && BankLibrary.NameComparer.Equals(b.Name, name))
            ? "A bank with this name already exists."
            : null;
    }

    public void CreateBank(string name)
    {
        var entry = BankEntryViewModel.CreateNew(name);
        _entries.Add(entry);
        RefreshList();
        SelectedBank = entry;
        OnPendingChanged();
        StatusRaised?.Invoke($"Bank “{name}” will be created on save.");
    }

    public void RenameSelected(string name)
    {
        if (SelectedBank is not { } entry || entry.Name == name)
            return;
        var previous = entry.Name;
        entry.Name = name;
        RefreshList();
        OnPendingChanged();
        StatusRaised?.Invoke(entry.IsNew || entry.IsRenamed
            ? $"Bank “{previous}” will be renamed to “{name}” on save."
            : $"Bank “{name}” keeps its name.");
    }

    /// <summary>Deletes the selected bank once the save is written, and moves on to a neighbouring one.</summary>
    public void DeleteSelected()
    {
        if (!CanDelete || SelectedBank is not { } entry)
            return;

        var index = Banks.IndexOf(entry);
        SelectedBank = Banks[index + 1 < Banks.Count ? index + 1 : index - 1];

        // Whatever was pending inside the bank goes with it.
        var wasNew = entry.IsNew;
        if (entry.Folder is { } folder)
            BankStorage.DiscardPending(folder);
        entry.Session = null;
        if (wasNew)
            _entries.Remove(entry);
        else
            entry.IsDeleted = true;

        RefreshList();
        OnPendingChanged();
        StatusRaised?.Invoke(wasNew
            ? $"Bank “{entry.Name}” discarded."
            : $"Bank “{entry.Name}” will be deleted on save.");
    }

    // ----- Banks folder --------------------------------------------------------

    /// <summary>
    ///     Switches to another banks folder. Moving takes the banks along, pending changes included; not
    ///     moving leaves them where they are and drops every pending bank change.
    /// </summary>
    /// <returns>An error message when the move went wrong part-way, otherwise null.</returns>
    public string? ChangeRoot(string configured, bool move)
    {
        var newRoot = BankLibrary.ResolveRoot(configured);
        var stored = BankLibrary.IsSamePath(newRoot, BankLibrary.DefaultRoot) ? string.Empty : newRoot;
        if (BankLibrary.IsSamePath(newRoot, Root))
        {
            _config.SetBanksPath(stored);
            return null;
        }

        string? error = null;
        if (move)
        {
            _library.DiscardPending(); // rewritten in the new folder below
            var moved = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                BankLibrary.MoveAll(Root, newRoot, moved);
            }
            catch (Exception ex)
            {
                error = $"Some banks could not be moved: {ex.Message}";
            }

            foreach (var (from, to) in moved)
                Relocate(from, to);
        }
        else
        {
            DiscardAllPending();
            CurrentBox = null;
        }

        _config.SetBanksPath(stored);
        _library = new BankLibrary(newRoot);
        _watcher.Watch(newRoot);
        Reconcile();
        return error;
    }

    /// <summary>
    ///     Brings the list in line with the folder: follows renames, repairs names the app does not allow,
    ///     drops banks whose folder is gone, and falls back to the first bank when the one on screen is gone.
    ///     When no bank is left, the default one is created.
    /// </summary>
    public void Reconcile()
    {
        foreach (var (from, to) in _watcher.TakeRenames())
            Relocate(from, to);

        IReadOnlyList<string> folders;
        try
        {
            folders = _library.Scan(out var repaired);
            foreach (var (from, to) in repaired)
                Relocate(from, to);
        }
        catch (Exception ex)
        {
            StatusRaised?.Invoke($"Banks folder unavailable: {ex.Message}");
            return;
        }

        var present = new HashSet<string>(folders, StringComparer.Ordinal);
        foreach (var gone in _entries.Where(e => e.Folder is { } f && !present.Contains(f)).ToList())
        {
            if (!gone.IsDeleted && gone.Session is { IsDirty: true })
                StatusRaised?.Invoke($"Bank “{gone.Name}” was removed; its unsaved changes are lost.");
            _entries.Remove(gone);
        }

        var known = new HashSet<string>(_entries.Select(static e => e.Folder).OfType<string>(), StringComparer.Ordinal);
        foreach (var folder in folders.Where(f => !known.Contains(f)))
            _entries.Add(new BankEntryViewModel(folder));

        EnsureOneBank();
        RefreshList(true);
        OnPendingChanged();
    }

    // ----- Save lifecycle -----------------------------------------------------

    /// <summary>
    ///     A save was loaded, closed or reloaded. Pending changes belong to the save they were made
    ///     against, so switching save drops them; a reload of the same save (a language change) keeps
    ///     them and only re-evaluates compatibility.
    /// </summary>
    public void OnSaveChanged(SaveFile? sav, string language)
    {
        if (sav is null || !ReferenceEquals(sav, _sav))
            DiscardAllPending();

        _sav = sav;
        _language = language;
        CurrentBox = null;

        // Reopen whatever was on screen against the new save.
        if (_selectedBank is null && Banks.Count > 0)
        {
            _selectedBank = Banks[0];
            OnPropertyChanged(nameof(SelectedBank));
        }

        _ = OpenSelectedAsync();
    }

    /// <summary>
    ///     Drops every pending change: bank creations, renames and deletions, and the entities moved in or
    ///     out. Pending files are deleted too, including those of banks never opened: a diff left behind by
    ///     a crash must not be replayed against a different save.
    /// </summary>
    public void DiscardAllPending()
    {
        foreach (var entry in _entries)
        {
            if (entry.Folder is { } folder)
            {
                BankStorage.DiscardPending(folder);
                entry.Name = BankLibrary.NameOf(folder);
            }

            entry.IsDeleted = false;
            entry.Session = null;
        }

        _entries.RemoveAll(static e => e.IsNew);
        _library.DiscardPending();
        RefreshList();
        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>
    ///     Applies every pending change to disk: deletions, renames and creations first, then the content
    ///     of each bank. Called after the save file itself was written, so it never throws.
    /// </summary>
    public string CommitPending()
    {
        var messages = new List<string>();

        foreach (var entry in _entries.Where(static e => e.IsDeleted).ToList())
            try
            {
                BankLibrary.Delete(entry.Folder!);
                _entries.Remove(entry);
                messages.Add($"Bank “{entry.Name}” deleted.");
            }
            catch (Exception ex)
            {
                messages.Add($"Bank “{entry.Name}” could not be deleted: {ex.Message}");
            }

        var renames = _entries.Where(static e => e.IsRenamed && !e.IsDeleted).ToList();
        if (renames.Count > 0)
        {
            var errors = new List<string>();
            var done = _library.RenameAll(renames.Select(static e => (e.Folder!, e.Name)).ToList(), errors);
            foreach (var (from, to) in done)
                foreach (var entry in renames.Where(e => e.Folder == from))
                {
                    entry.Folder = to;
                    entry.Name = BankLibrary.NameOf(to); // a suffix may have been added
                    entry.Session?.Relocate(to);
                    messages.Add($"Bank renamed to “{entry.Name}”.");
                }

            messages.AddRange(errors);
        }

        foreach (var entry in _entries.Where(static e => e.IsNew).ToList())
            try
            {
                var folder = _library.Create(entry.Name);
                entry.Folder = folder;
                entry.Name = BankLibrary.NameOf(folder);
                entry.Session?.Relocate(folder);
                messages.Add($"Bank “{entry.Name}” created.");
            }
            catch (Exception ex)
            {
                messages.Add($"Bank “{entry.Name}” could not be created: {ex.Message}");
            }

        // Content last, into folders that now exist under their final names.
        foreach (var entry in _entries.Where(static e => !e.IsNew && e.Session is { IsDirty: true }))
            messages.Add(entry.Session!.Commit().Trim());
        CurrentBox?.RefreshContainerNames(); // empty trailing boxes were dropped

        RefreshList();
        OnPendingChanged();
        return string.Concat(messages.Where(static m => m.Length != 0).Select(static m => " " + m));
    }

    /// <summary>Mirrors the pending changes next to the banks, so a second bank can be edited meanwhile.</summary>
    public void SavePending(string savePath)
    {
        foreach (var entry in _entries.Where(static e => !e.IsNew && !e.IsDeleted && e.Session is { IsDirty: true }))
            BankStorage.SavePending(entry.Folder!, entry.Session!.BuildPending(savePath));
        OnPendingChanged();
    }

    /// <summary>Called by the box panel when the arrows or the picker moved to another box.</summary>
    public void OnBoxChanged(int box) => _ = LoadBoxAsync(box);

    // ----- Internals ----------------------------------------------------------

    /// <summary>
    ///     A bank folder moved on disk: its entry and open session follow it. A name changed in the app and
    ///     not saved yet is kept; otherwise the name follows the folder.
    /// </summary>
    private void Relocate(string from, string to)
    {
        foreach (var entry in _entries.Where(e => e.Folder == from))
        {
            var followName = !entry.IsRenamed;
            entry.Folder = to;
            if (followName)
                entry.Name = BankLibrary.NameOf(to);
            entry.Session?.Relocate(to);
        }
    }

    /// <summary>
    ///     There is always a bank. When none is left on screen, a deletion made in the app is cancelled
    ///     rather than a new folder created; with nothing at all, the default bank is created on disk.
    /// </summary>
    private void EnsureOneBank()
    {
        if (_entries.Exists(static e => !e.IsDeleted))
            return;

        if (_entries.FirstOrDefault(static e => e.IsDeleted) is { } revived)
        {
            revived.IsDeleted = false;
            return;
        }

        try
        {
            _entries.Add(new BankEntryViewModel(_library.Create(BankLibrary.DefaultBankName)));
        }
        catch (Exception ex)
        {
            StatusRaised?.Invoke($"The default bank could not be created: {ex.Message}");
        }
    }

    /// <summary>
    ///     Rebuilds the picker from the entries and keeps a bank selected. <paramref name="fromDisk" />:
    ///     the change came from the folder, so losing the bank on screen is worth a word.
    /// </summary>
    private void RefreshList(bool fromDisk = false)
    {
        var visible = _entries.Where(static e => !e.IsDeleted)
            .OrderBy(static e => e.Name, BankLibrary.NameComparer)
            .ToList();
        SyncEntries(visible);

        var selected = SelectedBank;
        if (Banks.Count > 0 && (selected is null || !Banks.Contains(selected)))
        {
            if (selected is not null && fromDisk)
                StatusRaised?.Invoke($"Bank “{selected.Name}” is no longer on disk; showing “{Banks[0].Name}”.");
            SelectedBank = Banks[0];
        }
        else
        {
            OnPropertyChanged(nameof(SelectedBank)); // the picker may have dropped it while items moved
        }

        OnPropertyChanged(nameof(CanDelete));
    }

    /// <summary>Records the pending bank changes on disk and tells the save they exist.</summary>
    private void OnPendingChanged()
    {
        if (_entries.Exists(static e => e.HasPendingChange))
            _library.SavePending(new BankLibraryPending
            {
                Created = [.. _entries.Where(static e => e.IsNew).Select(static e => e.Name)],
                Renamed = [.. _entries.Where(static e => e.IsRenamed && !e.IsDeleted).Select(static e => new BankLibraryRename
                {
                    Folder = BankLibrary.NameOf(e.Folder!),
                    Name = e.Name
                })],
                Deleted = [.. _entries.Where(static e => e.IsDeleted).Select(static e => BankLibrary.NameOf(e.Folder!))]
            });
        else
            _library.DiscardPending();

        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>
    ///     Updates the list in place, keeping the entry instances, so the picker keeps its selection
    ///     instead of being reset.
    /// </summary>
    private void SyncEntries(IReadOnlyList<BankEntryViewModel> wanted)
    {
        var keep = new HashSet<BankEntryViewModel>(wanted);
        for (var i = Banks.Count - 1; i >= 0; i--)
            if (!keep.Contains(Banks[i]))
                Banks.RemoveAt(i);

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i < Banks.Count && Banks[i] == wanted[i])
                continue;

            var existing = Banks.IndexOf(wanted[i]);
            if (existing >= 0)
                Banks.Move(existing, i);
            else
                Banks.Insert(i, wanted[i]);
        }
    }

    private async Task OpenSelectedAsync()
    {
        var version = ++_openVersion;
        CurrentBox = null;
        if (_sav is not { } sav || SelectedBank is not { } bank)
            return;

        if (bank.Session is not { } session)
        {
            if (bank.Folder is not { } folder)
            {
                session = BankSession.CreateNew(Path.Combine(Root, bank.Name));
            }
            else
            {
                try
                {
                    session = await Task.Run(() => BankSession.Open(folder));
                }
                catch (Exception ex)
                {
                    if (version == _openVersion)
                        StatusRaised?.Invoke($"Bank “{bank.Name}” could not be opened: {ex.Message}");
                    return;
                }

                if (version != _openVersion || !_entries.Contains(bank) || bank.IsDeleted)
                    return; // superseded by another bank or save, or gone meanwhile
                if (bank.Folder is { } moved && moved != folder)
                    session.Relocate(moved); // renamed on disk while it was being read
            }

            bank.Session = session;
        }

        CurrentBox = new BankBoxPanelViewModel(this, new BankSlotStore(session, sav, _language), 0);
        await LoadBoxAsync(0);
    }

    /// <summary>
    ///     Decodes one box off the UI thread, cancelling whatever box was loading before, then refreshes
    ///     the slots. Until it lands the box shows as empty.
    /// </summary>
    private async Task LoadBoxAsync(int box)
    {
        if (CurrentBox is not { Store: BankSlotStore store } panel)
            return;

        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _load, cts)?.Cancel();
        IsLoading = true;
        try
        {
            await Task.Run(() => store.Session.LoadBox(box, cts.Token), cts.Token);
            if (!cts.Token.IsCancellationRequested)
                panel.RefreshSlots();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer box.
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _load, null, cts) == cts)
                IsLoading = false;
            cts.Dispose();
        }
    }
}
