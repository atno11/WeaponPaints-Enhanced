using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace WeaponPaints;

public partial class WeaponPaints
{
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
}
