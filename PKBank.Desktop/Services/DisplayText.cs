namespace PKBank.Desktop.Services;

public static class DisplayText
{
    /// <summary>
    /// Trims uninitialized-string artifacts: blank saves leave 0xFF bytes that
    /// decode to U+FFFF (the Gen 4+ terminator), rendering as tofu boxes.
    /// Everything from the first such char on is not real text.
    /// </summary>
    public static string Sanitize(string text)
    {
        var cut = text.IndexOf('\uFFFF');
        return cut < 0 ? text : text[..cut].TrimEnd();
    }
}
