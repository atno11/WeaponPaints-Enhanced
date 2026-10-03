using System.Collections.Concurrent;
using System.Globalization;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using MySqlConnector;

namespace WeaponPaints;

internal partial class WeaponSynchronization
{
	internal async Task GetPlayerData(PlayerInfo? player)
	{
		try
		{
			await using var connection = await _database.GetConnectionAsync();

			if (_config.Additional.KnifeEnabled)
				GetKnifeFromDatabase(player, connection);
			if (_config.Additional.GloveEnabled)
				GetGloveFromDatabase(player, connection);
			if (_config.Additional.AgentEnabled)
				GetAgentFromDatabase(player, connection);
			if (_config.Additional.MusicEnabled)
				GetMusicFromDatabase(player, connection);
			if (_config.Additional.SkinEnabled)
				GetWeaponPaintsFromDatabase(player, connection);
			if (_config.Additional.PinsEnabled)
				GetPinsFromDatabase(player, connection);
		}
		catch (Exception ex)
		{
			// Log the exception or handle it appropriately
			Console.WriteLine($"An error occurred: {ex.Message}");
		}
	}

	private void GetKnifeFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.KnifeEnabled || string.IsNullOrEmpty(player?.SteamId))
				return;

			var selectionVersionAtStart = WeaponPaints.GetKnifeSelectionVersion(player.Slot);

			const string query =
				"SELECT `knife`, `weapon_team` FROM `wp_player_knife` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var rows = connection.Query<dynamic>(query, new { steamid = player.SteamId }); // Retrieve all records for the player

			foreach (var row in rows)
			{
				if (WeaponPaints.GetKnifeSelectionVersion(player.Slot) != selectionVersionAtStart)
				{
					Utility.Log($"Ignoring stale knife database load for slot {player.Slot}");

					return;
				}

				// Check if knife is null or empty
				if (string.IsNullOrEmpty(row.knife))
					continue;

				// Determine the weapon team based on the query result
				CsTeam weaponTeam = (int)row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};

				// Get or create entries for the player’s slot
				var playerKnives = WeaponPaints.GPlayersKnife.GetOrAdd(player.Slot, _ => new ConcurrentDictionary<CsTeam, string>());

				if (weaponTeam == CsTeam.None)
				{
					// Assign knife to both teams if weaponTeam is None
					playerKnives[CsTeam.Terrorist] = row.knife;
					playerKnives[CsTeam.CounterTerrorist] = row.knife;
				}
				else
				{
					// Assign knife to the specific team
					playerKnives[weaponTeam] = row.knife;
				}
			}
		}
		catch (Exception ex)
		{
			Utility.Log($"An error occurred in GetKnifeFromDatabase: {ex.Message}");
		}
	}

	private void GetGloveFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.GloveEnabled || string.IsNullOrEmpty(player?.SteamId))
				return;

			var selectionVersionAtStart = WeaponPaints.GetGloveSelectionVersion(player.Slot);

			const string query =
				"SELECT `weapon_defindex`, `weapon_team` FROM `wp_player_gloves` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var rows = connection.Query<dynamic>(query, new { steamid = player.SteamId }); // Retrieve all records for the player

			foreach (var row in rows)
			{
				if (WeaponPaints.GetGloveSelectionVersion(player.Slot) != selectionVersionAtStart)
				{
					Utility.Log($"Ignoring stale glove database load for slot {player.Slot}");
					return;
				}

				// Check if weapon_defindex is null
				if (row.weapon_defindex == null)
					continue;
				// Determine the weapon team based on the query result
				var playerGloves = WeaponPaints.GPlayersGlove.GetOrAdd(player.Slot, _ => new ConcurrentDictionary<CsTeam, ushort>());
				CsTeam weaponTeam = (int)row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};

				// Get or create entries for the player’s slot

				if (weaponTeam == CsTeam.None)
				{
					// Assign glove ID to both teams if weaponTeam is None
					playerGloves[CsTeam.Terrorist] = (ushort)row.weapon_defindex;
					playerGloves[CsTeam.CounterTerrorist] = (ushort)row.weapon_defindex;
				}
				else
				{
					// Assign glove ID to the specific team
					playerGloves[weaponTeam] = (ushort)row.weapon_defindex;
				}
			}
		}
		catch (Exception ex)
		{
			Utility.Log($"An error occurred in GetGlovesFromDatabase: {ex.Message}");
		}
	}

	private void GetAgentFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.AgentEnabled || string.IsNullOrEmpty(player?.SteamId))
				return;

			const string query = "SELECT `agent_ct`, `agent_t` FROM `wp_player_agents` WHERE `steamid` = @steamid";
			var agentData = connection.QueryFirstOrDefault<(string, string)>(query, new { steamid = player.SteamId });

			if (agentData == default)
				return;
			var agentCT = agentData.Item1;
			var agentT = agentData.Item2;

			if (!string.IsNullOrEmpty(agentCT) || !string.IsNullOrEmpty(agentT))
			{
				WeaponPaints.GPlayersAgent[player.Slot] = (agentCT, agentT);
			}
		}
		catch (Exception ex)
		{
			Utility.Log($"An error occurred in GetAgentFromDatabase: {ex.Message}");
		}
	}

	private void GetWeaponPaintsFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.SkinEnabled || player == null || string.IsNullOrEmpty(player.SteamId))
				return;

			var knifeSelectionVersionAtStart = WeaponPaints.GetKnifeSelectionVersion(player.Slot);
			var gloveSelectionVersionAtStart = WeaponPaints.GetGloveSelectionVersion(player.Slot);
			var skinSelectionVersionsAtStart = WeaponPaints.GetSkinSelectionVersions(player.Slot);

			var playerWeapons = WeaponPaints.GPlayerWeaponsInfo.GetOrAdd(
				player.Slot,
				_ => new ConcurrentDictionary<CsTeam, ConcurrentDictionary<int, WeaponInfo>>()
			);

			// var weaponInfos = new ConcurrentDictionary<int, WeaponInfo>();

			const string query = "SELECT * FROM `wp_player_skins` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var playerSkins = connection.Query<dynamic>(query, new { steamid = player.SteamId });

			foreach (var row in playerSkins)
			{
				int weaponDefIndex = row.weapon_defindex ?? 0;
				int weaponPaintId = row.weapon_paint_id ?? 0;
				float weaponWear = row.weapon_wear ?? 0f;
				int weaponSeed = row.weapon_seed ?? 0;
				string weaponNameTag = row.weapon_nametag ?? "";
				bool weaponStatTrak = row.weapon_stattrak ?? false;
				int weaponStatTrakCount = row.weapon_stattrak_count ?? 0;

				if (
					WeaponPaints.IsKnifeDefindex(weaponDefIndex)
					&& WeaponPaints.GetKnifeSelectionVersion(player.Slot) != knifeSelectionVersionAtStart
				)
				{
					continue;
				}

				if (
					WeaponPaints.IsGloveDefindex(weaponDefIndex)
					&& WeaponPaints.GetGloveSelectionVersion(player.Slot) != gloveSelectionVersionAtStart
				)
				{
					continue;
				}

				if (
					!WeaponPaints.IsKnifeDefindex(weaponDefIndex)
					&& !WeaponPaints.IsGloveDefindex(weaponDefIndex)
					&& WeaponPaints.GetSkinSelectionVersion(player.Slot, weaponDefIndex)
						!= skinSelectionVersionsAtStart.GetValueOrDefault(weaponDefIndex)
				)
				{
					continue;
				}

				CsTeam weaponTeam = row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};

				string[]? keyChainParts = row.weapon_keychain?.ToString().Split(';');

				KeyChainInfo keyChainInfo = new KeyChainInfo();

				if (
					keyChainParts!.Length == 5
					&& uint.TryParse(keyChainParts[0], out uint keyChainId)
					&& float.TryParse(keyChainParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float keyChainOffsetX)
					&& float.TryParse(keyChainParts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float keyChainOffsetY)
					&& float.TryParse(keyChainParts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float keyChainOffsetZ)
					&& uint.TryParse(keyChainParts[4], out uint keyChainSeed)
				)
				{
					// Successfully parsed the values
					keyChainInfo.Id = keyChainId;
					keyChainInfo.OffsetX = keyChainOffsetX;
					keyChainInfo.OffsetY = keyChainOffsetY;
					keyChainInfo.OffsetZ = keyChainOffsetZ;
					keyChainInfo.Seed = keyChainSeed;
				}
				else
				{
					// Failed to parse the values, default to 0
					keyChainInfo.Id = 0;
					keyChainInfo.OffsetX = 0f;
					keyChainInfo.OffsetY = 0f;
					keyChainInfo.OffsetZ = 0f;
					keyChainInfo.Seed = 0;
				}

				// Create the WeaponInfo object
				WeaponInfo weaponInfo = new WeaponInfo
				{
					Paint = weaponPaintId,
					Seed = weaponSeed,
					Wear = weaponWear,
					Nametag = weaponNameTag,
					KeyChain = keyChainInfo,
					StatTrak = weaponStatTrak,
					StatTrakCount = weaponStatTrakCount,
				};

				// Retrieve and parse sticker data (up to 5 slots)
				for (int i = 0; i <= 4; i++)
				{
					// Access the sticker data dynamically using reflection
					string stickerColumn = $"weapon_sticker_{i}";
					var stickerData = ((IDictionary<string, object>)row!)[stickerColumn]; // Safely cast row to a dictionary

					if (string.IsNullOrEmpty(stickerData.ToString()))
						continue;

					var parts = stickerData.ToString()!.Split(';');

					//"id;schema;x;y;wear;scale;rotation"
					if (
						parts.Length != 7
						|| !uint.TryParse(parts[0], out uint stickerId)
						|| !uint.TryParse(parts[1], out uint stickerSchema)
						|| !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float stickerOffsetX)
						|| !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float stickerOffsetY)
						|| !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float stickerWear)
						|| !float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out float stickerScale)
						|| !float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float stickerRotation)
					)
						continue;

					StickerInfo stickerInfo = new StickerInfo
					{
						Id = stickerId,
						Schema = stickerSchema,
						OffsetX = stickerOffsetX,
						OffsetY = stickerOffsetY,
						Wear = stickerWear,
						Scale = stickerScale,
						Rotation = stickerRotation,
					};

					weaponInfo.Stickers.Add(stickerInfo);
				}

				if (weaponTeam == CsTeam.None)
				{
					// Get or create entries for both teams
					var terroristWeapons = playerWeapons.GetOrAdd(CsTeam.Terrorist, _ => new ConcurrentDictionary<int, WeaponInfo>());
					var counterTerroristWeapons = playerWeapons.GetOrAdd(
						CsTeam.CounterTerrorist,
						_ => new ConcurrentDictionary<int, WeaponInfo>()
					);

					// Add weaponInfo to both team weapon dictionaries
					terroristWeapons[weaponDefIndex] = weaponInfo;
					counterTerroristWeapons[weaponDefIndex] = weaponInfo;
				}
				else
				{
					// Add to the specific team
					var teamWeapons = playerWeapons.GetOrAdd(weaponTeam, _ => new ConcurrentDictionary<int, WeaponInfo>());
					teamWeapons[weaponDefIndex] = weaponInfo;
				}

				// weaponInfos[weaponDefIndex] = weaponInfo;
			}

			// WeaponPaints.GPlayerWeaponsInfo[player.Slot][weaponTeam] = weaponInfos;
		}
		catch (Exception ex)
		{
			Utility.Log($"An error occurred in GetWeaponPaintsFromDatabase: {ex.Message}");
		}
	}

	private void GetMusicFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (!_config.Additional.MusicEnabled || string.IsNullOrEmpty(player?.SteamId))
				return;

			var selectionVersionAtStart = WeaponPaints.GetMusicSelectionVersion(player.Slot);

			const string query =
				"SELECT `music_id`, `weapon_team` FROM `wp_player_music` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var rows = connection.Query<dynamic>(query, new { steamid = player.SteamId }); // Retrieve all records for the player

			foreach (var row in rows)
			{
				if (WeaponPaints.GetMusicSelectionVersion(player.Slot) != selectionVersionAtStart)
				{
					Utility.Log($"Ignoring stale music database load for slot {player.Slot}");
					return;
				}

				// Check if music_id is null
				if (row.music_id == null)
					continue;

				// Determine the weapon team based on the query result
				CsTeam weaponTeam = (int)row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};

				// Get or create entries for the player’s slot
				var playerMusic = WeaponPaints.GPlayersMusic.GetOrAdd(player.Slot, _ => new ConcurrentDictionary<CsTeam, ushort>());

				if (weaponTeam == CsTeam.None)
				{
					// Assign music ID to both teams if weaponTeam is None
					playerMusic[CsTeam.Terrorist] = (ushort)row.music_id;
					playerMusic[CsTeam.CounterTerrorist] = (ushort)row.music_id;
				}
				else
				{
					// Assign music ID to the specific team
					playerMusic[weaponTeam] = (ushort)row.music_id;
				}
			}
		}
		catch (Exception ex)
		{
			Utility.Log($"An error occurred in GetMusicFromDatabase: {ex.Message}");
		}
	}

	private void GetPinsFromDatabase(PlayerInfo? player, MySqlConnection connection)
	{
		try
		{
			if (string.IsNullOrEmpty(player?.SteamId))
				return;

			const string query = "SELECT `id`, `weapon_team` FROM `wp_player_pins` WHERE `steamid` = @steamid ORDER BY `weapon_team` ASC";
			var rows = connection.Query<dynamic>(query, new { steamid = player.SteamId }); // Retrieve all records for the player

			foreach (var row in rows)
			{
				// Check if id is null
				if (row.id == null)
					continue;

				// Determine the weapon team based on the query result
				CsTeam weaponTeam = (int)row.weapon_team switch
				{
					2 => CsTeam.Terrorist,
					3 => CsTeam.CounterTerrorist,
					_ => CsTeam.None,
				};

				// Get or create entries for the player’s slot
				var playerPins = WeaponPaints.GPlayersPin.GetOrAdd(player.Slot, _ => new ConcurrentDictionary<CsTeam, ushort>());

				if (weaponTeam == CsTeam.None)
				{
					// Assign pin ID to both teams if weaponTeam is None
					playerPins[CsTeam.Terrorist] = (ushort)row.id;
					playerPins[CsTeam.CounterTerrorist] = (ushort)row.id;
				}
				else
				{
					// Assign pin ID to the specific team
					playerPins[weaponTeam] = (ushort)row.id;
				}
			}
		}
		catch (Exception ex)
		{
			Utility.Log($"An error occurred in GetPinsFromDatabase: {ex.Message}");
		}
	}
}
