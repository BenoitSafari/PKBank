using System.Reflection;

namespace PKBank.Desktop;

public static class AppInfo
{
    private static readonly Assembly Assembly = typeof(AppInfo).Assembly;

    public static string Name { get; } = Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "PKBank";

    public static string Version { get; } = Assembly.GetName().Version?.ToString(3) ?? "?";
}
