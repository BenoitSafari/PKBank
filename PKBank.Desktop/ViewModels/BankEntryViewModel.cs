using PKBank.Desktop.Services.Banks;

namespace PKBank.Desktop.ViewModels;

/// <summary>One configured bank folder, as listed in the bank picker.</summary>
public sealed class BankEntryViewModel(string folder)
{
    public string Folder { get; } = folder;

    /// <summary>Folder name until the bank is opened and its manifest supplies one.</summary>
    public string Name { get; set; } = BankStorage.DefaultName(folder);

    public override string ToString() => Name;
}
