using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKBank.Core.Configuration;
using PKBank.Desktop.Services.SaveFiles;
using PKBank.Desktop.Services.SaveFiles.Types;
using PKBank.Desktop.Components;

namespace PKBank.Desktop.Components.SaveSelection.ViewModels;

public sealed class SaveSelectionViewModel(AppConfigService config) : ViewModelBase
{
    private bool _isScanning;
    private CancellationTokenSource? _scan;
    private SaveEntryViewModel? _selectedSave;

    public ObservableCollection<SaveEntryViewModel> Saves { get; } = [];

    public SaveEntryViewModel? SelectedSave
    {
        get => _selectedSave;
        set
        {
            if (SetField(ref _selectedSave, value))
                OnPropertyChanged(nameof(CanLoadSelected));
        }
    }

    public bool CanLoadSelected => SelectedSave is not null;

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetField(ref _isScanning, value))
                OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool HasSaves => Saves.Count > 0;

    /// <summary>The scan finished and turned up nothing: the empty state takes over the screen.</summary>
    public bool IsEmpty => !IsScanning && !HasSaves;

    /// <summary>Rescans the configured folders; a scan already running is abandoned.</summary>
    public async Task RefreshAsync()
    {
        var cts = new CancellationTokenSource();
        Interlocked.Exchange(ref _scan, cts)?.Cancel();

        var token = cts.Token;
        var folders = config.SavPaths.ToArray();
        IsScanning = true;
        try
        {
            var found = await Task.Run(() => SaveFileDiscovery.Scan(folders, token), token);
            if (!token.IsCancellationRequested)
                Apply(found);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer scan; that one owns the list now.
        }
        finally
        {
            if (Interlocked.CompareExchange(ref _scan, null, cts) == cts)
                IsScanning = false;
            cts.Dispose();
        }
    }

    private void Apply(IReadOnlyList<SaveFileSummary> found)
    {
        // Keep the selection across a rescan when the same file is still listed.
        var selectedPath = SelectedSave?.Path;

        Saves.Clear();
        foreach (var summary in found)
            Saves.Add(new SaveEntryViewModel(summary));

        SelectedSave = selectedPath is null ? null : Saves.FirstOrDefault(s => s.Path == selectedPath);

        OnPropertyChanged(nameof(HasSaves));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
