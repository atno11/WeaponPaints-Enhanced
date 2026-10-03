using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void RestoreInventoryGloves(CCSPlayerController player)
	{
		if (!Utility.IsPlayerValid(player) || (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE)
			return;

		var pawn = player.PlayerPawn.Value;
		if (pawn == null || !pawn.IsValid)
			return;

		if (!NativeGloveSnapshots.TryGetValue((player.Slot, player.SteamID, player.TeamNum, pawn.Handle), out var snapshot))
		{
			Logger.LogWarning("No native glove snapshot available for {PlayerName}", player.PlayerName);
			return;
		}

		ApplyNativeEconItemSnapshot(pawn.EconGloves, snapshot);
		pawn.EconGlovesChanged++;
		Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_nEconGlovesChanged");
		player.ExecuteClientCommand("lastinv");
		SetBodygroup(pawn, "first_or_third_person", 0);
		AddTimer(
			0.2f,
			() =>
			{
				if (pawn.IsValid)
					SetBodygroup(pawn, "first_or_third_person", 1);
			},
			TimerFlags.STOP_ON_MAPCHANGE
		);
	}

	private void RestoreInventoryWeapon(CCSPlayerController player, int weaponDefIndex)
	{
		if (
			!Utility.IsPlayerValid(player)
			|| player.PlayerPawn.Value == null
			|| player.PlayerPawn.Value.WeaponServices == null
			|| (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE
		)
		{
			return;
		}

		var pawn = player.PlayerPawn.Value;
		var pawnHandle = pawn.Handle;
		var weaponServices = pawn.WeaponServices;

		if (!NativeWeaponSnapshots.TryGetValue((player.Slot, player.SteamID, player.TeamNum, pawnHandle, weaponDefIndex), out var snapshot))
		{
			Logger.LogWarning(
				"No native weapon snapshot available for {PlayerName}, defindex {WeaponDefIndex}",
				player.PlayerName,
				weaponDefIndex
			);
			return;
		}

		foreach (var weaponHandle in weaponServices.MyWeapons.ToList())
		{
			if (!weaponHandle.IsValid || weaponHandle.Value == null || !weaponHandle.Value.IsValid)
				continue;

			var weapon = weaponHandle.Value;
			if (weapon.AttributeManager.Item.ItemDefinitionIndex != weaponDefIndex)
				continue;

			var weaponData = weapon.As<CCSWeaponBase>().VData;
			if (weaponData == null)
				return;

			var clip1 = weapon.Clip1;
			var reserveAmmo = weapon.ReserveAmmo.Length > 0 ? weapon.ReserveAmmo[0] : 0;
			var activeWeapon = weaponServices.ActiveWeapon.Value;
			var wasActive = activeWeapon != null && activeWeapon.IsValid && activeWeapon.Handle == weapon.Handle;
			var slotCommand =
				snapshot.Classname == "weapon_taser"
					? "slot11"
					: weaponData.GearSlot switch
					{
						gear_slot_t.GEAR_SLOT_RIFLE => "slot1",
						gear_slot_t.GEAR_SLOT_PISTOL => "slot2",
						_ => null,
					};

			weapon.Remove();

			Server.NextFrame(() =>
			{
				if (
					!Utility.IsPlayerValid(player)
					|| (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE
					|| player.PlayerPawn.Value == null
					|| player.PlayerPawn.Value.Handle != pawnHandle
				)
				{
					return;
				}

				var newWeapon = player.GiveNamedItem<CBasePlayerWeapon>(snapshot.Classname);
				if (newWeapon == null || !newWeapon.IsValid)
					return;

				if (HasChangedPaint(player, weaponDefIndex, out _))
				{
					GivePlayerWeaponSkin(player, newWeapon);
				}
				else
				{
					ApplyNativeWeaponSnapshot(newWeapon, snapshot);
					RestoreNativeWeaponMeshGroupMask(player, newWeapon, snapshot);
				}

				newWeapon.Clip1 = clip1;
				if (newWeapon.ReserveAmmo.Length > 0)
					newWeapon.ReserveAmmo[0] = reserveAmmo;

				Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");

				if (wasActive && !string.IsNullOrEmpty(slotCommand))
					player.ExecuteClientCommand(slotCommand);
			});

			return;
		}
	}

	private void RestoreInventoryKnife(CCSPlayerController player, int selectionVersion)
	{
		if (
			!Utility.IsPlayerValid(player)
			|| player.PlayerPawn.Value == null
			|| player.PlayerPawn.Value.WeaponServices == null
			|| (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE
		)
		{
			return;
		}

		var pawn = player.PlayerPawn.Value;
		var pawnHandle = pawn.Handle;
		if (!NativeKnifeSnapshots.TryGetValue((player.Slot, player.SteamID, player.TeamNum, pawnHandle), out var snapshot))
		{
			Logger.LogWarning("No native knife snapshot available for {PlayerName}", player.PlayerName);
			return;
		}

		foreach (var weaponHandle in pawn.WeaponServices.MyWeapons.ToList())
		{
			if (!weaponHandle.IsValid || weaponHandle.Value == null || !weaponHandle.Value.IsValid)
				continue;

			var weapon = weaponHandle.Value;
			if (!weapon.DesignerName.Contains("knife") && !weapon.DesignerName.Contains("bayonet"))
				continue;

			weapon.Remove();
		}

		AddTimer(
			0.10f,
			() =>
			{
				if (
					!Utility.IsPlayerValid(player)
					|| (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE
					|| player.PlayerPawn.Value == null
					|| player.PlayerPawn.Value.Handle != pawnHandle
					|| !KnifeSelectionVersions.TryGetValue(player.Slot, out var currentVersion)
					|| currentVersion != selectionVersion
				)
				{
					return;
				}

				var newKnife = player.GiveNamedItem<CBasePlayerWeapon>(GetDefaultKnifeClassname(player));
				if (newKnife == null || !newKnife.IsValid)
					return;

				ApplyNativeWeaponSnapshot(newKnife, snapshot);
				RestoreNativeWeaponMeshGroupMask(player, newKnife, snapshot);
				Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
				player.ExecuteClientCommand("slot3");
			},
			TimerFlags.STOP_ON_MAPCHANGE
		);
	}

	private static void RestoreInventoryMusicKit(CCSPlayerController player)
	{
		if (!Utility.IsPlayerValid(player) || player.InventoryServices == null)
			return;

		if (!NativeMusicKitSnapshots.TryGetValue((player.Slot, player.SteamID, player.Handle), out var snapshot))
		{
			return;
		}

		player.MusicKitID = snapshot.MusicKitID;
		player.InventoryServices.MusicID = snapshot.InventoryMusicID;
		Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMusicKitID");
		Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
	}
}
