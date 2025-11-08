using System.Text.Json;
using System.Text.Json.Serialization;

namespace ItemInfo.Models;

public class ModTranslation
{
    [JsonPropertyName("debug")]
    public ModTranslationDebug ModTranslationDebug { get; set; } = new();
	
    [JsonExtensionData]
    public Dictionary<string, JsonElement> RawLanguages { get; set; } = new();
	
    [JsonIgnore]
    public Dictionary<string, Dictionary<string, string>> Language { get; set; } = new();
}