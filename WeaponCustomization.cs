using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private const float DefaultWearMin = 0.0f;
	private const float DefaultWearMax = 1.0f;

	private static (float Min, float Max) GetWearRange()
	{
		// v0.1.0-dev.2:
		// Until per-paint wear_min / wear_max are wired into the runtime data,
		// use the full CS2 wear range.
		return (DefaultWearMin, DefaultWearMax);
	}

	private static bool IsWearInRange(float wear)
	{
		if (!float.IsFinite(wear))
			return false;

		var (minWear, maxWear) = GetWearRange();

		return wear >= minWear && wear <= maxWear;
	}

	private static float ClampWear(float wear)
	{
		var (minWear, maxWear) = GetWearRange();

		return Math.Clamp(wear, minWear, maxWear);
	}

	private bool TrySetWeaponWear(
		CCSPlayerController player,
		int weaponDefindex,
		CsTeam[] teams,
		float requestedWear,
		bool clampToRange,
		out float appliedWear
	)
	{
		appliedWear = 0.0f;

		if (!Utility.IsPlayerValid(player) || teams.Length == 0 || !float.IsFinite(requestedWear))
			return false;

		var wear = clampToRange ? ClampWear(requestedWear) : requestedWear;

		if (!IsWearInRange(wear))
			return false;

		if (!GPlayerWeaponsInfo.TryGetValue(player.Slot, out var playerWeapons))
			return false;

		var foundWeaponInfo = false;

		foreach (var team in teams)
		{
			if (!playerWeapons.TryGetValue(team, out var teamWeapons))
				continue;

			if (!teamWeapons.TryGetValue(weaponDefindex, out var weaponInfo))
				continue;

			weaponInfo.Wear = wear;
			foundWeaponInfo = true;
		}

		if (!foundWeaponInfo)
			return false;

		appliedWear = wear;

		ApplyWeaponWearChange(player, weaponDefindex, teams);

		return true;
	}

	private void ApplyWeaponWearChange(CCSPlayerController player, int weaponDefindex, CsTeam[] teams)
	{
		var playerInfo = new PlayerInfo
		{
			UserId = player.UserId,
			Slot = player.Slot,
			Index = (int)player.Index,
			SteamId = player.SteamID.ToString(),
			Name = player.PlayerName,
			IpAddress = player.IpAddress?.Split(":")[0],
		};

		if (IsGloveDefindex(weaponDefindex))
		{
			ApplyGloveWearChange(player, playerInfo, weaponDefindex, teams);
			return;
		}

		if (IsKnifeDefindex(weaponDefindex))
		{
			ApplyKnifeWearChange(player, playerInfo, weaponDefindex, teams);
			return;
		}

		ApplyRegularWeaponWearChange(player, playerInfo, weaponDefindex, teams);
	}

	private void ApplyRegularWeaponWearChange(CCSPlayerController player, PlayerInfo playerInfo, int weaponDefindex, CsTeam[] teams)
	{
		var versionKey = (player.Slot, weaponDefindex);

		var selectionVersion = SkinSelectionVersions.AddOrUpdate(versionKey, 1, (_, currentVersion) => currentVersion + 1);

		if (_gBCommandsAllowed && (LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
			RefreshWeaponSkin(player, weaponDefindex);

		var weaponSync = WeaponSync;

		if (weaponSync == null)
			return;

		_ = Task.Run(async () =>
		{
			var syncLock = SkinSyncLocks.GetOrAdd(player.Slot, _ => new SemaphoreSlim(1, 1));

			await syncLock.WaitAsync();

			try
			{
				if (!SkinSelectionVersions.TryGetValue(versionKey, out var currentVersion) || currentVersion != selectionVersion)
				{
					return;
				}

				await weaponSync.SyncWeaponPaintToDatabase(playerInfo, weaponDefindex, teams);
			}
			finally
			{
				syncLock.Release();
			}
		});
	}

	private void ApplyKnifeWearChange(CCSPlayerController player, PlayerInfo playerInfo, int weaponDefindex, CsTeam[] teams)
	{
		var selectionVersion = KnifeSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);

		if (_gBCommandsAllowed && (LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
			RecreatePlayerKnife(player, selectionVersion);

		var weaponSync = WeaponSync;

		if (weaponSync == null)
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

				await weaponSync.SyncWeaponPaintToDatabase(playerInfo, weaponDefindex, teams);
			}
			finally
			{
				syncLock.Release();
			}
		});
	}

	private void ApplyGloveWearChange(CCSPlayerController player, PlayerInfo playerInfo, int weaponDefindex, CsTeam[] teams)
	{
		var selectionVersion = GloveSelectionVersions.AddOrUpdate(player.Slot, 1, (_, currentVersion) => currentVersion + 1);

		if (_gBCommandsAllowed && (LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
		{
			AddTimer(0.1f, () => GivePlayerGloves(player), TimerFlags.STOP_ON_MAPCHANGE);

			AddTimer(0.25f, () => GivePlayerGloves(player), TimerFlags.STOP_ON_MAPCHANGE);
		}

		var weaponSync = WeaponSync;

		if (weaponSync == null)
			return;

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

				await weaponSync.SyncWeaponPaintToDatabase(playerInfo, weaponDefindex, teams);
			}
			finally
			{
				syncLock.Release();
			}
		});
	}
}
