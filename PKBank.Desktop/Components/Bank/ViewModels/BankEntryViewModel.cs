using PKBank.Desktop.Services.Banks;

namespace PKBank.Desktop.Components.Bank.ViewModels;

/// <summary>
///     One bank as the app sees it: the folder on disk plus whatever was done to it in the app and not saved
///     yet. A bank created in the app has no folder until the save is written; a rename or a deletion only
///     reaches the folder then.
/// </summary>
public sealed class BankEntryViewModel : ViewModelBase
{
    private string? _folder;
    private bool _isDeleted;
    private string _name;

    public BankEntryViewModel(string folder)
    {
        _folder = folder;
        _name = BankLibrary.NameOf(folder);
    }

    private BankEntryViewModel(string name, bool _)
    {
        _name = name;
    }

    /// <summary>A bank created in the app, not on disk yet.</summary>
    public static BankEntryViewModel CreateNew(string name) => new(name, true);

    /// <summary>The folder on disk; null until a new bank is saved.</summary>
    public string? Folder
    {
        get => _folder;
        set
        {
            if (SetField(ref _folder, value))
                NotifyPendingState();
        }
    }

    /// <summary>The bank name, as it will be once saved.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
                NotifyPendingState();
        }
    }

    /// <summary>Deleted in the app; the folder goes when the save is written.</summary>
    public bool IsDeleted
    {
        get => _isDeleted;
        set
        {
            if (SetField(ref _isDeleted, value))
                NotifyPendingState();
        }
    }

    /// <summary>The open bank, once it has been shown.</summary>
    public BankSession? Session { get; set; }

    public bool IsNew => Folder is null;

    /// <summary>Renamed in the app; exact comparison, so a change of case counts.</summary>
    public bool IsRenamed => Folder is { } folder && Name != BankLibrary.NameOf(folder);

    public bool HasPendingChange => IsNew || IsRenamed || IsDeleted;

    /// <summary>Shown next to the name in the picker, so an unsaved bank change is visible.</summary>
    public string PendingLabel => IsNew ? "new" : IsRenamed ? "renamed" : string.Empty;

    public bool HasPendingLabel => PendingLabel.Length != 0;

    public override string ToString() => Name;

    private void NotifyPendingState()
    {
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(IsRenamed));
        OnPropertyChanged(nameof(HasPendingChange));
        OnPropertyChanged(nameof(PendingLabel));
        OnPropertyChanged(nameof(HasPendingLabel));
    }
}
