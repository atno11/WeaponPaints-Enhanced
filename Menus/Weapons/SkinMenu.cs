using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OpenWeaponSkinMenu(
		CCSPlayerController? player,
		ChatMenuOption selectedOption,
		Action<CCSPlayerController> backAction,
		Dictionary<string, string> classNamesByWeapon,
		int[] knifeSkinDefindexes
	)
	{
		if (!Utility.IsPlayerValid(player) || player == null)
			return;

		var selectedWeapon = selectedOption.Text;

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
			(p, _defaultInventoryOption) =>
			{
				if (!Utility.IsPlayerValid(p))
					return;

				ApplyInventoryWeaponSelection(p, selectedWeaponDefindex, knifeSkinDefindexes);
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
				(p, _skinSubMenuOption) =>
				{
					if (!Utility.IsPlayerValid(p))
						return;

					if (Config.Additional.ShowSkinImage)
					{
						_playerWeaponImage[p.Slot] = image;

						AddTimer(2.0f, () => _playerWeaponImage.Remove(p.Slot), TimerFlags.STOP_ON_MAPCHANGE);
					}

					if (!string.IsNullOrEmpty(Localizer["wp_skin_menu_select"]))
					{
						p.Print(Localizer["wp_skin_menu_select", $"{paintName} ({paint})"]);
					}

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
						{
							ApplyPlayerKnifeRuntimeSelection(p, selectionVersion);
						}

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
								{
									return;
								}

								await WeaponSync.SyncKnifeToDatabase(playerInfo, selectedWeaponClassname, teamsToCheck);

								if (!KnifeSelectionVersions.TryGetValue(p.Slot, out currentVersion) || currentVersion != selectionVersion)
								{
									return;
								}

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
					{
						RefreshWeaponSkin(p, weaponDefindex);
					}

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
							{
								return;
							}

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
	}
}
