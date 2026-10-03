using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

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

		void RemoveRuntimePaintOverride(int slot, int weaponDefindex)
		{
			if (!GPlayerWeaponsInfo.TryGetValue(slot, out var playerWeapons))
				return;

			foreach (var teamWeapons in playerWeapons.Values)
				teamWeapons.TryRemove(weaponDefindex, out _);
		}

		void RemoveRuntimeKnifeOverrides(int slot)
		{
			GPlayersKnife.TryRemove(slot, out _);

			if (!GPlayerWeaponsInfo.TryGetValue(slot, out var playerWeapons))
				return;

			foreach (var teamWeapons in playerWeapons.Values)
			{
				foreach (var knifeDefindex in knifeSkinDefindexes)
					teamWeapons.TryRemove(knifeDefindex, out _);
			}
		}

		void ApplyInventoryWeaponSelection(CCSPlayerController player, int weaponDefindex)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			var playerInfo = new PlayerInfo
			{
				UserId = player.UserId,
				Slot = player.Slot,
				Index = (int)player.Index,
				SteamId = player.SteamID.ToString(),
				Name = player.PlayerName,
				IpAddress = player.IpAddress?.Split(":")[0],
			};

			if (IsKnifeDefindex(weaponDefindex))
			{
				var selectionVersion = KnifeSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);
				RemoveRuntimeKnifeOverrides(player.Slot);

				if ((LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
					RestoreInventoryKnife(player, selectionVersion);

				if (!string.IsNullOrEmpty(Localizer["wp_skin_menu_select"]))
					player.Print(Localizer["wp_skin_menu_select", Localizer["wp_glove_family_default_inventory"]]);

				if (WeaponSync == null)
					return;

				_ = Task.Run(async () =>
				{
					var syncLock = KnifeSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));
					await syncLock.WaitAsync();

					try
					{
						if (!KnifeSelectionVersions.TryGetValue(player.Slot, out var currentVersion) || currentVersion != selectionVersion)
							return;

						await WeaponSync.DeleteKnifeFromDatabase(playerInfo, knifeSkinDefindexes);
					}
					finally
					{
						syncLock.Release();
					}
				});

				return;
			}

			var versionKey = (player.Slot, weaponDefindex);
			var skinVersion = SkinSelectionVersions.AddOrUpdate(versionKey, 1, (_, currentVersion) => currentVersion + 1);

			RemoveRuntimePaintOverride(player.Slot, weaponDefindex);

			if ((LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
				RestoreInventoryWeapon(player, weaponDefindex);

			if (!string.IsNullOrEmpty(Localizer["wp_skin_menu_select"]))
				player.Print(Localizer["wp_skin_menu_select", Localizer["wp_glove_family_default_inventory"]]);

			if (WeaponSync == null)
				return;

			_ = Task.Run(async () =>
			{
				var syncLock = SkinSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));
				await syncLock.WaitAsync();

				try
				{
					if (!SkinSelectionVersions.TryGetValue(versionKey, out var currentVersion) || currentVersion != skinVersion)
						return;

					await WeaponSync.DeleteWeaponPaintFromDatabase(playerInfo, weaponDefindex);
				}
				finally
				{
					syncLock.Release();
				}
			});
		}

		var handleWeaponSelection = (CCSPlayerController? player, ChatMenuOption option, Action<CCSPlayerController> backAction) =>
		{
			if (!Utility.IsPlayerValid(player) || player == null)
				return;

			var selectedWeapon = option.Text;
			if (!classNamesByWeapon.TryGetValue(selectedWeapon, out var selectedWeaponClassname))
				return;

			var skinsForSelectedWeapon = SkinsList
				.Where(skin => skin.TryGetValue("weapon_name", out var weaponName) && weaponName?.ToString() == selectedWeaponClassname)
				.ToList();

			if (skinsForSelectedWeapon.Count == 0)
				return;

			var selectedWeaponDefindex = skinsForSelectedWeapon
				.Select(skin => int.TryParse(skin["weapon_defindex"]?.ToString(), out var weaponDefindex) ? weaponDefindex : 0)
				.FirstOrDefault(weaponDefindex => weaponDefindex > 0);

			if (selectedWeaponDefindex <= 0)
				return;

			var skinSubMenu = Utility.CreateMenu(Localizer["wp_skin_menu_skin_title", selectedWeapon]);
			if (skinSubMenu == null)
				return;

			AddBackMenuOption(skinSubMenu, backAction);
			skinSubMenu.AddMenuOption(
				Localizer["wp_glove_family_default_inventory"],
				(p, _) =>
				{
					if (!Utility.IsPlayerValid(p))
						return;

					ApplyInventoryWeaponSelection(p, selectedWeaponDefindex);
				}
			);

			foreach (var skin in skinsForSelectedWeapon)
			{
				if (
					!skin.TryGetValue("paint_name", out var paintNameObject)
					|| !skin.TryGetValue("paint", out var paintObject)
					|| !skin.TryGetValue("weapon_defindex", out var weaponDefindexObject)
				)
				{
					continue;
				}

				var paintName = paintNameObject?.ToString();
				if (
					string.IsNullOrEmpty(paintName)
					|| !int.TryParse(paintObject?.ToString(), out var paint)
					|| !int.TryParse(weaponDefindexObject?.ToString(), out var weaponDefindex)
				)
				{
					continue;
				}

				var separatorIndex = paintName.IndexOf('|');
				var finishName = separatorIndex >= 0 ? paintName[(separatorIndex + 1)..].Trim() : paintName;
				var image = skin["image"]?.ToString() ?? "";

				skinSubMenu.AddMenuOption(
					$"{paintName} ({paint})",
					(p, option) =>
					{
						if (!Utility.IsPlayerValid(p))
							return;

						if (Config.Additional.ShowSkinImage)
						{
							_playerWeaponImage[p.Slot] = image;
							AddTimer(2.0f, () => _playerWeaponImage.Remove(p.Slot), TimerFlags.STOP_ON_MAPCHANGE);
						}

						if (!string.IsNullOrEmpty(Localizer["wp_skin_menu_select"]))
							p.Print(Localizer["wp_skin_menu_select", $"{paintName} ({paint})"]);

						var playerSkins = GPlayerWeaponsInfo.GetOrAdd(
							p.Slot,
							new ConcurrentDictionary<CsTeam, ConcurrentDictionary<int, WeaponInfo>>()
						);
						var teamsToCheck = p.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [p.Team];

						foreach (var team in teamsToCheck)
						{
							var teamWeapons = playerSkins.GetOrAdd(team, _ => new ConcurrentDictionary<int, WeaponInfo>());
							var weaponInfo = teamWeapons.GetOrAdd(weaponDefindex, _ => new WeaponInfo { Wear = 0.01f, Seed = 0 });

							weaponInfo.Paint = paint;
						}

						var playerInfo = new PlayerInfo
						{
							UserId = p.UserId,
							Slot = p.Slot,
							Index = (int)p.Index,
							SteamId = p.SteamID.ToString(),
							Name = p.PlayerName,
							IpAddress = p.IpAddress?.Split(":")[0],
						};

						if (IsKnifeDefindex(weaponDefindex))
						{
							var playerKnives = GPlayersKnife.GetOrAdd(p.Slot, new ConcurrentDictionary<CsTeam, string>());
							foreach (var team in teamsToCheck)
								playerKnives[team] = selectedWeaponClassname;

							var selectionVersion = KnifeSelectionVersions.AddOrUpdate(p.Slot, 1, (_, currentVersion) => currentVersion + 1);

							if (_gBCommandsAllowed && (LifeState_t)p.LifeState == LifeState_t.LIFE_ALIVE)
								ApplyPlayerKnifeRuntimeSelection(p, selectionVersion);

							OpenWearCustomizationMenu(
								p,
								weaponDefindex,
								selectedWeapon,
								finishName,
								teamsToCheck,
								backPlayer => OpenWeaponPaintsMenu(skinSubMenu, backPlayer, backAction)
							);

							if (WeaponSync == null)
								return;

							_ = Task.Run(async () =>
							{
								var syncLock = KnifeSyncLocks.GetOrAdd(p.Slot, _ => new SemaphoreSlim(1, 1));
								await syncLock.WaitAsync();

								try
								{
									if (
										!KnifeSelectionVersions.TryGetValue(p.Slot, out var currentVersion)
										|| currentVersion != selectionVersion
									)
										return;

									await WeaponSync.SyncKnifeToDatabase(playerInfo, selectedWeaponClassname, teamsToCheck);

									if (
										!KnifeSelectionVersions.TryGetValue(p.Slot, out currentVersion)
										|| currentVersion != selectionVersion
									)
										return;

									await WeaponSync.SyncWeaponPaintToDatabase(playerInfo, weaponDefindex, teamsToCheck);
								}
								finally
								{
									syncLock.Release();
								}
							});

							return;
						}

						var versionKey = (p.Slot, weaponDefindex);
						var skinVersion = SkinSelectionVersions.AddOrUpdate(versionKey, 1, (_, currentVersion) => currentVersion + 1);

						if (_gBCommandsAllowed && (LifeState_t)p.LifeState == LifeState_t.LIFE_ALIVE)
							RefreshWeaponSkin(p, weaponDefindex);

						OpenWearCustomizationMenu(
							p,
							weaponDefindex,
							selectedWeapon,
							finishName,
							teamsToCheck,
							backPlayer => OpenWeaponPaintsMenu(skinSubMenu, backPlayer, backAction)
						);

						if (WeaponSync == null)
							return;

						_ = Task.Run(async () =>
						{
							var syncLock = SkinSyncLocks.GetOrAdd(p.Slot, _ => new SemaphoreSlim(1, 1));
							await syncLock.WaitAsync();

							try
							{
								if (!SkinSelectionVersions.TryGetValue(versionKey, out var currentVersion) || currentVersion != skinVersion)
									return;

								await WeaponSync.SyncWeaponPaintToDatabase(playerInfo, weaponDefindex, teamsToCheck);
							}
							finally
							{
								syncLock.Release();
							}
						});
					}
				);
			}

			OpenWeaponPaintsMenu(skinSubMenu, player, backAction);
		};

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
						return;

					Action<CCSPlayerController> backToCategories = backPlayer => OpenWeaponPaintsMenu(categorySelectionMenu, backPlayer);
					var weaponSelectionMenu = Utility.CreateMenu(Localizer["wp_skin_menu_weapon_title"]);

					if (weaponSelectionMenu == null)
						return;

					Action<CCSPlayerController> backToWeapons = backPlayer =>
						OpenWeaponPaintsMenu(weaponSelectionMenu, backPlayer, backToCategories);

					AddBackMenuOption(weaponSelectionMenu, backToCategories);

					foreach (var weapon in weaponsInCategory)
						weaponSelectionMenu.AddMenuOption(weapon.Value, (p, option) => handleWeaponSelection(p, option, backToWeapons));

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
						player.Print(Localizer["wp_command_cooldown"]);
				}
			);
		});
	}
}
