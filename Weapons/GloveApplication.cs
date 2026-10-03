using System.Collections.Concurrent;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

public partial class WeaponPaints
{
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
					if (!GPlayersGlove.TryGetValue(player.Slot, out var gloveInfo) || !gloveInfo.TryGetValue(player.Team, out var gloveId))
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
					CAttributeListSetOrAddAttributeValueByName.Invoke(item.AttributeList.Handle, "set item texture seed", weaponInfo.Seed);
					CAttributeListSetOrAddAttributeValueByName.Invoke(item.AttributeList.Handle, "set item texture wear", weaponInfo.Wear);

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
}
