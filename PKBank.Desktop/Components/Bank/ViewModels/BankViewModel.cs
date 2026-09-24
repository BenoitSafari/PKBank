using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKBank.Core.Configuration;
using PKBank.Desktop.Services.Banks;
using PKBank.Desktop.Services.Slots;
using PKHeX.Core;
using PKBank.Desktop.Components;

namespace PKBank.Desktop.Components.Bank.ViewModels;

/// <summary>
/// The banks of the banks folder and the box currently on screen. There is always at least one bank, and
/// the list follows the folder: sub-folders created, renamed or deleted outside the app show up here.
/// Sessions are kept per folder so switching bank in the picker never loses -- or silently commits --
/// what is pending in another one.
/// </summary>
public sealed class BankViewModel : ViewModelBase
{
    private readonly AppConfigService _config;
    private readonly Dictionary<string, BankSession> _sessions = new(StringComparer.Ordinal);
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

    public bool IsDirty => _sessions.Values.Any(static s => s.IsDirty);

    /// <summary>Called once at startup: makes sure a bank exists and starts following the folder.</summary>
    public void Initialize()
    {
        Reconcile();
        _watcher.Watch(Root);
    }

    // ----- Bank management ----------------------------------------------------

    /// <summary>Why <paramref name="name" /> cannot be used for a new bank, or to rename the selected one.</summary>
    public string? ValidateName(string name, bool forRename) =>
        _library.ValidateName(name, forRename ? SelectedBank?.Folder : null);

    public bool CreateBank(string name)
    {
        try
        {
            var folder = _library.Create(name);
            Reconcile();
            SelectedBank = Banks.FirstOrDefault(b => b.Folder == folder) ?? SelectedBank;
            StatusRaised?.Invoke($"Bank “{name}” created.");
            return true;
        }
        catch (Exception ex)
        {
            StatusRaised?.Invoke($"Could not create bank “{name}”: {ex.Message}");
            return false;
        }
    }

    public bool RenameSelected(string name)
    {
        if (SelectedBank is not { } entry)
            return false;

        var previous = entry.Name;
        try
        {
            var oldFolder = entry.Folder;
            Relocate(oldFolder, _library.Rename(oldFolder, name));
            Reconcile();
            StatusRaised?.Invoke($"Bank “{previous}” renamed to “{name}”.");
            return true;
        }
        catch (Exception ex)
        {
            StatusRaised?.Invoke($"Could not rename bank “{previous}”: {ex.Message}");
            return false;
        }
    }

    /// <summary>Deletes the selected bank, files included, and moves on to a neighbouring one.</summary>
    public bool DeleteSelected()
    {
        if (!CanDelete || SelectedBank is not { } entry)
            return false;

        var index = Banks.IndexOf(entry);
        SelectedBank = Banks[index + 1 < Banks.Count ? index + 1 : index - 1];
        try
        {
            _library.Delete(entry.Folder);
            _sessions.Remove(entry.Folder);
            OnPropertyChanged(nameof(IsDirty));
            Reconcile();
            StatusRaised?.Invoke($"Bank “{entry.Name}” deleted.");
            return true;
        }
        catch (Exception ex)
        {
            Reconcile(); // part of it may be gone already
            StatusRaised?.Invoke($"Could not delete bank “{entry.Name}”: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Switches to another banks folder. Moving takes the banks along, pending changes included; not
    ///     moving leaves them where they are and drops what is pending in them.
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
            foreach (var folder in _sessions.Keys)
                BankStorage.DiscardPending(folder);
            _sessions.Clear();
            CurrentBox = null;
            OnPropertyChanged(nameof(IsDirty));
        }

        _config.SetBanksPath(stored);
        _library = new BankLibrary(newRoot);
        _watcher.Watch(newRoot);
        Reconcile();
        return error;
    }

    /// <summary>
    ///     Brings the list in line with the folder: follows renames, repairs names the app does not allow,
    ///     recreates the default bank when none is left, and falls back to the first bank when the one on
    ///     screen is gone.
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
        foreach (var gone in _sessions.Keys.Where(f => !present.Contains(f)).ToList())
        {
            if (_sessions[gone].IsDirty)
                StatusRaised?.Invoke($"Bank “{BankLibrary.NameOf(gone)}” was removed; its unsaved changes are lost.");
            _sessions.Remove(gone);
        }

        SyncEntries(folders);

        var selected = SelectedBank;
        if (selected is null || !Banks.Contains(selected))
        {
            if (selected is not null)
                StatusRaised?.Invoke($"Bank “{selected.Name}” is no longer on disk; showing “{Banks[0].Name}”.");
            SelectedBank = Banks[0];
        }
        else
        {
            OnPropertyChanged(nameof(SelectedBank)); // the picker may have dropped it while items moved
        }

        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(IsDirty));
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
        {
            DiscardAllPending();
            _sessions.Clear();
        }

