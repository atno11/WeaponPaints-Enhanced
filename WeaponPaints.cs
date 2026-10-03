using System.Runtime.InteropServices;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace WeaponPaints;

[MinimumApiVersion(338)]
public partial class WeaponPaints : BasePlugin, IPluginConfig<WeaponPaintsConfig>
{
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
		{
			OnMapStart(string.Empty);

			GPlayerWeaponsInfo.Clear();
			GPlayersKnife.Clear();
			GPlayersGlove.Clear();
			GPlayersAgent.Clear();
			GPlayersPin.Clear();
			GPlayersMusic.Clear();

			foreach (
				var player in Enumerable
					.OfType<CCSPlayerController>(Utilities.GetPlayers().TakeWhile(_ => WeaponSync != null))
					.Where(player =>
						player.IsValid
						&& !string.IsNullOrEmpty(player.IpAddress)
						&& player is { IsBot: false, Connected: PlayerConnectedState.Connected }
					)
			)
			{
				var playerInfo = new PlayerInfo
				{
					UserId = player.UserId,
					Slot = player.Slot,
					Index = (int)player.Index,
					SteamId = player?.SteamID.ToString(),
					Name = player?.PlayerName,
					IpAddress = player?.IpAddress?.Split(":")[0],
				};

				_ = Task.Run(async () =>
				{
					if (WeaponSync != null)
						await WeaponSync.GetPlayerData(playerInfo);
				});
			}
		}

		Utility.LoadLocalizedCatalogs(Path.Combine(ModuleDirectory, "data"), _config.SkinsLanguage, Logger);
		RegisterListeners();
	}

	public void OnConfigParsed(WeaponPaintsConfig config)
	{
		Config = config;
		_config = config;

		if (config.DatabaseHost.Length < 1 || config.DatabaseName.Length < 1 || config.DatabaseUser.Length < 1)
		{
			Logger.LogError("You need to setup Database credentials in \"configs/plugins/WeaponPaints/WeaponPaints.json\"!");
			Unload(false);
			return;
		}

		if (!File.Exists(Path.GetDirectoryName(Path.GetDirectoryName(ModuleDirectory)) + "/gamedata/weaponpaints.json"))
		{
			Logger.LogError("You need to upload \"weaponpaints.json\" to \"gamedata directory\"!");
			Unload(false);
			return;
		}

		var builder = new MySqlConnectionStringBuilder
		{
			Server = config.DatabaseHost,
			UserID = config.DatabaseUser,
			Password = config.DatabasePassword,
			Database = config.DatabaseName,
			Port = (uint)config.DatabasePort,
			Pooling = true,
			MaximumPoolSize = 640,
		};

		Database = new Database(builder.ConnectionString);

		_ = Utility.CheckDatabaseTables();
		_localizer = Localizer;

		Utility.Config = config;
	}

	public override void OnAllPluginsLoaded(bool hotReload)
	{
		try
		{
			MenuApi = MenuCapability.Get();

			if (Config.Additional.KnifeEnabled)
				SetupKnifeMenu();
			if (Config.Additional.SkinEnabled)
				RegisterWeaponCommands();
			if (Config.Additional.GloveEnabled)
				RegisterGloveCommands();
			if (Config.Additional.AgentEnabled)
				SetupAgentsMenu();
			if (Config.Additional.MusicEnabled)
				SetupMusicMenu();
			if (Config.Additional.PinsEnabled)
				SetupPinsMenu();

			SetupMenuNavigationButtons();

			RegisterCommands();
		}
		catch (Exception)
		{
			MenuApi = null;
			Logger.LogError("Error while loading required plugins");
			throw;
		}
	}
}
