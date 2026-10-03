using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OpenAgentMenu(CCSPlayerController player)
	{
		if (!Utility.IsPlayerValid(player))
			return;

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
		var agentsSelectionMenu = player.TeamNum switch
		{
			2 => terroristAgentsMenu,
			3 => counterTerroristAgentsMenu,
			_ => null,
		};

		if (agentsSelectionMenu == null)
			return;

		OpenWeaponPaintsMenu(agentsSelectionMenu, player);
	}
}
