using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OpenGloveSkinMenu(
		CCSPlayerController player,
		string familyId,
		List<JObject> glovesInFamily,
		Action<CCSPlayerController> backToGloveFamilies
	)
	{
		if (!Utility.IsPlayerValid(player))
			return;

		var gloveSkinMenu = Utility.CreateMenu(Localizer[$"wp_glove_family_{familyId}"]);

		if (gloveSkinMenu == null)
			return;

		AddBackMenuOption(gloveSkinMenu, backToGloveFamilies);

		foreach (var glove in glovesInFamily)
		{
			var paintName = glove["paint_name"]?.ToString();

			if (string.IsNullOrEmpty(paintName))
				continue;

			var separatorIndex = paintName.IndexOf('|');

			var finishName = separatorIndex >= 0 ? paintName[(separatorIndex + 1)..].Trim() : paintName;

			gloveSkinMenu.AddMenuOption(
				finishName,
				(p, _) =>
				{
					if (!Utility.IsPlayerValid(p))
						return;

					ApplyGloveSelection(p, glove);

					if (!int.TryParse(glove["weapon_defindex"]?.ToString(), out var weaponDefindex) || weaponDefindex == 0)
					{
						return;
					}

					var teamsToCheck = p.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [p.Team];

					OpenWearCustomizationMenu(
						p,
						weaponDefindex,
						Localizer[$"wp_glove_family_{familyId}"],
						finishName,
						teamsToCheck,
						backPlayer => OpenWeaponPaintsMenu(gloveSkinMenu, backPlayer, backToGloveFamilies)
					);
				}
			);
		}

		OpenWeaponPaintsMenu(gloveSkinMenu, player, backToGloveFamilies);
	}
}
