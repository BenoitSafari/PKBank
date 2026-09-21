namespace PKBank.Core.Configuration;

public interface IConfigStore
{
    /// <summary>
    ///     Returns the stored payload, or <c>null</c> when nothing has been saved yet.
    /// </summary>
    string? Read();

    /// <summary>
    ///     Overwrites the stored payload. Implementations should write atomically.
    /// </summary>
    void Write(string content);
}
