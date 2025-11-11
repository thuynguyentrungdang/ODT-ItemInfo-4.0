using System.Text.Json.Serialization;

namespace ItemInfo.Models;

public class ModEasyAmmoName
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    [JsonPropertyName("items")]
    public Items ModItems { get; set; }
    public class Items
    {
        public Dictionary<string, Dictionary<string, string>> AmmoEntry { get; set; }
    }
}