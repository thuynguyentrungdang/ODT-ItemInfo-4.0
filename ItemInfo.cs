using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
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

/// <summary>
/// This is the replacement for the former package.json data. This is required for all mods.
///
/// This is where we define all the metadata associated with this mod.
/// You don't have to do anything with it, other than fill it out.
/// All properties must be overriden, properties you don't use may be left null.
/// It is read by the mod loader when this mod is loaded.
/// </summary>
public record ModMetadata : AbstractModMetadata
{
    /// <summary>
    /// Any string can be used for a modId, but it should ideally be unique and not easily duplicated
    /// a 'bad' ID would be: "mymod", "mod1", "questmod"
    /// It is recommended (but not mandatory) to use the reverse domain name notation,
    /// see: https://docs.oracle.com/javase/tutorial/java/package/namingpkgs.html
    /// </summary>
    public override string ModGuid { get; init; } = "com.odt.iteminfo";
    public override string Name { get; init; } = "ItemInfo";
    public override string Author { get; init; } = "ODT";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("2.0.0");
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
	private static readonly List<string> BsgBlacklist = [
		"544a11ac4bdc2d470e8b456a", // Secure container Alpha
		"544a37c44bdc2d25388b4567", // 5.56x45 SureFire MAG5-60 STANAG 60-round magazine
		"54527ac44bdc2d36668b4567", // 5.56x45mm M855A1
		"545cdb794bdc2d3a198b456a", // 6B43 Zabralo-Sh body armor (Digital Flora)
		"560d61e84bdc2da74d8b4571", // 7.62x54mm R SNB gzh
		"5648b62b4bdc2d9d488b4585", // GP-34 40mm underbarrel grenade launcher
		"56dfef82d2720bbd668b4567", // 5.45x39mm BP gs
		"56dff026d2720bb8668b4567", // 5.45x39mm BS gs
		"5732ee6a24597719ae0c0281", // Waist pouch
		"57372b832459776701014e41", // 5.45x39mm BS gs ammo pack (120 pcs)
		"57372bad245977670b7cd242", // 5.45x39mm BS gs ammo pack (120 pcs)
		"57372bd3245977670b7cd243", // 5.45x39mm BS gs ammo pack (30 pcs)
		"57372c21245977670937c6c2", // 5.45x39mm BT gs ammo pack (120 pcs)
		"57372c56245977685e584582", // 5.45x39mm BT gs ammo pack (120 pcs)
		"57372c89245977685d4159b1", // 5.45x39mm BT gs ammo pack (30 pcs)
		"57838ad32459774a17445cd2", // VSS Vintorez 9x39 special sniper rifle
		"57a0e5022459774d1673f889", // 9x39mm SP-6 gs
		"5857a8b324597729ab0a0e7d", // Secure container Beta
		"5857a8bc2459772bad15db29", // Secure container Gamma
		"58dd3ad986f77403051cba8f", // 7.62x51mm M80
		"5937ee6486f77408994ba448", // Machinery key
		"593962ca86f774068014d9af", // Unknown key
		"59c1383d86f774290a37e0ca", // 5.56x45 Magpul PMAG D-60 STANAG 60-round magazine
		"59db794186f77448bc595262", // Secure container Epsilon
		"59e0d99486f7744a32234762", // 7.62x39mm BP gzh
		"59e690b686f7746c9f75e848", // 5.56x45mm M995
		"59e763f286f7742ee57895da", // Pilgrim tourist backpack
		"59e77a2386f7742ee578960a", // 7.62x54mm R PS gzh
		"59f32bb586f774757e1e8442", // Dogtag BEAR
		"59f32c3b86f77472a31742f0", // Dogtag USEC
		"59faff1d86f7746c51718c9c", // Physical Bitcoin
		"5a154d5cfcdbcb001a3b00da", // Ops-Core FAST MT Super High Cut helmet (Black)
		"5a16b7e1fcdbcb00165aa6c9", // Ops-Core FAST multi-hit ballistic face shield
		"5a1eaa87fcdbcb001865f75e", // Trijicon REAP-IR thermal scope
		"5a6086ea4f39f99cd479502f", // 7.62x51mm M61
		"5a608bf24f39f98ffc77720e", // 7.62x51mm M62 Tracer
		"5aa7e276e5b5b000171d0647", // Altyn bulletproof helmet (Olive Drab)
		"5aafbcd986f7745e590fff23", // Medicine case
		"5ab8ebf186f7742d8b372e80", // SSO Attack 2 raid backpack (Khaki)
		"5ac8d6885acfc400180ae7b0", // Ops-Core FAST MT Super High Cut helmet (Urban Tan)
		"5b4329f05acfc47a86086aa1", // DevTac Ronin Respirator
		"5b6d9ce188a4501afc1b2b25", // T H I C C Weapon case
		"5b7c710788a4506dec015957", // Lucky Scav Junk box
		"5ba26835d4351e0035628ff5", // 4.6x30mm AP SX
		"5c0558060db834001b735271", // L3Harris GPNVG-18 night vision goggles
		"5c0919b50db834001b7ce3b9", // Maska-1SCh face shield (Olive Drab)
		"5c093ca986f7740a1867ab12", // Secure container Kappa
		"5c0a840b86f7742ffa4f2482", // T H I C C item case
		"5c0d5e4486f77478390952fe", // 5.45x39mm PPBS gs Igolnik
		"5c0d688c86f77413ae3407b2", // 9x39mm BP gs
		"5c0e66e2d174af02a96252f4", // Ops-Core SLAAP armor helmet plate (Tan)
		"5c0e774286f77468413cc5b2", // Mystery Ranch Blackjack 50 backpack (MultiCam)
		"5c0e805e86f774683f3dd637", // 3V Gear Paratus 3-Day Operator's Tactical backpack (Foliage Grey)
		"5c1260dc86f7746b106e8748", // 9x39mm BP gs ammo pack (8 pcs)
		"5c1262a286f7743f8a69aab2", // 5.45x39mm PPBS gs Igolnik ammo pack (30 pcs)
		"5c17a7ed2e2216152142459c", // Crye Precision AirFrame helmet (Tan)
		"5c6175362e221600133e3b94", // AK 7.62x39 ProMag AK-A-16 73-round drum magazine
		"5ca20ee186f774799474abc2", // Vulkan-5 LShZ-5 bulletproof helmet (Black)
		"5ca2113f86f7740b2547e1d2", // Vulkan-5 helmet face shield
		"5ca21c6986f77479963115a7", // FORT Redut-T5 body armor (Smog)
		"5cadf6eeae921500134b2799", // 12.7x55mm PS12B
		"5cc80f67e4a949035e43bbba", // 5.7x28mm SB193
		"5cfe8010d7ad1a59283b14c6", // AK 7.62x39 X Products X-47 50-round drum magazine
		"5d1b5e94d7ad1a2b865a96b0", // FLIR RS-32 2.25-9x 35mm 60Hz thermal riflescope
		"5d6e68a8a4b9360b6c0d54e2", // 12/70 AP-20 armor-piercing slug
		"5d6e68b3a4b9361bca7e50b5", // 12/70 Copper Sabot Premier HP slug
		"5dcbd56fdbd3d91b3e5468d5", // Desert Tech MDR 7.62x51 assault rifle
		"5df8a4d786f77412672a1e3b", // 6Sh118 raid backpack (Digital Flora)
		"5df8a6a186f77412640e2e80", // Christmas tree ornament (Red)
		"5df8a72c86f77412640e2e83", // Christmas tree ornament (Silver)
		"5df8a77486f77412672a1e3f", // Christmas tree ornament (Violet)
		"5e00c1ad86f774747333222c", // Team Wendy EXFIL Ballistic Helmet (Black)
		"5e00cdd986f7747473332240", // Team Wendy EXFIL Ballistic face shield (Black)
		"5e01ef6886f77445f643baa4", // Team Wendy EXFIL Ballistic Helmet (Coyote Brown)
		"5e01f37686f774773c6f6c15", // Team Wendy EXFIL Ballistic face shield (Coyote Brown)
		"5e023d34e8a400319a28ed44", // 7.62x54mm R BT gzh
		"5e023d48186a883be655e551", // 7.62x54mm R BS gs
		"5e81ebcd8e146c7080625e15", // FN40GL Mk2 40mm grenade launcher
		"5e848cc2988a8701445df1e8", // TOZ KS-23M 23x75mm pump-action shotgun
		"5e85a9f4add9fe03027d9bf1", // 23x75mm Zvezda flashbang round
		"5ea18c84ecf1982c7712d9a2", // Diamond Age Bastion helmet armor plate
		"5ede47405b097655935d7d16", // 40x46mm M441 (HE) grenade
		"5ede474b0c226a66f5402622", // 40x46mm M381 (HE) grenade
		"5efb0c1bd79ff02a1f5e68d9", // 7.62x51mm M993
		"5f0596629e22f464da6bbdd9", // .366 TKM AP-M
		"5f0c892565703e5c461894e9", // 40x46mm M433 (HEDP) grenade
		"5f5e46b96bdad616ad46d613", // Eberlestock F4 Terminator load bearing backpack (Tiger Stripe)
		"5f60b34a41e30a4ab12a6947", // Galvion Caiman Hybrid helmet (Grey)
		"5f60c74e3b85f6263c145586", // Rys-T bulletproof helmet (Black)
		"5f60c85b58eff926626a60f7", // Rys-T face shield
		"5fc22d7c187fea44d52eda44", // SWORD International Mk-18 .338 LM marksman rifle
		"5fc275cf85fd526b824a571a", // .338 Lapua Magnum FMJ
		"5fc382a9d724d907e2077dab", // .338 Lapua Magnum AP
		"5fd20ff893a8961fc660a954", // .300 Blackout AP
		"601949593ae8f707c4608daa", // 5.56x45mm SSA AP
		"601aa3d2b2bcb34913271e6d", // 7.62x39mm MAI AP
		"6034d2d697633951dc245ea6", // Eberlestock G2 Gunslinger II backpack (Dry Earth)
		"60a283193cb70855c43a381d", // NFM THOR Integrated Carrier body armor
		"60a7ad2a2198820d95707a2e", // Tagilla's welding mask "UBEY"
		"60a7ad3a0c5cb24b0134664a", // Tagilla's welding mask "Gorilla"
		"614451b71e5874611e2c7ae5", // Bottle of Tarkovskaya vodka
		"6165ac306ef05c2ce828ef74", // FN SCAR-H 7.62x51 assault rifle (FDE)
		"617fd91e5539a84ec44ce155", // RGN hand grenade
		"6183afd850224f204c1da514", // FN SCAR-H 7.62x51 assault rifle
		"618a431df1eb8e24b8741deb", // RGO hand grenade
		"61962b617c6c7b169525f168", // 5.45x39mm 7N40
		"61962d879bb3d20b0946d385", // 9x39mm PAB-9 gs
		"619bc61e86e01e16f839a999", // Armband (Alpha)
		"619bddc6c9546643a67df6ee", // Armband (DEADSKUL)
		"619bddffc9546643a67df6f0", // Armband (Train Hard)
		"619bde3dc9546643a67df6f2", // Armband (Kiba Arms)
		"619bdeb986e01e16f839a99e", // Armband (RFARMY)
		"619bdf9cc9546643a67df6f8", // Armband (UNTAR)
		"61b9e1aaef9a1b5d6a79899a", // Santa's bag
		"620109578d82e67e7911abf2", // ZiD SP-81 26x75 signal pistol
		"62178c4d4ecf221597654e3d", // RSP-30 reactive signal cartridge (Red)
		"62389aaba63f32501b1b444f", // 26x75mm flare cartridge (Green)
		"62389ba9a63f32501b1b4451", // 26x75mm flare cartridge (Red)
		"6275303a9f372d6ea97f9ec7", // Milkor M32A1 MSGL 40mm grenade launcher
		"627e14b21713922ded6f2c15", // Accuracy International AXMC .338 LM bolt-action sniper rifle
		"62963c18dbc8ab5f0d382d0b", // Death Knight mask
		"62a61bbf8ec41a51b34758d2", // Big Pipe's smoking pipe
		"62e7e7bbe6da9612f743f1e0", // GP-25 Kostyor 40mm underbarrel grenade launcher
		"62e910aaf957f2915e0a5e36", // Digital secure DSP radio transmitter
		"635267f063651329f75a4ee8", // 26x75mm flare cartridge (Acid Green)
		"6357c98711fb55120211f7e1", // M203 40mm underbarrel grenade launcher
		"6389c7750ef44505c87f5996", // Microcontroller board
		"6389c7f115805221fb410466", // Far-forward GPS Signal Amplifier Unit
		"6389c85357baa773a825b356", // Advanced current converter
		"6389c8fb46b54c634724d847", // Silicon Optoelectronic Integrated Circuits textbook
		"6389c92d52123d5dd17f8876", // Advanced Electronic Materials textbook
		"639346cc1c8f182ad90c8972", // Tasmanian Tiger Trooper 35 backpack (Khaki)
		"6398fd8ad3de3849057f5128", // Backup hideout key
		"63a0b2eabea67a6d93009e52", // Radio repeater
		"63a39e1d234195315d4020bd", // Primorsky 46-48 skybridge key
		"63fc44e2429a8a166c7f61e6", // Armasight Zeus-Pro 640 2-8x50 30Hz thermal scope
		"6410733d5dd49d77bd07847e", // Tokarev AVT-40 7.62x54R automatic rifle
		"64637076203536ad5600c990", // Kalashnikov PKM 7.62x54R machine gun
		"6478641c19d732620e045e17", // SIG Sauer ECHO1 1-2x30mm 30Hz thermal reflex scope
		"648983d6b5a2df1c815a04ec", // 12.7x55mm PS12B (10 pcs)
		"6489848173c462723909a14b", // .338 Lapua Magnum AP ammo pack (20 pcs)
		"648984b8d5b4df6140000a1a", // 7.62x54mm R BS ammo pack (20 pcs)
		"648984e3f09d032aa9399d53", // 7.62x51mm M993 ammo pack (20 pcs)
		"6489851fc827d4637f01791b", // 7.62x39mm MAI AP ammo pack (20 pcs)
		"6489854673c462723909a14e", // 9x39mm BP ammo pack (20 pcs)
		"64898583d5b4df6140000a1d", // 5.56x45mm SSA AP ammo pack (50 pcs)
		"648985c074a806211e4fb682", // .300 Blackout AP ammo pack (50 pcs)
		"64898602f09d032aa9399d56", // 5.45x39mm 7N40 ammo pack (30 pcs)
		"648986bbc827d4637f01791e", // 5.7x28mm SS190 ammo pack (50 pcs)
		"6489870774a806211e4fb685", // 4.6x30mm AP SX ammo pack (40 pcs)
		"6489875745f9ca4ba51c4808", // 9x21mm BT ammo pack (30 pcs)
		"6489879db5a2df1c815a04ef", // .45 ACP AP ammo pack (50 pcs)
		"648987d673c462723909a151", // 9x19mm PBP ammo pack (50 pcs)
		"64898838d5b4df6140000a20", // 12/70 AP-20 ammo pack (25 pcs)
		"64acea16c4eda9354b0226b0", // 7.62x39mm BP gzh ammo pack (20 pcs)
		"64afc71497cf3a403c01ff38", // Granit Br5 ballistic plate
		"64afdcb83efdfea28601d041", // ESAPI level IV ballistic plate
		"64b8725c4b75259c590fa899", // .300 Blackout CBJ
		"64ca3d3954fc657e230529cc", // Kalashnikov PKP 7.62x54R infantry machine gun
		"64d0b40fbe2eed70e254e2d4", // Sacred Amulet
		"64d4b23dc1b37504b41ac2b6", // Rusted bloody key
		"651450ce0e00edc794068371", // SR-3M 9x39 compact assault rifle
		"65268d8ecb944ff1e90ea385", // Degtyarev RPDN 7.62x39 machine gun
		"65290f395ae2ae97b80fdf2d", // SIG MCX-SPEAR 6.8x51 assault rifle
		"6529243824cbe3c74a05e5c1", // 6.8x51mm SIG Hybrid
		"654a4a964b446df1ad03f192", // Granit 4RS ballistic plates (Back)
		"65573fa5655447403702a816", // Granit Br4 ballistic plate
		"655746010177119f4a097ff7", // SAPI level III+ ballistic plate
		"656efaf54772930db4031ff5", // Granit 4 ballistic plates (Back)
		"656f611f94b480b8a500c0db", // Granit 4 ballistic plate (Front)
		"656f63c027aed95beb08f62c", // Granit 4RS ballistic plate (Front)
		"656f664200d62bcd2e024077", // Korund-VM ballistic plates (Front)
		"656f66b5c6baea13cd07e108", // Korund-VM-K ballistic plates (Front)
		"656fa53d94b480b8a500c0e4", // TallCom Guardian ballistic plate
		"656fa61e94b480b8a500c0e8", // NESCO 4400-SA-MC ballistic plate
		"656fa76500d62bcd2e024080", // Kiba Arms Steel ballistic plate
		"656fa8d700d62bcd2e024084", // Cult Locust ballistic plate
		"656fa99800d62bcd2e024088", // Cult Termite ballistic plate
		"656fae5f7c2d57afe200c0d7", // GAC 3s15m ballistic plate
		"656faf0ca0dce000a2020f77", // GAC 4sss2 ballistic plate
		"656fafe3498d1b7e3e071da4", // KITECO SC-IV SA ballistic plate
		"657023a9126cc4a57d0e17a6", // .300 Blackout CBJ ammo pack (50 pcs)
		"657023b1cfc010a0f50069e5", // .300 Blackout M62 Tracer ammo pack (50 pcs)
		"657023b71419851aef03e6e8", // .300 Blackout V-Max ammo pack (50 pcs)
		"657023bebfc87b3a34093207", // .300 Blackout BCP FMJ ammo pack (50 pcs)
		"657023c61419851aef03e6eb", // .300 Whisper ammo pack (50 pcs)
		"657023ccbfc87b3a3409320a", // .338 Lapua Magnum FMJ ammo pack (20 pcs)
		"657023d6cfc010a0f50069e9", // .338 Lapua Magnum TAC-X ammo pack (20 pcs)
		"657023dabfc87b3a3409320d", // .338 Lapua Magnum UCW ammo pack (20 pcs)
		"657023decfc010a0f50069ec", // .357 Magnum FMJ ammo pack (25 pcs)
		"657023e31419851aef03e6ee", // .357 Magnum HP ammo pack (25 pcs)
		"657023e7c5d7d4cb4d078552", // .357 Magnum JHP ammo pack (25 pcs)
		"657023eccfc010a0f50069ef", // .357 Magnum SP ammo pack (25 pcs)
		"657023f1bfc87b3a34093210", // .366 TKM FMJ ammo pack (20 pcs)
		"657023f81419851aef03e6f1", // .366 TKM AP-M ammo pack (20 pcs)
		"657023fcbfc87b3a34093213", // .366 TKM Geksa ammo pack (20 pcs)
		"657024011419851aef03e6f4", // .366 TKM EKO ammo pack (20 pcs)
		"65702406bfc87b3a34093216", // .45 ACP Hydra-Shok ammo pack (50 pcs)
		"6570240a1419851aef03e6f7", // .45 ACP Lasermatch FMJ ammo pack (50 pcs)
		"6570240ecfc010a0f50069f2", // .45 ACP Match FMJ ammo pack (50 pcs)
		"65702414c5d7d4cb4d078555", // .45 ACP RIP ammo pack (50 pcs)
		"6570241bcfc010a0f50069f5", // 12.7x55mm PS12 ammo pack (10 pcs)
		"65702420bfc87b3a34093219", // 12.7x55mm PS12A ammo pack (10 pcs)
		"65702426cfc010a0f50069f8", // 12/70 5.25mm buckshot ammo pack (25 pcs)
		"65702432bfc87b3a3409321c", // 12/70 6.5mm Express buckshot ammo pack (25 pcs)
		"657024361419851aef03e6fa", // 12/70 7mm buckshot ammo pack (25 pcs)
		"6570243bbfc87b3a3409321f", // 12/70 8.5mm Magnum buckshot ammo pack (25 pcs)
		"6570243fcfc010a0f50069fb", // 12/70 Dual Sabot slug ammo pack (25 pcs)
		"657024431419851aef03e6fd", // 12/70 Piranha ammo pack (25 pcs)
		"65702449bfc87b3a34093223", // 12/70 FTX Custom Lite slug ammo pack (25 pcs)
		"6570244ec5d7d4cb4d078558", // 12/70 Grizzly 40 slug ammo pack (25 pcs)
		"65702452cfc010a0f50069fe", // 12/70 Poleva-3 slug ammo pack (25 pcs)
		"657024581419851aef03e700", // 12/70 Poleva-6u slug ammo pack (25 pcs)
		"65702469c5d7d4cb4d07855b", // 12/70 makeshift .50 BMG slug ammo pack (25 pcs)
		"6570246fcfc010a0f5006a01", // 12/70 lead slug ammo pack (25 pcs)
		"65702474bfc87b3a34093226", // 12/70 flechette ammo pack (25 pcs)
		"65702479c5d7d4cb4d07855e", // 12/70 Copper Sabot Premier HP slug ammo pack (25 pcs)
		"6570247ebfc87b3a34093229", // 12/70 SuperFormance HP slug ammo pack (25 pcs)
		"657024831419851aef03e703", // 20/70 5.6mm buckshot ammo pack (25 pcs)
		"6570248dcfc010a0f5006a04", // 20/70 6.2mm buckshot ammo pack (25 pcs)
		"657024921419851aef03e706", // 20/70 7.3mm buckshot ammo pack (25 pcs)
		"65702495c5d7d4cb4d078561", // 20/70 7.5mm buckshot ammo pack (25 pcs)
		"6570249bcfc010a0f5006a07", // 20/70 Devastator slug ammo pack (25 pcs)
		"6570249f1419851aef03e709", // 20/70 Star slug ammo pack (25 pcs)
		"657024a4bfc87b3a3409322c", // 20/70 Poleva-3 slug ammo pack (25 pcs)
		"657024a91419851aef03e70c", // 20/70 Poleva-6u slug ammo pack (25 pcs)
		"657024aebfc87b3a3409322f", // 23x75mm Shrapnel-10 buckshot ammo pack (5 pcs)
		"657024b31419851aef03e70f", // 23x75mm Shrapnel-25 buckshot ammo pack (5 pcs)
		"657024b8bfc87b3a34093232", // 23x75mm Barrikada slug ammo pack (5 pcs)
		"657024bdc5d7d4cb4d078564", // 23x75mm Zvezda flashbang round ammo pack (5 pcs)
		"657024c81419851aef03e712", // 4.6x30mm Action SX ammo pack (40 pcs)
		"657024cecfc010a0f5006a0a", // 4.6x30mm FMJ SX ammo pack (40 pcs)
		"657024d2bfc87b3a34093235", // 4.6x30mm Subsonic SX ammo pack (40 pcs)
		"657024d8c5d7d4cb4d078567", // 5.56x45mm FMJ ammo pack (50 pcs)
		"657024debfc87b3a34093238", // 5.56x45mm HP ammo pack (50 pcs)
		"657024e3c5d7d4cb4d07856a", // 5.56x45mm M855A1 ammo pack (50 pcs)
		"657024e8cfc010a0f5006a0d", // 5.56x45mm M856 ammo pack (50 pcs)
		"657024ecc5d7d4cb4d07856d", // 5.56x45mm M856A1 ammo pack (50 pcs)
		"657024f01419851aef03e715", // 5.56x45mm M995 ammo pack (50 pcs)
		"657024f5cfc010a0f5006a10", // 5.56x45mm MK 255 Mod 0 (RRLP) ammo pack (50 pcs)
		"657024f9bfc87b3a3409323b", // 5.56x45mm MK 318 Mod 0 (SOST) ammo pack (50 pcs)
		"657025161419851aef03e718", // 5.7x28mm L191 ammo pack (50 pcs)
		"6570251ccfc010a0f5006a13", // 5.7x28mm R37.F ammo pack (50 pcs)
		"65702520bfc87b3a3409323e", // 5.7x28mm R37.X ammo pack (50 pcs)
		"65702524cfc010a0f5006a16", // 5.7x28mm SB193 ammo pack (50 pcs)
		"657025281419851aef03e71b", // 5.7x28mm SS197SR ammo pack (50 pcs)
		"6570252dbfc87b3a34093241", // 5.7x28mm SS198LF ammo pack (50 pcs)
		"65702532cfc010a0f5006a19", // 7.62x25mm ТТ FMJ43 ammo pack (25 pcs)
		"65702536c5d7d4cb4d078570", // 7.62x25mm ТТ LRN ammo pack (25 pcs)
		"6570253acfc010a0f5006a1c", // 7.62x25mm ТТ LRNPC ammo pack (25 pcs)
		"6570253ec5d7d4cb4d078573", // 7.62x25mm TT AKBS ammo pack (25 pcs)
		"657025421419851aef03e71e", // 7.62x25mm TT P gl ammo pack (25 pcs)
		"65702546cfc010a0f5006a1f", // 7.62x25mm TT Pst gzh ammo pack (25 pcs)
		"6570254abfc87b3a34093244", // 7.62x25mm TT PT gzh ammo pack (25 pcs)
		"6570254fcfc010a0f5006a22", // 7.62x51mm M61 ammo pack (20 pcs)
		"65702554bfc87b3a34093247", // 7.62x51mm M62 Tracer ammo pack (20 pcs)
		"65702558cfc010a0f5006a25", // 7.62x51mm M80 ammo pack (20 pcs)
		"6570255dbfc87b3a3409324a", // 7.62x51mm Ultra Nosler ammo pack (20 pcs)
		"65702561cfc010a0f5006a28", // 7.62x51mm BCP FMJ ammo pack (20 pcs)
		"65702566bfc87b3a3409324d", // 7.62x51mm TCW SP ammo pack (20 pcs)
		"65702572c5d7d4cb4d078576", // 7.62x54mm R BT gzh ammo pack (20 pcs)
		"65702577cfc010a0f5006a2c", // 7.62x54mm R LPS gzh ammo pack (20 pcs)
		"6570257cc5d7d4cb4d078579", // 7.62x54mm R PS gzh ammo pack (20 pcs)
		"65702584cfc010a0f5006a2f", // 7.62x54mm R T-46M gzh ammo pack (20 pcs)
		"65702591c5d7d4cb4d07857c", // 9x19mm AP 6.3 ammo pack (50 pcs)
		"657025961419851aef03e721", // 9x19mm Green Tracer ammo pack (50 pcs)
		"6570259bc5d7d4cb4d07857f", // 9x19mm Luger CCI ammo pack (50 pcs)
		"6570259fcfc010a0f5006a32", // 9x19mm QuakeMaker ammo pack (50 pcs)
		"657025a4bfc87b3a34093250", // 9x19mm PSO gzh ammo pack (50 pcs)
		"657025a81419851aef03e724", // 9x19mm Pst gzh ammo pack (50 pcs)
		"657025bbcfc010a0f5006a35", // 9x21mm P gzh ammo pack (30 pcs)
		"657025c4c5d7d4cb4d078582", // 9x21mm PS gzh ammo pack (30 pcs)
		"657025c9cfc010a0f5006a38", // 9x21mm PE gzh ammo pack (30 pcs)
		"657025cfbfc87b3a34093253", // 9x39mm PAB-9 gs ammo pack (20 pcs)
		"657025d4c5d7d4cb4d078585", // 9x39mm SP-5 gs ammo pack (20 pcs)
		"657025dabfc87b3a34093256", // 9x39mm SP-6 gs ammo pack (20 pcs)
		"657025dfcfc010a0f5006a3b", // 9x39mm SPP gs ammo pack (20 pcs)
		"657025ebc5d7d4cb4d078588", // 5.45x39mm PPBS gs Igolnik ammo pack (120 pcs)
		"65702606cfc010a0f5006a3e", // 9x18mm PM BZhT gzh ammo pack (50 pcs)
		"6570260c1419851aef03e727", // 9x18mm PM P gzh ammo pack (50 pcs)
		"65702610cfc010a0f5006a41", // 9x18mm PM PBM gzh ammo pack (50 pcs)
		"65702614c5d7d4cb4d07858b", // 9x18mm PM PPT gzh ammo pack (50 pcs)
		"65702619bfc87b3a34093259", // 9x18mm PM PPe gzh ammo pack (50 pcs)
		"6570261dc5d7d4cb4d07858e", // 9x18mm PM PRS gs ammo pack (50 pcs)
		"65702621cfc010a0f5006a44", // 9x18mm PM PS gs PPO ammo pack (50 pcs)
		"657026251419851aef03e72a", // 9x18mm PM PSV ammo pack (50 pcs)
		"65702629cfc010a0f5006a47", // 9x18mm PM PSO gzh ammo pack (50 pcs)
		"6570262d1419851aef03e72d", // 9x18mm PM Pst gzh ammo pack (50 pcs)
		"65702630cfc010a0f5006a4a", // 9x18mm PM RG028 gzh ammo pack (50 pcs)
		"657026341419851aef03e730", // 9x18mm PM SP7 gzh ammo pack (50 pcs)
		"65702639bfc87b3a3409325c", // 9x18mm PM SP8 gzh ammo pack (50 pcs)
		"65702640cfc010a0f5006a4d", // 9x18mm PMM PstM gzh ammo pack (50 pcs)
		"657026451419851aef03e733", // 5.56x45mm FMJ ammo pack (100 pcs)
		"6570264acfc010a0f5006a50", // 5.56x45mm HP ammo pack (100 pcs)
		"6570264d1419851aef03e736", // 5.56x45mm M855 ammo pack (100 pcs)
		"65702652cfc010a0f5006a53", // 5.56x45mm M855A1 ammo pack (100 pcs)
		"65702656c5d7d4cb4d078591", // 5.56x45mm M856 ammo pack (100 pcs)
		"6570265bcfc010a0f5006a56", // 5.56x45mm M856A1 ammo pack (100 pcs)
		"6570265f1419851aef03e739", // 5.56x45mm M995 ammo pack (100 pcs)
		"65702664cfc010a0f5006a59", // 5.56x45mm MK 255 Mod 0 (RRLP) ammo pack (100 pcs)
		"6570266bc5d7d4cb4d078594", // 5.56x45mm MK 318 Mod 0 (SOST) ammo pack (100 pcs)
		"65702681bfc87b3a3409325f", // 5.56x45mm SSA AP ammo pack (100 pcs)
		"657089638db3adca1009f4ca", // Atomic Defense CQCM ballistic mask (Black)
		"6570900858b315e8b70a8a98", // 5.45x39mm 7N40 ammo pack (120 pcs)
		"65709d2d21b9f815e208ff95", // Diamond Age NeoSteel High Cut helmet (Black)
		"6570aead4d84f81fd002a033", // Death Shadow lightweight armored mask
		"65719f0775149d62ce0a670b", // NPP KlASS Tor-2 helmet (Olive Drab)
		"65719f9ef392ad76c50a2ec8", // NPP KlASS Tor-2 helmet face shield
		"6579846c1ec1943afb14c15a", // 9x21mm 7U4 ammo pack (30 pcs)
		"6579847c5a0e5879d12f2873", // 9x21mm 7N42 ammo pack (30 pcs)
		"657984a50fbff513dd435765", // 9x39mm FMJ ammo pack (20 pcs)
		"657b2797c3dbcb01d60c35ea", // Korund-VM ballistic plate (Back)
		"657b28d25f444d6dff0c6c77", // Korund-VM-K ballistic plate (Back)
		"65ca457b4aafb5d7fc0dcb5d", // United Cutlery M48 Tactical Kukri
		"65ddcc9cfa85b9f17d0dfb07", // Mark of The Unheard
		"660312cc4d6cdfa6f500c703", // Armband of The Unheard
		"660bbc98c38b837877075e4a", // Decrypted flash drive
		"660bc341c38b837877075e4c", // Documents with decrypted data
		"664a5428d5e33a713b622379", // APOK Tactical Wasteland Gladius
		"664a5480bfcc521bad3192ca", // Armband (ARENA)
		"664a55d84a90fc2c8a6305c9", // Secure container Theta
		"664d3db6db5dea2bad286955", // Shatun's hideout key
		"664d3dd590294949fe2d81b7", // Grumpy's hideout key
		"664d3ddfdda2e85aca370d75", // Voron's hideout key
		"664d3de85f2355673b09aed5", // Leon's hideout key
		"6655e35b6bc645cb7b059912", // "The Eye" mortar strike signaling device
		"6656560053eaaa7a23349c86", // Lega Medal
		"66571bf06a723f7f005a0619", // Locked equipment crate (Rare)
		"66572b3f6a723f7f005a066c", // Locked weapon crate (Rare)
		"66572b88ac60f009f270d1dc", // Locked supply crate (Rare)
		"66572bb3ac60f009f270d1df", // Locked valuables crate (Rare)
		"665730fa4de4820934746c48", // Unlocked equipment crate (Rare)
		"665732e7ac60f009f270d1ef", // Unlocked weapon crate (Rare)
		"665732f4464c4b4ba4670fa9", // Unlocked supply crate (Rare)
		"66573310a1657263d816a139", // Unlocked valuables crate (Rare)
		"665ee77ccf2d642e98220bca", // Secure container Gamma
		"6662e9aca7e0b43baa3d5f74", // Dogtag BEAR
		"6662e9cda7e0b43baa3d5f76", // Dogtag BEAR
		"6662e9f37fa79a6d83730fa0", // Dogtag USEC
		"6662ea05f6259762c56f3189", // Dogtag USEC
		"666b11055a706400b717cfa5", // Tripwire installation kit
		"66bc98a01a47be227a5e956e", // Streamer item case
		"66d9f1abb16d9aacf5068468", // RSP-30 reactive signal cartridge (Special Yellow)
		"66d9f7256916142b3b02276e", // Radar station spare parts
		"66d9f7e7099cf6adcc07a369", // KOSA UAV electronic jamming device
		"66d9f8744827a77e870ecaf1", // GARY ZONT portable electronic warfare device
		"66ffa9b66e19cc902401c5e8", // MPS Auto Assault-12 Gen 1 12ga automatic shotgun
		"67124dcfa3541f2a1f0e788b", // MPS Auto Assault-12 Gen 2 12ga automatic shotgun
	];

