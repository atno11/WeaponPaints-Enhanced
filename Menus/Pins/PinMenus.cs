using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void SetupPinsMenu()
	{
		var pinsSelectionMenu = Utility.CreateMenu(Localizer["wp_pins_menu_title"]);

		if (pinsSelectionMenu == null)
			return;

		var handlePinsSelection = (CCSPlayerController? player, ChatMenuOption option) =>
		{
			if (!Utility.IsPlayerValid(player) || player is null)
				return;

			var selectedPaintName = option.Text;

			var playerPins = GPlayersPin.GetOrAdd(player.Slot, new ConcurrentDictionary<CsTeam, ushort>());

			var teamsToCheck = player.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [player.Team];

			var selectedPin = PinsList.FirstOrDefault(g => g.ContainsKey("name") && g["name"]?.ToString() == selectedPaintName);

			if (selectedPin != null)
			{
				if (
					!selectedPin.ContainsKey("id")
					|| !selectedPin.ContainsKey("name")
					|| !int.TryParse(selectedPin["id"]?.ToString(), out var paint)
				)
					return;

				var image = selectedPin["image"]?.ToString() ?? "";

				if (Config.Additional.ShowSkinImage)
				{
					_playerWeaponImage[player.Slot] = image;

					AddTimer(2.0f, () => _playerWeaponImage.Remove(player.Slot), TimerFlags.STOP_ON_MAPCHANGE);
				}

				PlayerInfo playerInfo = new PlayerInfo
				{
					UserId = player.UserId,
					Slot = player.Slot,
					Index = (int)player.Index,
					SteamId = player.SteamID.ToString(),
					Name = player.PlayerName,
					IpAddress = player.IpAddress?.Split(":")[0],
				};

				if (paint != 0)
				{
					foreach (var team in teamsToCheck)
						playerPins[team] = (ushort)paint;
				}
				else
				{
					foreach (var team in teamsToCheck)
						playerPins[team] = 0;
				}

				if (!string.IsNullOrEmpty(Localizer["wp_pins_menu_select"]))
				{
					player.Print(Localizer["wp_pins_menu_select", selectedPaintName]);
				}

				GivePlayerPin(player);

				if (WeaponSync != null)
				{
					_ = Task.Run(async () => await WeaponSync.SyncPinToDatabase(playerInfo, (ushort)paint, teamsToCheck));
				}
			}
			else
			{
				PlayerInfo playerInfo = new PlayerInfo
				{
					UserId = player.UserId,
					Slot = player.Slot,
					Index = (int)player.Index,
					SteamId = player.SteamID.ToString(),
					Name = player.PlayerName,
					IpAddress = player.IpAddress?.Split(":")[0],
				};

				foreach (var team in teamsToCheck)
					playerPins[team] = 0;

				if (!string.IsNullOrEmpty(Localizer["wp_pins_menu_select"]))
				{
					player.Print(Localizer["wp_pins_menu_select", Localizer["None"]]);
				}

				GivePlayerPin(player);

				if (WeaponSync != null)
				{
					_ = Task.Run(async () => await WeaponSync.SyncPinToDatabase(playerInfo, 0, teamsToCheck));
				}
			}
		};

		pinsSelectionMenu.AddMenuOption(Localizer["None"], handlePinsSelection);

		foreach (
			var paintName in PinsList.Select(musicObject => musicObject["name"]?.ToString() ?? "").Where(paintName => paintName.Length > 0)
		)
		{
			pinsSelectionMenu.AddMenuOption(paintName, handlePinsSelection);
		}

		_config.Additional.CommandPin.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Pin selection menu",
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

						OpenWeaponPaintsMenu(pinsSelectionMenu, player);

						return;
					}

					if (!string.IsNullOrEmpty(Localizer["wp_command_cooldown"]))
					{
						player.Print(Localizer["wp_command_cooldown"]);
					}
				}
			);
		});
	}
}
