using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void SetMenuBackAction(CCSPlayerController player, Action<CCSPlayerController>? backAction)
	{
		if (backAction == null)
		{
			MenuBackActions.TryRemove(player.Slot, out var removedBackAction);
			return;
		}

		MenuBackActions[player.Slot] = backAction;
	}

	private void OpenWeaponPaintsMenu(IMenu menu, CCSPlayerController player, Action<CCSPlayerController>? backAction = null)
	{
		SetMenuBackAction(player, backAction);
		menu.Open(player);
	}

	private void AddBackMenuOption(IMenu menu, Action<CCSPlayerController> backAction)
	{
		menu.AddMenuOption(
			Localizer["wp_menu_back"],
			(player, commandInfo) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				backAction(player);
			}
		);
	}

	private void SetupMenuNavigationButtons()
	{
		RegisterListener<Listeners.OnPlayerButtonsChanged>(
			(player, pressed, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				if ((pressed & PlayerButtons.Reload) != 0)
				{
					MenuBackActions.TryRemove(player.Slot, out var removedBackAction);
					return;
				}

				if ((pressed & PlayerButtons.Speed) == 0)
					return;

				if (!MenuBackActions.TryRemove(player.Slot, out var backAction))
					return;

				backAction(player);
			}
		);
	}
}
