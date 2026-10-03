using System.Collections.Concurrent;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	internal static readonly ConcurrentDictionary<int, ConcurrentDictionary<CsTeam, string>> GPlayersKnife = new();
	internal static readonly ConcurrentDictionary<int, ConcurrentDictionary<CsTeam, ushort>> GPlayersGlove = new();
	internal static readonly ConcurrentDictionary<int, ConcurrentDictionary<CsTeam, ushort>> GPlayersMusic = new();
	internal static readonly ConcurrentDictionary<int, ConcurrentDictionary<CsTeam, ushort>> GPlayersPin = new();
	internal static readonly ConcurrentDictionary<int, (string? CT, string? T)> GPlayersAgent = new();
	internal static readonly ConcurrentDictionary<
		int,
		ConcurrentDictionary<CsTeam, ConcurrentDictionary<int, WeaponInfo>>
	> GPlayerWeaponsInfo = new();
}