	private Timer? _timer;
	public required ModConfig Config;
	public required ModTiers Tiers { get; set; }
	public required ModTiersHex TiersHex { get; set; }
	public required ModTranslation Translation { get; set; }
	public required ModTranslationDebug ModTranslationDebug { get; set; }
	private double EuroRatio {get; set;}
	private double DollarRatio {get; set;}
	public required Dictionary<string, Dictionary<string, List<string>>> QuestRewardsDb { get; set; }
	public required Dictionary<MongoId, Quest> Quests { get; set; }
	public required Dictionary<MongoId,TemplateItem> Items { get; set; }
	public required Dictionary<ArmorMaterial, ArmorType> Armors { get; set; }
	public required string UserLocale { get; set; }
	public required Dictionary<string, string> i18n { get; set; }
	public required Dictionary<string, string> Localization { get; set; }
	
    public Task OnLoad()
    {
	    string pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
		
	    // Get configs
	    Config = modHelper.GetJsonDataFromFile<ModConfig>(pathToMod, "config/config.json");
	    
	    // Get tiers list
	    Tiers = modHelper.GetJsonDataFromFile<ModTiers>(pathToMod, "config/tiers.json");
	    TiersHex = modHelper.GetJsonDataFromFile<ModTiersHex>(pathToMod, "config/tiers_hex.json");
	    
	    // Get translations
	    Translation = jsonUtil.DeserializeFromFile<ModTranslation>(pathToMod + "/config/translations.json") ?? 
	                  throw new NullReferenceException("Could not find translations file");
	    ModTranslationDebug = Translation.ModTranslationDebug;
	    Translation.Language = new Dictionary<string, Dictionary<string, string>>();
	    
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
	    
        // Pass all the services and vars to Utils
        Utils.Initialize(databaseService, 
						localeService, 
						itemBaseClassService, 
						logger, 
						EuroRatio, 
						DollarRatio, 
						UserLocale, 
						Translation,
						Quests);

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
	    Stopwatch stopwatch = Stopwatch.StartNew();
	    
	    logger.Info("[ItemInfo] Processing items...");
	    
	    TranslationDebug();
	    QuestRewards();
	    ItemHandling();
	    
	    stopwatch.Stop();
	    
	    logger.Info("[ItemInfo] Completed in " + stopwatch.ElapsedMilliseconds + " ms.");
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

    private void ItemHandling()
    {
	    int a = 0;
	    StringBuilder descriptionString = new StringBuilder();
	    StringBuilder priceString = new StringBuilder();
	    StringBuilder barterString = new StringBuilder();
	    StringBuilder productionString = new StringBuilder();
	    StringBuilder usedForBarterString = new StringBuilder();
	    StringBuilder usedForQuestsString = new StringBuilder();
	    StringBuilder usedForHideoutString = new StringBuilder();
	    StringBuilder usedForCraftingString = new StringBuilder();
	    StringBuilder armorDurabilityString = new StringBuilder();
	    StringBuilder slotEfficiencyString = new StringBuilder();
	    StringBuilder headsetDescription = new StringBuilder();
	    StringBuilder advancedAmmoInfoString = new StringBuilder();
	    
	    foreach (KeyValuePair<MongoId, TemplateItem> kvp in Items)
	    {
		    // Clearing all vars
		    descriptionString.Clear();
		    priceString.Clear();
		    barterString.Clear();
		    productionString.Clear();
		    usedForBarterString.Clear();
		    usedForQuestsString.Clear();
		    usedForHideoutString.Clear();
		    usedForCraftingString.Clear();
		    armorDurabilityString.Clear();
		    slotEfficiencyString.Clear();
		    headsetDescription.Clear();
		    advancedAmmoInfoString.Clear();
		    
		    logger.Info("Processing item " + (a + 1) + "/" + Items.Count + ": " + Utils.GetItemName(kvp.Key));
		    a += 1;
		    
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
			
			string name = Utils.GetItemName(itemId, UserLocale);
			
		    // Boilerplate defaults
		    double fleaPrice = Utils.GetFleaPrice(itemId) ?? 0;
		    string fleaPriceString = fleaPrice.ToString(CultureInfo.CurrentCulture);
		    
		    ValueTuple<double?, string, string> itemBestVendor = Utils.GetItemBestTrader(itemId);

		    double? price = itemBestVendor.Item1;
		    
		    if (price is null)
			    continue;
		    
		    double traderPrice = Math.Round((double)price);
		    string itemBestTraderName = itemBestVendor.Item2;
		    int slotDensity = Utils.GetItemSlotDensity(itemProperties);
		    List<Utils.ResolvedBarter> itemBarters = Utils.BarterResolver(itemId);
		    ValueTuple<List<double>, string, List<int>> barterInfo = Utils.BarterInfoGenerator(itemBarters, UserLocale);
		    string barterResourceInfo = Utils.BarterResourceInfoGenerator(itemId, UserLocale);
		    List<int> rarityArray = barterInfo.Item3;
		    string itemQuestInfo = Utils.QuestInfoGenerator(itemId, UserLocale);
		    int itemRarity = rarityArray.Min();
		    bool isBanned;

		    // UseBsgStaticFleaBanList
		    if (Config.UseBsgStaticFleaBanList.Enabled)
			    isBanned = true;
		    else
			    isBanned = !itemProperties.CanSellOnRagfair ?? false;

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
				    double? value = Utils.GetItemBestTrader(itemId).price;
				    
				    if (value is not null &&
				        count is not null)
						traderPrice = (double)(value * count);
				    else
					    traderPrice = 0;
				    
				    if (itemRarity is 0 or 7)
					    itemRarity = BsgBlacklist.Contains(ammo ?? "")
						    ? 7
						    : Utils.BarterInfoGenerator(Utils.BarterResolver(ammo ?? "")).rarityArray.Min();
			    }
		    }

		    // BulletStatsInName
		    if (Config.ModBulletStatsInName.Enabled &&
		        itemProperties.AmmoType is "bullet" or "buckshot")
		    {
			    double damageMult = 1;

			    if (itemProperties.AmmoType == "buckshot")
				    damageMult = itemProperties.BuckshotBullets ?? 0;
			    
			    Utils.AddToName(itemId,
				    " " +
				    itemProperties.Damage * damageMult +
				    "/" +
				    itemProperties.PenetrationPower,
				    "append");
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

				    string tiersHexcode;
				    string tier;
				    
				    switch (itemRarity)
				    {
					    case 7:
						    tier = i18n["OVERPOWERED"];
						    itemProperties.BackgroundColor = TiersHex["OVERPOWERED"];
						    tiersHexcode = TiersHex["OVERPOWERED"];
						    break;
					    case 1:
						    tier = i18n["COMMON"];
						    itemProperties.BackgroundColor = TiersHex["COMMON"];
						    tiersHexcode = TiersHex["COMMON"];
						    break;
					    case 2:
						    tier = i18n["RARE"];
						    itemProperties.BackgroundColor = TiersHex["RARE"];
						    tiersHexcode = TiersHex["RARE"];
						    break;
					    case 3:
						    tier = i18n["EPIC"];
						    itemProperties.BackgroundColor = TiersHex["EPIC"];
						    tiersHexcode = TiersHex["EPIC"];
						    break;
					    case 4:
						    tier = i18n["LEGENDARY"];
						    itemProperties.BackgroundColor = TiersHex["LEGENDARY"];
						    tiersHexcode = TiersHex["LEGENDARY"];
						    break;
					    case 5:
						    tier = i18n["UBER"];
						    itemProperties.BackgroundColor = TiersHex["UBER"];
						    tiersHexcode = TiersHex["UBER"];
						    break;
					    case 6:
						    tier = i18n["UNOBTAINIUM"];
						    itemProperties.BackgroundColor = TiersHex["UNOBTAINIUM"];
						    tiersHexcode = TiersHex["UNOBTAINIUM"];
						    break;
					    case 8:
						    tier = i18n["CUSTOM"];
						    itemProperties.BackgroundColor = TiersHex["CUSTOM"];
						    tiersHexcode = TiersHex["CUSTOM"];
						    break;
					    default: // itemRarity >= 9
						    tier = i18n["CUSTOM2"];
						    itemProperties.BackgroundColor = TiersHex["CUSTOM2"];
						    tiersHexcode = TiersHex["CUSTOM2"];
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
					    {
						    IEnumerable<StackSlot>? stackSlots = itemProperties.StackSlots;
						    List<StackSlot>? stackSlotsList = stackSlots?.ToList();

						    if (stackSlotsList is not null)
						    {
							    double count = stackSlotsList[0].MaxCount ?? 0;
							    double value = Utils.GetItemBestTrader(itemId).price ?? 0;

							    itemValue = value * count;
						    }
					    }

					    switch (itemValue)
					    {
						    case var _ when itemValue < int.Parse(Tiers["COMMON_VALUE_FALLBACK"]):
							    tier = i18n["COMMON"];
							    itemProperties.BackgroundColor = TiersHex["COMMON"];
							    tiersHexcode = TiersHex["COMMON"];
							    break;
						    case var _ when itemValue < int.Parse(Tiers["RARE_VALUE_FALLBACK"]):
							    tier = i18n["RARE"];
							    itemProperties.BackgroundColor = TiersHex["RARE"];
							    tiersHexcode = TiersHex["RARE"];
							    break;
						    case var _ when itemValue < int.Parse(Tiers["EPIC_VALUE_FALLBACK"]):
							    tier = i18n["EPIC"];
							    itemProperties.BackgroundColor = TiersHex["EPIC"];
							    tiersHexcode = TiersHex["EPIC"];
							    break;
						    case var _ when itemValue < int.Parse(Tiers["LEGENDARY_VALUE_FALLBACK"]):
							    tier = i18n["LEGENDARY"];
							    itemProperties.BackgroundColor = TiersHex["LEGENDARY"];
							    tiersHexcode = TiersHex["LEGENDARY"];
							    break;
						    case var _ when itemValue < int.Parse(Tiers["UBER_VALUE_FALLBACK"]):
							    tier = i18n["UBER"];
							    itemProperties.BackgroundColor = TiersHex["UBER"];
							    tiersHexcode = TiersHex["UBER"];
							    break;
						    default:
							    tier = i18n["UNOBTAINIUM"];
							    itemProperties.BackgroundColor = TiersHex["UNOBTAINIUM"];
							    tiersHexcode = TiersHex["UNOBTAINIUM"];
							    break;
					    }
				    }

				    Utils.AddColorToName(itemId, tiersHexcode);

				    if (Config.ModRarityRecolor.AddTierNameToPricesInfo &&
				        !string.IsNullOrEmpty(tier))
					    priceString.Append(tier +
					                       " | ");
			    }
		    }

