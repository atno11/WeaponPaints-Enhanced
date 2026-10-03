using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WeaponPaints;

internal static partial class Utility
{
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
}
