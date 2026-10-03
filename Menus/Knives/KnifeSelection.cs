using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void ApplyInventoryKnifeSelection(CCSPlayerController player, int[] knifeSkinDefindexes)
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
		{
			RestoreInventoryKnife(player, selectionVersion);
		}

		if (!string.IsNullOrEmpty(Localizer["wp_knife_menu_select"]))
		{
			player.Print(Localizer["wp_knife_menu_select", Localizer["wp_glove_family_default_inventory"]]);
		}

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
				{
					return;
				}

				await WeaponSync.DeleteKnifeFromDatabase(playerInfo, knifeSkinDefindexes);
			}
			finally
			{
				syncLock.Release();
			}
		});
	}

	private void ApplyKnifeSelection(
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
		{
			player.Print(Localizer["wp_knife_menu_select", knifeName]);
		}

		if (!string.IsNullOrEmpty(paintName) && !string.IsNullOrEmpty(Localizer["wp_skin_menu_select"]))
		{
			player.Print(Localizer["wp_skin_menu_select", paintName]);
		}

		if (!string.IsNullOrEmpty(Localizer["wp_knife_menu_kill"]) && Config.Additional.CommandKillEnabled)
		{
			player.Print(Localizer["wp_knife_menu_kill"]);
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

		if (_gBCommandsAllowed && (LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
		{
			ApplyPlayerKnifeRuntimeSelection(player, selectionVersion);
		}

		if (WeaponSync == null)
			return;

		_ = Task.Run(async () =>
		{
			var syncLock = KnifeSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));

			await syncLock.WaitAsync();

			try
			{
				if (!KnifeSelectionVersions.TryGetValue(player.Slot, out var currentVersion) || currentVersion != selectionVersion)
				{
					return;
				}

				await WeaponSync.SyncKnifeToDatabase(playerInfo, knifeKey, teamsToCheck);

				if (weaponDefindex.HasValue && paint.HasValue)
				{
					if (!KnifeSelectionVersions.TryGetValue(player.Slot, out currentVersion) || currentVersion != selectionVersion)
					{
						return;
					}

					await WeaponSync.SyncWeaponPaintToDatabase(playerInfo, weaponDefindex.Value, teamsToCheck);
				}
			}
			finally
			{
				syncLock.Release();
			}
		});
	}
}
