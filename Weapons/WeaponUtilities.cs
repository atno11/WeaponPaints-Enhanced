using CounterStrikeSharp.API.Core;

namespace WeaponPaints;

public partial class WeaponPaints
{
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

	private const ulong MinimumCustomItemId = 65578;
	private ulong _nextItemId = MinimumCustomItemId;

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
