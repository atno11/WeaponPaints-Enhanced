using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;

namespace WeaponPaints;

public partial class WeaponPaints
{
	[GameEventHandler]
	public HookResult OnClientFullConnect(EventPlayerConnectFull @event, GameEventInfo info)
	{
		CCSPlayerController? player = @event.Userid;

		if (player is null || !player.IsValid || player.IsBot || WeaponSync == null || Database == null)
			return HookResult.Continue;

		var playerInfo = new PlayerInfo
		{
			UserId = player.UserId,
			Slot = player.Slot,
			Index = (int)player.Index,
			SteamId = player.SteamID.ToString(),
			Name = player.PlayerName,
			IpAddress = player.IpAddress?.Split(":")[0],
		};

		try
		{
			_ = Task.Run(async () => await WeaponSync.GetPlayerData(playerInfo));
			/*
			if (Config.Additional.SkinEnabled)
			{
				_ = Task.Run(async () => await weaponSync.GetWeaponPaintsFromDatabase(playerInfo));
			}
			if (Config.Additional.KnifeEnabled)
			{
				_ = Task.Run(async () => await weaponSync.GetKnifeFromDatabase(playerInfo));
			}
			if (Config.Additional.GloveEnabled)
			{
				_ = Task.Run(async () => await weaponSync.GetGloveFromDatabase(playerInfo));
			}
			if (Config.Additional.AgentEnabled)
			{
				_ = Task.Run(async () => await weaponSync.GetAgentFromDatabase(playerInfo));
			}
			if (Config.Additional.MusicEnabled)
			{
				_ = Task.Run(async () => await weaponSync.GetMusicFromDatabase(playerInfo));
			}
			*/
		}
		catch { }

		Players.Add(player);

		return HookResult.Continue;
	}

	[GameEventHandler]
	public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
	{
		CCSPlayerController? player = @event.Userid;

		if (player is null || !player.IsValid || player.IsBot)
			return HookResult.Continue;

		var playerInfo = new PlayerInfo
		{
			UserId = player.UserId,
			Slot = player.Slot,
			Index = (int)player.Index,
			SteamId = player.SteamID.ToString(),
			Name = player.PlayerName,
			IpAddress = player.IpAddress?.Split(":")[0],
		};

		Task.Run(async () =>
		{
			if (WeaponSync != null)
				await WeaponSync.SyncStatTrakToDatabase(playerInfo);

			if (Config.Additional.SkinEnabled)
			{
				GPlayerWeaponsInfo.TryRemove(player.Slot, out _);
			}
		});

		if (Config.Additional.KnifeEnabled)
		{
			GPlayersKnife.TryRemove(player.Slot, out _);
		}
		if (Config.Additional.GloveEnabled)
		{
			GPlayersGlove.TryRemove(player.Slot, out _);
		}
		if (Config.Additional.AgentEnabled)
		{
			GPlayersAgent.TryRemove(player.Slot, out _);
		}
		if (Config.Additional.MusicEnabled)
		{
			GPlayersMusic.TryRemove(player.Slot, out _);
		}
		if (Config.Additional.PinsEnabled)
		{
			GPlayersPin.TryRemove(player.Slot, out _);
		}

		_temporaryPlayerWeaponWear.TryRemove(player.Slot, out _);
		CommandsCooldown.Remove(player.Slot);
		Players.Remove(player);

		return HookResult.Continue;
	}
}
