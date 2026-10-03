using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void ApplyGloveSelection(CCSPlayerController player, JObject selectedGlove)
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
				playerGloves[team] = 0;

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
						if (!GloveSelectionVersions.TryGetValue(player.Slot, out var currentVersion) || currentVersion != selectionVersion)
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

	private void ApplyInventoryGloveSelection(CCSPlayerController player)
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
		{
			RestoreInventoryGloves(player);
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
}
