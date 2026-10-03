using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OnCommandStattrak(CCSPlayerController? player, CommandInfo commandInfo)
	{
		if (player == null || !player.IsValid)
			return;

		var weapon = player.PlayerPawn.Value?.WeaponServices?.ActiveWeapon.Value;

		if (weapon == null || !weapon.IsValid)
			return;

		if (!HasChangedPaint(player, weapon.AttributeManager.Item.ItemDefinitionIndex, out var weaponInfo) || weaponInfo == null)
			return;

		weaponInfo.StatTrak = !weaponInfo.StatTrak;

		RefreshWeapons(player);

		if (!string.IsNullOrEmpty(Localizer["wp_stattrak_action"]))
		{
			player.Print(Localizer["wp_stattrak_action"]);
		}
	}

	private void RegisterStattrakCommands()
	{
		_config.Additional.CommandStattrak.ForEach(c =>
		{
			AddCommand(
				$"css_{c}",
				"Stattrak toggle",
				(player, info) =>
				{
					if (!Utility.IsPlayerValid(player))
						return;

					OnCommandStattrak(player, info);
				}
			);
		});
	}
}
