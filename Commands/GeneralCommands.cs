namespace WeaponPaints;

public partial class WeaponPaints
{
	private void RegisterGeneralCommands()
	{
		if (Config.Additional.CommandKillEnabled)
		{
			_config.Additional.CommandKill.ForEach(c =>
			{
				AddCommand(
					$"css_{c}",
					"kill yourself",
					(player, _) =>
					{
						if (
							player == null
							|| !Utility.IsPlayerValid(player)
							|| player.PlayerPawn.Value == null
							|| !player.PlayerPawn.IsValid
						)
							return;

						player.PlayerPawn.Value.CommitSuicide(true, false);
					}
				);
			});
		}
	}
}
