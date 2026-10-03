using CounterStrikeSharp.API.Core;

namespace WeaponPaints;

internal static partial class Utility
{
	internal static bool IsPlayerValid(CCSPlayerController? player)
	{
		if (player is null || WeaponPaints.WeaponSync is null)
			return false;

		return player is { IsValid: true, IsBot: false, IsHLTV: false, UserId: not null };
	}
}
