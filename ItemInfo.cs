using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ItemInfo.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;

namespace ItemInfo;

public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.odt.iteminfo";
    public override string Name { get; init; } = "ItemInfo";
    public override string Author { get; init; } = "ODT";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("2.0.14");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string License { get; init; } = "MIT";
}

// We want to load after PostDBModLoader is complete and all other mods, so we set our type priority to that, plus 200.
[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 2)]
public class ItemInfo(
	ISptLogger<ItemInfo> logger, // We are injecting a logger similar to example 1, but notice the class inside <> is different
	ModHelper modHelper,
	JsonUtil jsonUtil,
    DatabaseService databaseService,
    LocaleService localeService,
    ItemBaseClassService itemBaseClassService,
	ItemHelper  itemHelper)
    : IOnLoad // Implement the `IOnLoad` interface so that this mod can do something
{
	
	private Timer? _timer;
	public required ModConfig Config;
	public required ModTiers Tiers { get; set; }
	public required ModTiersHex TiersHex { get; set; }
	public required ModTranslation Translation { get; set; }
	public required ModTranslationDebug ModTranslationDebug { get; set; }
	public required ModBsgBlackList ModBsgBlackList { get; set; }
	private double EuroRatio {get; set;}
	private double DollarRatio {get; set;}
	public required Dictionary<string, Dictionary<string, List<string>>> QuestRewardsDb { get; set; }
	public required Dictionary<MongoId, Quest> Quests { get; set; }
	public required Dictionary<MongoId,TemplateItem> Items { get; set; }
	public required Dictionary<ArmorMaterial, ArmorType> Armors { get; set; }
	public required string UserLocale { get; set; }
	public required Dictionary<string, string> i18n { get; set; }
	public required Dictionary<string, string> Localization { get; set; }
	public required List<string> BsgBlacklist { get; set; }
	public string PathToMod { get; set; }
	public Dictionary<MongoId, ModItemDescription> ItemDescription { get; set; } = new();
	
    public Task OnLoad()
    {
	    PathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
		
	    // Get configs
	    Config = modHelper.GetJsonDataFromFile<ModConfig>(PathToMod, "config/config.json");
	    ModBsgBlackList = modHelper.GetJsonDataFromFile<ModBsgBlackList>(PathToMod, "config/bsgblacklist.json");
	    
	    // Get tiers list
	    Tiers = modHelper.GetJsonDataFromFile<ModTiers>(PathToMod, "config/tiers.json");
	    TiersHex = modHelper.GetJsonDataFromFile<ModTiersHex>(PathToMod, "config/tiers_hex.json");
	    
	    // Get translations
	    Translation = jsonUtil.DeserializeFromFile<ModTranslation>(PathToMod + "/config/translations.json") ?? 
	                  throw new NullReferenceException("Could not find translations file");
	    ModTranslationDebug = Translation.ModTranslationDebug;
	    Translation.Language = new Dictionary<string, Dictionary<string, string>>();
	    BsgBlacklist = ModBsgBlackList.BsgBlackList;
	    
	    foreach (var kvp in Translation.RawLanguages)
	    {
		    if (kvp.Key == "debug") 
			    continue;

		    var langDict = kvp.Value.Deserialize<Dictionary<string, string>>()
		                   ?? new Dictionary<string, string>();

		    Translation.Language[kvp.Key] = langDict;
	    }
	    
	    // Init vars
	    EuroRatio = (double)databaseService.GetHandbook().Items.FirstOrDefault(item => item.Id == "569668774bdc2da2298b4568")!
		    .Price!;
	    DollarRatio = (double)databaseService.GetHandbook().Items.FirstOrDefault(item => item.Id == "5696686a4bdc2da3298b456a")!
		    .Price!;
	    Quests = databaseService.GetTemplates().Quests;
	    Items = databaseService.GetTemplates().Items;
	    Armors = databaseService.GetGlobals().Configuration.ArmorMaterials;
	    UserLocale = Config.UserLocale;
	    i18n = Translation.Language[UserLocale];
	    Localization = localeService.GetLocaleDb(UserLocale);
	    BsgBlacklist = ModBsgBlackList.BsgBlackList;
	    
        // Pass all the services and vars to Utils
        Utils.Initialize(databaseService, 
						localeService, 
						itemBaseClassService, 
						logger, 
						EuroRatio, 
						DollarRatio, 
						UserLocale, 
						Translation,
						Quests,
						Config);

        if (Config.Delay.Enabled)
        {
	        logger.LogWithColor("[Item Info] Mod compatibility delay enabled (" + Config.Delay.Seconds + " seconds), waiting for other mods data to load...",
								LogTextColor.Black,
								LogBackgroundColor.Cyan);
	        
	        _timer = new Timer(_ =>
	        {
		        ItemInfoMain();
	        }, null, Config.Delay.Seconds * 1000, Timeout.Infinite);
        }
        else
        {
	        ItemInfoMain();
        }

        // lets write a nice log message to the server console so players know our mod has made changes
        logger.Success("[ItemInfo] Item Info loaded!");
        
        // Inform server we have finished
        return Task.CompletedTask;
    }

    private void ItemInfoMain()
    {
	    TranslationDebug();

	    string cacheFingerprint = BuildCacheFingerprint();
	    Stopwatch stopwatch = Stopwatch.StartNew();

	    if (TryApplyCache(cacheFingerprint))
	    {
		    stopwatch.Stop();
		    logger.Info("[ItemInfo] Applied cached item info in " + stopwatch.ElapsedMilliseconds + " ms.");
		    return;
	    }

	    QuestRewards();
	    logger.Info("[ItemInfo] Processing items...");
	    ItemHandling();
	    stopwatch.Stop();
	    logger.Info("[ItemInfo] Completed processing " + Items.Count +" items in " + stopwatch.ElapsedMilliseconds + " ms.");

	    SaveCache(cacheFingerprint);
    }

    private void TranslationDebug()
    {
		logger.Debug("[ItemInfo] Translation debug.");

	    if (!Config.HideLanguageAlert)
	    {
		    logger.LogWithColor("[Item Info] This mod supports other languages! \n" +
		                        "Мод поддерживает другие языки! \n" +
		                        "Este mod es compatible con otros idiomas! \n" +
		                        "Ten mod obsługuje inne języki! \n" +
		                        "English, Russian, Spanish, Korean, French, Chinese, Japanese and German are fully translated.\n" +
		                        "Hide this message in config.json",
			    LogTextColor.Black,
			    LogBackgroundColor.White);
		    
		    logger.LogWithColor("[Item Info] Your selected language is \""+ UserLocale +". \n" +
		                        "You can now customise it in Item Info config.json file. \n" +
		                        "Looking for translators, PM me! \n" +
		                        "Translation debug mode is available in translations.json",
			    LogTextColor.Black,
			    LogBackgroundColor.Green);
	    }

	    if (Translation.ModTranslationDebug.Enabled)
	    {
		    logger.Warning("Translation debugging mode enabled! Changing userLocale to " + Translation.ModTranslationDebug.LanguageToDebug);
		    UserLocale = Translation.ModTranslationDebug.LanguageToDebug;
	    }

	    foreach (KeyValuePair<string, string> kvp in Translation.Language["en"])
	    {
		    string key = kvp.Key;
		    string enValue = kvp.Value;
		    
		    foreach (KeyValuePair<string, Dictionary<string, string>> lang in Translation.Language)
		    {
			    string langCode = lang.Key;
			    Dictionary<string, string> dict = lang.Value;
			    
			    if(Translation.ModTranslationDebug.Enabled &&
			       key != "" &&
			       langCode != "en" &&
			       langCode == Translation.ModTranslationDebug.LanguageToDebug &&
			       dict.TryGetValue(key, out string? val) &&
			       !string.IsNullOrEmpty(val) &&
			       val == enValue)
				    logger.Warning(Translation.ModTranslationDebug.LanguageToDebug + " language " + val + " is the same as English.");

			    if (dict.ContainsKey(key)) 
				    continue;
			    
			    if (Translation.ModTranslationDebug.Enabled &&
			        Translation.ModTranslationDebug.LanguageToDebug == langCode)
				    logger.Warning(lang.Key + " is missing " + key + " translation.");
				    
			    dict[key] = enValue;
		    }
	    }
    }

    private void QuestRewards()
    {
	    QuestRewardsDb = new Dictionary<string, Dictionary<string, List<string>>>();

	    foreach (MongoId questId in Quests.Keys)
	    {
		    var assortmentUnlocks = new List<Reward>();
		    
		    Dictionary<string, List<Reward>>? dictionary = Quests[questId].Rewards;

		    if (dictionary is not null)
		    {
			    foreach (List<Reward> rewards in dictionary.Values)
			    {
				    assortmentUnlocks.AddRange(rewards.Where(reward => reward.Type == RewardType.AssortmentUnlock));
			    }
		    }

		    if (assortmentUnlocks.Count > 1)
		    {
			    QuestRewardsDb[questId] = new Dictionary<string, List<string>>();
			    
			    foreach (Reward reward in assortmentUnlocks)
			    {
				    if (reward.Target is null)
					    continue;
				    
				    // Ensure the questID entry exists
				    if (!QuestRewardsDb.ContainsKey(questId))
					    QuestRewardsDb[questId] = new Dictionary<string, List<string>>();

				    // Initialize target list
				    QuestRewardsDb[questId][reward.Target] = [];

				    if (reward.Items is null)
					    continue;

				    // Push each _tpl value
				    foreach (Item item in reward.Items)
				    {
					    QuestRewardsDb[questId][reward.Target].Add(item.Template);
				    }
			    }
		    }
	    }
    }

    private string CacheDirectory => System.IO.Path.Combine(PathToMod, "cache");
    private string CachePath => System.IO.Path.Combine(CacheDirectory, "iteminfo-cache.json");

    private string BuildCacheFingerprint()
    {
	    using SHA256 sha256 = SHA256.Create();

	    AddHashText(sha256, "ItemInfo:" + new ModMetadata().Version);
	    AddHashText(sha256, "Locale:" + UserLocale);
	    AddHashFile(sha256, System.IO.Path.Combine(PathToMod, "config", "config.json"));
	    AddHashFile(sha256, System.IO.Path.Combine(PathToMod, "config", "bsgblacklist.json"));
	    AddHashFile(sha256, System.IO.Path.Combine(PathToMod, "config", "tiers.json"));
	    AddHashFile(sha256, System.IO.Path.Combine(PathToMod, "config", "tiers_hex.json"));
	    AddHashFile(sha256, System.IO.Path.Combine(PathToMod, "config", "translations.json"));
	    AddModsFingerprint(sha256);
	    sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

	    return Convert.ToHexString(sha256.Hash!);
    }

    private void AddModsFingerprint(HashAlgorithm hashAlgorithm)
    {
	    DirectoryInfo? modsDirectory = Directory.GetParent(PathToMod);

	    if (modsDirectory is null)
		    return;

	    foreach (AbstractModMetadata modMetadata in AppDomain.CurrentDomain.GetAssemblies()
	             .Where(assembly => !string.IsNullOrEmpty(assembly.Location) &&
	                                assembly.Location.StartsWith(modsDirectory.FullName, StringComparison.OrdinalIgnoreCase))
	             .SelectMany(assembly => assembly.GetTypes())
	             .Where(type => !type.IsAbstract && typeof(AbstractModMetadata).IsAssignableFrom(type))
	             .Select(type => (AbstractModMetadata)Activator.CreateInstance(type)!)
	             .OrderBy(metadata => metadata.ModGuid, StringComparer.OrdinalIgnoreCase)
	             .ThenBy(metadata => metadata.Version.ToString(), StringComparer.OrdinalIgnoreCase))
	    {
		    AddHashText(hashAlgorithm, modMetadata.ModGuid);
		    AddHashText(hashAlgorithm, modMetadata.Version.ToString());
		    AddHashText(hashAlgorithm, modMetadata.SptVersion.ToString());
	    }
    }

    private static void AddHashFile(HashAlgorithm hashAlgorithm, string path)
    {
	    AddHashText(hashAlgorithm, path);

	    if (!File.Exists(path))
		    return;

	    byte[] bytes = File.ReadAllBytes(path);
	    hashAlgorithm.TransformBlock(bytes, 0, bytes.Length, null, 0);
    }

    private static void AddHashText(HashAlgorithm hashAlgorithm, string value)
    {
	    byte[] bytes = Encoding.UTF8.GetBytes(value + "\n");
	    hashAlgorithm.TransformBlock(bytes, 0, bytes.Length, null, 0);
    }

    private bool TryApplyCache(string fingerprint)
    {
	    if (!File.Exists(CachePath))
		    return false;

	    CacheFile? cache = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(CachePath));

	    if (cache is null ||
	        cache.Fingerprint != fingerprint ||
	        cache.Locale != UserLocale)
		    return false;

	    foreach (CachedItemInfoPatch item in cache.Items)
	    {
		    if (!Items.TryGetValue(item.Id, out TemplateItem? templateItem))
			    continue;

		    if (item.Name is not null)
			    Utils.SetLocaleValue(item.Id, "Name", item.Name, UserLocale);

		    if (item.ShortName is not null)
			    Utils.SetLocaleValue(item.Id, "ShortName", item.ShortName, UserLocale);

		    if (!string.IsNullOrEmpty(item.DescriptionPrefix))
			    Utils.AddToDescription(item.Id, item.DescriptionPrefix, "prepend", UserLocale);

		    if (templateItem.Properties is not null && item.BackgroundColor is not null)
			    templateItem.Properties.BackgroundColor = item.BackgroundColor;
	    }

	    return true;
    }

    private void SaveCache(string fingerprint)
    {
	    Directory.CreateDirectory(CacheDirectory);

	    CacheFile cache = new()
	    {
		    Fingerprint = fingerprint,
		    Locale = UserLocale,
		    Items = Items.Select(kvp => new CachedItemInfoPatch
		    {
			    Id = kvp.Key.ToString(),
			    Name = Utils.GetItemName(kvp.Key, UserLocale),
			    ShortName = Utils.GetItemShortName(kvp.Key, UserLocale),
			    DescriptionPrefix = BuildDescriptionPrefix(kvp.Key),
			    BackgroundColor = kvp.Value.Properties?.BackgroundColor
		    }).ToList()
	    };

	    File.WriteAllText(CachePath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = false }));
	    logger.Info("[ItemInfo] Saved item info cache.");
    }

    private string BuildDescriptionPrefix(string itemId)
    {
	    if (!ItemDescription.TryGetValue(itemId, out ModItemDescription? itemDescription))
		    return string.Empty;

	    return itemDescription.PriceString +
	           itemDescription.HeadsetDescription +
	           itemDescription.ArmorDurabilityString +
	           itemDescription.SlotEfficiencyString +
	           itemDescription.UsedForQuestsString +
	           itemDescription.UsedForHideoutString +
	           itemDescription.BarterString +
	           itemDescription.ProductionString +
	           itemDescription.UsedForCraftingString +
	           itemDescription.UsedForBarterString +
	           itemDescription.AdvancedAmmoInfoString;
    }


    private void ItemHandling()
    {
	    int a = 0;
	    StringBuilder descriptionString = new StringBuilder(); ;
	    StringBuilder itemBestTraderName = new StringBuilder();
	    StringBuilder itemName = new StringBuilder();
	    StringBuilder logString = new StringBuilder();
	    StringBuilder tiersHexCode = new StringBuilder();
	    StringBuilder addToName = new StringBuilder();
	    StringBuilder addToShortName = new StringBuilder();
	    
	    foreach (KeyValuePair<MongoId, TemplateItem> kvp in Items)
	    {
		    // Clearing all vars
		    itemBestTraderName.Clear();
		    tiersHexCode.Clear();
		    itemName.Clear();
		    logString.Clear();
		    addToName.Clear();
		    addToShortName.Clear();
		    
		    MongoId itemId = kvp.Key;
		    ItemDescription[itemId] = new ModItemDescription();
		    TemplateItem templateItem = kvp.Value;
		    HandbookItem? itemInHandbook = Utils.GetItemInHandbook(itemId);
		    TemplateItemProperties? itemProperties = templateItem.Properties;
		    
		    if (itemProperties is null)
			    continue;

			bool isQuestItem = itemProperties.QuestItem ?? false;

			if (templateItem.Type != "Item" || // Check if the item is a real item and not a "node" type.
			    itemInHandbook is null || // Ignore "useless" items
			    isQuestItem || // Ignore quest items.
			    templateItem.Parent == "543be5dd4bdc2deb348b4569") // Ignore currencies.
				continue; 
			
		    // Boilerplate defaults
		    double fleaPrice = Utils.GetFleaPrice(itemId) ?? 0;
		    ValueTuple<double?, string, string> itemBestVendor = Utils.GetItemBestTrader(itemId);
		    double? price = itemBestVendor.Item1;
		    
		    if (price is null)
			    continue;
		    
		    double traderPrice = Math.Round((double)price);
		    List<Utils.ResolvedBarter> itemBarters = Utils.BarterResolver(itemId);
		    ValueTuple<List<double>, string, List<int>> barterInfo = Utils.BarterInfoGenerator(itemBarters, UserLocale);
		    List<int> rarityArray = barterInfo.Item3;
		    int itemRarity = rarityArray.Min();
		    int slotDensity = Utils.GetItemSlotDensity(itemProperties);
		    bool isBanned = false;
		    
		    string fleaPriceString = fleaPrice.ToString(CultureInfo.CurrentCulture);
		    string itemQuestInfo = Utils.QuestInfoGenerator(itemId, UserLocale);

		    itemBestTraderName.Append(itemBestVendor.Item2);
		    
		    Utils.RefreshName(itemId, UserLocale);
		    Utils.RefreshShortName(itemId, UserLocale);
		    
		    itemName.Append(Utils.GetItemName(kvp.Key, UserLocale));
		    itemName.Append(" | " + Utils.GetItemShortName(kvp.Key, UserLocale));

#if DEBUG
		    logString.Append("Processing item " +
		                     (a + 1) +
		                     "/" +
		                     Items.Count +
		                     ": " +
		                     itemName);

		    logger.Info(logString.ToString());
		    a += 1;
#endif

		    // UseBsgStaticFleaBanList
		    if (Config.UseBsgStaticFleaBanList.Enabled)
		    {
			    if (BsgBlacklist.Contains(itemId))
					isBanned = true;
		    }
		    else
		    {
			    isBanned = !itemProperties.CanSellOnRagfair ?? false;
		    }

		    // Rarity handling
		    if (isBanned)
		    {
			    fleaPriceString = i18n["BANNED"];
				itemRarity = 7;
		    }

		    if ((itemHelper.IsOfBaseclass(itemId, BaseClasses.MOD) ||
		         itemHelper.IsOfBaseclass(itemId, BaseClasses.ARMOR) ||
		         itemHelper.IsOfBaseclass(itemId, BaseClasses.AMMO) ||
		         itemHelper.IsOfBaseclass(itemId, BaseClasses.ARMOR_PLATE) ||
		         itemHelper.IsOfBaseclass(itemId, BaseClasses.VEST) ||
		         itemHelper.IsOfBaseclass(itemId, BaseClasses.WEAPON) ||
		         templateItem.Parent == "57bef4c42459772e8d35a53b") && // strictly ARMORED_EQUIPMENT
		         string.IsNullOrEmpty(barterInfo.Item2) &&
		         !isBanned)
			    itemRarity = 6;

		    if (itemQuestInfo.Contains('↺') &&
		        !itemQuestInfo.Contains('∈') &&
		        rarityArray.Count < 4)
			    itemRarity += 2;

		    if (templateItem.Parent == "543be5cb4bdc2deb348b4568")
		    {
			    IEnumerable<StackSlot>? stackSlots = itemProperties.StackSlots;
			    List<StackSlot>? stackSlotsList = stackSlots?.ToList();

			    if (stackSlotsList is not null)
			    {
				    double? count = stackSlotsList[0].MaxCount;
				    MongoId? ammo = stackSlotsList[0].Properties?.Filters?.ToList()[0].Filter?.ToList()[0];
				    double? value = Utils.GetItemBestTrader(ammo!).price;
				    
				    if (value is not null &&
				        count is not null)
						traderPrice = (double)(value * count);
				    else
					    traderPrice = 0;
				    
				    if (itemRarity != 7)
					    fleaPriceString = "";
			    }
		    }

		    if (isBanned &&
		        rarityArray.Min() == 0)
			    itemRarity = 9;
			    
		    // BulletStatsInName
		    if (Config.ModBulletStatsInName.Enabled)
		    {
			    double damageMult = 1;
			    if (itemProperties.AmmoType is "bullet" or "buckshot")
			    {
				    if (itemProperties.AmmoType == "buckshot")
					    damageMult = itemProperties.BuckshotBullets ?? 0;

				    addToName.Clear().Append(" (" +
				                             itemProperties.Damage * damageMult +
				                             "/" +
				                             itemProperties.PenetrationPower +
				                             ")");

				    Utils.AddToName(itemId, addToName.ToString(), "append");
			    } 
			    else if (itemProperties.Name != null &&
			             itemProperties.Name.Contains("ammo_box"))
			    {
				    TemplateItem ammo =
					    Items[itemProperties.StackSlots.First().Properties.Filters.First().Filter.First()];
				    TemplateItemProperties? ammoProperties = ammo.Properties;
				    
				    if (ammoProperties != null &&
				        ammoProperties.AmmoType == "buckshot")
					    damageMult = ammoProperties.BuckshotBullets ?? 0;

				    addToName.Clear().Append(" (" +
				                             ammoProperties.Damage * damageMult +
				                             "/" +
				                             ammoProperties.PenetrationPower +
				                             ")");

				    Utils.AddToName(itemId, addToName.ToString(), "append");
			    }
		    }
		    
		    if (Config.ModArmorInfo.Enabled)
		    {
			    if (itemProperties.ArmorClass > 0 &&
			        itemProperties.ArmorMaterial is not null)
			    {
				    ArmorMaterial armorMaterial = (ArmorMaterial)itemProperties.ArmorMaterial;
				    
				    ArmorType armor = Armors[armorMaterial];

				    ItemDescription[itemId].ArmorDurabilityString = (Config.ModArmorInfo.AddArmorClassInfo
					                                                    ? i18n["Armorclass"] +
					                                                      ": " +
					                                                      itemProperties.ArmorClass +
					                                                      " | "
					                                                    : "") +
				                                                    i18n["Effectivedurability"] +
				                                                    ": " +
				                                                    Math.Round((itemProperties.MaxDurability ?? 0) /
				                                                               armor.Destructibility) +
				                                                    " (" +
				                                                    i18n["Max"] +
				                                                    ": " +
				                                                    Math.Round(itemProperties.MaxDurability ?? 0) +
				                                                    " x " +
				                                                    Localization["Mat" + armorMaterial] +
				                                                    ": " +
				                                                    Math.Round(1 / armor.Destructibility, 1) +
				                                                    ") | " +
				                                                    i18n["Repairdegradation"] +
				                                                    ": " +
				                                                    Math.Round(armor.MinRepairDegradation * 100) +
				                                                    "% - " +
				                                                    Math.Round(armor.MaxRepairDegradation * 100) +
				                                                    "%\n\n";

				    addToName.Clear().Append(" (" +
										   itemProperties.ArmorClass +
										   "/" +
										   Math.Round(itemProperties.MaxDurability ?? 0 / armor.Destructibility) +
										   ")");
				    addToShortName.Clear().Append(" (" + 
												itemProperties.ArmorClass + 
												"/" + 
												Math.Round(itemProperties.MaxDurability ?? 0/ armor.Destructibility) +
												")");

				    if (Config.ModArmorInfo.AddArmorToName)
					    Utils.AddToName(itemId, addToName.ToString(), "append");

				    if (Config.ModArmorInfo.AddArmorToShortName)
					    Utils.AddToShortName(itemId, addToShortName.ToString(), "append");
			    }
		    }
		    
		    if (Config.ModAdvancedAmmoInfo.Enabled)
		    {
			    if (templateItem.Parent == "5485a8684bdc2da71d8b4567")
			    {
				    TemplateItemProperties ammoProps = itemProperties;

				    ItemDescription[itemId].AdvancedAmmoInfoString = "Damage: " +
				                                                     ammoProps.Damage +
				                                                     "\nPenetration Power: " +
				                                                     ammoProps.PenetrationPower +
				                                                     "\nArmor Damage: " +
				                                                     ammoProps.ArmorDamage +
				                                                     (ammoProps.ProjectileCount > 1
					                                                     ? "\nProjectile Count: " +
					                                                       ammoProps.ProjectileCount
					                                                     : "") +
				                                                     (ammoProps.BuckshotBullets > 0
					                                                     ? "\nBuckshot Bullets: " +
					                                                       ammoProps.BuckshotBullets
					                                                     : "") +
				                                                     "\nInitial Speed: " +
				                                                     ammoProps.InitialSpeed +
				                                                     "\nSpeed Retardation: " +
				                                                     ammoProps.SpeedRetardation +
				                                                     "\nBallistic Coeficient: " +
				                                                     ammoProps.BallisticCoeficient +
				                                                     "\nAmmo Tooltip Class: " +
				                                                     ammoProps.AmmoTooltipClass +
				                                                     "\nFragmentation Chance: " +
				                                                     (Math.Round((ammoProps.FragmentationChance ?? 0) *
					                                                      100) + "%" +
				                                                      (ammoProps.MaxFragmentsCount > 1
					                                                      ? "\nMin Fragments Count: " +
					                                                        ammoProps.MinFragmentsCount +
					                                                        "\nMax Fragments Count: " +
					                                                        ammoProps.MaxFragmentsCount
					                                                      : "")) +
				                                                     "\nRicochet Chance: " +
				                                                     Math.Round((ammoProps.RicochetChance ?? 0) * 100) +
				                                                     "%" +
				                                                     "\nMisfire Chance: " +
				                                                     Math.Round((ammoProps.MisfireChance ?? 0) * 100) +
				                                                     "%" +
				                                                     "\nMalf Feed Chance: " +
				                                                     Math.Round((ammoProps.MalfFeedChance ?? 0) * 100) +
				                                                     "%" +
				                                                     "\nMalf Misfire Chance: " +
				                                                     Math.Round(
					                                                     (ammoProps.MalfMisfireChance ?? 0) * 100) +
				                                                     "%" +
				                                                     "\nDurability Burn Modificator: " +
				                                                     ammoProps.DurabilityBurnModificator +
				                                                     "\nHeat Factor: " +
				                                                     ammoProps.HeatFactor +
				                                                     "\nHeavy blleding Delta: " +
				                                                     ammoProps.HeavyBleedingDelta +
				                                                     "\nLight Bleeding Delta: " +
				                                                     ammoProps.LightBleedingDelta +
				                                                     "\nStamina Burn Per Damage: " +
				                                                     ammoProps.StaminaBurnPerDamage +
				                                                     (ammoProps.Tracer ?? false
					                                                     ? "\nTracer: Yes" +
					                                                       "\nTracer Color: " +
					                                                       ammoProps.TracerColor +
					                                                       "\nTracer Distance: " +
					                                                       ammoProps.TracerDistance
					                                                     : "Tracer: No") +
				                                                     "\nPenetration Chance Obstacle: " +
				                                                     ammoProps.PenetrationChanceObstacle +
				                                                     "\nPenetration Damage Mod: " +
				                                                     ammoProps.PenetrationDamageMod +
				                                                     "\nPenetration Power Diviation: " +
				                                                     ammoProps.PenetrationPowerDiviation +
				                                                     "\nAccr(?): " +
				                                                     ammoProps.AmmoAccr +
				                                                     "\nDist(?): " +
				                                                     ammoProps.AmmoDist +
				                                                     "\nHear(?): " +
				                                                     ammoProps.AmmoHear +
				                                                     "\nRec(?): " +
				                                                     ammoProps.AmmoRec +
				                                                     "\nShift Chance(?): " +
				                                                     ammoProps.AmmoShiftChance +
				                                                     (ammoProps.ExplosionStrength > 0
					                                                     ? "\nExplosion Strength: " +
					                                                       ammoProps.ExplosionStrength +
					                                                       "\nMax Explosion Distance: " +
					                                                       ammoProps.MaxExplosionDistance +
					                                                       "\nExplosion Type: " +
					                                                       ammoProps.ExplosionType +
					                                                       "\nHasGrenadeComponent: " +
					                                                       ammoProps.HasGrenaderComponent
					                                                     : "") +
				                                                     "\nBullet Mass Gram: " +
				                                                     ammoProps.BulletMassGram +
				                                                     "\nBullet Diameter Millimeters: " +
				                                                     ammoProps.BulletDiameterMilimeters +
				                                                     "\nWeight: " +
				                                                     ammoProps.Weight +
				              
				                                                     "\n\n";
			    }
		    }

		    if (Config.ModContainerInfo.Enabled)
		    {
			    if (itemProperties.Grids is not null)
			    {
				    IEnumerable<Grid> gridsIe = itemProperties.Grids;
				    List<Grid> grids = gridsIe.ToList();

				    if (grids.Count > 0)
				    {
					    int totalSlots = 0;

					    foreach (Grid grid in grids)
					    {
						    totalSlots += grid.Properties?.CellsH * grid.Properties?.CellsV ?? 0;
					    }

					    double slotEfficiency = Math.Round((double)totalSlots / ((itemProperties.Width ?? 1) * (itemProperties.Height ?? 1)) , 2);

					    ItemDescription[itemId].SlotEfficiencyString = i18n["Slotefficiency"] +
					                                                   ": x" +
					                                                   slotEfficiency +
					                                                   " (" +
					                                                   totalSlots +
					                                                   "/" +
					                                                   itemProperties.Width * itemProperties.Height +
					                                                   ")\n\n";
				    }
			    }
		    }

		    if (Config.ModMarkValuableItems.Enabled)
		    {
			    if (Config.ModSoftcoreAmmoStackMultiFix.Enabled &&
			        itemHelper.IsOfBaseclass(itemId, BaseClasses.AMMO) &&
			        itemProperties.StackMaxSize > 1)
				    slotDensity *= 10;

			    double itemValue = traderPrice / slotDensity;

			    double fleaValue;

			    if (isBanned)
			    {
				    fleaValue = Utils.GetFleaPrice(itemId) / slotDensity ?? 0;

				    if (Config.ModMarkValuableItems.AlwaysMarkBannedItems)
					    fleaValue = Config.ModMarkValuableItems.FleaSlotValueThresholdBest + 1;
			    }
			    else
			    {
				    fleaValue = fleaPrice / slotDensity;
			    }

			    if (templateItem.Parent != "5795f317245977243854e041")
			    {
				    bool useAlt = UserLocale is "jp" or "kr" || Config.ModMarkValuableItems.UseAltValueMark;
				    string mark = "";

				    if (itemValue > Config.ModMarkValuableItems.TraderSlotValueThresholdBest ||
				        fleaValue > Config.ModMarkValuableItems.FleaSlotValueThresholdBest)
				    {
					    mark = useAlt
						    ? Config.ModMarkValuableItems.AltBestValueMark
						    : Config.ModMarkValuableItems.BestValueMark;
				    }
				    else if (itemValue > Config.ModMarkValuableItems.TraderSlotValueThresholdGood ||
				             fleaValue > Config.ModMarkValuableItems.FleaSlotValueThresholdGood)
				    {
					    mark = useAlt
						    ? Config.ModMarkValuableItems.AltGoodValueMark
						    : Config.ModMarkValuableItems.GoodValueMark;
				    }

				    if (!string.IsNullOrEmpty(mark))
				    {
					    if (Config.ModMarkValuableItems.AddToShortName)
						    Utils.AddToShortName(itemId, mark + " ", "prepend");

					    if (Config.ModMarkValuableItems.AddToName)
						    Utils.AddToName(itemId, " " + mark, "append");
				    }
			    }
		    }

		    if (Config.ModPriceInfo.Enabled)
			    ItemDescription[itemId].PriceString = (Config.ModPriceInfo.AddFleaPrice
				                                          ? i18n["Fleaprice"] +
				                                            ": " +
				                                            (fleaPriceString == i18n["BANNED"]
					                                            ? "BANNED"
					                                            : Utils.FormatPrice(fleaPrice) +
					                                              (fleaPrice > 0 ? "₽" : "")) +
				                                            " | "
				                                          : "") +
			                                          (Config.ModPriceInfo.AddItemValue
				                                          ? i18n["ItemValue"] +
				                                            ": " +
				                                            Utils.FormatPrice(itemInHandbook.Price ?? 0) +
				                                            " | "
				                                          : "") +
			                                          i18n["Valuation1"] +
			                                          itemBestTraderName +
			                                          i18n["Valuation2"] +
			                                          ": " +
			                                          Utils.FormatPrice(traderPrice) +
			                                          "₽";

		    if (Config.ModHeadsetInfo.Enabled)
		    {
			    if (itemProperties.Distortion is not null)
			    {
				    double? gain = itemProperties.CompressorGain;
				    double? thresh = itemProperties.CompressorThreshold;

				    ItemDescription[itemId].HeadsetDescription = "<color=#f59542>" +
				                                                 i18n["AmbientVolume"] +
				                                                 ": " +
				                                                 Math.Round(
					                                                 ((itemProperties.AmbientCompressorSendLevel ??
					                                                   -10) + 10 +
					                                                  (itemProperties
						                                                   .EffectsReturnsGrEnvCommonCompressorSendLeveloupVolume ??
					                                                   -7) + 7 +
					                                                  (itemProperties.EnvNatureCompressorSendLevel ??
					                                                   -5) + 5 +
					                                                  (itemProperties.EnvTechnicalCompressorSendLevel ??
					                                                   -7) + 7) * 10) / 10 +
				                                                 "db</color> | " +
				                                                 i18n["Boost"] +
				                                                 ": +" +
				                                                 gain + Math.Abs((thresh ?? -20) + 20) +
				                                                 "db" +
				                                                 (itemProperties.Distortion > 0
					                                                 ? " | " +
					                                                   i18n["Distortion"] +
					                                                   ": " +
					                                                   Math.Round(
						                                                   (itemProperties.Distortion ?? 0) * 100) +
					                                                   "%"
					                                                 : "") +
				                                                 "\n\n";
			    }
		    }

		    if (Config.ModBarterInfo.Enabled)
		    {
			    if (barterInfo.Item2.Length > 1)
				    ItemDescription[itemId].BarterString = barterInfo.Item2 + "\n";
		    }
		    
		    if (Config.ModQuestInfo.Enabled)
		    {
			    if (itemQuestInfo.Length > 1)
			    {
				    ItemDescription[itemId].UsedForQuestsString = itemQuestInfo + "\n";

				    if (Config.ModQuestInfo.FirInName &&
				        itemQuestInfo.Contains("✔"))
					    Utils.AddToName(itemId, "✔", "append");
			    }
		    }
		    
		    // Rarity recolor handling
		    if (Config.ModRarityRecolor.Enabled &&
		        !Config.RarityRecolorBlacklist.Contains(templateItem.Parent))
		    {
			    if ((!Config.ModRarityRecolor.BypassAmmoRecolor ||
			         templateItem.Parent != BaseClasses.AMMO) &&
			        (!Config.ModRarityRecolor.BypassKeysRecolor ||
			         (templateItem.Parent != BaseClasses.KEY_MECHANICAL &&
			          templateItem.Parent != BaseClasses.KEYCARD)))
			    {
				    foreach (KeyValuePair<string, int> customItem in Config.ModRarityRecolor.CustomRarity)
				    {
					    if (customItem.Key == itemId)
						    itemRarity = customItem.Value;
				    }
				    
				    string tier;
				    
				    switch (itemRarity)
				    {
					    case 7:
						    tier = i18n["OVERPOWERED"];
						    itemProperties.BackgroundColor = TiersHex["OVERPOWERED"];
						    tiersHexCode.Clear().Append(TiersHex["OVERPOWERED"]);
						    break;
					    case 1:
						    tier = i18n["COMMON"];
						    itemProperties.BackgroundColor = TiersHex["COMMON"];
						    tiersHexCode.Clear().Append(TiersHex["COMMON"]);
						    break;
					    case 2:
						    tier = i18n["RARE"];
						    itemProperties.BackgroundColor = TiersHex["RARE"];
						    tiersHexCode.Clear().Append(TiersHex["RARE"]);
						    break;
					    case 3:
						    tier = i18n["EPIC"];
						    itemProperties.BackgroundColor = TiersHex["EPIC"];
						    tiersHexCode.Clear().Append(TiersHex["EPIC"]);
						    break;
					    case 4:
						    tier = i18n["LEGENDARY"];
						    itemProperties.BackgroundColor = TiersHex["LEGENDARY"];
						    tiersHexCode.Clear().Append(TiersHex["LEGENDARY"]);
						    break;
					    case 5:
						    tier = i18n["UBER"];
						    itemProperties.BackgroundColor = TiersHex["UBER"];
						    tiersHexCode.Clear().Append(TiersHex["UBER"]);
						    break;
					    case 6:
						    tier = i18n["UNOBTAINIUM"];
						    itemProperties.BackgroundColor = TiersHex["UNOBTAINIUM"];
						    tiersHexCode.Clear().Append(TiersHex["UNOBTAINIUM"]);
						    break;
					    case 8:
						    tier = i18n["CUSTOM"];
						    itemProperties.BackgroundColor = TiersHex["CUSTOM"];
						    tiersHexCode.Clear().Append(TiersHex["CUSTOM"]);
						    break;
					    default: // itemRarity >= 9 or itemRarity == 0 with fallback disabled
						    tier = i18n["CUSTOM2"];
						    itemProperties.BackgroundColor = TiersHex["CUSTOM2"];
						    tiersHexCode.Clear().Append(TiersHex["CUSTOM2"]);
						    break;
				    }

				    // Rarity recolor fallback handling
				    if (Config.ModRarityRecolor.FallbackValueBasedRecolor &&
				        itemRarity == 0)
				    {
					    double itemValue = itemInHandbook.Price ?? 0;
					    int itemSlots = itemProperties.Width * itemProperties.Height ?? 0;

					    if (itemSlots > 1)
						    itemValue = Math.Round(itemValue / itemSlots);

					    if (templateItem.Parent == "543be5cb4bdc2deb348b4568")
						    itemValue = traderPrice;

					    switch (itemValue)
					    {
						    case var _ when itemValue < int.Parse(Tiers["COMMON_VALUE_FALLBACK"]):
							    tier = i18n["COMMON"];
							    itemProperties.BackgroundColor = TiersHex["COMMON"];
							    tiersHexCode.Clear().Append(TiersHex["COMMON"]);
							    break;
						    case var _ when itemValue < int.Parse(Tiers["RARE_VALUE_FALLBACK"]):
							    tier = i18n["RARE"];
							    itemProperties.BackgroundColor = TiersHex["RARE"];
							    tiersHexCode.Clear().Append(TiersHex["RARE"]);
							    break;
						    case var _ when itemValue < int.Parse(Tiers["EPIC_VALUE_FALLBACK"]):
							    tier = i18n["EPIC"];
							    itemProperties.BackgroundColor = TiersHex["EPIC"];
							    tiersHexCode.Clear().Append(TiersHex["EPIC"]);
							    break;
						    case var _ when itemValue < int.Parse(Tiers["LEGENDARY_VALUE_FALLBACK"]):
							    tier = i18n["LEGENDARY"];
							    itemProperties.BackgroundColor = TiersHex["LEGENDARY"];
							    tiersHexCode.Clear().Append(TiersHex["LEGENDARY"]);
							    break;
						    case var _ when itemValue < int.Parse(Tiers["UBER_VALUE_FALLBACK"]):
							    tier = i18n["UBER"];
							    itemProperties.BackgroundColor = TiersHex["UBER"];
							    tiersHexCode.Clear().Append(TiersHex["UBER"]);
							    break;
						    default:
							    tier = i18n["UNOBTAINIUM"];
							    itemProperties.BackgroundColor = TiersHex["UNOBTAINIUM"];
							    tiersHexCode.Clear().Append(TiersHex["UNOBTAINIUM"]);
							    break;
					    }
				    }
				    
				    if (Config.ModRarityRecolor.AddColorToName)
						Utils.AddColorToName(itemId, tiersHexCode.ToString(), UserLocale);
				    
				    Utils.AddColorToShortName(itemId, TiersHex["COMMON"], UserLocale);

				    if (Config.ModRarityRecolor.AddTierNameToPricesInfo &&
				        !string.IsNullOrEmpty(tier))
				    {
					    ItemDescription[itemId].PriceString += " | " +
					                                           "<color=" +
					                                           tiersHexCode +
					                                           ">" +
					                                           tier +
					                                           "</color>\n\n";
				    }
			    }
		    }
	    }
	    
	    logger.Info("[ItemInfo] Processing additional info...");

	    foreach (KeyValuePair<MongoId, TemplateItem> kvp in Items)
	    {
		    
		    descriptionString.Clear();

		    MongoId itemId = kvp.Key;
		    TemplateItem templateItem = kvp.Value;
		    HandbookItem? itemInHandbook = Utils.GetItemInHandbook(itemId);
		    TemplateItemProperties? itemProperties = templateItem.Properties;
		    
		    if (itemProperties is null)
			    continue;

		    bool isQuestItem = itemProperties.QuestItem ?? false;

		    if (templateItem.Type != "Item" || // Check if the item is a real item and not a "node" type.
		        itemInHandbook is null || // Ignore "useless" items
		        isQuestItem || // Ignore quest items.
		        templateItem.Parent == "543be5dd4bdc2deb348b4569") // Ignore currencies.
			    continue;
		    
		    if (Config.ModHideoutInfo.Enabled)
		    {
			    string itemHideoutInfo = Utils.HideoutInfoGenerator(itemId, UserLocale);

			    if (itemHideoutInfo.Length > 1)
				    ItemDescription[itemId].UsedForHideoutString = itemHideoutInfo + "\n";
		    }
		    
		    if (Config.ModProductionInfo.Enabled)
		    {
			    string productionInfo = Utils.ProductionGenerator(itemId, UserLocale);

			    if (productionInfo.Length > 1)
				    ItemDescription[itemId].ProductionString = productionInfo + "\n";
		    }
		    
		    if (Config.ModCraftingMaterialInfo.Enabled)
		    {
			    string itemCraftingMaterialInfo = Utils.CraftingMaterialInfoGenerator(itemId, UserLocale);

			    if (itemCraftingMaterialInfo.Length > 1)
				    ItemDescription[itemId].UsedForCraftingString = itemCraftingMaterialInfo + "\n";
		    }
		    
		    if (Config.ModBarterResourceInfo.Enabled)
		    {
			    string barterResourceInfo = Utils.BarterResourceInfoGenerator(itemId, UserLocale);
			    
			    if (barterResourceInfo.Length > 1)
				    ItemDescription[itemId].UsedForBarterString = barterResourceInfo + "\n";
		    }
		    
		    descriptionString.Append(ItemDescription[itemId].PriceString +
		                             ItemDescription[itemId].HeadsetDescription +
		                             ItemDescription[itemId].ArmorDurabilityString +
		                             ItemDescription[itemId].SlotEfficiencyString +
		                             ItemDescription[itemId].UsedForQuestsString +
		                             ItemDescription[itemId].UsedForHideoutString +
		                             ItemDescription[itemId].BarterString +
		                             ItemDescription[itemId].ProductionString +
		                             ItemDescription[itemId].UsedForCraftingString +
		                             ItemDescription[itemId].UsedForBarterString +
		                             ItemDescription[itemId].AdvancedAmmoInfoString);
		    
		    Utils.AddToDescription(itemId, descriptionString.ToString(), "prepend");
	    }
    }
}

public sealed class CacheFile
{
	public string Fingerprint { get; set; } = string.Empty;
	public string Locale { get; set; } = string.Empty;
	public List<CachedItemInfoPatch> Items { get; set; } = [];
}

public sealed class CachedItemInfoPatch
{
	public string Id { get; set; } = string.Empty;
	public string? Name { get; set; }
	public string? ShortName { get; set; }
	public string DescriptionPrefix { get; set; } = string.Empty;
	public string? BackgroundColor { get; set; }
}

public class ModTiers : Dictionary<string, string>;

public class ModTiersHex : Dictionary<string, string>;
