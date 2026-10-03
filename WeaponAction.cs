using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace WeaponPaints
{
	public partial class WeaponPaints
	{
		private const ushort PaintKitAttributeDefinitionIndex = 6;
		private const ushort PaintSeedAttributeDefinitionIndex = 7;
		private const ushort PaintWearAttributeDefinitionIndex = 8;

		// silly method to update sticker when call RefreshWeapons()

		private void RefreshWeapons(CCSPlayerController? player)
		{
			if (!_gBCommandsAllowed)
				return;
			if (
				player == null
				|| !player.IsValid
				|| player.PlayerPawn.Value == null
				|| (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE
			)
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

		private static int GetRandomPaint(int defindex)
		{
			if (SkinsList.Count == 0)
				return 0;

			Random rnd = new Random();

			// Filter weapons by the provided defindex
			var filteredWeapons = SkinsList.Where(w => w["weapon_defindex"]?.ToString() == defindex.ToString()).ToList();

			if (filteredWeapons.Count == 0)
				return 0;

			var randomWeapon = filteredWeapons[rnd.Next(filteredWeapons.Count)];

			return int.TryParse(randomWeapon["paint"]?.ToString(), out var paintValue) ? paintValue : 0;
		}

		//xstage idea on css discord
		public static void SubclassChange(CBasePlayerWeapon weapon, ushort itemD)
		{
			weapon.AcceptInput("ChangeSubclass", value: itemD.ToString());
		}

		public static void SetBodygroup(CCSPlayerPawn pawn, string group, int value)
		{
			pawn.AcceptInput("SetBodygroup", value: $"{group},{value}");
		}

		private void UpdateWeaponMeshGroupMask(CBaseEntity weapon, bool isLegacy = false)
		{
			if (weapon.CBodyComponent?.SceneNode == null)
				return;
			//var skeleton = weapon.CBodyComponent.SceneNode.GetSkeletonInstance();
			// skeleton.ModelState.MeshGroupMask = isLegacy ? 2UL : 1UL;

			weapon.AcceptInput("SetBodygroup", value: $"body,{(isLegacy ? 1 : 0)}");
		}

		private void UpdatePlayerWeaponMeshGroupMask(CCSPlayerController player, CBasePlayerWeapon weapon, bool isLegacy)
		{
			UpdateWeaponMeshGroupMask(weapon, isLegacy);
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

		private void UpdatePlayerEconItemId(CEconItemView econItemView)
		{
			var itemId = _nextItemId++;

			econItemView.ItemID = itemId;
			econItemView.ItemIDLow = (uint)itemId & 0xFFFFFFFF;
			econItemView.ItemIDHigh = (uint)itemId >> 32;
		}

		private static CCSPlayerController? GetPlayerFromItemServices(CCSPlayer_ItemServices itemServices)
		{
			var pawn = itemServices.Pawn.Value;
			if (!pawn.IsValid || !pawn.Controller.IsValid || pawn.Controller.Value == null)
				return null;
			var player = new CCSPlayerController(pawn.Controller.Value.Handle);
			return !Utility.IsPlayerValid(player) ? null : player;
		}

		private static bool HasChangedKnife(CCSPlayerController player, out string? knifeValue)
		{
			knifeValue = null;

			// Check if player has knife info for their slot and team
			if (
				!GPlayersKnife.TryGetValue(player.Slot, out var knife)
				|| !knife.TryGetValue(player.Team, out var value)
				|| value == "weapon_knife"
			)
				return false;
			knifeValue = value; // Assign the knife value to the out parameter
			return true;
		}

		private static bool HasChangedPaint(CCSPlayerController player, int weaponDefIndex, out WeaponInfo? weaponInfo)
		{
			weaponInfo = null;

			// Check if player has weapons info for their slot and team
			if (!GPlayerWeaponsInfo.TryGetValue(player.Slot, out var teamInfo) || !teamInfo.TryGetValue(player.Team, out var teamWeapons))
			{
				return false;
			}

			// Check if the specified weapon has a paint/skin change
			if (!teamWeapons.TryGetValue(weaponDefIndex, out var value))
				return false;

			weaponInfo = value; // Assign the out variable when it exists
			return true;
		}

		private static float ViewAsFloat(uint value)
		{
			return BitConverter.Int32BitsToSingle((int)value);
		}
	}
}
