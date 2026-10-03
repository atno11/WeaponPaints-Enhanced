using Dapper;

namespace WeaponPaints;

internal partial class WeaponSynchronization
{
	internal async Task DeleteGloveFromDatabase(PlayerInfo player, int[] gloveDefindexes)
	{
		if (!_config.Additional.GloveEnabled || string.IsNullOrEmpty(player.SteamId))
			return;

		const string deleteGlovesQuery = "DELETE FROM `wp_player_gloves` WHERE `steamid` = @steamid";
		const string deleteSkinsQuery =
			"DELETE FROM `wp_player_skins` WHERE `steamid` = @steamid AND `weapon_defindex` IN @weaponDefindexes";

		try
		{
			await using var connection = await _database.GetConnectionAsync();

			await connection.ExecuteAsync(deleteGlovesQuery, new { steamid = player.SteamId });

			if (gloveDefindexes.Length > 0)
			{
				await connection.ExecuteAsync(deleteSkinsQuery, new { steamid = player.SteamId, weaponDefindexes = gloveDefindexes });
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error deleting glove override from database: {e.Message}");
		}
	}

	internal async Task DeleteKnifeFromDatabase(PlayerInfo player, int[] knifeDefindexes)
	{
		if (!_config.Additional.KnifeEnabled || string.IsNullOrEmpty(player.SteamId))
			return;

		const string deleteKnifeQuery = "DELETE FROM `wp_player_knife` WHERE `steamid` = @steamid";
		const string deleteSkinsQuery =
			"DELETE FROM `wp_player_skins` WHERE `steamid` = @steamid AND `weapon_defindex` IN @weaponDefindexes";

		try
		{
			await using var connection = await _database.GetConnectionAsync();

			await connection.ExecuteAsync(deleteKnifeQuery, new { steamid = player.SteamId });

			if (knifeDefindexes.Length > 0)
			{
				await connection.ExecuteAsync(deleteSkinsQuery, new { steamid = player.SteamId, weaponDefindexes = knifeDefindexes });
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error deleting knife override from database: {e.Message}");
		}
	}

	internal async Task DeleteWeaponPaintFromDatabase(PlayerInfo player, int weaponDefIndex)
	{
		if (string.IsNullOrEmpty(player.SteamId))
			return;

		const string query = "DELETE FROM `wp_player_skins` WHERE `steamid` = @steamid AND `weapon_defindex` = @weaponDefIndex";

		try
		{
			await using var connection = await _database.GetConnectionAsync();
			await connection.ExecuteAsync(query, new { steamid = player.SteamId, weaponDefIndex });
		}
		catch (Exception e)
		{
			Utility.Log($"Error deleting weapon paint override from database: {e.Message}");
		}
	}

	internal async Task DeleteMusicFromDatabase(PlayerInfo player)
	{
		if (!_config.Additional.MusicEnabled || string.IsNullOrEmpty(player.SteamId))
			return;

		const string query = "DELETE FROM `wp_player_music` WHERE `steamid` = @steamid";

		try
		{
			await using var connection = await _database.GetConnectionAsync();
			await connection.ExecuteAsync(query, new { steamid = player.SteamId });
		}
		catch (Exception e)
		{
			Utility.Log($"Error deleting music kit override from database: {e.Message}");
		}
	}
}
