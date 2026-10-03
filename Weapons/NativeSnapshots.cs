using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;

namespace WeaponPaints;

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
}
