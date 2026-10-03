using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using MenuManager;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private static readonly ConcurrentDictionary<int, Action<CCSPlayerController>> MenuBackActions = new();

	private readonly Dictionary<int, string> _playerWeaponImage = new();

	internal static IMenuApi? MenuApi;

	private static readonly PluginCapability<IMenuApi> MenuCapability = new("menu:nfcore");
}
