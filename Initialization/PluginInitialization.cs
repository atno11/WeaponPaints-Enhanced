using Microsoft.Extensions.Logging;

namespace WeaponPaints;

public partial class WeaponPaints
{
	public override void OnAllPluginsLoaded(bool hotReload)
	{
		try
		{
			MenuApi = MenuCapability.Get();

			if (Config.Additional.KnifeEnabled)
				RegisterKnifeCommands();

			if (Config.Additional.SkinEnabled)
				RegisterWeaponCommands();

			if (Config.Additional.GloveEnabled)
				RegisterGloveCommands();

			if (Config.Additional.AgentEnabled)
				RegisterAgentCommands();

			if (Config.Additional.MusicEnabled)
				SetupMusicMenu();

			if (Config.Additional.PinsEnabled)
				RegisterPinCommands();

			SetupMenuNavigationButtons();

			RegisterGeneralCommands();
			RegisterCustomizationCommands();
			RegisterStattrakCommands();
			RegisterRefreshCommands();
			RegisterSkinInfoCommands();
		}
		catch (Exception)
		{
			MenuApi = null;
			Logger.LogError("Error while loading required plugins");
			throw;
		}
	}
}
