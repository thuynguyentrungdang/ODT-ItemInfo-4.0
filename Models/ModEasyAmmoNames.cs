using System.Text.Json.Serialization;

namespace ItemInfo.Models;

public class ModEasyAmmoName
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
    [JsonPropertyName("items")]
    public Dictionary<string, Items> ModItems { get; set; }
    public class Items
    {
        [JsonPropertyName("Name")]
        public string Name { get; set; }
        [JsonPropertyName("ShortName")]
        public string ShortName { get; set; }
        [JsonPropertyName("Description")]
        public string Description { get; set; }
    }
}