        _sav = sav;
        _language = language;
        CurrentBox = null;
        OnPropertyChanged(nameof(IsDirty));

        // Reopen whatever was on screen against the new save.
        if (_selectedBank is null && Banks.Count > 0)
        {
            _selectedBank = Banks[0];
            OnPropertyChanged(nameof(SelectedBank));
        }

        _ = OpenSelectedAsync();
    }

    /// <summary>
    ///     Deletes every pending file, including banks this session never opened: a diff left behind by a
    ///     crash must not be replayed against a different save.
    /// </summary>
    public void DiscardAllPending()
    {
        foreach (var entry in Banks)
            BankStorage.DiscardPending(entry.Folder);
    }

    /// <summary>Writes out every dirty bank. Called after the save file itself was written.</summary>
    public string CommitPending()
    {
        var messages = _sessions.Values.Where(static s => s.IsDirty).Select(static s => s.Commit()).ToList();
        OnPropertyChanged(nameof(IsDirty));
        return string.Concat(messages);
    }

    /// <summary>Mirrors the pending changes next to the bank, so a second bank can be edited meanwhile.</summary>
    public void SavePending(string savePath)
    {
        foreach (var session in _sessions.Values.Where(static s => s.IsDirty))
            BankStorage.SavePending(session.Folder, session.BuildPending(savePath));
        OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>Called by the box panel when the arrows or the picker moved to another box.</summary>
    public void OnBoxChanged(int box) => _ = LoadBoxAsync(box);

    // ----- Internals ----------------------------------------------------------

    /// <summary>A bank folder moved: its entry and its open session follow it.</summary>
    private void Relocate(string from, string to)
    {
        if (_sessions.Remove(from, out var session))
        {
            session.Relocate(to);
            _sessions[to] = session;
        }

        foreach (var entry in Banks.Where(b => b.Folder == from))
            entry.Folder = to;
    }

    /// <summary>
    ///     Updates the list in place, keeping the entry instances, so the picker keeps its selection
    ///     instead of being reset.
    /// </summary>
    private void SyncEntries(IReadOnlyList<string> folders)
    {
        var wanted = new HashSet<string>(folders, StringComparer.Ordinal);
        for (var i = Banks.Count - 1; i >= 0; i--)
            if (!wanted.Contains(Banks[i].Folder))
                Banks.RemoveAt(i);

        for (var i = 0; i < folders.Count; i++)
        {
            if (i < Banks.Count && Banks[i].Folder == folders[i])
                continue;

            var existing = -1;
            for (var j = i + 1; j < Banks.Count; j++)
                if (Banks[j].Folder == folders[i])
                {
                    existing = j;
                    break;
                }

            if (existing >= 0)
                Banks.Move(existing, i);
            else
                Banks.Insert(i, new BankEntryViewModel(folders[i]));
        }
    }

    private async Task OpenSelectedAsync()
    {
        var version = ++_openVersion;
        CurrentBox = null;
        if (_sav is not { } sav || SelectedBank is not { } bank)
            return;

        if (!_sessions.TryGetValue(bank.Folder, out var session))
        {
            var folder = bank.Folder;
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

            if (version != _openVersion)
                return; // superseded by another bank or save
            if (bank.Folder != folder)
                session.Relocate(bank.Folder); // renamed while it was being read
            _sessions[bank.Folder] = session;
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
