using CounterStrikeSharp.API.Core;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void SetupSkinsMenu()
	{
		var classNamesByWeapon = WeaponList
			.Except([new KeyValuePair<string, string>("weapon_knife", "Default Knife")])
			.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

		var supportedWeaponClassnames = SkinsList
			.Select(skin => skin["weapon_name"]?.ToString())
			.Where(weaponName => !string.IsNullOrEmpty(weaponName))
			.Select(weaponName => weaponName!)
			.ToHashSet(StringComparer.Ordinal);

		var knifeSkinDefindexes = SkinsList
			.Where(skin =>
				skin["weapon_name"]?.ToString() is { } weaponName
				&& (
					weaponName.StartsWith("weapon_knife", StringComparison.Ordinal)
					|| weaponName.StartsWith("weapon_bayonet", StringComparison.Ordinal)
				)
			)
			.Select(skin => int.TryParse(skin["weapon_defindex"]?.ToString(), out var weaponDefindex) ? weaponDefindex : 0)
			.Where(weaponDefindex => weaponDefindex > 0)
			.Distinct()
			.ToArray();

		var categorySelectionMenu = Utility.CreateMenu(Localizer["wp_skin_menu_category_title"]);

		if (categorySelectionMenu == null)
			return;

		BuildWeaponCategoryMenu(categorySelectionMenu, supportedWeaponClassnames, classNamesByWeapon, knifeSkinDefindexes);

		RegisterWeaponSkinCommands(categorySelectionMenu);
	}
}
