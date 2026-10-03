namespace WeaponPaints;

internal static partial class Utility
{
	internal static void Log(string message)
	{
		Console.BackgroundColor = ConsoleColor.DarkGray;
		Console.ForegroundColor = ConsoleColor.Cyan;
		Console.WriteLine("[WeaponPaints] " + message);
		Console.ResetColor();
	}

	internal static void ShowAd(string moduleVersion)
	{
		Console.WriteLine(" ");
		Console.WriteLine("     _  _____ _   _  ___  ");
		Console.WriteLine("    / \\|_   _| \\ | |/ _ \\ ");
		Console.WriteLine("   / _ \\ | | |  \\| | | | |");
		Console.WriteLine("  / ___ \\| | | |\\  | |_| |");
		Console.WriteLine(" /_/   \\_\\_| |_| \\_|\\___/ ");
		Console.WriteLine(" ");
		Console.WriteLine($" WeaponPaints-Enhanced >> Version: {moduleVersion}");
		Console.WriteLine(" GitHub >> https://github.com/atno11/WeaponPaints-Enhanced/tags");
		Console.WriteLine(" ");
	}
}
