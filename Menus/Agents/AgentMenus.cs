using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void SetupAgentsMenu()
	{
		var terroristAgentsMenu = Utility.CreateMenu(Localizer["wp_agent_menu_title"]);
		var counterTerroristAgentsMenu = Utility.CreateMenu(Localizer["wp_agent_menu_title"]);

		if (terroristAgentsMenu == null || counterTerroristAgentsMenu == null)
			return;

		foreach (var agentObject in AgentsList)
		{
			if (agentObject["team"]?.Value<int>() is not { } teamNum)
				continue;

			var paintName = agentObject["agent_name"]?.ToString() ?? "";

			if (string.IsNullOrEmpty(paintName))
				continue;

			switch (teamNum)
			{
				case 2:
					terroristAgentsMenu.AddMenuOption(
						paintName,
						(player, option) =>
						{
							if (!Utility.IsPlayerValid(player))
								return;

							ApplyAgentSelection(player, option);
						}
					);
					break;

				case 3:
					terroristAgentsMenu.AddMenuOption(
						paintName,
						(player, option) =>
						{
							if (!Utility.IsPlayerValid(player))
								return;

							ApplyAgentSelection(player, option);
						}
					);
					break;
			}
		}

		_config.Additional.CommandAgent.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Agents selection menu",
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player) || !_gBCommandsAllowed)
						return;

					if (player == null || player.UserId == null)
						return;

					if (
						!CommandsCooldown.TryGetValue(player.Slot, out DateTime cooldownEndTime)
						|| DateTime.UtcNow
							>= (CommandsCooldown.TryGetValue(player.Slot, out cooldownEndTime) ? cooldownEndTime : DateTime.UtcNow)
					)
					{
						var agentsSelectionMenu = player.TeamNum switch
						{
							2 => terroristAgentsMenu,
							3 => counterTerroristAgentsMenu,
							_ => null,
						};

						if (agentsSelectionMenu == null)
							return;
						CommandsCooldown[player.Slot] = DateTime.UtcNow.AddSeconds(Config.CmdRefreshCooldownSeconds);

						OpenWeaponPaintsMenu(agentsSelectionMenu, player);

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
