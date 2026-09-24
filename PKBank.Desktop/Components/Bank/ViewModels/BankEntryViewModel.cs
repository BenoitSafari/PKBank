using PKBank.Desktop.Services.Banks;

namespace PKBank.Desktop.Components.Bank.ViewModels;

/// <summary>One bank folder, as listed in the bank picker. Its name is the folder name.</summary>
public sealed class BankEntryViewModel(string folder) : ViewModelBase
{
    private string _folder = folder;

    public string Folder
    {
        get => _folder;
        set
        {
            if (SetField(ref _folder, value))
                OnPropertyChanged(nameof(Name));
        }
    }

    public string Name => BankLibrary.NameOf(Folder);

    public override string ToString() => Name;
}
