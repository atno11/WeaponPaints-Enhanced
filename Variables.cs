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
	private static readonly ConcurrentDictionary<int, Action<CCSPlayerController>> MenuBackActions = new();

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

	private const ulong MinimumCustomItemId = 65578;
	private ulong _nextItemId = MinimumCustomItemId;
	private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, float>> _temporaryPlayerWeaponWear = new();

	internal static IMenuApi? MenuApi;
	private static readonly PluginCapability<IMenuApi> MenuCapability = new("menu:nfcore");

	private int _fadeSeed;

	internal List<CCSPlayerController> Players = [];
}
