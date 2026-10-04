using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private static void GivePlayerAgent(CCSPlayerController player)
	{
		if (!GPlayersAgent.TryGetValue(player.Slot, out var value))
			return;

		var model = player.TeamNum == 3 ? value.CT : value.T;
		if (string.IsNullOrEmpty(model))
			return;

		if (player.PlayerPawn.Value == null)
			return;

		try
		{
			Server.NextFrame(() =>
			{
				player.PlayerPawn.Value.SetModel($"agents/models/{model}.vmdl");
			});
		}
		catch (Exception) { }
	}

	private static void GivePlayerMusicKit(CCSPlayerController player)
	{
		if (player.IsBot || player.InventoryServices == null)
			return;

		CaptureNativeMusicKitSnapshot(player);

		if (!GPlayersMusic.TryGetValue(player.Slot, out var musicInfo) || !musicInfo.TryGetValue(player.Team, out var musicId))
			return;

		player.MusicKitID = musicId;
		player.InventoryServices.MusicID = musicId;
		player.MusicKitMVPs = 0;
		player.MvpNoMusic = false;

		Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMusicKitID");
		Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
		Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMusicKitMVPs");
		Utilities.SetStateChanged(player, "CCSPlayerController", "m_bMvpNoMusic");
	}

	private static void GivePlayerPin(CCSPlayerController player)
	{
		if (!GPlayersPin.TryGetValue(player.Slot, out var pinInfo) || !pinInfo.TryGetValue(player.Team, out var pinId))
			return;
		if (player.InventoryServices == null)
			return;

		player.InventoryServices.Rank[5] = pinId > 0 ? (MedalRank_t)pinId : MedalRank_t.MEDAL_RANK_NONE;
		Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
	}
}
