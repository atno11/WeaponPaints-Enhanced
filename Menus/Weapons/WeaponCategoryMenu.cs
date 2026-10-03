using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void BuildWeaponCategoryMenu(
		IMenu categorySelectionMenu,
		HashSet<string> supportedWeaponClassnames,
		Dictionary<string, string> classNamesByWeapon,
		int[] knifeSkinDefindexes
	)
	{
		foreach (var category in WeaponCategoryOrder)
		{
			var weaponsInCategory = WeaponList
				.Where(weapon =>
					supportedWeaponClassnames.Contains(weapon.Key)
					&& WeaponCategoryByClassname.TryGetValue(weapon.Key, out var weaponCategory)
					&& weaponCategory == category
				)
				.ToList();

			if (weaponsInCategory.Count == 0)
				continue;

			var categoryId = category;

			categorySelectionMenu.AddMenuOption(
				Localizer[$"wp_skin_category_{categoryId}"],
				(player, _) =>
				{
					if (player == null || !Utility.IsPlayerValid(player))
					{
						return;
					}

					Action<CCSPlayerController> backToCategories = backPlayer => OpenWeaponPaintsMenu(categorySelectionMenu, backPlayer);

					OpenWeaponMenu(player, weaponsInCategory, backToCategories, classNamesByWeapon, knifeSkinDefindexes);
				}
			);
		}
	}
}
