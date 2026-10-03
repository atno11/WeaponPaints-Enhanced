using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void InitializeHotReload()
	{
		OnMapStart(string.Empty);

		GPlayerWeaponsInfo.Clear();
		GPlayersKnife.Clear();
		GPlayersGlove.Clear();
		GPlayersAgent.Clear();
		GPlayersPin.Clear();
		GPlayersMusic.Clear();

		foreach (
			var player in Enumerable
				.OfType<CCSPlayerController>(Utilities.GetPlayers().TakeWhile(_ => WeaponSync != null))
				.Where(player =>
					player.IsValid
					&& !string.IsNullOrEmpty(player.IpAddress)
					&& player is { IsBot: false, Connected: PlayerConnectedState.Connected }
				)
		)
		{
			var playerInfo = new PlayerInfo
			{
				UserId = player.UserId,
				Slot = player.Slot,
				Index = (int)player.Index,
				SteamId = player?.SteamID.ToString(),
				Name = player?.PlayerName,
				IpAddress = player?.IpAddress?.Split(":")[0],
			};

			_ = Task.Run(async () =>
			{
				if (WeaponSync != null)
					await WeaponSync.GetPlayerData(playerInfo);
			});
		}
	}
}
