using System.Globalization;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OnCommandFloat(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (!Config.Additional.SkinEnabled || !_gBCommandsAllowed)
			return;

		if (
			player == null
			|| !Utility.IsPlayerValid(player)
			|| player.PlayerPawn.Value == null
			|| player.PlayerPawn.Value.WeaponServices == null
		)
		{
			return;
		}

		var rawWear = commandInfo.GetArg(1);

		if (string.IsNullOrWhiteSpace(rawWear))
		{
			player.Print(Localizer["wp_float_usage"]);
			return;
		}

		var normalizedWear = rawWear.Trim().Replace(',', '.');

		if (
			!float.TryParse(normalizedWear, NumberStyles.Float, CultureInfo.InvariantCulture, out var requestedWear)
			|| !float.IsFinite(requestedWear)
		)
		{
			player.Print(Localizer["wp_float_invalid"]);
			return;
		}

		if (!IsWearInRange(requestedWear))
		{
			var (minWear, maxWear) = GetWearRange();

			player.Print(
				Localizer[
					"wp_float_range",
					minWear.ToString("0.000000", CultureInfo.InvariantCulture),
					maxWear.ToString("0.000000", CultureInfo.InvariantCulture)
				]
			);

			return;
		}

		var weapon = player.PlayerPawn.Value.WeaponServices.ActiveWeapon.Value;

		if (weapon == null || !weapon.IsValid)
		{
			player.Print(Localizer["wp_float_no_weapon"]);
			return;
		}

		var weaponDefindex = weapon.AttributeManager.Item.ItemDefinitionIndex;

		if (!HasChangedPaint(player, weaponDefindex, out var weaponInfo) || weaponInfo == null)
		{
			player.Print(Localizer["wp_float_no_skin"]);
			return;
		}

		var teams = new[] { player.Team };

		if (!TrySetWeaponWear(player, weaponDefindex, teams, requestedWear, clampToRange: false, out var appliedWear))
		{
			player.Print(Localizer["wp_float_failed"]);
			return;
		}

		player.Print(Localizer["wp_float_updated", appliedWear.ToString("0.000000", CultureInfo.InvariantCulture)]);
	}
}
