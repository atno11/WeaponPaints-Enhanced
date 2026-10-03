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

		private static float? GetEconAttributeValue(CAttributeList attributeList, ushort attributeDefinitionIndex)
		{
			var attributes = attributeList.Attributes;
			var count = NativeAPI.GetNetworkVectorSize(attributes.Handle);

			for (var i = 0; i < count; i++)
			{
				var attributePointer = NativeAPI.GetNetworkVectorElementAt(attributes.Handle, i);

				if (attributePointer == nint.Zero)
					continue;

				var attribute = new CEconItemAttribute(attributePointer);

				if (attribute.AttributeDefinitionIndex == attributeDefinitionIndex)
					return attribute.Value;
			}

			return null;
		}

		private static float? GetEconAttributeValue(CEconItemView item, ushort attributeDefinitionIndex)
		{
			try
			{
				return GetEconAttributeValue(item.NetworkedDynamicAttributes, attributeDefinitionIndex)
					?? GetEconAttributeValue(item.AttributeList, attributeDefinitionIndex);
			}
			catch
			{
				// Snapshot failure must never prevent WeaponPaints from applying the selected custom item.
				return null;
			}
		}

		private static NativeEconItemSnapshot CreateNativeEconItemSnapshot(CEconItemView item)
		{
			return new NativeEconItemSnapshot(
				item.ItemDefinitionIndex,
				item.EntityQuality,
				item.EntityLevel,
				item.ItemID,
				item.ItemIDHigh,
				item.ItemIDLow,
				item.AccountID,
				item.InventoryPosition,
				item.Initialized,
				item.CustomName,
				item.CustomNameOverride,
				GetEconAttributeValue(item, PaintKitAttributeDefinitionIndex),
				GetEconAttributeValue(item, PaintSeedAttributeDefinitionIndex),
				GetEconAttributeValue(item, PaintWearAttributeDefinitionIndex)
			);
		}

		private static NativeWeaponSnapshot CreateNativeWeaponSnapshot(CBasePlayerWeapon weapon)
		{
			return new NativeWeaponSnapshot(
				CreateNativeEconItemSnapshot(weapon.AttributeManager.Item),
				weapon.DesignerName,
				weapon.OriginalOwnerXuidLow,
				weapon.OriginalOwnerXuidHigh,
				weapon.FallbackPaintKit,
				weapon.FallbackSeed,
				weapon.FallbackWear,
				weapon.FallbackStatTrak
			);
		}

		private static void PruneNativePawnSnapshots(CCSPlayerController player, nint pawnHandle)
		{
			foreach (var key in NativeGloveSnapshots.Keys)
			{
				if (key.Slot == player.Slot && (key.SteamId != player.SteamID || key.PawnHandle != pawnHandle))
					NativeGloveSnapshots.TryRemove(key, out _);
			}

			foreach (var key in NativeKnifeSnapshots.Keys)
			{
				if (key.Slot == player.Slot && (key.SteamId != player.SteamID || key.PawnHandle != pawnHandle))
					NativeKnifeSnapshots.TryRemove(key, out _);
			}

			foreach (var key in NativeWeaponSnapshots.Keys)
			{
				if (key.Slot == player.Slot && (key.SteamId != player.SteamID || key.PawnHandle != pawnHandle))
					NativeWeaponSnapshots.TryRemove(key, out _);
			}
		}

		private static void CaptureNativeWeaponSnapshot(CCSPlayerController player, CBasePlayerWeapon weapon)
		{
			var pawn = player.PlayerPawn.Value;
			if (pawn == null || !pawn.IsValid || !weapon.IsValid)
				return;

			PruneNativePawnSnapshots(player, pawn.Handle);

			var snapshot = CreateNativeWeaponSnapshot(weapon);
			var isKnife = weapon.DesignerName.Contains("knife") || weapon.DesignerName.Contains("bayonet");

			if (isKnife)
			{
				NativeKnifeSnapshots.TryAdd((player.Slot, player.SteamID, player.TeamNum, pawn.Handle), snapshot);
				return;
			}

			NativeWeaponSnapshots.TryAdd(
				(player.Slot, player.SteamID, player.TeamNum, pawn.Handle, snapshot.Item.ItemDefinitionIndex),
				snapshot
			);
		}

		private static void CaptureNativeGloveSnapshot(CCSPlayerController player)
		{
			var pawn = player.PlayerPawn.Value;
			if (pawn == null || !pawn.IsValid)
				return;

			PruneNativePawnSnapshots(player, pawn.Handle);
			NativeGloveSnapshots.TryAdd(
				(player.Slot, player.SteamID, player.TeamNum, pawn.Handle),
				CreateNativeEconItemSnapshot(pawn.EconGloves)
			);
		}

		private static void CaptureNativeMusicKitSnapshot(CCSPlayerController player)
		{
			if (player.InventoryServices == null)
				return;

			foreach (var key in NativeMusicKitSnapshots.Keys)
			{
				if (key.Slot == player.Slot && (key.SteamId != player.SteamID || key.ControllerHandle != player.Handle))
					NativeMusicKitSnapshots.TryRemove(key, out _);
			}

			NativeMusicKitSnapshots.TryAdd(
				(player.Slot, player.SteamID, player.Handle),
				new NativeMusicKitSnapshot(player.MusicKitID, player.InventoryServices.MusicID)
			);
		}

		private static void RestoreTextureAttributes(CEconItemView item, NativeEconItemSnapshot snapshot)
		{
			item.NetworkedDynamicAttributes.Attributes.RemoveAll();
			item.AttributeList.Attributes.RemoveAll();

			if (snapshot.PaintKitAttribute.HasValue)
			{
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					item.NetworkedDynamicAttributes.Handle,
					"set item texture prefab",
					snapshot.PaintKitAttribute.Value
				);
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					item.AttributeList.Handle,
					"set item texture prefab",
					snapshot.PaintKitAttribute.Value
				);
			}

			if (snapshot.PaintSeedAttribute.HasValue)
			{
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					item.NetworkedDynamicAttributes.Handle,
					"set item texture seed",
					snapshot.PaintSeedAttribute.Value
				);
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					item.AttributeList.Handle,
					"set item texture seed",
					snapshot.PaintSeedAttribute.Value
				);
			}

			if (snapshot.PaintWearAttribute.HasValue)
			{
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					item.NetworkedDynamicAttributes.Handle,
					"set item texture wear",
					snapshot.PaintWearAttribute.Value
				);
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					item.AttributeList.Handle,
					"set item texture wear",
					snapshot.PaintWearAttribute.Value
				);
			}
		}

		private static void ApplyNativeEconItemSnapshot(CEconItemView item, NativeEconItemSnapshot snapshot)
		{
			item.ItemDefinitionIndex = snapshot.ItemDefinitionIndex;
			item.EntityQuality = snapshot.EntityQuality;
			item.EntityLevel = snapshot.EntityLevel;
			item.ItemID = snapshot.ItemID;
			item.ItemIDHigh = snapshot.ItemIDHigh;
			item.ItemIDLow = snapshot.ItemIDLow;
			item.AccountID = snapshot.AccountID;
			item.InventoryPosition = snapshot.InventoryPosition;
			item.CustomName = snapshot.CustomName;
			item.CustomNameOverride = snapshot.CustomNameOverride;
			RestoreTextureAttributes(item, snapshot);
			item.Initialized = snapshot.Initialized;
		}

		private static void ApplyNativeWeaponSnapshot(CBasePlayerWeapon weapon, NativeWeaponSnapshot snapshot)
		{
			if (weapon.AttributeManager.Item.ItemDefinitionIndex != snapshot.Item.ItemDefinitionIndex)
				SubclassChange(weapon, snapshot.Item.ItemDefinitionIndex);

			ApplyNativeEconItemSnapshot(weapon.AttributeManager.Item, snapshot.Item);
			weapon.OriginalOwnerXuidLow = snapshot.OriginalOwnerXuidLow;
			weapon.OriginalOwnerXuidHigh = snapshot.OriginalOwnerXuidHigh;
			weapon.FallbackPaintKit = snapshot.FallbackPaintKit;
			weapon.FallbackSeed = snapshot.FallbackSeed;
			weapon.FallbackWear = snapshot.FallbackWear;
			weapon.FallbackStatTrak = snapshot.FallbackStatTrak;
		}

		private void RestoreNativeWeaponMeshGroupMask(CCSPlayerController player, CBasePlayerWeapon weapon, NativeWeaponSnapshot snapshot)
		{
			var paintKit = snapshot.FallbackPaintKit;

			if (paintKit <= 0 && snapshot.Item.PaintKitAttribute.HasValue)
				paintKit = (int)snapshot.Item.PaintKitAttribute.Value;

			if (paintKit <= 0)
				return;

			var skinInfo = SkinsList.FirstOrDefault(skin =>
				skin["weapon_defindex"]?.ToObject<int>() == snapshot.Item.ItemDefinitionIndex && skin["paint"]?.ToObject<int>() == paintKit
			);

			if (skinInfo == null)
				return;

			UpdatePlayerWeaponMeshGroupMask(player, weapon, skinInfo.Value<bool>("legacy_model"));
		}

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

			if (
				!NativeWeaponSnapshots.TryGetValue(
					(player.Slot, player.SteamID, player.TeamNum, pawnHandle, weaponDefIndex),
					out var snapshot
				)
			)
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

		// silly method to update sticker when call RefreshWeapons()
		private void IncrementWearForWeaponWithStickers(CCSPlayerController player, CBasePlayerWeapon weapon)
		{
			int weaponDefIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;
			if (!HasChangedPaint(player, weaponDefIndex, out var weaponInfo) || weaponInfo == null || weaponInfo.Stickers.Count <= 0)
				return;

			float wearIncrement = 0.001f;
			float currentWear = weaponInfo.Wear;

			var playerWear = _temporaryPlayerWeaponWear.GetOrAdd(player.Slot, _ => new ConcurrentDictionary<int, float>());

			float incrementedWear = playerWear.AddOrUpdate(
				weaponDefIndex,
				currentWear + wearIncrement,
				(_, oldWear) => Math.Min(oldWear + wearIncrement, 1.0f)
			);

			weapon.FallbackWear = incrementedWear;
		}

		private void SetStickers(CCSPlayerController? player, CBasePlayerWeapon weapon)
		{
			if (player == null || !player.IsValid)
				return;

			int weaponDefIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;

			if (!HasChangedPaint(player, weaponDefIndex, out var weaponInfo) || weaponInfo == null)
				return;

			foreach (var sticker in weaponInfo.Stickers)
			{
				int stickerSlot = weaponInfo.Stickers.IndexOf(sticker);

				CAttributeListSetOrAddAttributeValueByName.Invoke(
					weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
					$"sticker slot {stickerSlot} id",
					ViewAsFloat(sticker.Id)
				);
				if (sticker.OffsetX != 0 || sticker.OffsetY != 0)
					CAttributeListSetOrAddAttributeValueByName.Invoke(
						weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
						$"sticker slot {stickerSlot} schema",
						0
					);
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
					$"sticker slot {stickerSlot} offset x",
					sticker.OffsetX
				);
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
					$"sticker slot {stickerSlot} offset y",
					sticker.OffsetY
				);
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
					$"sticker slot {stickerSlot} wear",
					sticker.Wear
				);
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
					$"sticker slot {stickerSlot} scale",
					sticker.Scale
				);
				CAttributeListSetOrAddAttributeValueByName.Invoke(
					weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
					$"sticker slot {stickerSlot} rotation",
					sticker.Rotation
				);
			}

			if (
				_temporaryPlayerWeaponWear.TryGetValue(player.Slot, out var playerWear)
				&& playerWear.TryGetValue(weaponDefIndex, out float storedWear)
			)
			{
				weapon.FallbackWear = storedWear;
			}
		}

		private void SetKeychain(CCSPlayerController? player, CBasePlayerWeapon weapon)
		{
			if (player == null || !player.IsValid)
				return;

			int weaponDefIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;

			if (!HasChangedPaint(player, weaponDefIndex, out var value) || value?.KeyChain == null)
				return;

			var keyChain = value.KeyChain;

			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"keychain slot 0 id",
				ViewAsFloat(keyChain.Id)
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"keychain slot 0 offset x",
				keyChain.OffsetX
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"keychain slot 0 offset y",
				keyChain.OffsetY
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"keychain slot 0 offset z",
				keyChain.OffsetZ
			);
			CAttributeListSetOrAddAttributeValueByName.Invoke(
				weapon.AttributeManager.Item.NetworkedDynamicAttributes.Handle,
				"keychain slot 0 seed",
				ViewAsFloat(keyChain.Seed)
			);
		}

		private static string GetDefaultKnifeClassname(CCSPlayerController player)
		{
			return player.TeamNum == (int)CsTeam.Terrorist ? "weapon_knife_t" : "weapon_knife";
		}

		private void ApplyPlayerKnifeRuntimeSelection(CCSPlayerController player, int selectionVersion)
		{
			AddTimer(
				0.20f,
				() =>
				{
					if (
						!Utility.IsPlayerValid(player)
						|| (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE
						|| !KnifeSelectionVersions.TryGetValue(player.Slot, out var currentVersion)
						|| currentVersion != selectionVersion
					)
					{
						return;
					}

					RecreatePlayerKnife(player, selectionVersion);
				},
				TimerFlags.STOP_ON_MAPCHANGE
			);
		}

		private void RecreatePlayerKnife(CCSPlayerController player, int selectionVersion)
		{
			if (
				!Utility.IsPlayerValid(player)
				|| player.PlayerPawn.Value == null
				|| player.PlayerPawn.Value.WeaponServices == null
				|| (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE
				|| !KnifeSelectionVersions.TryGetValue(player.Slot, out var currentVersion)
				|| currentVersion != selectionVersion
			)
			{
				return;
			}

			var pawn = player.PlayerPawn.Value;
			var pawnHandle = pawn.Handle;
			var weapons = pawn.WeaponServices.MyWeapons;

			foreach (var weaponHandle in weapons.ToList())
			{
				if (!weaponHandle.IsValid || weaponHandle.Value == null || !weaponHandle.Value.IsValid)
					continue;

				var existingWeapon = weaponHandle.Value;

				if (existingWeapon.DesignerName.Contains("knife") || existingWeapon.DesignerName.Contains("bayonet"))
				{
					CaptureNativeWeaponSnapshot(player, existingWeapon);
					break;
				}
			}

			if (!KnifeSelectionVersions.TryGetValue(player.Slot, out currentVersion) || currentVersion != selectionVersion)
			{
				return;
			}

			foreach (var weaponHandle in weapons.ToList())
			{
				if (!weaponHandle.IsValid || weaponHandle.Value == null || !weaponHandle.Value.IsValid)
					continue;

				var weapon = weaponHandle.Value;

				if (!weapon.DesignerName.Contains("knife") && !weapon.DesignerName.Contains("bayonet"))
					continue;

				try
				{
					weapon.Remove();
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, "Failed to remove knife {KnifeName} from {PlayerName}", weapon.DesignerName, player.PlayerName);
				}
			}

			AddTimer(
				0.10f,
				() =>
				{
					if (
						!Utility.IsPlayerValid(player)
						|| player.PlayerPawn.Value == null
						|| player.PlayerPawn.Value.Handle != pawnHandle
						|| (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE
						|| !KnifeSelectionVersions.TryGetValue(player.Slot, out var latestVersion)
						|| latestVersion != selectionVersion
					)
					{
						return;
					}

					var defaultKnifeClassname = GetDefaultKnifeClassname(player);

					var newKnife = player.GiveNamedItem<CBasePlayerWeapon>(defaultKnifeClassname);

					if (newKnife == null || !newKnife.IsValid)
					{
						Logger.LogWarning(
							"Failed to give {KnifeClassname} to {PlayerName}, team {Team}",
							defaultKnifeClassname,
							player.PlayerName,
							player.Team
						);

						return;
					}

					Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");

					AddTimer(
						0.05f,
						() =>
						{
							if (
								!Utility.IsPlayerValid(player)
								|| player.PlayerPawn.Value == null
								|| player.PlayerPawn.Value.Handle != pawnHandle
								|| !newKnife.IsValid
								|| !KnifeSelectionVersions.TryGetValue(player.Slot, out var finalVersion)
								|| finalVersion != selectionVersion
							)
							{
								return;
							}

							GivePlayerWeaponSkin(player, newKnife);

							player.ExecuteClientCommand("slot3");
						},
						TimerFlags.STOP_ON_MAPCHANGE
					);
				},
				TimerFlags.STOP_ON_MAPCHANGE
			);
		}

		private static void GiveKnifeToPlayer(CCSPlayerController? player)
		{
			if (!_config.Additional.KnifeEnabled || player == null || !player.IsValid)
				return;

			if (PlayerHasKnife(player))
				return;

			//string knifeToGive = (CsTeam)player.TeamNum == CsTeam.Terrorist ? "weapon_knife_t" : "weapon_knife";
			player.GiveNamedItem(GetDefaultKnifeClassname(player));
			Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
		}

		private static bool PlayerHasKnife(CCSPlayerController? player)
		{
			if (!_config.Additional.KnifeEnabled)
				return false;

			if (player == null || !player.IsValid || !player.PlayerPawn.IsValid)
			{
				return false;
			}

			if (
				player.PlayerPawn.Value == null
				|| player.PlayerPawn.Value.WeaponServices == null
				|| player.PlayerPawn.Value.ItemServices == null
			)
				return false;

			var weapons = player.PlayerPawn.Value.WeaponServices?.MyWeapons;
			if (weapons == null)
				return false;
			foreach (var weapon in weapons)
			{
				if (!weapon.IsValid || weapon.Value == null || !weapon.Value.IsValid)
					continue;
				if (weapon.Value.DesignerName.Contains("knife") || weapon.Value.DesignerName.Contains("bayonet"))
				{
					return true;
				}
			}
			return false;
		}

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

		private void GivePlayerGloves(CCSPlayerController player)
		{
			if (!Utility.IsPlayerValid(player) || (LifeState_t)player.LifeState != LifeState_t.LIFE_ALIVE)
				return;

			CCSPlayerPawn? pawn = player.PlayerPawn.Value;
			if (pawn == null || !pawn.IsValid)
				return;

			CaptureNativeGloveSnapshot(player);

			CEconItemView item = pawn.EconGloves;

			//force gloves model refresh to prevent model overlap
			player.ExecuteClientCommand("lastinv");
			Instance.AddTimer(
				0.08f,
				() =>
				{
					try
					{
						if (!player.IsValid)
							return;

						if (!player.PawnIsAlive)
							return;

						// No WeaponPaints override:
						// leave the CS2 inventory glove untouched.
						if (
							!GPlayersGlove.TryGetValue(player.Slot, out var gloveInfo)
							|| !gloveInfo.TryGetValue(player.Team, out var gloveId)
						)
						{
							return;
						}

						// Explicit Default:
						// force the vanilla/default CT/T glove.
						if (gloveId == 0)
						{
							item.ItemDefinitionIndex = 0;
							item.NetworkedDynamicAttributes.Attributes.RemoveAll();
							item.AttributeList.Attributes.RemoveAll();

							UpdatePlayerEconItemId(item);
							pawn.EconGlovesChanged++;
							Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_nEconGlovesChanged");

							player.ExecuteClientCommand("lastinv");

							SetBodygroup(pawn, "first_or_third_person", 0);

							AddTimer(0.2f, () => SetBodygroup(pawn, "first_or_third_person", 1), TimerFlags.STOP_ON_MAPCHANGE);

							return;
						}

						if (!HasChangedPaint(player, gloveId, out var weaponInfo) || weaponInfo == null)
						{
							return;
						}

						item.ItemDefinitionIndex = gloveId;

						UpdatePlayerEconItemId(item);

						item.NetworkedDynamicAttributes.Attributes.RemoveAll();
						CAttributeListSetOrAddAttributeValueByName.Invoke(
							item.NetworkedDynamicAttributes.Handle,
							"set item texture prefab",
							weaponInfo.Paint
						);
						CAttributeListSetOrAddAttributeValueByName.Invoke(
							item.NetworkedDynamicAttributes.Handle,
							"set item texture seed",
							weaponInfo.Seed
						);
						CAttributeListSetOrAddAttributeValueByName.Invoke(
							item.NetworkedDynamicAttributes.Handle,
							"set item texture wear",
							weaponInfo.Wear
						);

						item.AttributeList.Attributes.RemoveAll();
						CAttributeListSetOrAddAttributeValueByName.Invoke(
							item.AttributeList.Handle,
							"set item texture prefab",
							weaponInfo.Paint
						);
						CAttributeListSetOrAddAttributeValueByName.Invoke(
							item.AttributeList.Handle,
							"set item texture seed",
							weaponInfo.Seed
						);
						CAttributeListSetOrAddAttributeValueByName.Invoke(
							item.AttributeList.Handle,
							"set item texture wear",
							weaponInfo.Wear
						);

						item.Initialized = true;
						pawn.EconGlovesChanged++;
						Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_nEconGlovesChanged");

						//force gloves model refresh to prevent model overlap
						player.ExecuteClientCommand("lastinv");
						SetBodygroup(pawn, "first_or_third_person", 0);
						AddTimer(0.2f, () => SetBodygroup(pawn, "first_or_third_person", 1), TimerFlags.STOP_ON_MAPCHANGE);
					}
					catch (Exception) { }
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

		private static void GivePlayerAgent(CCSPlayerController player)
		{
			if (!GPlayersAgent.TryGetValue(player.Slot, out var value))
				return;

			var model = player.TeamNum == 3 ? value.CT : value.T;
			if (string.IsNullOrEmpty(model))
				return;

			if (player.PlayerPawn.Value == null)
				return;

			try
			{
				Server.NextFrame(() =>
				{
					player.PlayerPawn.Value.SetModel($"agents/models/{model}.vmdl");
				});
			}
			catch (Exception) { }
		}

		private static void GivePlayerMusicKit(CCSPlayerController player)
		{
			if (player.IsBot || player.InventoryServices == null)
				return;

			CaptureNativeMusicKitSnapshot(player);

			if (!GPlayersMusic.TryGetValue(player.Slot, out var musicInfo) || !musicInfo.TryGetValue(player.Team, out var musicId))
				return;

			player.MusicKitID = musicId;
			player.InventoryServices.MusicID = musicId;
			Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMusicKitID");
			Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
		}

		private static void GivePlayerPin(CCSPlayerController player)
		{
			if (!GPlayersPin.TryGetValue(player.Slot, out var pinInfo) || !pinInfo.TryGetValue(player.Team, out var pinId))
				return;
			if (player.InventoryServices == null)
				return;

			player.InventoryServices.Rank[5] = pinId > 0 ? (MedalRank_t)pinId : MedalRank_t.MEDAL_RANK_NONE;
			Utilities.SetStateChanged(player, "CCSPlayerController", "m_pInventoryServices");
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
