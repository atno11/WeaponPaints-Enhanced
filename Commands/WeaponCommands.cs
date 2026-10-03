using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void RegisterWeaponSkinCommands(IMenu categorySelectionMenu)
	{
		_config.Additional.CommandSkinSelection.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Skins selection menu",
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player))
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

						OpenWeaponPaintsMenu(categorySelectionMenu, player);

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
