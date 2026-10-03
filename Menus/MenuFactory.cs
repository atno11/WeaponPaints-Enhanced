using CounterStrikeSharp.API.Modules.Menu;
using MenuManager;

namespace WeaponPaints;

internal static partial class Utility
{
	internal static IMenu? CreateMenu(string title)
	{
		var menuType = WeaponPaints.Instance.Config.MenuType.ToLower();

		var menu = menuType switch
		{
			_ when menuType.Equals("selectable", StringComparison.CurrentCultureIgnoreCase) => WeaponPaints.MenuApi?.NewMenu(title),

			_ when menuType.Equals("dynamic", StringComparison.CurrentCultureIgnoreCase) => WeaponPaints.MenuApi?.NewMenuForcetype(
				title,
				MenuType.ButtonMenu
			),

			_ when menuType.Equals("center", StringComparison.CurrentCultureIgnoreCase) => WeaponPaints.MenuApi?.NewMenuForcetype(
				title,
				MenuType.CenterMenu
			),

			_ when menuType.Equals("chat", StringComparison.CurrentCultureIgnoreCase) => WeaponPaints.MenuApi?.NewMenuForcetype(
				title,
				MenuType.ChatMenu
			),

			_ when menuType.Equals("console", StringComparison.CurrentCultureIgnoreCase) => WeaponPaints.MenuApi?.NewMenuForcetype(
				title,
				MenuType.ConsoleMenu
			),

			_ => WeaponPaints.MenuApi?.NewMenu(title),
		};

		if (menu != null)
			menu.PostSelectAction = PostSelectAction.Nothing;

		return menu;
	}
}
