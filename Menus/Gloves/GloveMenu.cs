using CounterStrikeSharp.API.Core;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OpenGloveMenu(CCSPlayerController player)
	{
		if (!Utility.IsPlayerValid(player))
			return;

		var gloveFamilyMenu = Utility.CreateMenu(Localizer["wp_glove_menu_family_title"]);

		if (gloveFamilyMenu == null)
			return;

		gloveFamilyMenu.AddMenuOption(
			Localizer["wp_glove_family_default_inventory"],
			(p, _) =>
			{
				if (!Utility.IsPlayerValid(p))
					return;

				ApplyInventoryGloveSelection(p);
			}
		);

		var defaultGlove = GlovesList.FirstOrDefault(glove =>
			int.TryParse(glove["weapon_defindex"]?.ToString(), out var weaponDefindex) && weaponDefindex == 0
		);

		if (defaultGlove != null)
		{
			gloveFamilyMenu.AddMenuOption(
				Localizer["wp_glove_family_default"],
				(p, _) =>
				{
					if (!Utility.IsPlayerValid(p))
						return;

					ApplyGloveSelection(p, defaultGlove);
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
				(p, _) =>
				{
					if (!Utility.IsPlayerValid(p))
						return;

					Action<CCSPlayerController> backToGloveFamilies = backPlayer => OpenWeaponPaintsMenu(gloveFamilyMenu, backPlayer);

					OpenGloveSkinMenu(p, familyId, glovesInFamily, backToGloveFamilies);
				}
			);
		}

		OpenWeaponPaintsMenu(gloveFamilyMenu, player);
	}
}
