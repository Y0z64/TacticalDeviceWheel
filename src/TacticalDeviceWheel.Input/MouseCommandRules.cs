using EFT.InputSystem;

namespace TacticalDeviceWheel.Input;

internal static class MouseCommandRules
{
	public static bool ShouldBlock(ECommand command, bool wheelOpen, bool leftOwned, bool rightOwned)
	{
		switch ((int)command - 1)
		{
		default:
			if ((int)command == 58)
			{
				return rightOwned;
			}
			return false;
		case 0:
			return wheelOpen || leftOwned;
		case 1:
			return leftOwned;
		case 2:
			return wheelOpen || rightOwned;
		}
	}
}
