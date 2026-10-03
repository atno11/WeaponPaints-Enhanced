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

	public static IStringLocalizer? _localizer;
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
