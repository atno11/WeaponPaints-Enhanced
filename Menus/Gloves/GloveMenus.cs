using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void SetupGlovesMenu()
	{
		var gloveFamilyMenu = Utility.CreateMenu(Localizer["wp_glove_menu_family_title"]);

		if (gloveFamilyMenu == null)
			return;

		void ApplyGloveSelection(CCSPlayerController player, JObject selectedGlove)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			if (
				!int.TryParse(selectedGlove["weapon_defindex"]?.ToString(), out var weaponDefindex)
				|| !int.TryParse(selectedGlove["paint"]?.ToString(), out var paint)
			)
			{
				return;
			}

			var paintName = selectedGlove["paint_name"]?.ToString() ?? "";
			var image = selectedGlove["image"]?.ToString() ?? "";

			var playerGloves = GPlayersGlove.GetOrAdd(player.Slot, new ConcurrentDictionary<CsTeam, ushort>());

			var teamsToCheck = player.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [player.Team];

			var selectionVersion = GloveSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);

			if (Config.Additional.ShowSkinImage && !string.IsNullOrEmpty(image))
			{
				_playerWeaponImage[player.Slot] = image;

				AddTimer(2.0f, () => _playerWeaponImage.Remove(player.Slot), TimerFlags.STOP_ON_MAPCHANGE);
			}

			var playerInfo = new PlayerInfo
			{
				UserId = player.UserId,
				Slot = player.Slot,
				Index = (int)player.Index,
				SteamId = player.SteamID.ToString(),
				Name = player.PlayerName,
				IpAddress = player.IpAddress?.Split(":")[0],
			};

			if (weaponDefindex == 0)
			{
				foreach (var team in teamsToCheck)
				{
					playerGloves[team] = 0;
				}

				if (!string.IsNullOrEmpty(Localizer["wp_glove_menu_select"]))
				{
					player.Print(Localizer["wp_glove_menu_select", Localizer["wp_glove_family_default"]]);
				}

				if (WeaponSync != null)
				{
					_ = Task.Run(async () =>
					{
						var syncLock = GloveSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));

						await syncLock.WaitAsync();

						try
						{
							if (
								!GloveSelectionVersions.TryGetValue(player.Slot, out var currentVersion)
								|| currentVersion != selectionVersion
							)
							{
								return;
							}

							await WeaponSync.SyncGloveToDatabase(playerInfo, 0, teamsToCheck);
						}
						finally
						{
							syncLock.Release();
						}
					});
				}

				AddTimer(0.1f, () => GivePlayerGloves(player));
				AddTimer(0.25f, () => GivePlayerGloves(player));

				return;
			}

			var playerWeapons = GPlayerWeaponsInfo.GetOrAdd(
				player.Slot,
				new ConcurrentDictionary<CsTeam, ConcurrentDictionary<int, WeaponInfo>>()
			);

			foreach (var team in teamsToCheck)
			{
				playerGloves[team] = (ushort)weaponDefindex;

				var teamWeapons = playerWeapons.GetOrAdd(team, _ => new ConcurrentDictionary<int, WeaponInfo>());

				var weaponInfo = teamWeapons.GetOrAdd(weaponDefindex, _ => new WeaponInfo { Wear = 0.00f, Seed = 0 });

				weaponInfo.Paint = paint;
			}

			if (!string.IsNullOrEmpty(Localizer["wp_glove_menu_select"]))
			{
				player.Print(Localizer["wp_glove_menu_select", paintName]);
			}

			if (WeaponSync != null)
			{
				_ = Task.Run(async () =>
				{
					var syncLock = GloveSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));

					await syncLock.WaitAsync();

					try
					{
						if (!GloveSelectionVersions.TryGetValue(player.Slot, out var currentVersion) || currentVersion != selectionVersion)
						{
							return;
						}

						await WeaponSync.SyncGloveToDatabase(playerInfo, (ushort)weaponDefindex, teamsToCheck);

						if (!GloveSelectionVersions.TryGetValue(player.Slot, out currentVersion) || currentVersion != selectionVersion)
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

			AddTimer(0.1f, () => GivePlayerGloves(player));
			AddTimer(0.25f, () => GivePlayerGloves(player));
		}

		void ApplyInventoryGloveSelection(CCSPlayerController player)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			var selectionVersion = GloveSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);

			GPlayersGlove.TryRemove(player.Slot, out _);

			if (GPlayerWeaponsInfo.TryGetValue(player.Slot, out var playerWeapons))
			{
				foreach (var teamWeapons in playerWeapons.Values)
				{
					foreach (var gloveDefindex in GloveFamilyOrder)
						teamWeapons.TryRemove(gloveDefindex, out _);
				}
			}

			if ((LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
				RestoreInventoryGloves(player);

			var playerInfo = new PlayerInfo
			{
				UserId = player.UserId,
				Slot = player.Slot,
				Index = (int)player.Index,
				SteamId = player.SteamID.ToString(),
				Name = player.PlayerName,
				IpAddress = player.IpAddress?.Split(":")[0],
			};

			if (!string.IsNullOrEmpty(Localizer["wp_glove_menu_select"]))
			{
				player.Print(Localizer["wp_glove_menu_select", Localizer["wp_glove_family_default_inventory"]]);
			}

			if (WeaponSync != null)
			{
				_ = Task.Run(async () =>
				{
					var syncLock = GloveSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));

					await syncLock.WaitAsync();

					try
					{
						if (!GloveSelectionVersions.TryGetValue(player.Slot, out var currentVersion) || currentVersion != selectionVersion)
						{
							return;
						}

						await WeaponSync.DeleteGloveFromDatabase(playerInfo, GloveFamilyOrder);
					}
					finally
					{
						syncLock.Release();
					}
				});
			}
		}

		gloveFamilyMenu.AddMenuOption(
			Localizer["wp_glove_family_default_inventory"],
			(player, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				ApplyInventoryGloveSelection(player);
			}
		);

		var defaultGlove = GlovesList.FirstOrDefault(glove =>
			int.TryParse(glove["weapon_defindex"]?.ToString(), out var weaponDefindex) && weaponDefindex == 0
		);

		if (defaultGlove != null)
		{
			gloveFamilyMenu.AddMenuOption(
				Localizer["wp_glove_family_default"],
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					ApplyGloveSelection(player, defaultGlove);
				}
			);
		}

		foreach (var gloveDefindex in GloveFamilyOrder)
		{
			if (!GloveFamilyByDefindex.TryGetValue(gloveDefindex, out var familyId))
			{
				continue;
			}

			var glovesInFamily = GlovesList
				.Where(glove =>
					int.TryParse(glove["weapon_defindex"]?.ToString(), out var weaponDefindex) && weaponDefindex == gloveDefindex
				)
				.ToList();

			if (glovesInFamily.Count == 0)
				continue;

			gloveFamilyMenu.AddMenuOption(
				Localizer[$"wp_glove_family_{familyId}"],
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					Action<CCSPlayerController> backToGloveFamilies = backPlayer => OpenWeaponPaintsMenu(gloveFamilyMenu, backPlayer);

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
									return;

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
			);
		}

		_config.Additional.CommandGlove.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Gloves selection menu",
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player) || !_gBCommandsAllowed)
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

						OpenWeaponPaintsMenu(gloveFamilyMenu, player);

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
