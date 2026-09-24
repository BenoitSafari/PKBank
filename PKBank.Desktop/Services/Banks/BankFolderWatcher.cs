using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Threading;

namespace PKBank.Desktop.Services.Banks;

/// <summary>
///     Watches the banks folder for sub-folders appearing, disappearing or being renamed outside the app.
///     Bursts of events are coalesced into one <see cref="Changed" /> on the UI thread; renames are kept
///     so an open bank can follow its folder instead of being dropped.
/// </summary>
public sealed class BankFolderWatcher : IDisposable
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    private readonly object _gate = new();
    private readonly List<(string OldPath, string NewPath)> _renames = [];
    private readonly DispatcherTimer _timer;
    private FileSystemWatcher? _watcher;

    public BankFolderWatcher()
    {
        _timer = new DispatcherTimer { Interval = Debounce };
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Changed?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <summary>Raised on the UI thread once the folder has settled.</summary>
    public event EventHandler? Changed;

    /// <summary>Starts watching <paramref name="root" />, replacing whatever was watched before.</summary>
    public void Watch(string root)
    {
        Stop();
        try
        {
            _watcher = new FileSystemWatcher(root)
            {
                NotifyFilter = NotifyFilters.DirectoryName,
                IncludeSubdirectories = false
            };
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnChanged;
            _watcher.EnableRaisingEvents = true;
        }
        catch
        {
            // Not watchable (network share, inotify limit): changes are still picked up on the next scan.
            Stop();
        }
    }

    /// <summary>Folder renames seen since the last call, oldest first.</summary>
    public IReadOnlyList<(string OldPath, string NewPath)> TakeRenames()
    {
        lock (_gate)
        {
            var renames = _renames.ToArray();
            _renames.Clear();
            return renames;
        }
    }

    public void Dispose() => Stop();

    private void Stop()
    {
        if (_watcher is null)
            return;
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        lock (_gate)
            _renames.Add((e.OldFullPath, e.FullPath));
        Schedule();
    }

    private void OnChanged(object sender, EventArgs e) => Schedule();

    /// <summary>Restarts the debounce timer; watcher events arrive on a thread-pool thread.</summary>
    private void Schedule() => Dispatcher.UIThread.Post(() =>
    {
        _timer.Stop();
        _timer.Start();
    });
}
