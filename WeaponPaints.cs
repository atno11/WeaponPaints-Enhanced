using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace WeaponPaints;

[MinimumApiVersion(338)]
public partial class WeaponPaints : BasePlugin, IPluginConfig<WeaponPaintsConfig>
{
	public static IStringLocalizer? _localizer;
	internal static WeaponPaints Instance { get; private set; } = new();

	public WeaponPaintsConfig Config { get; set; } = new();
	private static WeaponPaintsConfig _config { get; set; } = new();
	internal const string EnhancedVersion = "0.1.0-dev.1";
	internal const string EnhancedRepository = "https://github.com/atno11/WeaponPaints-Enhanced";

	public override string ModuleAuthor => "Nereziel & daffyy / WeaponPaints-Enhanced contributors";
	public override string ModuleDescription => "Standalone CS2 weapon cosmetics selector";
	public override string ModuleName => "WeaponPaints-Enhanced";
	public override string ModuleVersion => EnhancedVersion;

	public override void Load(bool hotReload)
	{
		// Hardcoded hotfix needs to be changed later (Not needed 17.09.2025)
		//if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		//	Patch.PerformPatch("0F 85 ? ? ? ? 31 C0 B9 ? ? ? ? BA ? ? ? ? 66 0F EF C0 31 F6 31 FF 48 C7 45 ? ? ? ? ? 48 C7 45 ? ? ? ? ? 48 C7 45 ? ? ? ? ? 48 C7 45 ? ? ? ? ? 0F 29 45 ? 48 C7 45 ? ? ? ? ? C7 45 ? ? ? ? ? 66 89 45 ? E8 ? ? ? ? 41 89 C5 85 C0 0F 8E", "90 90 90 90 90 90");
		//else
		//	Patch.PerformPatch("74 ? 48 8D 0D ? ? ? ? FF 15 ? ? ? ? EB ? BA", "EB");

		Instance = this;

		Logger.LogInformation("{ModuleName} v{Version} loaded", ModuleName, ModuleVersion);
		Logger.LogInformation("Repository: {Repository}", EnhancedRepository);

		if (hotReload)
			InitializeHotReload();

		Utility.LoadLocalizedCatalogs(Path.Combine(ModuleDirectory, "data"), _config.SkinsLanguage, Logger);
		RegisterListeners();
	}
}
