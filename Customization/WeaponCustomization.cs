using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private const float DefaultWearMin = 0.0f;
	private const float DefaultWearMax = 1.0f;
	private static readonly (string LocalizationKey, float Min, float Max)[] WearExteriors =
	[
		("wp_float_exterior_factory_new", 0.00f, 0.07f),
		("wp_float_exterior_minimal_wear", 0.07f, 0.15f),
		("wp_float_exterior_field_tested", 0.15f, 0.38f),
		("wp_float_exterior_well_worn", 0.38f, 0.45f),
		("wp_float_exterior_battle_scarred", 0.45f, 1.00f),
	];

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

	private static string GetWearExteriorLocalizationKey(float wear)
	{
		var clampedWear = ClampWear(wear);

		for (var index = 0; index < WearExteriors.Length; index++)
		{
			var exterior = WearExteriors[index];
			var isLastExterior = index == WearExteriors.Length - 1;

			if (clampedWear >= exterior.Min && (clampedWear < exterior.Max || (isLastExterior && clampedWear <= exterior.Max)))
			{
				return exterior.LocalizationKey;
			}
		}

		return WearExteriors[^1].LocalizationKey;
	}

	private static bool TryGetWearPreset(string localizationKey, out float presetWear)
	{
		presetWear = 0.0f;

		var exterior = WearExteriors.FirstOrDefault(exterior => exterior.LocalizationKey == localizationKey);

		if (string.IsNullOrEmpty(exterior.LocalizationKey))
			return false;

		var (minWear, maxWear) = GetWearRange();

		var effectiveMin = Math.Max(exterior.Min, minWear);
		var effectiveMax = Math.Min(exterior.Max, maxWear);

		if (effectiveMin > effectiveMax)
			return false;

		presetWear = (effectiveMin + effectiveMax) / 2.0f;

		return true;
	}

	private bool TryGetWeaponWear(CCSPlayerController player, int weaponDefindex, CsTeam[] teams, out float wear)
	{
		wear = 0.0f;

		if (!Utility.IsPlayerValid(player) || teams.Length == 0)
			return false;

		if (!GPlayerWeaponsInfo.TryGetValue(player.Slot, out var playerWeapons))
			return false;

		foreach (var team in teams)
		{
			if (playerWeapons.TryGetValue(team, out var teamWeapons) && teamWeapons.TryGetValue(weaponDefindex, out var weaponInfo))
			{
				wear = weaponInfo.Wear;
				return true;
			}
		}

		return false;
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

			if (Math.Abs(weaponInfo.Wear - wear) < 0.000001f)
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

	private bool TryAdjustWeaponWear(CCSPlayerController player, int weaponDefindex, CsTeam[] teams, float delta)
	{
		if (!Utility.IsPlayerValid(player) || teams.Length == 0 || !float.IsFinite(delta))
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

			var newWear = ClampWear(weaponInfo.Wear + delta);

			if (Math.Abs(newWear - weaponInfo.Wear) < 0.000001f)
				continue;

			weaponInfo.Wear = newWear;
			foundWeaponInfo = true;
		}

		if (!foundWeaponInfo)
			return false;

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

		var selectionVersion = SkinSelectionVersions.GetOrAdd(versionKey, 0);

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
		var selectionVersion = KnifeSelectionVersions.GetOrAdd(player.Slot, 0);
		if (_gBCommandsAllowed && (LifeState_t)player.LifeState == LifeState_t.LIFE_ALIVE)
			ApplyPlayerKnifeRuntimeSelection(player, selectionVersion);

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
		var selectionVersion = GloveSelectionVersions.GetOrAdd(player.Slot, 0);

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
