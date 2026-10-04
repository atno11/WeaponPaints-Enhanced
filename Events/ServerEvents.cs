using CounterStrikeSharp.API;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OnMapStart(string mapName)
	{
		if (Config.Additional is { KnifeEnabled: false, SkinEnabled: false, GloveEnabled: false })
			return;

		if (Database != null)
			WeaponSync = new WeaponSynchronization(Database, Config);

		_fadeSeed = 0;
		_nextItemId = MinimumCustomItemId;
	}

	private void OnTick()
	{
		foreach (var player in Players)
		{
			if (!Utility.IsPlayerValid(player))
				continue;

			if (
				Config.Additional.ShowSkinImage
				&& _playerWeaponImage.TryGetValue(player.Slot, out var value)
				&& !string.IsNullOrEmpty(value)
			)
			{
				player.PrintToCenterHtml("<img src='{PATH}'</img>".Replace("{PATH}", value));
			}

			if (
				!Config.Additional.MusicEnabled
				|| player.IsBot
				|| player.InventoryServices == null
				|| !GPlayersMusic.TryGetValue(player.Slot, out var musicInfo)
				|| !musicInfo.TryGetValue(player.Team, out var musicId)
			)
			{
				continue;
			}

			if (player.InventoryServices.MusicID != musicId)
			{
				player.InventoryServices.MusicID = musicId;
				Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
			}

			if (player.MusicKitID != musicId)
			{
				player.MusicKitID = musicId;
				Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMusicKitID");
			}

			if (player.MusicKitMVPs != 0)
			{
				player.MusicKitMVPs = 0;
				Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMusicKitMVPs");
			}

			if (player.MvpNoMusic)
			{
				player.MvpNoMusic = false;
				Utilities.SetStateChanged(player, "CCSPlayerController", "m_bMvpNoMusic");
			}
		}
	}
}
