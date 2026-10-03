using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void SetupKnifeMenu()
	{
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

		void ApplyInventoryKnifeSelection(CCSPlayerController player)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			var selectionVersion = KnifeSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);

			GPlayersKnife.TryRemove(player.Slot, out _);

			if (GPlayerWeaponsInfo.TryGetValue(player.Slot, out var playerWeapons))
			{
				foreach (var teamWeapons in playerWeapons.Values)
				{
					foreach (var knifeDefindex in knifeSkinDefindexes)
						teamWeapons.TryRemove(knifeDefindex, out _);
				}
			}

			if ((LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
				RestoreInventoryKnife(player, selectionVersion);

			if (!string.IsNullOrEmpty(Localizer["wp_knife_menu_select"]))
				player.Print(Localizer["wp_knife_menu_select", Localizer["wp_glove_family_default_inventory"]]);

			if (WeaponSync == null)
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
		}

		void ApplyKnifeSelection(
			CCSPlayerController player,
			string knifeKey,
			string knifeName,
			int? weaponDefindex = null,
			int? paint = null,
			string? paintName = null,
			string? image = null
		)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			var playerKnives = GPlayersKnife.GetOrAdd(player.Slot, new ConcurrentDictionary<CsTeam, string>());
			var teamsToCheck = player.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [player.Team];
			var selectionVersion = KnifeSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);

			foreach (var team in teamsToCheck)
				playerKnives[team] = knifeKey;

			if (weaponDefindex.HasValue && paint.HasValue)
			{
				var playerSkins = GPlayerWeaponsInfo.GetOrAdd(
					player.Slot,
					new ConcurrentDictionary<CsTeam, ConcurrentDictionary<int, WeaponInfo>>()
				);

				foreach (var team in teamsToCheck)
				{
					var teamWeapons = playerSkins.GetOrAdd(team, _ => new ConcurrentDictionary<int, WeaponInfo>());
					var weaponInfo = teamWeapons.GetOrAdd(weaponDefindex.Value, _ => new WeaponInfo { Wear = 0.01f, Seed = 0 });

					weaponInfo.Paint = paint.Value;
				}
			}

			if (Config.Additional.ShowSkinImage && !string.IsNullOrEmpty(image))
			{
				_playerWeaponImage[player.Slot] = image;
				AddTimer(2.0f, () => _playerWeaponImage.Remove(player.Slot), TimerFlags.STOP_ON_MAPCHANGE);
			}

			if (!string.IsNullOrEmpty(Localizer["wp_knife_menu_select"]))
				player.Print(Localizer["wp_knife_menu_select", knifeName]);

			if (!string.IsNullOrEmpty(paintName) && !string.IsNullOrEmpty(Localizer["wp_skin_menu_select"]))
				player.Print(Localizer["wp_skin_menu_select", paintName]);

			if (!string.IsNullOrEmpty(Localizer["wp_knife_menu_kill"]) && Config.Additional.CommandKillEnabled)
				player.Print(Localizer["wp_knife_menu_kill"]);

			var playerInfo = new PlayerInfo
			{
				UserId = player.UserId,
				Slot = player.Slot,
				Index = (int)player.Index,
				SteamId = player.SteamID.ToString(),
				Name = player.PlayerName,
				IpAddress = player.IpAddress?.Split(":")[0],
			};

			if (_gBCommandsAllowed && (LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
				ApplyPlayerKnifeRuntimeSelection(player, selectionVersion);

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

					await WeaponSync.SyncKnifeToDatabase(playerInfo, knifeKey, teamsToCheck);

					if (weaponDefindex.HasValue && paint.HasValue)
					{
						if (!KnifeSelectionVersions.TryGetValue(player.Slot, out currentVersion) || currentVersion != selectionVersion)
							return;

						await WeaponSync.SyncWeaponPaintToDatabase(playerInfo, weaponDefindex.Value, teamsToCheck);
					}
				}
				finally
				{
					syncLock.Release();
				}
			});
		}

		void OpenKnifeSkinMenu(CCSPlayerController player, string knifeKey, string knifeName)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			var skinsForKnife = SkinsList
				.Where(skin => skin.TryGetValue("weapon_name", out var weaponName) && weaponName?.ToString() == knifeKey)
				.ToList();

			if (skinsForKnife.Count == 0)
				return;

			Action<CCSPlayerController> backToKnifeModels = backPlayer => OpenWeaponPaintsMenu(knifeModelMenu, backPlayer);
			var knifeSkinMenu = Utility.CreateMenu(Localizer["wp_skin_menu_skin_title", knifeName]);

			if (knifeSkinMenu == null)
				return;

			AddBackMenuOption(knifeSkinMenu, backToKnifeModels);
			knifeSkinMenu.AddMenuOption(
				Localizer["wp_glove_family_default_inventory"],
				(p, _) =>
				{
					if (!Utility.IsPlayerValid(p))
						return;

					ApplyInventoryKnifeSelection(p);
				}
			);

			foreach (var skin in skinsForKnife)
			{
				if (
					!int.TryParse(skin["weapon_defindex"]?.ToString(), out var weaponDefindex)
					|| !int.TryParse(skin["paint"]?.ToString(), out var paint)
				)
				{
					continue;
				}

				var paintName = skin["paint_name"]?.ToString();
				if (string.IsNullOrEmpty(paintName))
					continue;

				var separatorIndex = paintName.IndexOf('|');
				var finishName = separatorIndex >= 0 ? paintName[(separatorIndex + 1)..].Trim() : paintName;
				var image = skin["image"]?.ToString();

				knifeSkinMenu.AddMenuOption(
					finishName,
					(p, _) =>
					{
						if (!Utility.IsPlayerValid(p))
							return;

						ApplyKnifeSelection(p, knifeKey, knifeName, weaponDefindex, paint, paintName, image);

						var teamsToCheck = p.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [p.Team];

						OpenWearCustomizationMenu(
							p,
							weaponDefindex,
							knifeName,
							finishName,
							teamsToCheck,
							backPlayer => OpenWeaponPaintsMenu(knifeSkinMenu, backPlayer, backToKnifeModels)
						);
					}
				);
			}

			OpenWeaponPaintsMenu(knifeSkinMenu, player, backToKnifeModels);
		}

		knifeModelMenu.AddMenuOption(
			Localizer["wp_glove_family_default_inventory"],
			(player, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				ApplyInventoryKnifeSelection(player);
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

					OpenKnifeSkinMenu(player, knifeKey, knifeName);
				}
			);
		}

		_config.Additional.CommandKnife.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Knife Menu",
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
						OpenWeaponPaintsMenu(knifeModelMenu, player);
						return;
					}

					if (!string.IsNullOrEmpty(Localizer["wp_command_cooldown"]))
						player.Print(Localizer["wp_command_cooldown"]);
				}
			);
		});
	}
}
