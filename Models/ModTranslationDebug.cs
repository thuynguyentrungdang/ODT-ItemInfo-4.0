using System.Text.Json.Serialization;

namespace ItemInfo.Models;

public class ModTranslationDebug
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("languageToDebug")] 
    public string LanguageToDebug { get; set; } = null!;
}