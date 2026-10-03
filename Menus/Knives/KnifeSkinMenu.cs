using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OpenKnifeSkinMenu(
		CCSPlayerController player,
		string knifeKey,
		string knifeName,
		int[] knifeSkinDefindexes,
		Action<CCSPlayerController> backToKnifeModels
	)
	{
		if (!Utility.IsPlayerValid(player))
			return;

		var skinsForKnife = SkinsList
			.Where(skin => skin.TryGetValue("weapon_name", out var weaponName) && weaponName?.ToString() == knifeKey)
			.ToList();

		if (skinsForKnife.Count == 0)
			return;

		var knifeSkinMenu = Utility.CreateMenu(Localizer["wp_skin_menu_skin_title", knifeName]);

		if (knifeSkinMenu == null)
			return;

		AddBackMenuOption(knifeSkinMenu, backToKnifeModels);

		knifeSkinMenu.AddMenuOption(
			Localizer["wp_glove_family_default_inventory"],
			(p, _) =>
			{
				if (!Utility.IsPlayerValid(p))
					return;

				ApplyInventoryKnifeSelection(p, knifeSkinDefindexes);
			}
		);

		foreach (var skin in skinsForKnife)
		{
			if (
				!int.TryParse(skin["weapon_defindex"]?.ToString(), out var weaponDefindex)
				|| !int.TryParse(skin["paint"]?.ToString(), out var paint)
			)
			{
				continue;
			}

			var paintName = skin["paint_name"]?.ToString();

			if (string.IsNullOrEmpty(paintName))
				continue;

			var separatorIndex = paintName.IndexOf('|');

			var finishName = separatorIndex >= 0 ? paintName[(separatorIndex + 1)..].Trim() : paintName;

			var image = skin["image"]?.ToString();

			knifeSkinMenu.AddMenuOption(
				finishName,
				(p, _) =>
				{
					if (!Utility.IsPlayerValid(p))
						return;

					ApplyKnifeSelection(p, knifeKey, knifeName, weaponDefindex, paint, paintName, image);

					var teamsToCheck = p.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [p.Team];

					OpenWearCustomizationMenu(
						p,
						weaponDefindex,
						knifeName,
						finishName,
						teamsToCheck,
						backPlayer => OpenWeaponPaintsMenu(knifeSkinMenu, backPlayer, backToKnifeModels)
					);
				}
			);
		}

		OpenWeaponPaintsMenu(knifeSkinMenu, player, backToKnifeModels);
	}
}
