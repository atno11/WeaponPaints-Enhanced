using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OnCommandWS(CCSPlayerController? player, CommandInfo command)
	{
		if (!Config.Additional.SkinEnabled)
			return;

		if (!Utility.IsPlayerValid(player))
			return;

		if (!string.IsNullOrEmpty(Localizer["wp_info_refresh"]))
		{
			player!.Print(Localizer["wp_info_refresh"]);
		}

		if (Config.Additional.GloveEnabled && !string.IsNullOrEmpty(Localizer["wp_info_glove"]))
		{
			player!.Print(Localizer["wp_info_glove"]);
		}

		if (Config.Additional.AgentEnabled && !string.IsNullOrEmpty(Localizer["wp_info_agent"]))
		{
			player!.Print(Localizer["wp_info_agent"]);
		}

		if (Config.Additional.MusicEnabled && !string.IsNullOrEmpty(Localizer["wp_info_music"]))
		{
			player!.Print(Localizer["wp_info_music"]);
		}

		if (Config.Additional.PinsEnabled && !string.IsNullOrEmpty(Localizer["wp_info_pin"]))
		{
			player!.Print(Localizer["wp_info_pin"]);
		}

		if (!Config.Additional.KnifeEnabled)
			return;

		if (!string.IsNullOrEmpty(Localizer["wp_info_knife"]))
		{
			player!.Print(Localizer["wp_info_knife"]);
		}
	}
}
