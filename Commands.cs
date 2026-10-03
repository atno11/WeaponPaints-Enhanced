using System.Collections.Concurrent;
using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OnCommandWS(CCSPlayerController? player, CommandInfo command)
	{
		if (!Config.Additional.SkinEnabled)
			return;

		if (!Utility.IsPlayerValid(player))
			return;

		if (!string.IsNullOrEmpty(Localizer["wp_info_refresh"]))
		{
			player!.Print(Localizer["wp_info_refresh"]);
		}

		if (Config.Additional.GloveEnabled && !string.IsNullOrEmpty(Localizer["wp_info_glove"]))
		{
			player!.Print(Localizer["wp_info_glove"]);
		}

		if (Config.Additional.AgentEnabled && !string.IsNullOrEmpty(Localizer["wp_info_agent"]))
		{
			player!.Print(Localizer["wp_info_agent"]);
		}

		if (Config.Additional.MusicEnabled && !string.IsNullOrEmpty(Localizer["wp_info_music"]))
		{
			player!.Print(Localizer["wp_info_music"]);
		}

		if (Config.Additional.PinsEnabled && !string.IsNullOrEmpty(Localizer["wp_info_pin"]))
		{
			player!.Print(Localizer["wp_info_pin"]);
		}

		if (!Config.Additional.KnifeEnabled)
			return;

		if (!string.IsNullOrEmpty(Localizer["wp_info_knife"]))
		{
			player!.Print(Localizer["wp_info_knife"]);
		}
	}

	private void RegisterCommands()
	{
		_config.Additional.CommandSkin.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Skins info",
				(player, info) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					OnCommandWS(player, info);
				}
			);
		});

		_config.Additional.CommandRefresh.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Skins refresh",
				(player, info) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					OnCommandRefresh(player, info);
				}
			);
		});

		if (Config.Additional.CommandKillEnabled)
		{
			_config.Additional.CommandKill.ForEach(c =>
			{
				AddCommand(
					$"css_{c}",
					"kill yourself",
					(player, _) =>
					{
						if (
							player == null
							|| !Utility.IsPlayerValid(player)
							|| player.PlayerPawn.Value == null
							|| !player.PlayerPawn.IsValid
						)
							return;

						player.PlayerPawn.Value.CommitSuicide(true, false);
					}
				);
			});
		}

		AddCommand(
			"wp_refresh",
			"Admin refresh player skins",
			(player, info) =>
			{
				OnCommandSkinRefresh(player, info);
			}
		);
	}

	private void SetupMusicMenu()
	{
		var musicSelectionMenu = Utility.CreateMenu(Localizer["wp_music_menu_title"]);

		if (musicSelectionMenu == null)
			return;

		void ApplyInventoryMusicSelection(CCSPlayerController player)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			var selectionVersion = MusicSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);
			GPlayersMusic.TryRemove(player.Slot, out _);
			RestoreInventoryMusicKit(player);

			if (!string.IsNullOrEmpty(Localizer["wp_music_menu_select"]))
				player.Print(Localizer["wp_music_menu_select", Localizer["wp_glove_family_default_inventory"]]);

			if (WeaponSync == null)
				return;

			var playerInfo = new PlayerInfo
			{
				UserId = player.UserId,
				Slot = player.Slot,
				Index = (int)player.Index,
				SteamId = player.SteamID.ToString(),
				Name = player.PlayerName,
				IpAddress = player.IpAddress?.Split(":")[0],
			};

			_ = Task.Run(async () =>
			{
				var syncLock = MusicSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));
				await syncLock.WaitAsync();

				try
				{
					if (!MusicSelectionVersions.TryGetValue(player.Slot, out var currentVersion) || currentVersion != selectionVersion)
						return;

					await WeaponSync.DeleteMusicFromDatabase(playerInfo);
				}
				finally
				{
					syncLock.Release();
				}
			});
		}

		void ApplyMusicSelection(CCSPlayerController player, ushort musicId, string displayName, string? image = null)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			var teamsToCheck = player.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [player.Team];
			var playerMusic = GPlayersMusic.GetOrAdd(player.Slot, new ConcurrentDictionary<CsTeam, ushort>());
			var selectionVersion = MusicSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);

			foreach (var team in teamsToCheck)
				playerMusic[team] = musicId;

			if (Config.Additional.ShowSkinImage && !string.IsNullOrEmpty(image))
			{
				_playerWeaponImage[player.Slot] = image;
				AddTimer(2.0f, () => _playerWeaponImage.Remove(player.Slot), TimerFlags.STOP_ON_MAPCHANGE);
			}

			GivePlayerMusicKit(player);

			if (!string.IsNullOrEmpty(Localizer["wp_music_menu_select"]))
				player.Print(Localizer["wp_music_menu_select", displayName]);

			if (WeaponSync == null)
				return;

			var playerInfo = new PlayerInfo
			{
				UserId = player.UserId,
				Slot = player.Slot,
				Index = (int)player.Index,
				SteamId = player.SteamID.ToString(),
				Name = player.PlayerName,
				IpAddress = player.IpAddress?.Split(":")[0],
			};

			_ = Task.Run(async () =>
			{
				var syncLock = MusicSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));
				await syncLock.WaitAsync();

				try
				{
					if (!MusicSelectionVersions.TryGetValue(player.Slot, out var currentVersion) || currentVersion != selectionVersion)
						return;

					await WeaponSync.SyncMusicToDatabase(playerInfo, musicId, teamsToCheck);
				}
				finally
				{
					syncLock.Release();
				}
			});
		}

		musicSelectionMenu.AddMenuOption(
			Localizer["wp_glove_family_default_inventory"],
			(player, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				ApplyInventoryMusicSelection(player);
			}
		);

		musicSelectionMenu.AddMenuOption(
			Localizer["None"],
			(player, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				ApplyMusicSelection(player, 0, Localizer["None"].Value);
			}
		);

		foreach (var musicObject in MusicList)
		{
			var musicName = musicObject["name"]?.ToString();
			if (string.IsNullOrEmpty(musicName) || !ushort.TryParse(musicObject["id"]?.ToString(), out var musicId))
				continue;

			var image = musicObject["image"]?.ToString();
			musicSelectionMenu.AddMenuOption(
				musicName,
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					ApplyMusicSelection(player, musicId, musicName, image);
				}
			);
		}

		_config.Additional.CommandMusic.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Music selection menu",
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player) || !_gBCommandsAllowed)
						return;

					if (player == null || player.UserId == null)
						return;

					if (
						!CommandsCooldown.TryGetValue(player.Slot, out var cooldownEndTime)
						|| DateTime.UtcNow
							>= (CommandsCooldown.TryGetValue(player.Slot, out cooldownEndTime) ? cooldownEndTime : DateTime.UtcNow)
					)
					{
						CommandsCooldown[player.Slot] = DateTime.UtcNow.AddSeconds(Config.CmdRefreshCooldownSeconds);
						OpenWeaponPaintsMenu(musicSelectionMenu, player);
						return;
					}

					if (!string.IsNullOrEmpty(Localizer["wp_command_cooldown"]))
						player.Print(Localizer["wp_command_cooldown"]);
				}
			);
		});
	}
}
