namespace WeaponPaints;

public partial class WeaponPaints
{
	private static bool _gBCommandsAllowed = true;

	private static readonly Dictionary<int, DateTime> CommandsCooldown = new();
}
