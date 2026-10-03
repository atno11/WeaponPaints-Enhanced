using System.Globalization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OpenWearCustomizationMenu(
		CCSPlayerController player,
		int weaponDefindex,
		string weaponName,
		string paintName,
		CsTeam[] teams,
		Action<CCSPlayerController> backAction
	)
	{
		if (!Utility.IsPlayerValid(player))
			return;

		if (!TryGetWeaponWear(player, weaponDefindex, teams, out var currentWear))
			return;

		var currentExteriorKey = GetWearExteriorLocalizationKey(currentWear);

		var wearMenu = Utility.CreateMenu(
			Localizer["wp_float_menu_title", weaponName, paintName, currentWear.ToString("0.000000", CultureInfo.InvariantCulture)]
		);

		if (wearMenu == null)
			return;

		AddBackMenuOption(wearMenu, backAction);

		foreach (var exterior in WearExteriors)
		{
			if (!TryGetWearPreset(exterior.LocalizationKey, out var presetWear))
				continue;

			var optionText =
				exterior.LocalizationKey == currentExteriorKey
					? $"• {Localizer[exterior.LocalizationKey]}"
					: Localizer[exterior.LocalizationKey];

			wearMenu.AddMenuOption(
				optionText,
				(p, _option) =>
				{
					if (!Utility.IsPlayerValid(p))
						return;

					if (!TrySetWeaponWear(p, weaponDefindex, teams, presetWear, clampToRange: true, out _))
						return;

					OpenWearCustomizationMenu(p, weaponDefindex, weaponName, paintName, teams, backAction);
				}
			);
		}

		var wearAdjustments = new[] { -0.100f, -0.010f, -0.001f, 0.001f, 0.010f, 0.100f };

		foreach (var delta in wearAdjustments)
		{
			var optionText = delta > 0 ? $"+{delta:0.000}" : $"{delta:0.000}";

			wearMenu.AddMenuOption(
				optionText,
				(p, _) =>
				{
					if (!Utility.IsPlayerValid(p))
						return;

					if (!TryAdjustWeaponWear(p, weaponDefindex, teams, delta))
						return;

					OpenWearCustomizationMenu(p, weaponDefindex, weaponName, paintName, teams, backAction);
				}
			);
		}

		OpenWeaponPaintsMenu(wearMenu, player, backAction);
	}
}
