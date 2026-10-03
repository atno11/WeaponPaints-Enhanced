using CounterStrikeSharp.API;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void OnMapStart(string mapName)
	{
		if (Config.Additional is { KnifeEnabled: false, SkinEnabled: false, GloveEnabled: false })
			return;

		if (Database != null)
			WeaponSync = new WeaponSynchronization(Database, Config);

		_fadeSeed = 0;
		_nextItemId = MinimumCustomItemId;
	}

	private void OnTick()
	{
		if (!Config.Additional.ShowSkinImage)
			return;

		foreach (var player in Players)
		{
			if (_playerWeaponImage.TryGetValue(player.Slot, out var value) && !string.IsNullOrEmpty(value))
			{
				player.PrintToCenterHtml("<img src='{PATH}'</img>".Replace("{PATH}", value));
			}
		}
	}
}
