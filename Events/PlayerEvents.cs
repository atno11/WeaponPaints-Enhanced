using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
	{
		CCSPlayerController? player = @event.Userid;

		if (player is null || !player.IsValid)
			return HookResult.Continue;

		CCSPlayerPawn? pawn = player.PlayerPawn.Value;

		if (pawn == null || !pawn.IsValid)
			return HookResult.Continue;

		GivePlayerMusicKit(player);
		GivePlayerAgent(player);
		Server.NextFrame(() =>
		{
			GivePlayerGloves(player);
		});
		GivePlayerPin(player);

		return HookResult.Continue;
	}
}
