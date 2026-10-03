using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Memory;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using Microsoft.Extensions.Logging;

namespace WeaponPaints
{
	public partial class WeaponPaints
	{
		private void OnMapStart(string mapName)
		{
			if (Config.Additional is { KnifeEnabled: false, SkinEnabled: false, GloveEnabled: false })
				return;

			if (Database != null)
				WeaponSync = new WeaponSynchronization(Database, Config);

			_fadeSeed = 0;
			_nextItemId = MinimumCustomItemId;
		}

		private void OnTick()
		{
			if (!Config.Additional.ShowSkinImage)
				return;

			foreach (var player in Players)
			{
				if (_playerWeaponImage.TryGetValue(player.Slot, out var value) && !string.IsNullOrEmpty(value))
				{
					player.PrintToCenterHtml("<img src='{PATH}'</img>".Replace("{PATH}", value));
				}
			}
		}

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
}
