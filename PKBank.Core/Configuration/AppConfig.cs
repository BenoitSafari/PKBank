using System.Collections.Generic;
using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKBank.Core.Configuration;

public sealed class AppConfig
{
    /// <summary>
    ///     Schema version of the persisted payload, for migrations.
    /// </summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public string Language { get; set; } = GameLanguage.DefaultLanguage;

    [JsonConverter(typeof(JsonStringEnumConverter<GameVersion>))]
    public GameVersion BlankSaveVersion { get; set; } = Latest.Version;

    public List<string> SavPaths { get; set; } = [];

    public List<string> BankPaths { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
internal sealed partial class AppConfigJsonContext : JsonSerializerContext;
