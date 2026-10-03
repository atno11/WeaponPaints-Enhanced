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

		foreach (
			var paintName in PinsList.Select(pinObject => pinObject["name"]?.ToString() ?? "").Where(paintName => paintName.Length > 0)
		)
		{
			pinsSelectionMenu.AddMenuOption(
				paintName,
				(player, option) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					ApplyPinSelection(player, option);
				}
			);
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
