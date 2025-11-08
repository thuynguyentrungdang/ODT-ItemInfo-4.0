using System.Text.Json.Serialization;

namespace ItemInfo.Models;

public class ModConfig
{
    [JsonPropertyName("UserLocale")]
	public string UserLocale { get; set; } = "en-US";
	
	[JsonPropertyName("HideLanguageAlert")]
	public bool HideLanguageAlert { get; set; } = true;

	[JsonPropertyName("delay")]
	public ModDelay Delay { get; set; } = new();
	public class ModDelay
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
		[JsonPropertyName("seconds")]
		public int Seconds { get; set;}
	}
	
	[JsonPropertyName("useBSGStaticFleaBanList")]
	public UseBSGStaticFleaBanList UseBsgStaticFleaBanList { get; set; } = new();
	public class UseBSGStaticFleaBanList
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	
	[JsonPropertyName("BulletStatsInName")]
	public BulletStatsInName ModBulletStatsInName {get; set;} = new();
	public class BulletStatsInName
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	
	[JsonPropertyName("RarityRecolor")]
	public RarityRecolor ModRarityRecolor { get; set; } = new();
	public class RarityRecolor
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
		[JsonPropertyName("addTierNameToPricesInfo")]
		public bool AddTierNameToPricesInfo { get; set; }
		[JsonPropertyName("fallbackValueBasedRecolor")]
		public bool FallbackValueBasedRecolor { get; set; }
		[JsonPropertyName("bypassAmmoRecolor")]
		public bool BypassAmmoRecolor { get; set; }
		[JsonPropertyName("bypassKeysRecolor")]
		public bool BypassKeysRecolor { get; set; }
		[JsonPropertyName("customRarity")]
		public Dictionary<string, int> CustomRarity { get; set; }
	}
	
	[JsonPropertyName("RarityRecolorBlacklist")]
	public List<string> RarityRecolorBlacklist { get; set; }
	
	[JsonPropertyName("ArmorInfo")]
	public ArmorInfo ModArmorInfo { get; set; } = new();
	public class ArmorInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
		[JsonPropertyName("addArmorClassInfo")]
		public bool AddArmorClassInfo { get; set; }
		[JsonPropertyName("addArmorToName")]
		public bool AddArmorToName { get; set; }
		[JsonPropertyName("addArmorToShortName")]
		public bool AddArmorToShortName { get; set; }
	}
	
	[JsonPropertyName("AdvancedAmmoInfo")]
	public AdvancedAmmoInfo ModAdvancedAmmoInfo { get; set; } = new();
	public class AdvancedAmmoInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	
	[JsonPropertyName("ContainerInfo")]
	public ContainerInfo ModContainerInfo { get; set; } = new();
	public class ContainerInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	[JsonPropertyName("HeadsetInfo")]
	public HeadsetInfo ModHeadsetInfo { get; set; } = new();
	public class HeadsetInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	
	[JsonPropertyName("ProductionInfo")]
	public ProductionInfo ModProductionInfo { get; set; } = new();
	public class ProductionInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	
	[JsonPropertyName("CraftingMaterialInfo")]
	public CraftingMaterialInfo ModCraftingMaterialInfo { get; set; } = new();
	public class CraftingMaterialInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	
	[JsonPropertyName("BarterInfo")]
	public BarterInfo ModBarterInfo { get; set; } = new();
	public class BarterInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	
	[JsonPropertyName("QuestInfo")]
	public QuestInfo ModQuestInfo { get; set; } = new();
	public class QuestInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
		[JsonPropertyName("FIRInName")]
		public bool FirInName {get; set;}
	}
	
	[JsonPropertyName("HideoutInfo")]
	public HideoutInfo ModHideoutInfo { get; set; } = new();
	public class HideoutInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	
	[JsonPropertyName("BarterResourceInfo")]
	public BarterResourceInfo ModBarterResourceInfo { get; set; } = new();
	public class BarterResourceInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
	
	[JsonPropertyName("PricesInfo")]
	public PriceInfo ModPriceInfo { get; set; } = new();
	public class PriceInfo
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
		[JsonPropertyName("addFleaPrice")]
		public bool AddFleaPrice { get; set; }
		[JsonPropertyName("addItemValue")]
		public bool AddItemValue {get; set;}
	}
	
	[JsonPropertyName("MarkValuableItems")]
	public MarkValuableItems ModMarkValuableItems { get; set; } = new();
	public class MarkValuableItems
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
		[JsonPropertyName("addToName")]
		public bool AddToName {get; set;}
		[JsonPropertyName("addToShortName")]
		public bool AddToShortName {get; set;}
		[JsonPropertyName("useAltValueMark")]
		public bool UseAltValueMark {get; set;}
		[JsonPropertyName("alwaysMarkBannedItems")]
		public bool AlwaysMarkBannedItems {get; set;}
		[JsonPropertyName("BestValueMark")]
		public string BestValueMark {get; set;}
		[JsonPropertyName("GoodValueMark")]
		public string GoodValueMark {get; set;}
		[JsonPropertyName("AltBestValueMark")]
		public string AltBestValueMark {get; set;}
		[JsonPropertyName("AltGoodValueMark")]
		public string AltGoodValueMark {get; set;}
		[JsonPropertyName("traderSlotValueThresholdBest")]
		public double TraderSlotValueThresholdBest {get; set;}
		[JsonPropertyName("traderSlotValueThresholdGood")]
		public double TraderSlotValueThresholdGood {get; set;}
		[JsonPropertyName("fleaSlotValueThresholdBest")]
		public double FleaSlotValueThresholdBest {get; set;}
		[JsonPropertyName("fleaSlotValueThresholdGood")]
		public double FleaSlotValueThresholdGood {get; set;}
	}
	
	[JsonPropertyName("SoftcoreAmmoStackMultiFix")]
	public SoftcoreAmmoStackMultiFix ModSoftcoreAmmoStackMultiFix { get; set; } = new();
	public class SoftcoreAmmoStackMultiFix
	{
		[JsonPropertyName("enabled")]
		public bool Enabled { get; set; }
	}
}