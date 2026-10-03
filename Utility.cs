using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Menu;
using Dapper;
using MenuManager;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WeaponPaints
{
	internal static partial class Utility
	{
		internal static WeaponPaintsConfig? Config { get; set; }

		internal static bool IsPlayerValid(CCSPlayerController? player)
		{
			if (player is null || WeaponPaints.WeaponSync is null)
				return false;

			return player is { IsValid: true, IsBot: false, IsHLTV: false, UserId: not null };
		}

		private static List<JObject> LoadCatalogFile(string filePath, string catalogName, ILogger logger)
		{
			try
			{
				if (!File.Exists(filePath))
					return [];

				var json = File.ReadAllText(filePath);

				return JsonConvert.DeserializeObject<List<JObject>>(json) ?? [];
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to load {CatalogName} catalog from {FilePath}", catalogName, filePath);

				return [];
			}
		}

		private static List<JObject> LoadLocalizedCatalog(
			string dataDirectory,
			string filePrefix,
			string language,
			string catalogName,
			ILogger logger
		)
		{
			var englishPath = Path.Combine(dataDirectory, $"{filePrefix}_en.json");

			if (string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
			{
				var englishCatalog = LoadCatalogFile(englishPath, catalogName, logger);

				if (englishCatalog.Count == 0)
				{
					logger.LogError("English {CatalogName} catalog is missing or empty: {FilePath}", catalogName, englishPath);
				}

				return englishCatalog;
			}

			var localizedPath = Path.Combine(dataDirectory, $"{filePrefix}_{language}.json");

			var englishFallback = LoadCatalogFile(englishPath, catalogName, logger);
			var localizedCatalog = LoadCatalogFile(localizedPath, catalogName, logger);

			if (localizedCatalog.Count == 0)
			{
				logger.LogWarning(
					"{CatalogName} catalog for language {Language} is missing or empty. Using English fallback ({EnglishCount} entries).",
					catalogName,
					language,
					englishFallback.Count
				);

				return englishFallback;
			}

			if (englishFallback.Count > 0 && localizedCatalog.Count < englishFallback.Count)
			{
				logger.LogWarning(
					"{CatalogName} catalog for language {Language} appears incomplete ({LocalizedCount}/{EnglishCount}). Using English fallback.",
					catalogName,
					language,
					localizedCatalog.Count,
					englishFallback.Count
				);

				return englishFallback;
			}

			return localizedCatalog;
		}

		internal static void LoadLocalizedCatalogs(string dataDirectory, string language, ILogger logger)
		{
			WeaponPaints.SkinsList = LoadLocalizedCatalog(dataDirectory, "skins", language, "skins", logger);

			WeaponPaints.GlovesList = LoadLocalizedCatalog(dataDirectory, "gloves", language, "gloves", logger);

			WeaponPaints.AgentsList = LoadLocalizedCatalog(dataDirectory, "agents", language, "agents", logger);

			WeaponPaints.MusicList = LoadLocalizedCatalog(dataDirectory, "music", language, "music", logger);

			WeaponPaints.PinsList = LoadLocalizedCatalog(dataDirectory, "collectibles", language, "collectibles", logger);
		}

		internal static void Log(string message)
		{
			Console.BackgroundColor = ConsoleColor.DarkGray;
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.WriteLine("[WeaponPaints] " + message);
			Console.ResetColor();
		}

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

		internal static async Task CheckVersion(string version, ILogger logger)
		{
			using HttpClient client = new();

			try
			{
				var response = await client
					.GetAsync("https://raw.githubusercontent.com/Nereziel/cs2-WeaponPaints/main/VERSION")
					.ConfigureAwait(false);

				if (response.IsSuccessStatusCode)
				{
					var remoteVersion = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
					remoteVersion = remoteVersion.Trim();

					var comparisonResult = string.CompareOrdinal(version, remoteVersion);

					switch (comparisonResult)
					{
						case < 0:
							logger.LogWarning("Plugin is outdated! Check https://github.com/Nereziel/cs2-WeaponPaints");
							break;
						case > 0:
							logger.LogInformation("Probably dev version detected");
							break;
						default:
							logger.LogInformation("Plugin is up to date");
							break;
					}
				}
				else
				{
					logger.LogWarning("Failed to check version");
				}
			}
			catch (HttpRequestException ex)
			{
				logger.LogError(ex, "Failed to connect to the version server.");
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "An error occurred while checking version.");
			}
		}

		internal static void ShowAd(string moduleVersion)
		{
			Console.WriteLine(" ");
			Console.WriteLine(" _     _  _______  _______  _______  _______  __    _  _______  _______  ___   __    _  _______  _______ ");
			Console.WriteLine("| | _ | ||       ||   _   ||       ||       ||  |  | ||       ||   _   ||   | |  |  | ||       ||       |");
			Console.WriteLine("| || || ||    ___||  |_|  ||    _  ||   _   ||   |_| ||    _  ||  |_|  ||   | |   |_| ||_     _||  _____|");
			Console.WriteLine("|       ||   |___ |       ||   |_| ||  | |  ||       ||   |_| ||       ||   | |       |  |   |  | |_____ ");
			Console.WriteLine("|       ||    ___||       ||    ___||  |_|  ||  _    ||    ___||       ||   | |  _    |  |   |  |_____  |");
			Console.WriteLine("|   _   ||   |___ |   _   ||   |    |       || | |   ||   |    |   _   ||   | | | |   |  |   |   _____| |");
			Console.WriteLine("|__| |__||_______||__| |__||___|    |_______||_|  |__||___|    |__| |__||___| |_|  |__|  |___|  |_______|");
			Console.WriteLine("						>> Version: " + moduleVersion);
			Console.WriteLine("			>> GitHub: https://github.com/Nereziel/cs2-WeaponPaints");
			Console.WriteLine(" ");
		}
	}
}
