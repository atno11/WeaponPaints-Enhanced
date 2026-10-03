using Microsoft.Extensions.Logging;

namespace WeaponPaints;

internal static partial class Utility
{
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
}
