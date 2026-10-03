using CounterStrikeSharp.API.Core;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void RegisterPinCommands()
	{
		_config.Additional.CommandPin.ForEach(command =>
		{
			AddCommand(
				$"css_{command}",
				"Pin selection menu",
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player) || !_gBCommandsAllowed)
					{
						return;
					}

					if (player == null || player.UserId == null)
					{
						return;
					}

					if (
						!CommandsCooldown.TryGetValue(player.Slot, out var cooldownEndTime)
						|| DateTime.UtcNow
							>= (CommandsCooldown.TryGetValue(player.Slot, out cooldownEndTime) ? cooldownEndTime : DateTime.UtcNow)
					)
					{
						CommandsCooldown[player.Slot] = DateTime.UtcNow.AddSeconds(Config.CmdRefreshCooldownSeconds);

						OpenPinMenu(player);
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
