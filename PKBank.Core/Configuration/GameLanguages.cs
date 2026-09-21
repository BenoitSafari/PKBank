using System.Collections.Generic;

namespace PKBank.Core.Configuration;

public static class GameLanguages
{
    public static readonly IReadOnlyList<(string Code, string DisplayName)> All =
    [
        ("ja", "日本語"),
        ("en", "English"),
        ("fr", "Français"),
        ("it", "Italiano"),
        ("de", "Deutsch"),
        ("es", "Español"),
        ("es-419", "Español (Latinoamérica)"),
        ("ko", "한국어"),
        ("zh-Hans", "简体中文"),
        ("zh-Hant", "繁體中文")
    ];
}
