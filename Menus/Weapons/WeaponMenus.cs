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

					var weaponSelectionMenu = Utility.CreateMenu(Localizer["wp_skin_menu_weapon_title"]);

					if (weaponSelectionMenu == null)
						return;

					Action<CCSPlayerController> backToWeapons = backPlayer =>
						OpenWeaponPaintsMenu(weaponSelectionMenu, backPlayer, backToCategories);

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
			);
		}

		_config.Additional.CommandSkinSelection.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Skins selection menu",
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player))
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

						OpenWeaponPaintsMenu(categorySelectionMenu, player);

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
