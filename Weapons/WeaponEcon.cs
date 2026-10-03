using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;

namespace WeaponPaints;

public partial class WeaponPaints
{
	private static readonly MemoryFunctionVoid<nint, string, float> CAttributeListSetOrAddAttributeValueByName = new(
		GameData.GetSignature("CAttributeList_SetOrAddAttributeValueByName")
	);
}
