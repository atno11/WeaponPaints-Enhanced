using System.Collections.Concurrent;
using System.Globalization;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using MySqlConnector;

namespace WeaponPaints;

internal partial class WeaponSynchronization
{
	private readonly WeaponPaintsConfig _config;
	private readonly Database _database;

	internal WeaponSynchronization(Database database, WeaponPaintsConfig config)
	{
		_database = database;
		_config = config;
	}

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

	internal async Task SyncWeaponPaintToDatabase(PlayerInfo player, int weaponDefIndex, CsTeam[] teams)
	{
		if (
			string.IsNullOrEmpty(player.SteamId)
			|| teams.Length == 0
			|| !WeaponPaints.GPlayerWeaponsInfo.TryGetValue(player.Slot, out var teamWeaponInfos)
		)
		{
			return;
		}

		const string query =
			@"
        INSERT INTO `wp_player_skins`
            (`steamid`, `weapon_defindex`, `weapon_team`, `weapon_paint_id`, `weapon_wear`, `weapon_seed`)
        VALUES
            (@steamid, @weaponDefIndex, @weaponTeam, @paintId, @wear, @seed)
        ON DUPLICATE KEY UPDATE
            `weapon_paint_id` = @paintId,
            `weapon_wear` = @wear,
            `weapon_seed` = @seed";

		try
		{
			await using var connection = await _database.GetConnectionAsync();

			foreach (var team in teams)
			{
				if (!teamWeaponInfos.TryGetValue(team, out var weaponsInfo) || !weaponsInfo.TryGetValue(weaponDefIndex, out var weaponInfo))
					continue;

				await connection.ExecuteAsync(
					query,
					new
					{
						steamid = player.SteamId,
						weaponDefIndex,
						weaponTeam = (int)team,
						paintId = weaponInfo.Paint,
						wear = weaponInfo.Wear,
						seed = weaponInfo.Seed,
					}
				);
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing weapon paint to database: {e.Message}");
		}
	}

	internal async Task SyncWeaponPaintsToDatabase(PlayerInfo player)
	{
		if (string.IsNullOrEmpty(player.SteamId) || !WeaponPaints.GPlayerWeaponsInfo.TryGetValue(player.Slot, out var teamWeaponInfos))
			return;

		try
		{
			await using var connection = await _database.GetConnectionAsync();

			// Loop through each team (Terrorist and CounterTerrorist)
			foreach (var (teamId, weaponsInfo) in teamWeaponInfos)
			{
				foreach (var (weaponDefIndex, weaponInfo) in weaponsInfo)
				{
					var paintId = weaponInfo.Paint;
					var wear = weaponInfo.Wear;
					var seed = weaponInfo.Seed;

					// Prepare the queries to check and update/insert weapon skin data
					const string queryCheckExistence =
						"SELECT COUNT(*) FROM `wp_player_skins` WHERE `steamid` = @steamid AND `weapon_defindex` = @weaponDefIndex AND `weapon_team` = @weaponTeam";

					var existingRecordCount = await connection.ExecuteScalarAsync<int>(
						queryCheckExistence,
						new
						{
							steamid = player.SteamId,
							weaponDefIndex,
							weaponTeam = teamId,
						}
					);

					string query;
					object parameters;

					if (existingRecordCount > 0)
					{
						// Update existing record
						query =
							"UPDATE `wp_player_skins` SET `weapon_paint_id` = @paintId, `weapon_wear` = @wear, `weapon_seed` = @seed "
							+ "WHERE `steamid` = @steamid AND `weapon_defindex` = @weaponDefIndex AND `weapon_team` = @weaponTeam";
						parameters = new
						{
							steamid = player.SteamId,
							weaponDefIndex,
							weaponTeam = (int)teamId,
							paintId,
							wear,
							seed,
						};
					}
					else
					{
						// Insert new record
						query =
							"INSERT INTO `wp_player_skins` (`steamid`, `weapon_defindex`, `weapon_team`, `weapon_paint_id`, `weapon_wear`, `weapon_seed`) "
							+ "VALUES (@steamid, @weaponDefIndex, @weaponTeam, @paintId, @wear, @seed)";
						parameters = new
						{
							steamid = player.SteamId,
							weaponDefIndex,
							weaponTeam = (int)teamId,
							paintId,
							wear,
							seed,
						};
					}

					await connection.ExecuteAsync(query, parameters);
				}
			}
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing weapon paints to database: {e.Message}");
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

	internal async Task SyncStatTrakToDatabase(PlayerInfo player)
	{
		if (WeaponPaints.WeaponSync == null || WeaponPaints.GPlayerWeaponsInfo.IsEmpty)
			return;
		if (string.IsNullOrEmpty(player.SteamId))
			return;

		try
		{
			await using var connection = await _database.GetConnectionAsync();
			await using var transaction = await connection.BeginTransactionAsync();

			// Check if player's slot exists in GPlayerWeaponsInfo
			if (!WeaponPaints.GPlayerWeaponsInfo.TryGetValue(player.Slot, out var teamWeaponsInfo))
				return;

			// Iterate through each team in the player's weapon info
			foreach (var teamInfo in teamWeaponsInfo)
			{
				// Retrieve weaponInfos for the current team
				var weaponInfos = teamInfo.Value;

				// Get StatTrak weapons for the current team
				var statTrakWeapons = weaponInfos.ToDictionary(
					w => w.Key,
					w => (w.Value.StatTrak, w.Value.StatTrakCount) // Store both StatTrak and StatTrakCount in a tuple
				);

				// Check if there are StatTrak weapons to sync
				if (statTrakWeapons.Count == 0)
					continue;

				// Get the current team ID
				int weaponTeam = (int)teamInfo.Key;

				// Sync StatTrak values for the current team
				foreach (var (defindex, (statTrak, statTrakCount)) in statTrakWeapons)
				{
					const string query =
						@"
					    UPDATE `wp_player_skins`
					    SET `weapon_stattrak` = @StatTrak,
					        `weapon_stattrak_count` = @StatTrakCount
					    WHERE `steamid` = @steamid
					      AND `weapon_defindex` = @weaponDefIndex
					      AND `weapon_team` = @weaponTeam";

					var parameters = new
					{
						steamid = player.SteamId,
						weaponDefIndex = defindex,
						StatTrak = statTrak,
						StatTrakCount = statTrakCount,
						weaponTeam,
					};

					await connection.ExecuteAsync(query, parameters, transaction);
				}
			}

			await transaction.CommitAsync();
		}
		catch (Exception e)
		{
			Utility.Log($"Error syncing stattrak to database: {e.Message}");
		}
	}
}
