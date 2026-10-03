using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OpenKnifeMenu(CCSPlayerController player)
	{
		if (!Utility.IsPlayerValid(player))
			return;

		if (!Config.Additional.KnifeEnabled || !_gBCommandsAllowed)
			return;

		var supportedKnifeClassnames = SkinsList
			.Select(skin => skin["weapon_name"]?.ToString())
			.Where(weaponName =>
				!string.IsNullOrEmpty(weaponName)
				&& (
					weaponName.StartsWith("weapon_knife", StringComparison.Ordinal)
					|| weaponName.StartsWith("weapon_bayonet", StringComparison.Ordinal)
				)
			)
			.Select(weaponName => weaponName!)
			.ToHashSet(StringComparer.Ordinal);

		var knifeSkinDefindexes = SkinsList
			.Where(skin => skin["weapon_name"]?.ToString() is { } weaponName && supportedKnifeClassnames.Contains(weaponName))
			.Select(skin => int.TryParse(skin["weapon_defindex"]?.ToString(), out var weaponDefindex) ? weaponDefindex : 0)
			.Where(weaponDefindex => weaponDefindex > 0)
			.Distinct()
			.ToArray();

		var knivesOnly = WeaponList
			.Where(pair => pair.Key == "weapon_knife" || supportedKnifeClassnames.Contains(pair.Key))
			.ToDictionary(pair => pair.Key, pair => pair.Value);

		var knifeModelMenu = Utility.CreateMenu(Localizer["wp_knife_menu_title"]);

		if (knifeModelMenu == null)
			return;

		Action<CCSPlayerController> backToKnifeModels = backPlayer => OpenWeaponPaintsMenu(knifeModelMenu, backPlayer);

		knifeModelMenu.AddMenuOption(
			Localizer["wp_glove_family_default_inventory"],
			(player, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				ApplyInventoryKnifeSelection(player, knifeSkinDefindexes);
			}
		);

		foreach (var knifePair in knivesOnly)
		{
			var knifeKey = knifePair.Key;
			var knifeName = knifePair.Value;

			knifeModelMenu.AddMenuOption(
				knifeName,
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					if (knifeKey == "weapon_knife")
					{
						ApplyKnifeSelection(player, knifeKey, knifeName);
						return;
					}

					OpenKnifeSkinMenu(player, knifeKey, knifeName, knifeSkinDefindexes, backToKnifeModels);
				}
			);
		}

		OpenWeaponPaintsMenu(knifeModelMenu, player);
	}
}
