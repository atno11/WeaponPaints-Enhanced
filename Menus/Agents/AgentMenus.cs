using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void SetupAgentsMenu()
	{
		var handleAgentSelection = (CCSPlayerController? player, ChatMenuOption option) =>
		{
			if (!Utility.IsPlayerValid(player) || player is null)
				return;

			var selectedPaintName = option.Text;

			var selectedAgent = AgentsList.FirstOrDefault(g =>
				g.ContainsKey("agent_name")
				&& g["agent_name"] != null
				&& g["agent_name"]!.ToString() == selectedPaintName
				&& g["team"] != null
				&& (int)(g["team"]!) == player.TeamNum
			);

			if (selectedAgent == null)
				return;

			if (selectedAgent.ContainsKey("model"))
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

				if (Config.Additional.ShowSkinImage)
				{
					var image = selectedAgent["image"]?.ToString() ?? "";

					_playerWeaponImage[player.Slot] = image;

					AddTimer(2.0f, () => _playerWeaponImage.Remove(player.Slot), TimerFlags.STOP_ON_MAPCHANGE);
				}

				if (!string.IsNullOrEmpty(Localizer["wp_agent_menu_select"]))
				{
					player.Print(Localizer["wp_agent_menu_select", selectedPaintName]);
				}

				if (player.TeamNum == 3)
				{
					GPlayersAgent.AddOrUpdate(
						player.Slot,
						key => (selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString(), null),
						(key, oldValue) =>
							(selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString(), oldValue.T)
					);
				}
				else
				{
					GPlayersAgent.AddOrUpdate(
						player.Slot,
						key => (null, selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString()),
						(key, oldValue) =>
							(oldValue.CT, selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString())
					);
				}

				GivePlayerAgent(player);

				if (WeaponSync != null)
				{
					_ = Task.Run(async () => await WeaponSync.SyncAgentToDatabase(playerInfo));
				}
			}
		};

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
					terroristAgentsMenu.AddMenuOption(paintName, handleAgentSelection);
					break;

				case 3:
					counterTerroristAgentsMenu.AddMenuOption(paintName, handleAgentSelection);
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
