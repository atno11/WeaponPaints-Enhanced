using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void RemoveRuntimePaintOverride(int slot, int weaponDefindex)
	{
		if (!GPlayerWeaponsInfo.TryGetValue(slot, out var playerWeapons))
			return;

		foreach (var teamWeapons in playerWeapons.Values)
			teamWeapons.TryRemove(weaponDefindex, out _);
	}

	private void RemoveRuntimeKnifeOverrides(int slot, int[] knifeSkinDefindexes)
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

	private void ApplyInventoryWeaponSelection(CCSPlayerController player, int weaponDefindex, int[] knifeSkinDefindexes)
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

			RemoveRuntimeKnifeOverrides(player.Slot, knifeSkinDefindexes);

			if ((LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
				RestoreInventoryKnife(player, selectionVersion);

			if (!string.IsNullOrEmpty(Localizer["wp_skin_menu_select"]))
			{
				player.Print(Localizer["wp_skin_menu_select", Localizer["wp_glove_family_default_inventory"]]);
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
		{
			player.Print(Localizer["wp_skin_menu_select", Localizer["wp_glove_family_default_inventory"]]);
		}

		if (WeaponSync == null)
			return;

		_ = Task.Run(async () =>
		{
			var syncLock = SkinSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));

			await syncLock.WaitAsync();

			try
			{
				if (!SkinSelectionVersions.TryGetValue(versionKey, out var currentVersion) || currentVersion != skinVersion)
				{
					return;
				}

				await WeaponSync.DeleteWeaponPaintFromDatabase(playerInfo, weaponDefindex);
			}
			finally
			{
				syncLock.Release();
			}
		});
	}
}
