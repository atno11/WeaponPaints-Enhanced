using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void SetupGlovesMenu()
	{
		var gloveFamilyMenu = Utility.CreateMenu(Localizer["wp_glove_menu_family_title"]);

		if (gloveFamilyMenu == null)
			return;

		gloveFamilyMenu.AddMenuOption(
			Localizer["wp_glove_family_default_inventory"],
			(player, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				ApplyInventoryGloveSelection(player);
			}
		);

		var defaultGlove = GlovesList.FirstOrDefault(glove =>
			int.TryParse(glove["weapon_defindex"]?.ToString(), out var weaponDefindex) && weaponDefindex == 0
		);

		if (defaultGlove != null)
		{
			gloveFamilyMenu.AddMenuOption(
				Localizer["wp_glove_family_default"],
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					ApplyGloveSelection(player, defaultGlove);
				}
			);
		}

		foreach (var gloveDefindex in GloveFamilyOrder)
		{
			if (!GloveFamilyByDefindex.TryGetValue(gloveDefindex, out var familyId))
			{
				continue;
			}

			var glovesInFamily = GlovesList
				.Where(glove =>
					int.TryParse(glove["weapon_defindex"]?.ToString(), out var weaponDefindex) && weaponDefindex == gloveDefindex
				)
				.ToList();

			if (glovesInFamily.Count == 0)
				continue;

			gloveFamilyMenu.AddMenuOption(
				Localizer[$"wp_glove_family_{familyId}"],
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					Action<CCSPlayerController> backToGloveFamilies = backPlayer => OpenWeaponPaintsMenu(gloveFamilyMenu, backPlayer);

					OpenGloveSkinMenu(player, familyId, glovesInFamily, backToGloveFamilies);
				}
			);
		}

		_config.Additional.CommandGlove.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Gloves selection menu",
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

						OpenWeaponPaintsMenu(gloveFamilyMenu, player);

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
