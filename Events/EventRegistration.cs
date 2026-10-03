using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private void RegisterListeners()
	{
		RegisterListener<Listeners.OnMapStart>(OnMapStart);

		RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
		RegisterEventHandler<EventRoundStart>(OnRoundStart);
		RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
		RegisterEventHandler<EventRoundMvp>(OnRoundMvp);
		RegisterListener<Listeners.OnEntitySpawned>(OnEntityCreated);
		RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);

		if (Config.Additional.ShowSkinImage)
			RegisterListener<Listeners.OnTick>(OnTick);

		VirtualFunctions.GiveNamedItemFunc.Hook(OnGiveNamedItemPost, HookMode.Post);
	}
}
