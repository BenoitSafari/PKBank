using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PKBank.Desktop.Utils;

namespace PKBank.Desktop.Services.Banks;

/// <summary>
///     The folder holding every bank, one sub-folder each. The folder name <em>is</em> the bank name, so
///     everything that names a bank goes through here: creating, renaming, deleting, and repairing a name
///     that was changed by hand into something the app does not allow.
/// </summary>
public sealed class BankLibrary(string root)
{
    public const string DefaultBankName = "Default Bank";
    public const int MaxNameLength = 32;

    private const string DefaultRootFolderName = "banks";

    /// <summary>Names compare case-insensitively: two banks differing only by case collide on most systems.</summary>
    public static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    public string Root { get; } = root;

    /// <summary>The banks folder used when none is configured, next to the config file.</summary>
    public static string DefaultRoot => Path.Combine(FileConfigStore.ConfigDirectory, DefaultRootFolderName);

    /// <summary>
    ///     The configured folder when it is usable, the default one otherwise (blank, malformed, or a
    ///     location that cannot be created).
    /// </summary>
    public static string ResolveRoot(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            try
            {
                var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured.Trim()));
                Directory.CreateDirectory(full);
                return full;
            }
            catch
            {
                // Unusable: fall through to the default.
            }

        return DefaultRoot;
    }

    /// <summary>Whether two paths name the same folder; false when either is malformed.</summary>
    public static bool IsSamePath(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(a.Trim())),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(b.Trim())),
                OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsNameCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is ' ' or '-' or '+' or '_';

    /// <summary>Letters, digits, spaces, "-", "+" and "_"; no leading or trailing space; at most 32 characters.</summary>
    public static bool IsValidName(string name) =>
        name.Length is > 0 and <= MaxNameLength
        && name.All(IsNameCharacter)
        && name[0] != ' ' && name[^1] != ' ';

    /// <summary>Why a name cannot be used for a new bank, or for renaming <paramref name="currentFolder" />; null when it can.</summary>
    public string? ValidateName(string name, string? currentFolder = null)
    {
        if (name.Length == 0)
            return "A name is required.";
        if (name.Length > MaxNameLength)
            return $"At most {MaxNameLength} characters.";
        if (!name.All(IsNameCharacter))
            return "Only letters, digits, spaces, \"-\", \"+\" and \"_\" are allowed.";
        if (name[0] == ' ' || name[^1] == ' ')
            return "The name cannot start or end with a space.";

        var current = currentFolder is null ? null : NameOf(currentFolder);
        if (current is not null && NameComparer.Equals(current, name))
            return null; // same bank, possibly a change of case
        return ListNames().Contains(name, NameComparer) ? "A bank with this name already exists." : null;
    }

    /// <summary>
    ///     Turns any folder name into an allowed one that none of <paramref name="taken" /> uses: disallowed
    ///     characters become "_", then "-2", "-3"… is appended on a collision.
    /// </summary>
    public static string Sanitize(string name, IEnumerable<string> taken)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
            builder.Append(IsNameCharacter(c) ? c : '_');

        var stem = builder.ToString().Trim();
        if (stem.Length > MaxNameLength)
            stem = stem[..MaxNameLength].TrimEnd();
        if (stem.Length == 0)
            stem = "Bank";

        var used = new HashSet<string>(taken, NameComparer);
        var candidate = stem;
        for (var n = 2; used.Contains(candidate); n++)
        {
            var suffix = $"-{n}";
            var head = stem.Length + suffix.Length > MaxNameLength
                ? stem[..(MaxNameLength - suffix.Length)].TrimEnd()
                : stem;
            candidate = head + suffix;
        }

        return candidate;
    }

    public static string NameOf(string folder) => BankStorage.DefaultName(folder);

    /// <summary>
    ///     Makes the library consistent and lists it: creates the root, renames folders whose name is not
    ///     allowed, and creates the default bank when there is none. Returns the bank folders sorted by name;
    ///     <paramref name="repaired" /> lists the folders renamed on the way (old path, new path).
    /// </summary>
    public IReadOnlyList<string> Scan(out IReadOnlyList<(string From, string To)> repaired)
    {
        Directory.CreateDirectory(Root);
        var renames = new List<(string From, string To)>();
        repaired = renames;

        var folders = ListFolders();
        var names = folders.Select(NameOf).ToList();
        for (var i = 0; i < folders.Count; i++)
        {
            if (IsValidName(names[i]))
                continue;

            var others = names.Where((_, j) => j != i);
            var fixedName = Sanitize(names[i], others);
            try
            {
                var target = Path.Combine(Root, fixedName);
                Directory.Move(folders[i], target);
                renames.Add((folders[i], target));
                folders[i] = target;
                names[i] = fixedName;
            }
            catch
            {
                // Could not repair it (locked, read-only): leave it out rather than show a bad name.
                folders.RemoveAt(i);
                names.RemoveAt(i);
                i--;
            }
        }

        // Two folders sanitized into the same name, or a case-only duplicate left by another system.
        for (var i = folders.Count - 1; i > 0; i--)
            if (names.Take(i).Contains(names[i], NameComparer))
            {
                folders.RemoveAt(i);
                names.RemoveAt(i);
            }

        if (folders.Count == 0)
            folders.Add(Create(DefaultBankName));

        folders.Sort((a, b) => NameComparer.Compare(NameOf(a), NameOf(b)));
        return folders;
    }

    /// <summary>Creates an empty bank; the name must have passed <see cref="ValidateName" />.</summary>
    public string Create(string name)
    {
        var folder = Path.Combine(Root, name);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Renames a bank folder; returns its new path.</summary>
    public string Rename(string folder, string newName)
    {
        var target = Path.Combine(Root, newName);
        if (string.Equals(NameOf(folder), newName, StringComparison.Ordinal))
            return folder;

        if (NameComparer.Equals(NameOf(folder), newName))
        {
            // Case-only change: a direct move is a no-op (or fails) on case-insensitive file systems.
            var temp = Path.Combine(Root, $".rename-{Guid.NewGuid():N}");
            Directory.Move(folder, temp);
            Directory.Move(temp, target);
            return target;
        }

        Directory.Move(folder, target);
        return target;
    }

    /// <summary>Deletes a bank and every file in it. The last bank cannot go.</summary>
    public void Delete(string folder)
    {
        if (ListFolders().Count <= 1)
            throw new InvalidOperationException("At least one bank must remain.");
        Directory.Delete(folder, true);
    }

    /// <summary>
    ///     Moves every bank of <paramref name="oldRoot" /> into <paramref name="newRoot" />, renaming on a
    ///     name clash, then removes <paramref name="oldRoot" /> if that left it empty. Each bank that made it
    ///     is recorded in <paramref name="moved" /> (old folder → new folder) as it goes, so a failure
    ///     part-way still tells the caller where everything is.
    /// </summary>
    public static void MoveAll(string oldRoot, string newRoot, IDictionary<string, string> moved)
    {
        if (!Directory.Exists(oldRoot))
            return;

        Directory.CreateDirectory(newRoot);
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(newRoot));
        var taken = new BankLibrary(newRoot).ListNames().ToList();
        foreach (var folder in new BankLibrary(oldRoot).ListFolders())
        {
            // The new banks folder may sit inside the old one: it is not a bank to move into itself.
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
            if (target == full || target.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                continue;

            var name = Sanitize(NameOf(folder), taken);
            var destination = Path.Combine(newRoot, name);
            try
            {
                Directory.Move(folder, destination);
            }
            catch (IOException) when (!Directory.Exists(destination))
            {
                // Another volume: Directory.Move cannot cross it.
                CopyDirectory(folder, destination);
                Directory.Delete(folder, true);
            }

            taken.Add(name);
            moved[folder] = destination;
        }

        try
        {
            if (!Directory.EnumerateFileSystemEntries(oldRoot).Any())
                Directory.Delete(oldRoot);
        }
        catch
        {
            // Left behind: harmless.
        }
    }

    private List<string> ListFolders()
    {
        if (!Directory.Exists(Root))
            return [];
        // Hidden folders (a leftover temp rename, ".git"…) are not banks.
        return Directory.EnumerateDirectories(Root)
            .Where(static d => !NameOf(d).StartsWith('.'))
            .ToList();
    }

    private IEnumerable<string> ListNames() => ListFolders().Select(NameOf);

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
}
