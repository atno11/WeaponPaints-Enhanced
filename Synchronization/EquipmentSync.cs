using CounterStrikeSharp.API.Modules.Utils;
using Dapper;

namespace WeaponPaints;

internal partial class WeaponSynchronization
{
	internal async Task SyncKnifeToDatabase(PlayerInfo player, string knife, CsTeam[] teams)
	{
		if (!_config.Additional.KnifeEnabled || string.IsNullOrEmpty(player.SteamId) || string.IsNullOrEmpty(knife) || teams.Length == 0)
			return;

		const string query =
			"INSERT INTO `wp_player_knife` (`steamid`, `weapon_team`, `knife`) VALUES(@steamid, @team, @newKnife) ON DUPLICATE KEY UPDATE `knife` = @newKnife";

		try
		{
			await using var connection = await _database.GetConnectionAsync();

			// Loop through each team and insert/update accordingly
			foreach (var team in teams)
			{
				await connection.ExecuteAsync(
					query,
					new
					{
						steamid = player.SteamId,
						team = (int)team,
						newKnife = knife,
					}
				);
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing knife to database: {e.Message}");
		}
	}

	internal async Task SyncGloveToDatabase(PlayerInfo player, ushort gloveDefIndex, CsTeam[] teams)
	{
		// Check if the necessary conditions are met
		if (!_config.Additional.GloveEnabled || string.IsNullOrEmpty(player.SteamId) || teams.Length == 0)
			return;

		const string query =
			@"
        INSERT INTO `wp_player_gloves` (`steamid`, `weapon_team`, `weapon_defindex`)
        VALUES(@steamid, @team, @gloveDefIndex)
        ON DUPLICATE KEY UPDATE `weapon_defindex` = @gloveDefIndex";

		try
		{
			// Get a database connection
			await using var connection = await _database.GetConnectionAsync();

			// Loop through each team and insert/update accordingly
			foreach (var team in teams)
			{
				// Execute the SQL command for each team
				await connection.ExecuteAsync(
					query,
					new
					{
						steamid = player.SteamId,
						team = (int)team, // Cast the CsTeam enum to int for insertion
						gloveDefIndex,
					}
				);
			}
		}
		catch (Exception e)
		{
			// Log any exceptions that occur
			Utility.Log($"Error syncing glove to database: {e.Message}");
		}
	}

	internal async Task SyncAgentToDatabase(PlayerInfo player)
	{
		if (!_config.Additional.AgentEnabled || string.IsNullOrEmpty(player.SteamId))
			return;

		const string query = """
								INSERT INTO `wp_player_agents` (`steamid`, `agent_ct`, `agent_t`)
								VALUES(@steamid, @agent_ct, @agent_t)
								ON DUPLICATE KEY UPDATE
									`agent_ct` = @agent_ct,
									`agent_t` = @agent_t
			""";
		try
		{
			await using var connection = await _database.GetConnectionAsync();

			await connection.ExecuteAsync(
				query,
				new
				{
					steamid = player.SteamId,
					agent_ct = WeaponPaints.GPlayersAgent[player.Slot].CT,
					agent_t = WeaponPaints.GPlayersAgent[player.Slot].T,
				}
			);
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing agents to database: {e.Message}");
		}
	}

	internal async Task SyncMusicToDatabase(PlayerInfo player, ushort music, CsTeam[] teams)
	{
		if (!_config.Additional.MusicEnabled || string.IsNullOrEmpty(player.SteamId))
			return;

		const string query =
			"INSERT INTO `wp_player_music` (`steamid`, `weapon_team`, `music_id`) VALUES(@steamid, @team, @newMusic) ON DUPLICATE KEY UPDATE `music_id` = @newMusic";

		try
		{
			await using var connection = await _database.GetConnectionAsync();

			// Loop through each team and insert/update accordingly
			foreach (var team in teams)
			{
				await connection.ExecuteAsync(
					query,
					new
					{
						steamid = player.SteamId,
						team,
						newMusic = music,
					}
				);
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing music kit to database: {e.Message}");
		}
	}

	internal async Task SyncPinToDatabase(PlayerInfo player, ushort pin, CsTeam[] teams)
	{
		if (!_config.Additional.PinsEnabled || string.IsNullOrEmpty(player.SteamId))
			return;

		const string query =
			"INSERT INTO `wp_player_pins` (`steamid`, `weapon_team`, `id`) VALUES(@steamid, @team, @newPin) ON DUPLICATE KEY UPDATE `id` = @newPin";

		try
		{
			await using var connection = await _database.GetConnectionAsync();

			// Loop through each team and insert/update accordingly
			foreach (var team in teams)
			{
				await connection.ExecuteAsync(
					query,
					new
					{
						steamid = player.SteamId,
						team,
						newPin = pin,
					}
				);
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing pin to database: {e.Message}");
		}
	}
}
