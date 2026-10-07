using EFT.InputSystem;

namespace TacticalDeviceWheel.Input;

internal static class StrobeCommandRules
{
	public static bool Yields(ECommand command)
	{
		switch ((int)command - 36)
		{
		default:
			if ((int)command != 143)
			{
				break;
			}
			goto case 0;
		case 0:
		case 1:
		case 2:
		case 4:
		case 5:
		case 6:
		case 7:
		case 10:
		case 11:
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 23:
			return true;
		case 3:
		case 8:
		case 9:
		case 17:
		case 18:
		case 19:
		case 20:
		case 21:
		case 22:
			break;
		}
		return false;
	}
}