		    if (Config.ModArmorInfo.Enabled)
		    {
			    if (itemProperties.ArmorClass > 0 &&
			        itemProperties.ArmorMaterial is not null)
			    {
				    ArmorMaterial armorMaterial = (ArmorMaterial)itemProperties.ArmorMaterial;
				    
				    ArmorType armor = Armors[armorMaterial];

				    armorDurabilityString.Append((Config.ModArmorInfo.AddArmorClassInfo
													 ? i18n["Armorclass"] +
													   ": " +
													   itemProperties.ArmorClass +
													   " | "
													 : "") +
												 i18n["Effectivedurability"] +
												 ": " +
												 Math.Round((itemProperties.MaxDurability ?? 0)/ armor.Destructibility) +
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
												 "%\n\n");
				    
				    if (Config.ModArmorInfo.AddArmorToName)
					    Utils.AddToName(itemId, 
								" (" + 
								itemProperties.ArmorClass + 
								"/" + 
								Math.Round(itemProperties.MaxDurability ?? 0 / armor.Destructibility) + 
								")", 
									"append");
				    
				    if (Config.ModArmorInfo.AddArmorToShortName)
					    Utils.AddToShortName(itemId, 
						    " (" + 
								itemProperties.ArmorClass + 
								"/" + 
								Math.Round(itemProperties.MaxDurability ?? 0/ armor.Destructibility) +
								")", 
										"append");
			    }
		    }

