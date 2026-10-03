using System.Collections.Concurrent;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private static readonly ConcurrentDictionary<int, int> KnifeSelectionVersions = new();
	private static readonly ConcurrentDictionary<int, int> GloveSelectionVersions = new();
	private static readonly ConcurrentDictionary<(int Slot, int WeaponDefIndex), int> SkinSelectionVersions = new();
	private static readonly ConcurrentDictionary<int, int> MusicSelectionVersions = new();

	private static readonly ConcurrentDictionary<int, SemaphoreSlim> KnifeSyncLocks = new();
	private static readonly ConcurrentDictionary<int, SemaphoreSlim> GloveSyncLocks = new();
	private static readonly ConcurrentDictionary<int, SemaphoreSlim> SkinSyncLocks = new();
	private static readonly ConcurrentDictionary<int, SemaphoreSlim> MusicSyncLocks = new();

	internal static int GetKnifeSelectionVersion(int slot)
	{
		return KnifeSelectionVersions.TryGetValue(slot, out var version) ? version : 0;
	}

	internal static int GetGloveSelectionVersion(int slot)
	{
		return GloveSelectionVersions.TryGetValue(slot, out var version) ? version : 0;
	}

	internal static int GetMusicSelectionVersion(int slot)
	{
		return MusicSelectionVersions.TryGetValue(slot, out var version) ? version : 0;
	}

	internal static int GetSkinSelectionVersion(int slot, int weaponDefindex)
	{
		return SkinSelectionVersions.TryGetValue((slot, weaponDefindex), out var version) ? version : 0;
	}

	internal static Dictionary<int, int> GetSkinSelectionVersions(int slot)
	{
		return SkinSelectionVersions
			.Where(entry => entry.Key.Slot == slot)
			.ToDictionary(entry => entry.Key.WeaponDefIndex, entry => entry.Value);
	}

	internal static bool IsKnifeDefindex(int defindex)
	{
		return WeaponDefindex.TryGetValue(defindex, out var classname) && (classname.Contains("knife") || classname.Contains("bayonet"));
	}

	internal static bool IsGloveDefindex(int defindex)
	{
		return GloveFamilyByDefindex.ContainsKey(defindex);
	}
}
