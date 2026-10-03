using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.Utils;
using MenuManager;
using Microsoft.Extensions.Localization;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private sealed record NativeEconItemSnapshot(
		ushort ItemDefinitionIndex,
		int EntityQuality,
		uint EntityLevel,
		ulong ItemID,
		uint ItemIDHigh,
		uint ItemIDLow,
		uint AccountID,
		uint InventoryPosition,
		bool Initialized,
		string CustomName,
		string CustomNameOverride,
		float? PaintKitAttribute,
		float? PaintSeedAttribute,
		float? PaintWearAttribute
	);

	private sealed record NativeWeaponSnapshot(
		NativeEconItemSnapshot Item,
		string Classname,
		uint OriginalOwnerXuidLow,
		uint OriginalOwnerXuidHigh,
		int FallbackPaintKit,
		int FallbackSeed,
		float FallbackWear,
		int FallbackStatTrak
	);

	private sealed record NativeMusicKitSnapshot(int MusicKitID, ushort InventoryMusicID);

	private static readonly ConcurrentDictionary<int, Action<CCSPlayerController>> MenuBackActions = new();

	private static readonly Dictionary<string, string> WeaponList = new()
	{
		{ "weapon_deagle", "Desert Eagle" },
		{ "weapon_elite", "Dual Berettas" },
		{ "weapon_fiveseven", "Five-SeveN" },
		{ "weapon_glock", "Glock-18" },
		{ "weapon_ak47", "AK-47" },
		{ "weapon_aug", "AUG" },
		{ "weapon_awp", "AWP" },
		{ "weapon_famas", "FAMAS" },
		{ "weapon_g3sg1", "G3SG1" },
		{ "weapon_galilar", "Galil AR" },
		{ "weapon_m249", "M249" },
		{ "weapon_m4a1", "M4A4" },
		{ "weapon_mac10", "MAC-10" },
		{ "weapon_p90", "P90" },
		{ "weapon_mp5sd", "MP5-SD" },
		{ "weapon_ump45", "UMP-45" },
		{ "weapon_xm1014", "XM1014" },
		{ "weapon_bizon", "PP-Bizon" },
		{ "weapon_mag7", "MAG-7" },
		{ "weapon_negev", "Negev" },
		{ "weapon_sawedoff", "Sawed-Off" },
		{ "weapon_tec9", "Tec-9" },
		{ "weapon_taser", "Zeus x27" },
		{ "weapon_hkp2000", "P2000" },
		{ "weapon_mp7", "MP7" },
		{ "weapon_mp9", "MP9" },
		{ "weapon_nova", "Nova" },
		{ "weapon_p250", "P250" },
		{ "weapon_scar20", "SCAR-20" },
		{ "weapon_sg556", "SG 553" },
		{ "weapon_ssg08", "SSG 08" },
		{ "weapon_m4a1_silencer", "M4A1-S" },
		{ "weapon_usp_silencer", "USP-S" },
		{ "weapon_cz75a", "CZ75-Auto" },
		{ "weapon_revolver", "R8 Revolver" },
		{ "weapon_knife", "Default Knife" },
		{ "weapon_knife_m9_bayonet", "M9 Bayonet" },
		{ "weapon_knife_karambit", "Karambit" },
		{ "weapon_bayonet", "Bayonet" },
		{ "weapon_knife_survival_bowie", "Bowie Knife" },
		{ "weapon_knife_butterfly", "Butterfly Knife" },
		{ "weapon_knife_falchion", "Falchion Knife" },
		{ "weapon_knife_flip", "Flip Knife" },
		{ "weapon_knife_gut", "Gut Knife" },
		{ "weapon_knife_tactical", "Huntsman Knife" },
		{ "weapon_knife_push", "Shadow Daggers" },
		{ "weapon_knife_gypsy_jackknife", "Navaja Knife" },
		{ "weapon_knife_stiletto", "Stiletto Knife" },
		{ "weapon_knife_widowmaker", "Talon Knife" },
		{ "weapon_knife_ursus", "Ursus Knife" },
		{ "weapon_knife_css", "Classic Knife" },
		{ "weapon_knife_cord", "Paracord Knife" },
		{ "weapon_knife_canis", "Survival Knife" },
		{ "weapon_knife_outdoor", "Nomad Knife" },
		{ "weapon_knife_skeleton", "Skeleton Knife" },
		{ "weapon_knife_kukri", "Kukri Knife" },
	};

	private static readonly Dictionary<int, string> GloveFamilyByDefindex = new()
	{
		{ 5027, "bloodhound" },
		{ 4725, "broken_fang" },
		{ 5031, "driver" },
		{ 5032, "hand_wraps" },
		{ 5035, "hydra" },
		{ 5033, "moto" },
		{ 5034, "specialist" },
		{ 5030, "sport" },
	};

	private static readonly int[] GloveFamilyOrder = [5027, 4725, 5031, 5032, 5035, 5033, 5034, 5030];

	private static readonly Dictionary<string, string> WeaponCategoryByClassname = new()
	{
		// Pistols
		{ "weapon_deagle", "pistols" },
		{ "weapon_elite", "pistols" },
		{ "weapon_fiveseven", "pistols" },
		{ "weapon_glock", "pistols" },
		{ "weapon_hkp2000", "pistols" },
		{ "weapon_p250", "pistols" },
		{ "weapon_tec9", "pistols" },
		{ "weapon_usp_silencer", "pistols" },
		{ "weapon_cz75a", "pistols" },
		{ "weapon_revolver", "pistols" },
		// Rifles
		{ "weapon_ak47", "rifles" },
		{ "weapon_aug", "rifles" },
		{ "weapon_famas", "rifles" },
		{ "weapon_galilar", "rifles" },
		{ "weapon_m4a1", "rifles" },
		{ "weapon_m4a1_silencer", "rifles" },
		{ "weapon_sg556", "rifles" },
		// SMGs
		{ "weapon_bizon", "smgs" },
		{ "weapon_mac10", "smgs" },
		{ "weapon_mp5sd", "smgs" },
		{ "weapon_mp7", "smgs" },
		{ "weapon_mp9", "smgs" },
		{ "weapon_p90", "smgs" },
		{ "weapon_ump45", "smgs" },
		// Shotguns
		{ "weapon_mag7", "shotguns" },
		{ "weapon_nova", "shotguns" },
		{ "weapon_sawedoff", "shotguns" },
		{ "weapon_xm1014", "shotguns" },
		// Sniper Rifles
		{ "weapon_awp", "sniper_rifles" },
		{ "weapon_g3sg1", "sniper_rifles" },
		{ "weapon_scar20", "sniper_rifles" },
		{ "weapon_ssg08", "sniper_rifles" },
		// Machine Guns
		{ "weapon_m249", "machine_guns" },
		{ "weapon_negev", "machine_guns" },
		// Equipment
		{ "weapon_taser", "equipment" },
		// Knives
		{ "weapon_bayonet", "knives" },
		{ "weapon_knife_butterfly", "knives" },
		{ "weapon_knife_canis", "knives" },
		{ "weapon_knife_cord", "knives" },
		{ "weapon_knife_css", "knives" },
		{ "weapon_knife_falchion", "knives" },
		{ "weapon_knife_flip", "knives" },
		{ "weapon_knife_gut", "knives" },
		{ "weapon_knife_gypsy_jackknife", "knives" },
		{ "weapon_knife_karambit", "knives" },
		{ "weapon_knife_kukri", "knives" },
		{ "weapon_knife_m9_bayonet", "knives" },
		{ "weapon_knife_outdoor", "knives" },
		{ "weapon_knife_push", "knives" },
		{ "weapon_knife_skeleton", "knives" },
		{ "weapon_knife_stiletto", "knives" },
		{ "weapon_knife_survival_bowie", "knives" },
		{ "weapon_knife_tactical", "knives" },
		{ "weapon_knife_ursus", "knives" },
		{ "weapon_knife_widowmaker", "knives" },
	};

	private static readonly string[] WeaponCategoryOrder =
	[
		"pistols",
		"rifles",
		"smgs",
		"shotguns",
		"sniper_rifles",
		"machine_guns",
		"equipment",
		"knives",
	];

	private static readonly ConcurrentDictionary<int, int> KnifeSelectionVersions = new();

	internal static int GetKnifeSelectionVersion(int slot)
	{
		return KnifeSelectionVersions.TryGetValue(slot, out var version) ? version : 0;
	}

	internal static int GetGloveSelectionVersion(int slot)
	{
		return GloveSelectionVersions.TryGetValue(slot, out var version) ? version : 0;
	}

	internal static int GetMusicSelectionVersion(int slot)
	{
		return MusicSelectionVersions.TryGetValue(slot, out var version) ? version : 0;
	}

	internal static int GetSkinSelectionVersion(int slot, int weaponDefindex)
	{
		return SkinSelectionVersions.TryGetValue((slot, weaponDefindex), out var version) ? version : 0;
	}

	internal static Dictionary<int, int> GetSkinSelectionVersions(int slot)
	{
		return SkinSelectionVersions
			.Where(entry => entry.Key.Slot == slot)
			.ToDictionary(entry => entry.Key.WeaponDefIndex, entry => entry.Value);
	}

	internal static bool IsKnifeDefindex(int defindex)
	{
		return WeaponDefindex.TryGetValue(defindex, out var classname) && (classname.Contains("knife") || classname.Contains("bayonet"));
	}

	internal static bool IsGloveDefindex(int defindex)
	{
		return GloveFamilyByDefindex.ContainsKey(defindex);
	}

	private static readonly ConcurrentDictionary<int, SemaphoreSlim> KnifeSyncLocks = new();
	private static readonly ConcurrentDictionary<int, int> GloveSelectionVersions = new();
	private static readonly ConcurrentDictionary<int, SemaphoreSlim> GloveSyncLocks = new();
	private static readonly ConcurrentDictionary<(int Slot, int WeaponDefIndex), int> SkinSelectionVersions = new();
	private static readonly ConcurrentDictionary<int, SemaphoreSlim> SkinSyncLocks = new();
	private static readonly ConcurrentDictionary<int, int> MusicSelectionVersions = new();
	private static readonly ConcurrentDictionary<int, SemaphoreSlim> MusicSyncLocks = new();

	private static readonly ConcurrentDictionary<
		(int Slot, ulong SteamId, int Team, nint PawnHandle),
		NativeEconItemSnapshot
	> NativeGloveSnapshots = new();
	private static readonly ConcurrentDictionary<
		(int Slot, ulong SteamId, int Team, nint PawnHandle),
		NativeWeaponSnapshot
	> NativeKnifeSnapshots = new();
	private static readonly ConcurrentDictionary<
		(int Slot, ulong SteamId, int Team, nint PawnHandle, int WeaponDefIndex),
		NativeWeaponSnapshot
	> NativeWeaponSnapshots = new();
	private static readonly ConcurrentDictionary<
		(int Slot, ulong SteamId, nint ControllerHandle),
		NativeMusicKitSnapshot
	> NativeMusicKitSnapshots = new();
	public static IStringLocalizer? _localizer;
	internal static readonly ConcurrentDictionary<int, ConcurrentDictionary<CsTeam, string>> GPlayersKnife = new();
	internal static readonly ConcurrentDictionary<int, ConcurrentDictionary<CsTeam, ushort>> GPlayersGlove = new();
	internal static readonly ConcurrentDictionary<int, ConcurrentDictionary<CsTeam, ushort>> GPlayersMusic = new();
	internal static readonly ConcurrentDictionary<int, ConcurrentDictionary<CsTeam, ushort>> GPlayersPin = new();
	internal static readonly ConcurrentDictionary<int, (string? CT, string? T)> GPlayersAgent = new();
	internal static readonly ConcurrentDictionary<
		int,
		ConcurrentDictionary<CsTeam, ConcurrentDictionary<int, WeaponInfo>>
	> GPlayerWeaponsInfo = new();
	internal static List<JObject> SkinsList = [];
	internal static List<JObject> PinsList = [];
	internal static List<JObject> GlovesList = [];
	internal static List<JObject> AgentsList = [];
	internal static List<JObject> MusicList = [];
	internal static WeaponSynchronization? WeaponSync;
	private static bool _gBCommandsAllowed = true;
	private readonly Dictionary<int, string> _playerWeaponImage = new();

	private static readonly Dictionary<int, DateTime> CommandsCooldown = new();
	internal static Database? Database;

	private static readonly MemoryFunctionVoid<nint, string, float> CAttributeListSetOrAddAttributeValueByName = new(
		GameData.GetSignature("CAttributeList_SetOrAddAttributeValueByName")
	);

	//we dont need anymore because we use AcceptInput
	//private static readonly MemoryFunctionWithReturn<nint, string, int, int> SetBodygroupFunc = new(
	//	GameData.GetSignature("CBaseModelEntity_SetBodygroup"));

	//private static readonly Func<nint, string, int, int> SetBodygroup = SetBodygroupFunc.Invoke;

	private static Dictionary<int, string> WeaponDefindex { get; } =
		new()
		{
			{ 1, "weapon_deagle" },
			{ 2, "weapon_elite" },
			{ 3, "weapon_fiveseven" },
			{ 4, "weapon_glock" },
			{ 7, "weapon_ak47" },
			{ 8, "weapon_aug" },
			{ 9, "weapon_awp" },
			{ 10, "weapon_famas" },
			{ 11, "weapon_g3sg1" },
			{ 13, "weapon_galilar" },
			{ 14, "weapon_m249" },
			{ 16, "weapon_m4a1" },
			{ 17, "weapon_mac10" },
			{ 19, "weapon_p90" },
			{ 23, "weapon_mp5sd" },
			{ 24, "weapon_ump45" },
			{ 25, "weapon_xm1014" },
			{ 26, "weapon_bizon" },
			{ 27, "weapon_mag7" },
			{ 28, "weapon_negev" },
			{ 29, "weapon_sawedoff" },
			{ 30, "weapon_tec9" },
			{ 31, "weapon_taser" },
			{ 32, "weapon_hkp2000" },
			{ 33, "weapon_mp7" },
			{ 34, "weapon_mp9" },
			{ 35, "weapon_nova" },
			{ 36, "weapon_p250" },
			{ 38, "weapon_scar20" },
			{ 39, "weapon_sg556" },
			{ 40, "weapon_ssg08" },
			{ 60, "weapon_m4a1_silencer" },
			{ 61, "weapon_usp_silencer" },
			{ 63, "weapon_cz75a" },
			{ 64, "weapon_revolver" },
			{ 500, "weapon_bayonet" },
			{ 503, "weapon_knife_css" },
			{ 505, "weapon_knife_flip" },
			{ 506, "weapon_knife_gut" },
			{ 507, "weapon_knife_karambit" },
			{ 508, "weapon_knife_m9_bayonet" },
			{ 509, "weapon_knife_tactical" },
			{ 512, "weapon_knife_falchion" },
			{ 514, "weapon_knife_survival_bowie" },
			{ 515, "weapon_knife_butterfly" },
			{ 516, "weapon_knife_push" },
			{ 517, "weapon_knife_cord" },
			{ 518, "weapon_knife_canis" },
			{ 519, "weapon_knife_ursus" },
			{ 520, "weapon_knife_gypsy_jackknife" },
			{ 521, "weapon_knife_outdoor" },
			{ 522, "weapon_knife_stiletto" },
			{ 523, "weapon_knife_widowmaker" },
			{ 525, "weapon_knife_skeleton" },
			{ 526, "weapon_knife_kukri" },
		};

	private const ulong MinimumCustomItemId = 65578;
	private ulong _nextItemId = MinimumCustomItemId;
	private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, float>> _temporaryPlayerWeaponWear = new();

	internal static IMenuApi? MenuApi;
	private static readonly PluginCapability<IMenuApi> MenuCapability = new("menu:nfcore");

	private int _fadeSeed;

	internal List<CCSPlayerController> Players = [];
}
