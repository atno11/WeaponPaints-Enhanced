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
	public static IStringLocalizer? _localizer;
	internal static WeaponSynchronization? WeaponSync;

	internal static Database? Database;

	//we dont need anymore because we use AcceptInput
	//private static readonly MemoryFunctionWithReturn<nint, string, int, int> SetBodygroupFunc = new(
	//	GameData.GetSignature("CBaseModelEntity_SetBodygroup"));

	//private static readonly Func<nint, string, int, int> SetBodygroup = SetBodygroupFunc.Invoke;

	private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	internal List<CCSPlayerController> Players = [];
}
