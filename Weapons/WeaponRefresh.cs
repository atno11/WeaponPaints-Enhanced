using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace WeaponPaints;

public partial class WeaponPaints
{
	// silly method to update sticker when call RefreshWeapons()
	private void RefreshWeapons(CCSPlayerController? player)
	{
		if (!_gBCommandsAllowed)
			return;
		if (player == null || !player.IsValid || player.PlayerPawn.Value == null || (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE)
			return;
		if (player.PlayerPawn.Value.WeaponServices == null || player.PlayerPawn.Value.ItemServices == null)
			return;

		var weapons = player.PlayerPawn.Value.WeaponServices.MyWeapons;

		if (weapons.Count == 0)
			return;
		if (player.Team is CsTeam.None or CsTeam.Spectator)
			return;

		var hasKnife = false;

		Dictionary<string, List<(int, int)>> weaponsWithAmmo = [];

		foreach (var weapon in weapons)
		{
			if (!weapon.IsValid || weapon.Value == null || !weapon.Value.IsValid || !weapon.Value.DesignerName.Contains("weapon_"))
				continue;

			CCSWeaponBaseGun gun = weapon.Value.As<CCSWeaponBaseGun>();

			if (weapon.Value.Entity == null)
				continue;
			if (!weapon.Value.OwnerEntity.IsValid)
				continue;
			if (gun.Entity == null)
				continue;
			if (!gun.IsValid)
				continue;

			try
			{
				CCSWeaponBaseVData? weaponData = weapon.Value.As<CCSWeaponBase>().VData;

				if (weaponData == null)
					continue;

				if (weaponData.GearSlot is gear_slot_t.GEAR_SLOT_RIFLE or gear_slot_t.GEAR_SLOT_PISTOL)
				{
					if (!WeaponDefindex.TryGetValue(weapon.Value.AttributeManager.Item.ItemDefinitionIndex, out var weaponByDefindex))
						continue;

					int clip1 = weapon.Value.Clip1;
					int reservedAmmo = weapon.Value.ReserveAmmo[0];

					if (!weaponsWithAmmo.TryGetValue(weaponByDefindex, out var value))
					{
						value = [];
						weaponsWithAmmo.Add(weaponByDefindex, value);
					}

					value.Add((clip1, reservedAmmo));

					if (gun.VData == null)
						return;

					weapon.Value?.AddEntityIOEvent("Kill", weapon.Value, null, "", 0.1f);
				}

				if (weaponData.GearSlot == gear_slot_t.GEAR_SLOT_KNIFE)
				{
					weapon.Value?.AddEntityIOEvent("Kill", weapon.Value, null, "", 0.1f);
					hasKnife = true;
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex.Message);
			}
		}

		AddTimer(
			0.23f,
			() =>
			{
				if (!_gBCommandsAllowed)
					return;

				if (!PlayerHasKnife(player) && hasKnife)
				{
					var defaultKnife = GetDefaultKnifeClassname(player);

					var newKnife = new CBasePlayerWeapon(player.GiveNamedItem(defaultKnife));

					var newWeapon = new CBasePlayerWeapon(player.GiveNamedItem(CsItem.USP));

					player.GiveNamedItem(defaultKnife);
					player.ExecuteClientCommand("slot3");

					Server.NextFrame(() =>
					{
						try
						{
							if (newKnife != null && newKnife.IsValid)
								newKnife.AddEntityIOEvent("Kill", newKnife, null, "", 0.01f);
							if (newWeapon != null && newWeapon.IsValid)
								newWeapon.AddEntityIOEvent("Kill", newWeapon, null, "", 0.01f);
						}
						catch (Exception ex)
						{
							Logger.LogWarning("Error AddEntityIOEvent " + ex.Message);
						}
					});
				}

				foreach (var entry in weaponsWithAmmo)
				{
					foreach (var ammo in entry.Value)
					{
						var newWeapon = new CBasePlayerWeapon(player.GiveNamedItem(entry.Key));
						Server.NextFrame(() =>
						{
							try
							{
								newWeapon.Clip1 = ammo.Item1;
								newWeapon.ReserveAmmo[0] = ammo.Item2;

								IncrementWearForWeaponWithStickers(player, newWeapon);
							}
							catch (Exception ex)
							{
								Logger.LogWarning("Error setting weapon properties: " + ex.Message);
							}
						});
					}
				}
			},
			TimerFlags.STOP_ON_MAPCHANGE
		);
	}

	private void GiveOnItemPickup(CCSPlayerController player)
	{
		var pawn = player.PlayerPawn.Value;
		if (pawn == null)
			return;

		var myWeapons = pawn.WeaponServices?.MyWeapons;
		if (myWeapons == null)
			return;

		foreach (var handle in myWeapons)
		{
			var weapon = handle.Value;

			if (weapon == null || !weapon.IsValid)
				continue;
			if (myWeapons.Count == 1)
			{
				var newWeapon = new CBasePlayerWeapon(player.GiveNamedItem(CsItem.USP));
				weapon.AddEntityIOEvent("Kill", weapon, null, "", 0.01f);
				player.GiveNamedItem(GetDefaultKnifeClassname(player));
				player.ExecuteClientCommand("slot3");
				newWeapon.AddEntityIOEvent("Kill", newWeapon, null, "", 0.01f);
			}

			GivePlayerWeaponSkin(player, weapon);
		}
	}
}
