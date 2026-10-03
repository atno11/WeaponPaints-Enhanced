using CounterStrikeSharp.API.Core;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private bool _mvpPlayed;

	private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
	{
		_gBCommandsAllowed = false;
		return HookResult.Continue;
	}

	private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
	{
		_gBCommandsAllowed = true;
		_mvpPlayed = false;
		return HookResult.Continue;
	}

	private HookResult OnRoundMvp(EventRoundMvp @event, GameEventInfo info)
	{
		if (_mvpPlayed)
			return HookResult.Continue;

		var player = @event.Userid;

		if (player == null || !player.IsValid || player.IsBot)
			return HookResult.Continue;

		if (
			!(
				GPlayersMusic.TryGetValue(player.Slot, out var musicInfo)
				&& musicInfo.TryGetValue(player.Team, out var musicId)
				&& musicId != 0
			)
		)
			return HookResult.Continue;

		@event.Musickitid = musicId;
		@event.Nomusic = 0;
		info.DontBroadcast = true;

		var newEvent = new EventRoundMvp(true)
		{
			Userid = player,
			Musickitid = musicId,
			Nomusic = 0,
		};

		_mvpPlayed = true;

		newEvent.FireEvent(false);
		return HookResult.Continue;
	}
}
