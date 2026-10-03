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
}