		    if (Config.ModAdvancedAmmoInfo.Enabled)
		    {
			    if (templateItem.Parent == "5485a8684bdc2da71d8b4567")
			    {
				    TemplateItemProperties ammoProps = itemProperties;
				    
				    advancedAmmoInfoString.Append("Damage: " +
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
												 (Math.Round((ammoProps.FragmentationChance ?? 0) * 100) + "%" +
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
												  Math.Round((ammoProps.MalfMisfireChance ?? 0) * 100) +
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
												  "\n\n");
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

					    double slotEfficiency =
						    Math.Round((double)totalSlots / (itemProperties.Width ?? 1) * (itemProperties.Height ?? 1)
							    , 2);

					    slotEfficiencyString.Append(i18n["Slotefficiency"] +
													": x" +
													slotEfficiency +
													" (" +
													totalSlots +
													"/" +
													itemProperties.Width * itemProperties.Height +
													")\n\n");
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
						    Utils.AddToShortName(itemId, mark, "prepend");

					    if (Config.ModMarkValuableItems.AddToName)
						    Utils.AddToName(itemId, mark, "append");
				    }
			    }
		    }

		    if (Config.ModPriceInfo.Enabled)
			    priceString.Append((Config.ModPriceInfo.AddFleaPrice
										? i18n["Fleaprice"] +
										  ": " +
										  (fleaPriceString == i18n["BANNED"]
											  ? "BANNED"
											  : Utils.FormatPrice(fleaPrice) + (fleaPrice > 0 ? "₽" : "")) +
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
									"₽\n\n");

		    if (Config.ModHeadsetInfo.Enabled)
		    {
			    if (itemProperties.Distortion is not null)
			    {
				    double? gain = itemProperties.CompressorGain;
				    double? thresh = itemProperties.CompressorThreshold;

				    headsetDescription.Append(i18n["AmbientVolume"] +
											 ": " +
											 Math.Round(((itemProperties.AmbientCompressorSendLevel ?? -10) + 10 +
											             (itemProperties.EffectsReturnsGrEnvCommonCompressorSendLeveloupVolume ?? -7) + 7 +
											             (itemProperties.EnvNatureCompressorSendLevel ?? -5) + 5 +
											             (itemProperties.EnvTechnicalCompressorSendLevel ?? -7) + 7) * 10) / 10 +
											 "db | " +
											 i18n["Boost"] +
											 ": +" +
											 gain + Math.Abs((thresh ?? -20) + 20) +
											 "db" +
											 (itemProperties.Distortion > 0
												 ? " | " +
												   i18n["Distortion"] +
												   ": " +
												   Math.Round((itemProperties.Distortion ?? 0) * 100) +
												   "%"
												 : "") +
											 "\n\n");
			    }
		    }

		    if (Config.ModBarterInfo.Enabled)
		    {
			    if (barterInfo.Item2.Length > 1)
			    {
				    barterString.Clear();
				    barterString.Append(barterInfo.Item2 + "\n");
			    }
		    }

		    if (Config.ModProductionInfo.Enabled)
		    {
			    string productionInfo = Utils.ProductionGenerator(itemId, UserLocale);

			    if (productionInfo.Length > 1)
			    {
				    productionString.Clear();
				    productionString.Append(productionInfo + "\n");
			    }
		    }

		    if (Config.ModBarterResourceInfo.Enabled)
		    {
			    if (barterResourceInfo.Length > 1)
			    {
				    usedForBarterString.Clear();
				    usedForBarterString.Append(barterResourceInfo + "\n");
			    }
		    }
		    
		    if (Config.ModQuestInfo.Enabled)
		    {
			    if (itemQuestInfo.Length > 1)
			    {
				    usedForQuestsString.Clear();
				    usedForQuestsString.Append(itemQuestInfo + "\n");

				    if (Config.ModQuestInfo.FirInName &&
				        itemQuestInfo.Contains("✔"))
					    Utils.AddToName(itemId, "✔", "append");
			    }
		    }

		    if (Config.ModHideoutInfo.Enabled)
		    {
			    string itemHideoutInfo = Utils.HideoutInfoGenerator(itemId, UserLocale);

			    if (itemHideoutInfo.Length > 1)
			    {
				    usedForHideoutString.Clear();
				    usedForHideoutString.Append(itemHideoutInfo + "\n");
			    }
		    }

		    if (Config.ModCraftingMaterialInfo.Enabled)
		    {
			    string itemCraftingMaterialInfo = Utils.CraftingMaterialInfoGenerator(itemId, UserLocale);

			    if (itemCraftingMaterialInfo.Length > 1)
			    {
				    usedForCraftingString.Clear();
				    usedForCraftingString.Append(itemCraftingMaterialInfo + "\n");
			    }
		    }
		    
		    descriptionString.Append(priceString.ToString() +
									headsetDescription +
									armorDurabilityString +
									slotEfficiencyString +
									usedForQuestsString +
									usedForHideoutString +
									barterString +
									productionString +
									usedForCraftingString +
									usedForBarterString +
									advancedAmmoInfoString);

		    Utils.AddToDescription(itemId, descriptionString.ToString(), "prepend");

		    bool debug = false;
		    
		    if (debug)
				logger.Info("Item \"" + 
							name + 
							"\" : " + 
							descriptionString);
	    }
    }
}

public class ModTiers : Dictionary<string, string>;

public class ModTiersHex : Dictionary<string, string>;