using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ItemInfo.Models;

public class ModBsgBlackList
{
    [JsonPropertyName("bsgBlackList")]
    public List<string> BsgBlackList { get; set; } = [];
}