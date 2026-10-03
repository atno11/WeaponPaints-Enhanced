using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace WeaponPaints;

public partial class WeaponPaints
{
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
	}
}
