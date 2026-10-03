using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void RefreshWeaponSkin(CCSPlayerController? player, int weaponDefIndex)
	{
		if (
			!_gBCommandsAllowed
			|| player == null
			|| !Utility.IsPlayerValid(player)
			|| player.PlayerPawn.Value == null
			|| player.PlayerPawn.Value.WeaponServices == null
			|| (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE
			|| IsKnifeDefindex(weaponDefIndex)
		)
		{
			return;
		}

		var weaponServices = player.PlayerPawn.Value.WeaponServices;

		foreach (var weaponHandle in weaponServices.MyWeapons.ToList())
		{
			if (!weaponHandle.IsValid || weaponHandle.Value == null || !weaponHandle.Value.IsValid)
			{
				continue;
			}

			var weapon = weaponHandle.Value;

			if (weapon.AttributeManager.Item.ItemDefinitionIndex != weaponDefIndex)
			{
				continue;
			}

			if (weapon.DesignerName.Contains("knife") || weapon.DesignerName.Contains("bayonet"))
			{
				return;
			}

			CaptureNativeWeaponSnapshot(player, weapon);

			var weaponData = weapon.As<CCSWeaponBase>().VData;

			if (weaponData == null)
			{
				return;
			}

			var classname = weapon.DesignerName;
			var clip1 = weapon.Clip1;
			var reserveAmmo = weapon.ReserveAmmo.Length > 0 ? weapon.ReserveAmmo[0] : 0;

			var gearSlot = weaponData.GearSlot;

			var activeWeapon = weaponServices.ActiveWeapon.Value;

			var wasActive = activeWeapon != null && activeWeapon.IsValid && activeWeapon.Handle == weapon.Handle;

			var slotCommand =
				classname == "weapon_taser"
					? "slot11"
					: gearSlot switch
					{
						gear_slot_t.GEAR_SLOT_RIFLE => "slot1",
						gear_slot_t.GEAR_SLOT_PISTOL => "slot2",
						_ => null,
					};

			try
			{
				weapon.Remove();
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "Failed to remove weapon {WeaponName} from {PlayerName}", classname, player.PlayerName);

				return;
			}

			Server.NextFrame(() =>
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

				var newWeapon = player.GiveNamedItem<CBasePlayerWeapon>(classname);

				if (newWeapon == null || !newWeapon.IsValid)
				{
					Logger.LogWarning("Failed to recreate weapon {WeaponName} for {PlayerName}", classname, player.PlayerName);

					return;
				}

				Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");

				Server.NextFrame(() =>
				{
					if (!Utility.IsPlayerValid(player) || !newWeapon.IsValid || (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE)
					{
						return;
					}

					GivePlayerWeaponSkin(player, newWeapon);

					newWeapon.Clip1 = clip1;

					if (newWeapon.ReserveAmmo.Length > 0)
					{
						newWeapon.ReserveAmmo[0] = reserveAmmo;
					}

					if (wasActive && !string.IsNullOrEmpty(slotCommand))
					{
						player.ExecuteClientCommand(slotCommand);
					}
				});
			});

			return;
		}
	}

	private void GivePlayerWeaponSkin(CCSPlayerController player, CBasePlayerWeapon weapon)
	{
		if (!Config.Additional.SkinEnabled)
			return;

		CaptureNativeWeaponSnapshot(player, weapon);

		if (!GPlayerWeaponsInfo.TryGetValue(player.Slot, out _))
			return;

		bool isKnife = weapon.DesignerName.Contains("knife") || weapon.DesignerName.Contains("bayonet");

		switch (isKnife)
		{
			case true when !HasChangedKnife(player, out var _):
				return;

			case true:
			{
				var newDefIndex = WeaponDefindex.FirstOrDefault(x => x.Value == GPlayersKnife[player.Slot][player.Team]);
				if (newDefIndex.Key == 0)
					return;

				if (weapon.AttributeManager.Item.ItemDefinitionIndex != newDefIndex.Key)
				{
					SubclassChange(weapon, (ushort)newDefIndex.Key);
				}

				weapon.AttributeManager.Item.ItemDefinitionIndex = (ushort)newDefIndex.Key;
				weapon.AttributeManager.Item.EntityQuality = 3;

				weapon.AttributeManager.Item.AttributeList.Attributes.RemoveAll();
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Attributes.RemoveAll();
				break;
			}
			default:
				weapon.AttributeManager.Item.EntityQuality = 0;
				break;
		}

		UpdatePlayerEconItemId(weapon.AttributeManager.Item);

		int weaponDefIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;
		int fallbackPaintKit;

		weapon.AttributeManager.Item.AccountID = (uint)player.SteamID;

		List<JObject> skinInfo;
		bool isLegacyModel;

		if (_config.Additional.GiveRandomSkin && !HasChangedPaint(player, weaponDefIndex, out _))
		{
			// Random skins
			weapon.FallbackPaintKit = GetRandomPaint(weaponDefIndex);
			weapon.FallbackSeed = 0;
			weapon.FallbackWear = 0.01f;

			weapon.AttributeManager.Item.NetworkedDynamicAttributes.Attributes.RemoveAll();
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"set item texture prefab",
				GetRandomPaint(weaponDefIndex)
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"set item texture seed",
				0
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"set item texture wear",
				0.01f
			);

			weapon.AttributeManager.Item.AttributeList.Attributes.RemoveAll();
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.AttributeList.Handle,
				"set item texture prefab",
				GetRandomPaint(weaponDefIndex)
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.AttributeList.Handle,
				"set item texture seed",
				0
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.AttributeList.Handle,
				"set item texture wear",
				0.01f
			);

			fallbackPaintKit = weapon.FallbackPaintKit;

			if (fallbackPaintKit == 0)
				return;

			skinInfo = SkinsList
				.Where(w => w["weapon_defindex"]?.ToObject<int>() == weaponDefIndex && w["paint"]?.ToObject<int>() == fallbackPaintKit)
				.ToList();

			isLegacyModel = skinInfo.Count <= 0 || skinInfo[0].Value<bool>("legacy_model");
			UpdatePlayerWeaponMeshGroupMask(player, weapon, isLegacyModel);
			return;
		}

		if (!HasChangedPaint(player, weaponDefIndex, out var weaponInfo) || weaponInfo == null)
			return;

		//Log($"Apply on {weapon.DesignerName}({weapon.AttributeManager.Item.ItemDefinitionIndex}) paint {gPlayerWeaponPaints[steamId.SteamId64][weapon.AttributeManager.Item.ItemDefinitionIndex]} seed {gPlayerWeaponSeed[steamId.SteamId64][weapon.AttributeManager.Item.ItemDefinitionIndex]} wear {gPlayerWeaponWear[steamId.SteamId64][weapon.AttributeManager.Item.ItemDefinitionIndex]}");

		weapon.AttributeManager.Item.AttributeList.Attributes.RemoveAll();
		weapon.AttributeManager.Item.NetworkedDynamicAttributes.Attributes.RemoveAll();

		UpdatePlayerEconItemId(weapon.AttributeManager.Item);

		weapon.AttributeManager.Item.CustomName = weaponInfo.Nametag;
		weapon.FallbackPaintKit = weaponInfo.Paint;

		weapon.FallbackSeed = weaponInfo is { Paint: 38, Seed: 0 } ? _fadeSeed++ : weaponInfo.Seed;

		weapon.FallbackWear = weaponInfo.Wear;
		CAttributeListSetOrAddAttributeValueByName.Invoke(
			weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
			"set item texture prefab",
			weapon.FallbackPaintKit
		);

		if (weaponInfo.StatTrak)
		{
			weapon.AttributeManager.Item.EntityQuality = 9;

			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"kill eater",
				ViewAsFloat((uint)weaponInfo.StatTrakCount)
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"kill eater score type",
				0
			);

			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.AttributeList.Handle,
				"kill eater",
				ViewAsFloat((uint)weaponInfo.StatTrakCount)
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.AttributeList.Handle,
				"kill eater score type",
				0
			);
		}

		fallbackPaintKit = weapon.FallbackPaintKit;

		if (fallbackPaintKit == 0)
			return;

		if (weaponInfo.KeyChain != null)
			SetKeychain(player, weapon);
		if (weaponInfo.Stickers.Count > 0)
			SetStickers(player, weapon);

		skinInfo = SkinsList
			.Where(w => w["weapon_defindex"]?.ToObject<int>() == weaponDefIndex && w["paint"]?.ToObject<int>() == fallbackPaintKit)
			.ToList();

		isLegacyModel = skinInfo.Count <= 0 || skinInfo[0].Value<bool>("legacy_model");
		UpdatePlayerWeaponMeshGroupMask(player, weapon, isLegacyModel);
	}
}
