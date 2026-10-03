using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void ApplyPinSelection(CCSPlayerController player, ChatMenuOption option)
	{
		if (!Utility.IsPlayerValid(player))
			return;

		var selectedPaintName = option.Text;

		var playerPins = GPlayersPin.GetOrAdd(player.Slot, new ConcurrentDictionary<CsTeam, ushort>());

		var teamsToCheck = player.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [player.Team];

		var selectedPin = PinsList.FirstOrDefault(pin => pin.ContainsKey("name") && pin["name"]?.ToString() == selectedPaintName);

		if (selectedPin == null)
			return;

		if (
			!selectedPin.ContainsKey("id")
			|| !selectedPin.ContainsKey("name")
			|| !int.TryParse(selectedPin["id"]?.ToString(), out var paint)
		)
		{
			return;
		}

		foreach (var team in teamsToCheck)
			playerPins[team] = (ushort)paint;

		var image = selectedPin["image"]?.ToString() ?? "";

		if (Config.Additional.ShowSkinImage)
		{
			_playerWeaponImage[player.Slot] = image;

			AddTimer(2.0f, () => _playerWeaponImage.Remove(player.Slot), TimerFlags.STOP_ON_MAPCHANGE);
		}

		if (!string.IsNullOrEmpty(Localizer["wp_pins_menu_select"]))
		{
			player.Print(Localizer["wp_pins_menu_select", selectedPaintName]);
		}

		var playerInfo = new PlayerInfo
		{
			UserId = player.UserId,
			Slot = player.Slot,
			Index = (int)player.Index,
			SteamId = player.SteamID.ToString(),
			Name = player.PlayerName,
			IpAddress = player.IpAddress?.Split(":")[0],
		};

		if (WeaponSync != null)
		{
			_ = Task.Run(async () => await WeaponSync.SyncPinToDatabase(playerInfo, (ushort)paint, teamsToCheck));
		}
	}
}
