using System.Collections.Concurrent;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
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
}
