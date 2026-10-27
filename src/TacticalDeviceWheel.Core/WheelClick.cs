namespace TacticalDeviceWheel.Core;

public static class WheelClick
{
	public static WheelClickAction Decide(bool tHeld, bool leftDown, bool rightDown, bool hasSelection, bool closeOnTRelease = true, bool selectOnRelease = false, bool clickedThisOpen = false)
	{
		if (rightDown)
		{
			return WheelClickAction.Cancel;
		}
		if (!tHeld && closeOnTRelease)
		{
			if (selectOnRelease && hasSelection && !clickedThisOpen && !leftDown)
			{
				return WheelClickAction.ConfirmAndClose;
			}
			return WheelClickAction.Cancel;
		}
		if (!(leftDown && hasSelection))
		{
			return WheelClickAction.None;
		}
		return WheelClickAction.Confirm;
	}
}
