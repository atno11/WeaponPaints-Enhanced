using CounterStrikeSharp.API.Core;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OpenWeaponMenu(
		CCSPlayerController player,
		List<KeyValuePair<string, string>> weaponsInCategory,
		Action<CCSPlayerController> backToCategories,
		Dictionary<string, string> classNamesByWeapon,
		int[] knifeSkinDefindexes
	)
	{
		if (!Utility.IsPlayerValid(player))
			return;

		var weaponSelectionMenu = Utility.CreateMenu(Localizer["wp_skin_menu_weapon_title"]);

		if (weaponSelectionMenu == null)
			return;

		Action<CCSPlayerController> backToWeapons = backPlayer => OpenWeaponPaintsMenu(weaponSelectionMenu, backPlayer, backToCategories);

		AddBackMenuOption(weaponSelectionMenu, backToCategories);

		foreach (var weapon in weaponsInCategory)
		{
			weaponSelectionMenu.AddMenuOption(
				weapon.Value,
				(p, option) => OpenWeaponSkinMenu(p, option, backToWeapons, classNamesByWeapon, knifeSkinDefindexes)
			);
		}

		OpenWeaponPaintsMenu(weaponSelectionMenu, player, backToCategories);
	}
}
