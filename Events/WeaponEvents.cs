using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private HookResult OnGiveNamedItemPost(DynamicHook hook)
	{
		try
		{
			var itemServices = hook.GetParam<CCSPlayer_ItemServices>(0);
			var weapon = hook.GetReturn<CBasePlayerWeapon>();
			if (!weapon.DesignerName.Contains("weapon"))
				return HookResult.Continue;

			var player = GetPlayerFromItemServices(itemServices);
			if (player != null)
			{
				GivePlayerWeaponSkin(player, weapon);
			}
		}
		catch { }

		return HookResult.Continue;
	}

	private void OnEntityCreated(CEntityInstance entity)
	{
		var designerName = entity.DesignerName;

		if (designerName.Contains("weapon"))
		{
			Server.NextWorldUpdate(() =>
			{
				var weapon = new CBasePlayerWeapon(entity.Handle);
				if (!weapon.IsValid)
					return;

				try
				{
					SteamID? steamid = null;

					if (weapon.OriginalOwnerXuidLow > 0)
						steamid = new SteamID(weapon.OriginalOwnerXuidLow);

					CCSPlayerController? player;

					if (steamid != null && steamid.IsValid())
					{
						player = Players.FirstOrDefault(p => p.IsValid && p.SteamID == steamid.SteamId64);

						if (player == null)
							player = Utilities.GetPlayerFromSteamId(weapon.OriginalOwnerXuidLow);
					}
					else
					{
						CCSWeaponBaseGun gun = weapon.As<CCSWeaponBaseGun>();
						player =
							Utilities.GetPlayerFromIndex((int)weapon.OwnerEntity.Index)
							?? Utilities.GetPlayerFromIndex((int)gun.OwnerEntity.Value!.Index);
					}

					if (string.IsNullOrEmpty(player?.PlayerName))
						return;
					if (!Utility.IsPlayerValid(player))
						return;

					GivePlayerWeaponSkin(player, weapon);
				}
				catch (Exception) { }
			});
		}
	}

	[GameEventHandler]
	public HookResult OnItemPickup(EventItemPickup @event, GameEventInfo _)
	{
		// if (!IsWindows) return HookResult.Continue;
		var player = @event.Userid;
		if (player == null || !player.IsValid || player.IsBot)
			return HookResult.Continue;
		if (!@event.Item.Contains("knife"))
			return HookResult.Continue;

		var weaponDefIndex = (int)@event.Defindex;

		if (!HasChangedKnife(player, out var _) || !HasChangedPaint(player, weaponDefIndex, out var _))
			return HookResult.Continue;

		if (player is { Connected: PlayerConnectedState.Connected, PawnIsAlive: true, PlayerPawn.IsValid: true })
		{
			GiveOnItemPickup(player);
		}

		return HookResult.Continue;
	}

	private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
	{
		CCSPlayerController? player = @event.Attacker;
		CCSPlayerController? victim = @event.Userid;

		if (player is null || !player.IsValid)
			return HookResult.Continue;

		if (victim == null || !victim.IsValid || victim == player)
			return HookResult.Continue;

		CBasePlayerWeapon? weapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;

		if (weapon == null)
			return HookResult.Continue;

		int weaponDefIndex = weapon.AttributeManager.Item.ItemDefinitionIndex;

		if (!HasChangedPaint(player, weaponDefIndex, out var weaponInfo) || weaponInfo == null)
			return HookResult.Continue;

		if (!weaponInfo.StatTrak)
			return HookResult.Continue;

		weaponInfo.StatTrakCount += 1;

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
		CAttributeListSetOrAddAttributeValueByName.Invoke(weapon.AttributeManager.Item.AttributeList.Handle, "kill eater score type", 0);

		return HookResult.Continue;
	}
}
