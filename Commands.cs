using System.Collections.Concurrent;
using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void SetMenuBackAction(CCSPlayerController player, Action<CCSPlayerController>? backAction)
	{
		if (backAction == null)
		{
			MenuBackActions.TryRemove(player.Slot, out var removedBackAction);
			return;
		}

		MenuBackActions[player.Slot] = backAction;
	}

	private void OpenWeaponPaintsMenu(IMenu menu, CCSPlayerController player, Action<CCSPlayerController>? backAction = null)
	{
		SetMenuBackAction(player, backAction);
		menu.Open(player);
	}

	private void AddBackMenuOption(IMenu menu, Action<CCSPlayerController> backAction)
	{
		menu.AddMenuOption(
			Localizer["wp_menu_back"],
			(player, commandInfo) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				backAction(player);
			}
		);
	}

	private void SetupMenuNavigationButtons()
	{
		RegisterListener<Listeners.OnPlayerButtonsChanged>(
			(player, pressed, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				if ((pressed & PlayerButtons.Reload) != 0)
				{
					MenuBackActions.TryRemove(player.Slot, out var removedBackAction);
					return;
				}

				if ((pressed & PlayerButtons.Speed) == 0)
					return;

				if (!MenuBackActions.TryRemove(player.Slot, out var backAction))
					return;

				backAction(player);
			}
		);
	}

	private void OnCommandRefresh(CCSPlayerController? player, CommandInfo command)
	{
		if (!Config.Additional.CommandWpEnabled || !Config.Additional.SkinEnabled || !_gBCommandsAllowed)
			return;

		if (!Utility.IsPlayerValid(player))
			return;

		if (player == null || !player.IsValid || player.UserId == null || player.IsBot)
			return;

		PlayerInfo? playerInfo = new PlayerInfo
		{
			UserId = player.UserId,
			Slot = player.Slot,
			Index = (int)player.Index,
			SteamId = player?.SteamID.ToString(),
			Name = player?.PlayerName,
			IpAddress = player?.IpAddress?.Split(":")[0],
		};

		try
		{
			if (
				player != null && !CommandsCooldown.TryGetValue(player.Slot, out var cooldownEndTime)
				|| player != null
					&& DateTime.UtcNow
						>= (CommandsCooldown.TryGetValue(player.Slot, out cooldownEndTime) ? cooldownEndTime : DateTime.UtcNow)
			)
			{
				CommandsCooldown[player.Slot] = DateTime.UtcNow.AddSeconds(Config.CmdRefreshCooldownSeconds);

				if (WeaponSync != null)
				{
					_ = Task.Run(async () => await WeaponSync.GetPlayerData(playerInfo));

					GivePlayerGloves(player);
					RefreshWeapons(player);
					GivePlayerAgent(player);
					GivePlayerMusicKit(player);
					AddTimer(0.15f, () => GivePlayerPin(player));
				}

				if (!string.IsNullOrEmpty(Localizer["wp_command_refresh_done"]))
				{
					player.Print(Localizer["wp_command_refresh_done"]);
				}

				return;
			}

			if (!string.IsNullOrEmpty(Localizer["wp_command_cooldown"]))
			{
				player!.Print(Localizer["wp_command_cooldown"]);
			}
		}
		catch (Exception) { }
	}

	private void OnCommandWS(CCSPlayerController? player, CommandInfo command)
	{
		if (!Config.Additional.SkinEnabled)
			return;

		if (!Utility.IsPlayerValid(player))
			return;

		if (!string.IsNullOrEmpty(Localizer["wp_info_refresh"]))
		{
			player!.Print(Localizer["wp_info_refresh"]);
		}

		if (Config.Additional.GloveEnabled && !string.IsNullOrEmpty(Localizer["wp_info_glove"]))
		{
			player!.Print(Localizer["wp_info_glove"]);
		}

		if (Config.Additional.AgentEnabled && !string.IsNullOrEmpty(Localizer["wp_info_agent"]))
		{
			player!.Print(Localizer["wp_info_agent"]);
		}

		if (Config.Additional.MusicEnabled && !string.IsNullOrEmpty(Localizer["wp_info_music"]))
		{
			player!.Print(Localizer["wp_info_music"]);
		}

		if (Config.Additional.PinsEnabled && !string.IsNullOrEmpty(Localizer["wp_info_pin"]))
		{
			player!.Print(Localizer["wp_info_pin"]);
		}

		if (!Config.Additional.KnifeEnabled)
			return;

		if (!string.IsNullOrEmpty(Localizer["wp_info_knife"]))
		{
			player!.Print(Localizer["wp_info_knife"]);
		}
	}

	private void OnCommandFloat(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (!Config.Additional.SkinEnabled || !_gBCommandsAllowed)
			return;

		if (
			player == null
			|| !Utility.IsPlayerValid(player)
			|| player.PlayerPawn.Value == null
			|| player.PlayerPawn.Value.WeaponServices == null
		)
		{
			return;
		}

		var rawWear = commandInfo.GetArg(1);

		if (string.IsNullOrWhiteSpace(rawWear))
		{
			player.Print(Localizer["wp_float_usage"]);
			return;
		}

		var normalizedWear = rawWear.Trim().Replace(',', '.');

		if (
			!float.TryParse(normalizedWear, NumberStyles.Float, CultureInfo.InvariantCulture, out var requestedWear)
			|| !float.IsFinite(requestedWear)
		)
		{
			player.Print(Localizer["wp_float_invalid"]);
			return;
		}

		if (!IsWearInRange(requestedWear))
		{
			var (minWear, maxWear) = GetWearRange();

			player.Print(
				Localizer[
					"wp_float_range",
					minWear.ToString("0.000000", CultureInfo.InvariantCulture),
					maxWear.ToString("0.000000", CultureInfo.InvariantCulture)
				]
			);

			return;
		}

		var weapon = player.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value;

		if (weapon == null || !weapon.IsValid)
		{
			player.Print(Localizer["wp_float_no_weapon"]);
			return;
		}

		var weaponDefindex = weapon.AttributeManager.Item.ItemDefinitionIndex;

		if (!HasChangedPaint(player, weaponDefindex, out var weaponInfo) || weaponInfo == null)
		{
			player.Print(Localizer["wp_float_no_skin"]);
			return;
		}

		var teams = new[] { player.Team };

		if (!TrySetWeaponWear(player, weaponDefindex, teams, requestedWear, clampToRange: false, out var appliedWear))
		{
			player.Print(Localizer["wp_float_failed"]);
			return;
		}

		player.Print(Localizer["wp_float_updated", appliedWear.ToString("0.000000", CultureInfo.InvariantCulture)]);
	}

	private void RegisterCommands()
	{
		AddCommand(
			"css_float",
			"Set float/wear for the currently equipped weapon",
			(player, info) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;
				OnCommandFloat(player, info);
			}
		);
		_config.Additional.CommandStattrak.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Stattrak toggle",
				(player, info) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					OnCommandStattrak(player, info);
				}
			);
		});

		_config.Additional.CommandSkin.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Skins info",
				(player, info) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					OnCommandWS(player, info);
				}
			);
		});

		_config.Additional.CommandRefresh.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Skins refresh",
				(player, info) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					OnCommandRefresh(player, info);
				}
			);
		});

		if (Config.Additional.CommandKillEnabled)
		{
			_config.Additional.CommandKill.ForEach(c =>
			{
				AddCommand(
					$"css_{c}",
					"kill yourself",
					(player, _) =>
					{
						if (
							player == null
							|| !Utility.IsPlayerValid(player)
							|| player.PlayerPawn.Value == null
							|| !player.PlayerPawn.IsValid
						)
							return;

						player.PlayerPawn.Value.CommitSuicide(true, false);
					}
				);
			});
		}

		AddCommand(
			"wp_refresh",
			"Admin refresh player skins",
			(player, info) =>
			{
				OnCommandSkinRefresh(player, info);
			}
		);
	}

	private void OnCommandSkinRefresh(CCSPlayerController? player, CommandInfo command)
	{
		if (!Config.Additional.CommandWpEnabled || !Config.Additional.SkinEnabled || !_gBCommandsAllowed)
			return;

		if (player != null)
			return;

		var args = command.GetArg(1);

		if (string.IsNullOrEmpty(args))
		{
			Console.WriteLine("[WeaponPaints] Usage: wp_refresh <steamid64|all>");
			Console.WriteLine("[WeaponPaints] Examples:");
			Console.WriteLine("[WeaponPaints]   wp_refresh all - Refresh skins for all players");
			Console.WriteLine("[WeaponPaints]   wp_refresh 76561198012345678 - Refresh skins by SteamID64");
			return;
		}

		var targetPlayers = new List<CCSPlayerController>();

		if (args.Equals("all", StringComparison.OrdinalIgnoreCase))
		{
			targetPlayers = Utilities.GetPlayers().Where(p => p != null && p.IsValid && !p.IsBot && p.UserId != null).ToList();

			if (targetPlayers.Count == 0)
			{
				Console.WriteLine("[WeaponPaints] No players connected to refresh.");
				return;
			}

			Console.WriteLine($"[WeaponPaints] Refreshing skins for {targetPlayers.Count} players...");
		}
		else
		{
			var foundPlayer = Utilities
				.GetPlayers()
				.FirstOrDefault(p => p != null && p.IsValid && !p.IsBot && p.UserId != null && p.SteamID.ToString() == args);

			if (foundPlayer == null)
			{
				Console.WriteLine($"[WeaponPaints] Player with SteamID64 '{args}' not found.");
				return;
			}

			targetPlayers.Add(foundPlayer);
			Console.WriteLine($"[WeaponPaints] Refreshing skins for {foundPlayer.PlayerName}...");
		}

		foreach (var targetPlayer in targetPlayers)
		{
			try
			{
				PlayerInfo? playerInfo = new PlayerInfo
				{
					UserId = targetPlayer.UserId,
					Slot = targetPlayer.Slot,
					Index = (int)targetPlayer.Index,
					SteamId = targetPlayer.SteamID.ToString(),
					Name = targetPlayer.PlayerName,
					IpAddress = targetPlayer.IpAddress?.Split(":")[0],
				};

				if (WeaponSync != null)
				{
					_ = Task.Run(async () => await WeaponSync.GetPlayerData(playerInfo));
				}

				GivePlayerGloves(targetPlayer);
				RefreshWeapons(targetPlayer);
				GivePlayerAgent(targetPlayer);
				GivePlayerMusicKit(targetPlayer);

				AddTimer(0.15f, () => GivePlayerPin(targetPlayer));

				if (!string.IsNullOrEmpty(Localizer["wp_command_refresh_done"]))
				{
					targetPlayer.Print(Localizer["wp_command_refresh_done"]);
				}

				Console.WriteLine($"[WeaponPaints] Skins refreshed for {targetPlayer.PlayerName}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[WeaponPaints] Error refreshing skins for {targetPlayer.PlayerName}: {ex.Message}");
			}
		}

		Console.WriteLine("[WeaponPaints] Refresh process completed.");
	}

	private void OnCommandStattrak(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid)
			return;

		var weapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;

		if (weapon == null || !weapon.IsValid)
			return;

		if (!HasChangedPaint(player, weapon.AttributeManager.Item.ItemDefinitionIndex, out var weaponInfo) || weaponInfo == null)
			return;

		weaponInfo.StatTrak = !weaponInfo.StatTrak;

		RefreshWeapons(player);

		if (!string.IsNullOrEmpty(Localizer["wp_stattrak_action"]))
		{
			player.Print(Localizer["wp_stattrak_action"]);
		}
	}

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
					var weaponInfo = teamWeapons.GetOrAdd(weaponDefindex.Value, _ => new WeaponInfo());

					weaponInfo.Paint = paint.Value;
					weaponInfo.Wear = 0.01f;
					weaponInfo.Seed = 0;
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
				RecreatePlayerKnife(player, selectionVersion);

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
							var weaponInfo = teamWeapons.GetOrAdd(weaponDefindex, _ => new WeaponInfo());

							weaponInfo.Paint = paint;
							weaponInfo.Wear = 0.01f;
							weaponInfo.Seed = 0;
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
								RecreatePlayerKnife(p, selectionVersion);

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

				var weaponInfo = teamWeapons.GetOrAdd(weaponDefindex, _ => new WeaponInfo());

				weaponInfo.Paint = paint;
				weaponInfo.Wear = 0.00f;
				weaponInfo.Seed = 0;
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

	private void SetupAgentsMenu()
	{
		var handleAgentSelection = (CCSPlayerController? player, ChatMenuOption option) =>
		{
			if (!Utility.IsPlayerValid(player) || player is null)
				return;

			var selectedPaintName = option.Text;

			var selectedAgent = AgentsList.FirstOrDefault(g =>
				g.ContainsKey("agent_name")
				&& g["agent_name"] != null
				&& g["agent_name"]!.ToString() == selectedPaintName
				&& g["team"] != null
				&& (int)(g["team"]!) == player.TeamNum
			);

			if (selectedAgent == null)
				return;

			if (selectedAgent.ContainsKey("model"))
			{
				PlayerInfo playerInfo = new PlayerInfo
				{
					UserId = player.UserId,
					Slot = player.Slot,
					Index = (int)player.Index,
					SteamId = player.SteamID.ToString(),
					Name = player.PlayerName,
					IpAddress = player.IpAddress?.Split(":")[0],
				};

				if (Config.Additional.ShowSkinImage)
				{
					var image = selectedAgent["image"]?.ToString() ?? "";

					_playerWeaponImage[player.Slot] = image;

					AddTimer(2.0f, () => _playerWeaponImage.Remove(player.Slot), TimerFlags.STOP_ON_MAPCHANGE);
				}

				if (!string.IsNullOrEmpty(Localizer["wp_agent_menu_select"]))
				{
					player.Print(Localizer["wp_agent_menu_select", selectedPaintName]);
				}

				if (player.TeamNum == 3)
				{
					GPlayersAgent.AddOrUpdate(
						player.Slot,
						key => (selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString(), null),
						(key, oldValue) =>
							(selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString(), oldValue.T)
					);
				}
				else
				{
					GPlayersAgent.AddOrUpdate(
						player.Slot,
						key => (null, selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString()),
						(key, oldValue) =>
							(oldValue.CT, selectedAgent["model"]!.ToString().Equals("null") ? null : selectedAgent["model"]!.ToString())
					);
				}

				GivePlayerAgent(player);

				if (WeaponSync != null)
				{
					_ = Task.Run(async () => await WeaponSync.SyncAgentToDatabase(playerInfo));
				}
			}
		};

		var terroristAgentsMenu = Utility.CreateMenu(Localizer["wp_agent_menu_title"]);
		var counterTerroristAgentsMenu = Utility.CreateMenu(Localizer["wp_agent_menu_title"]);

		if (terroristAgentsMenu == null || counterTerroristAgentsMenu == null)
			return;

		foreach (var agentObject in AgentsList)
		{
			if (agentObject["team"]?.Value<int>() is not { } teamNum)
				continue;

			var paintName = agentObject["agent_name"]?.ToString() ?? "";

			if (string.IsNullOrEmpty(paintName))
				continue;

			switch (teamNum)
			{
				case 2:
					terroristAgentsMenu.AddMenuOption(paintName, handleAgentSelection);
					break;

				case 3:
					counterTerroristAgentsMenu.AddMenuOption(paintName, handleAgentSelection);
					break;
			}
		}

		_config.Additional.CommandAgent.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Agents selection menu",
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player) || !_gBCommandsAllowed)
						return;

					if (player == null || player.UserId == null)
						return;

					if (
						!CommandsCooldown.TryGetValue(player.Slot, out DateTime cooldownEndTime)
						|| DateTime.UtcNow
							>= (CommandsCooldown.TryGetValue(player.Slot, out cooldownEndTime) ? cooldownEndTime : DateTime.UtcNow)
					)
					{
						var agentsSelectionMenu = player.TeamNum switch
						{
							2 => terroristAgentsMenu,
							3 => counterTerroristAgentsMenu,
							_ => null,
						};

						if (agentsSelectionMenu == null)
							return;
						CommandsCooldown[player.Slot] = DateTime.UtcNow.AddSeconds(Config.CmdRefreshCooldownSeconds);

						OpenWeaponPaintsMenu(agentsSelectionMenu, player);

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

	private void SetupMusicMenu()
	{
		var musicSelectionMenu = Utility.CreateMenu(Localizer["wp_music_menu_title"]);

		if (musicSelectionMenu == null)
			return;

		void ApplyInventoryMusicSelection(CCSPlayerController player)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			var selectionVersion = MusicSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);
			GPlayersMusic.TryRemove(player.Slot, out _);
			RestoreInventoryMusicKit(player);

			if (!string.IsNullOrEmpty(Localizer["wp_music_menu_select"]))
				player.Print(Localizer["wp_music_menu_select", Localizer["wp_glove_family_default_inventory"]]);

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
				var syncLock = MusicSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));
				await syncLock.WaitAsync();

				try
				{
					if (!MusicSelectionVersions.TryGetValue(player.Slot, out var currentVersion) || currentVersion != selectionVersion)
						return;

					await WeaponSync.DeleteMusicFromDatabase(playerInfo);
				}
				finally
				{
					syncLock.Release();
				}
			});
		}

		void ApplyMusicSelection(CCSPlayerController player, ushort musicId, string displayName, string? image = null)
		{
			if (!Utility.IsPlayerValid(player))
				return;

			var teamsToCheck = player.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [player.Team];
			var playerMusic = GPlayersMusic.GetOrAdd(player.Slot, new ConcurrentDictionary<CsTeam, ushort>());
			var selectionVersion = MusicSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);

			foreach (var team in teamsToCheck)
				playerMusic[team] = musicId;

			if (Config.Additional.ShowSkinImage && !string.IsNullOrEmpty(image))
			{
				_playerWeaponImage[player.Slot] = image;
				AddTimer(2.0f, () => _playerWeaponImage.Remove(player.Slot), TimerFlags.STOP_ON_MAPCHANGE);
			}

			GivePlayerMusicKit(player);

			if (!string.IsNullOrEmpty(Localizer["wp_music_menu_select"]))
				player.Print(Localizer["wp_music_menu_select", displayName]);

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
				var syncLock = MusicSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));
				await syncLock.WaitAsync();

				try
				{
					if (!MusicSelectionVersions.TryGetValue(player.Slot, out var currentVersion) || currentVersion != selectionVersion)
						return;

					await WeaponSync.SyncMusicToDatabase(playerInfo, musicId, teamsToCheck);
				}
				finally
				{
					syncLock.Release();
				}
			});
		}

		musicSelectionMenu.AddMenuOption(
			Localizer["wp_glove_family_default_inventory"],
			(player, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				ApplyInventoryMusicSelection(player);
			}
		);

		musicSelectionMenu.AddMenuOption(
			Localizer["None"],
			(player, _) =>
			{
				if (!Utility.IsPlayerValid(player))
					return;

				ApplyMusicSelection(player, 0, Localizer["None"].Value);
			}
		);

		foreach (var musicObject in MusicList)
		{
			var musicName = musicObject["name"]?.ToString();
			if (string.IsNullOrEmpty(musicName) || !ushort.TryParse(musicObject["id"]?.ToString(), out var musicId))
				continue;

			var image = musicObject["image"]?.ToString();
			musicSelectionMenu.AddMenuOption(
				musicName,
				(player, _) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					ApplyMusicSelection(player, musicId, musicName, image);
				}
			);
		}

		_config.Additional.CommandMusic.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Music selection menu",
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
						OpenWeaponPaintsMenu(musicSelectionMenu, player);
						return;
					}

					if (!string.IsNullOrEmpty(Localizer["wp_command_cooldown"]))
						player.Print(Localizer["wp_command_cooldown"]);
				}
			);
		});
	}

	private void SetupPinsMenu()
	{
		var pinsSelectionMenu = Utility.CreateMenu(Localizer["wp_pins_menu_title"]);

		if (pinsSelectionMenu == null)
			return;

		var handlePinsSelection = (CCSPlayerController? player, ChatMenuOption option) =>
		{
			if (!Utility.IsPlayerValid(player) || player is null)
				return;

			var selectedPaintName = option.Text;

			var playerPins = GPlayersPin.GetOrAdd(player.Slot, new ConcurrentDictionary<CsTeam, ushort>());

			var teamsToCheck = player.TeamNum < 2 ? new[] { CsTeam.Terrorist, CsTeam.CounterTerrorist } : [player.Team];

			var selectedPin = PinsList.FirstOrDefault(g => g.ContainsKey("name") && g["name"]?.ToString() == selectedPaintName);

			if (selectedPin != null)
			{
				if (
					!selectedPin.ContainsKey("id")
					|| !selectedPin.ContainsKey("name")
					|| !int.TryParse(selectedPin["id"]?.ToString(), out var paint)
				)
					return;

				var image = selectedPin["image"]?.ToString() ?? "";

				if (Config.Additional.ShowSkinImage)
				{
					_playerWeaponImage[player.Slot] = image;

					AddTimer(2.0f, () => _playerWeaponImage.Remove(player.Slot), TimerFlags.STOP_ON_MAPCHANGE);
				}

				PlayerInfo playerInfo = new PlayerInfo
				{
					UserId = player.UserId,
					Slot = player.Slot,
					Index = (int)player.Index,
					SteamId = player.SteamID.ToString(),
					Name = player.PlayerName,
					IpAddress = player.IpAddress?.Split(":")[0],
				};

				if (paint != 0)
				{
					foreach (var team in teamsToCheck)
						playerPins[team] = (ushort)paint;
				}
				else
				{
					foreach (var team in teamsToCheck)
						playerPins[team] = 0;
				}

				if (!string.IsNullOrEmpty(Localizer["wp_pins_menu_select"]))
				{
					player.Print(Localizer["wp_pins_menu_select", selectedPaintName]);
				}

				GivePlayerPin(player);

				if (WeaponSync != null)
				{
					_ = Task.Run(async () => await WeaponSync.SyncPinToDatabase(playerInfo, (ushort)paint, teamsToCheck));
				}
			}
			else
			{
				PlayerInfo playerInfo = new PlayerInfo
				{
					UserId = player.UserId,
					Slot = player.Slot,
					Index = (int)player.Index,
					SteamId = player.SteamID.ToString(),
					Name = player.PlayerName,
					IpAddress = player.IpAddress?.Split(":")[0],
				};

				foreach (var team in teamsToCheck)
					playerPins[team] = 0;

				if (!string.IsNullOrEmpty(Localizer["wp_pins_menu_select"]))
				{
					player.Print(Localizer["wp_pins_menu_select", Localizer["None"]]);
				}

				GivePlayerPin(player);

				if (WeaponSync != null)
				{
					_ = Task.Run(async () => await WeaponSync.SyncPinToDatabase(playerInfo, 0, teamsToCheck));
				}
			}
		};

		pinsSelectionMenu.AddMenuOption(Localizer["None"], handlePinsSelection);

		foreach (
			var paintName in PinsList.Select(musicObject => musicObject["name"]?.ToString() ?? "").Where(paintName => paintName.Length > 0)
		)
		{
			pinsSelectionMenu.AddMenuOption(paintName, handlePinsSelection);
		}

		_config.Additional.CommandPin.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Pin selection menu",
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

						OpenWeaponPaintsMenu(pinsSelectionMenu, player);

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
