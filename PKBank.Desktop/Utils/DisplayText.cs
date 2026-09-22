namespace PKBank.Desktop.Utils;

public static class DisplayText
{
    public static string Sanitize(string text)
    {
        var cut = text.IndexOf('\uFFFF');
        return cut < 0 ? text : text[..cut].TrimEnd();
    }
}
