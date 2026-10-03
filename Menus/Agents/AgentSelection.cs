using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void ApplyAgentSelection(CCSPlayerController player, ChatMenuOption option)
	{
		if (!Utility.IsPlayerValid(player))
			return;

		var selectedPaintName = option.Text;

		var selectedAgent = AgentsList.FirstOrDefault(agent =>
			agent.ContainsKey("agent_name")
			&& agent["agent_name"] != null
			&& agent["agent_name"]!.ToString() == selectedPaintName
			&& agent["team"] != null
			&& (int)(agent["team"]!) == player.TeamNum
		);

		if (selectedAgent == null)
			return;

		if (!selectedAgent.ContainsKey("model"))
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
				_ => (selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString(), null),
				(_, oldValue) => (selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString(), oldValue.T)
			);
		}
		else
		{
			GPlayersAgent.AddOrUpdate(
				player.Slot,
				_ => (null, selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString()),
				(_, oldValue) =>
					(oldValue.CT, selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString())
			);
		}

		GivePlayerAgent(player);

		if (WeaponSync != null)
		{
			_ = Task.Run(async () => await WeaponSync.SyncAgentToDatabase(playerInfo));
		}
	}
}
