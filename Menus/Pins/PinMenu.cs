using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OpenPinMenu(CCSPlayerController player)
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
		OpenWeaponPaintsMenu(pinsSelectionMenu, player);
	}
}
