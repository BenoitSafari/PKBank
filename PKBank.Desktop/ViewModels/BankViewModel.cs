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

namespace PKBank.Desktop.ViewModels;

/// <summary>
/// The configured banks and the page currently on screen. Sessions are kept per folder so switching bank
/// in the picker never loses -- or silently commits -- what is pending in another one.
/// </summary>
public sealed class BankViewModel(AppConfigService config) : ViewModelBase
{
    private readonly Dictionary<string, BankSession> _sessions = new(StringComparer.Ordinal);
    private BankPagePanelViewModel? _currentPage;
    private bool _isLoading;
    private string _language = GameLanguage.DefaultLanguage;
    private CancellationTokenSource? _load;
    private SaveFile? _sav;
    private BankEntryViewModel? _selectedBank;

    public ObservableCollection<BankEntryViewModel> Banks { get; } = [];

    public BankEntryViewModel? SelectedBank
    {
        get => _selectedBank;
        set
        {
            if (SetField(ref _selectedBank, value))
                _ = OpenSelectedAsync();
        }
    }

    public BankPagePanelViewModel? CurrentPage
    {
        get => _currentPage;
        private set => SetField(ref _currentPage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    /// <summary>The store behind the page on screen, used to tell bank slots from save slots.</summary>
    public ISlotStore? CurrentStore => CurrentPage?.Store;

    public bool IsDirty => _sessions.Values.Any(static s => s.IsDirty);

    public void RefreshBanks()
    {
        var folders = config.BankPaths;
        var known = Banks.ToDictionary(static b => b.Folder, StringComparer.Ordinal);

        Banks.Clear();
        foreach (var folder in folders)
            Banks.Add(known.TryGetValue(folder, out var existing) ? existing : new BankEntryViewModel(folder));

        if (SelectedBank is { } selected && !Banks.Contains(selected))
            SelectedBank = null;
    }

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
        CurrentPage = null;

        var selected = SelectedBank;
        _selectedBank = null;
        OnPropertyChanged(nameof(SelectedBank));
        OnPropertyChanged(nameof(IsDirty));

        // Reopen whatever was on screen against the new save.
        if (sav is not null && selected is not null && Banks.Contains(selected))
            SelectedBank = selected;
    }

    /// <summary>
    ///     Deletes every pending file, including banks this session never opened: a diff left behind by a
    ///     crash must not be replayed against a different save.
    /// </summary>
    public void DiscardAllPending()
    {
        foreach (var folder in config.BankPaths)
            BankStorage.DiscardPending(folder);
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

    /// <summary>Called by the page panel when the arrows or the picker moved to another page.</summary>
    public void OnPageChanged(int page) => _ = LoadPageAsync(page);

    private async Task OpenSelectedAsync()
    {
        CurrentPage = null;
        if (_sav is not { } sav || SelectedBank is not { } bank)
            return;

        if (!_sessions.TryGetValue(bank.Folder, out var session))
        {
            try
            {
                session = await Task.Run(() => BankSession.Open(bank.Folder));
            }
            catch (Exception)
            {
                return; // unreadable folder: the picker simply shows nothing
            }

            _sessions[bank.Folder] = session;
        }

        bank.Name = session.Name;
        CurrentPage = new BankPagePanelViewModel(this, new BankSlotStore(session, sav, _language), 0);
        await LoadPageAsync(0);
    }

    /// <summary>
    ///     Decodes one page off the UI thread, cancelling whatever page was loading before, then refreshes
    ///     the slots. Until it lands the page shows as empty.
    /// </summary>
    private async Task LoadPageAsync(int page)
    {
        if (CurrentPage is not { Store: BankSlotStore store } panel)
            return;

        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _load, cts)?.Cancel();
        IsLoading = true;
        try
        {
            await Task.Run(() => store.Session.LoadPage(page, cts.Token), cts.Token);
            if (!cts.Token.IsCancellationRequested)
                panel.RefreshSlots();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer page.
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _load, null, cts) == cts)
                IsLoading = false;
            cts.Dispose();
        }
    }
}
