using FirearmController = EFT.Player.FirearmController;
using System;
using EFT;
using EFT.InputSystem;
using TacticalDeviceWheel.Compatibility;
using TacticalDeviceWheel.Core;
using UnityEngine;

namespace TacticalDeviceWheel.Input;

internal sealed class InputController
{
	private readonly Plugin plugin;

	private readonly Gesture gesture = new Gesture();

	private readonly MouseCapture leftMouse = new MouseCapture();

	private readonly MouseCapture rightMouse = new MouseCapture();

	private GamePlayerOwner observed;

	private GamePlayerOwner owner;

	private GamePlayerOwner mouseOwner;

	private FirearmController firearm;

	private int observedFrame = -100;

	private int releaseFrame = -100;

	private bool replaying;

	private bool draining;

	public bool WheelOpen
	{
		get
		{
			if (gesture.Opened)
			{
				return plugin.Menu.IsOpen;
			}
			return false;
		}
	}

	public static bool ModifiersHeld
	{
		get
		{
			if (!UnityEngine.Input.GetKey((KeyCode)306) && !UnityEngine.Input.GetKey((KeyCode)305) && !UnityEngine.Input.GetKey((KeyCode)308) && !UnityEngine.Input.GetKey((KeyCode)307) && !UnityEngine.Input.GetKey((KeyCode)304))
			{
				return UnityEngine.Input.GetKey((KeyCode)303);
			}
			return true;
		}
	}

	public InputController(Plugin plugin)
	{
		this.plugin = plugin;
	}

	private static readonly System.Collections.Generic.HashSet<int> diagCommands = new System.Collections.Generic.HashSet<int>();
	private static string lastValidDiag;

	private bool Valid(GamePlayerOwner candidate)
	{
		bool c1 = (object)candidate != null;
		PlayerOwner po = (c1 ? (PlayerOwner)candidate : null);
		bool c2 = c1 && (object)po.Player != null;
		Player pl = (c2 ? po.Player : null);
		bool c3 = c2 && pl.IsYourPlayer;
		bool c4 = c2 && (int)po.State == 1;
		bool c5 = c2 && pl.HealthController != null && pl.HealthController.IsAlive;
		bool c6 = c2 && !pl.IsInventoryOpened;
		bool c7 = c2 && pl.HandsController is FirearmController;
		bool c8 = Application.isFocused;
		bool c9 = (int)Cursor.lockState == 1 && !Cursor.visible;
		bool ok = c1 && c2 && c3 && c4 && c5 && c6 && c7 && c8 && c9;
		// 0.4.1: 4.0.13 这里调用的是 PlayerOwner.method_13（只校验、无副作用）；
		// 4.1.5 对应名是静态的 TranslatePlayerInput，它会真正执行命令，不能当校验用，故移除。
		if (!ok && plugin.Options.DebugLogging.Value)
		{
			string diag = "c1=" + c1 + " c2=" + c2 + " yours=" + c3 + " state=" + c4 + " hp=" + c5 + " inv=" + c6 + " hands=" + c7 + " focused=" + c8 + " cursor=" + c9;
			if (diag != lastValidDiag)
			{
				lastValidDiag = diag;
				plugin.Debug("[DIAG] Valid=false -> " + diag);
			}
		}
		return ok;
	}

	public void ObserveAxes(PlayerOwner candidate, float[] axes)
	{
		if (!plugin.Options.Enabled.Value || !NativeSettings.IsToggle)
		{
			return;
		}
		GamePlayerOwner val = (GamePlayerOwner)(object)((candidate is GamePlayerOwner) ? candidate : null);
		if (val == null || !Valid(val))
		{
			return;
		}
		observed = val;
		observedFrame = Time.frameCount;
		if (WheelOpen && (object)owner == (object)val && axes != null)
		{
			for (int i = 2; i <= 5 && i < axes.Length; i++)
			{
				axes[i] = 0f;
			}
		}
	}

	private void Begin(GamePlayerOwner candidate)
	{
		if (plugin.Options.DebugLogging.Value)
		{
			plugin.Debug("[DIAG] Begin gate: active=" + gesture.Active + " draining=" + draining + " valid=" + Valid(candidate) + " modifiers=" + ModifiersHeld + " plainT=" + NativeSettings.HasPlainTBinding());
		}
		if (!gesture.Active && !draining && Valid(candidate) && !ModifiersHeld && NativeSettings.HasPlainTBinding())
		{
			owner = candidate;
			firearm = (FirearmController)((PlayerOwner)owner).Player.HandsController;
			gesture.Begin(Time.realtimeSinceStartupAsDouble, plugin.Options.HoldThreshold.Value);
		}
	}

	private void PollMouseCapture()
	{
		if ((object)mouseOwner == null)
		{
			return;
		}
		if (WheelOpen)
		{
			if (UnityEngine.Input.GetMouseButtonDown(0))
			{
				leftMouse.Capture();
			}
			if (UnityEngine.Input.GetMouseButtonDown(1))
			{
				rightMouse.Capture();
			}
		}
		leftMouse.Observe(UnityEngine.Input.GetMouseButton(0), Application.isFocused, Time.frameCount);
		rightMouse.Observe(UnityEngine.Input.GetMouseButton(1), Application.isFocused, Time.frameCount);
		if (!WheelOpen && !leftMouse.Blocks(Time.frameCount) && !rightMouse.Blocks(Time.frameCount))
		{
			mouseOwner = null;
		}
	}

	public bool BeforeCommand(GamePlayerOwner candidate, ECommand command)
	{
		if (plugin.Options.DebugLogging.Value && diagCommands.Add((int)command))
		{
			plugin.Debug("[DIAG] command seen: " + (int)command + " (" + command.ToString() + ")");
		}
		bool num = ProcessCommand(candidate, command);
		if (num && (object)((candidate != null) ? ((PlayerOwner)candidate).Player : null) != null && ((PlayerOwner)candidate).Player.IsYourPlayer && StrobeCommandRules.Yields(command))
		{
			plugin.Strobes.YieldToNative(command.ToString());
		}
		return num;
	}

	private bool ProcessCommand(GamePlayerOwner candidate, ECommand command)
	{
		if (replaying)
		{
			return true;
		}
		if (!plugin.Options.Enabled.Value || !NativeSettings.IsToggle)
		{
			Cancel(suppressUntilRelease: false);
			ResetMouse();
			return true;
		}
		PollMouseCapture();
		if ((object)candidate != null && (object)candidate == (object)mouseOwner && MouseCommandRules.ShouldBlock(command, WheelOpen, leftMouse.Blocks(Time.frameCount), rightMouse.Blocks(Time.frameCount)))
		{
			plugin.Debug("Mouse command consumed: " + command.ToString());
			return false;
		}
		if ((int)command != 38)
		{
			if (gesture.Active && ((int)command == 143 || (int)command == 59))
			{
				Cancel(suppressUntilRelease: true);
			}
			return true;
		}
		if ((object)candidate != null && ((object)owner == (object)candidate || (object)owner == null) && (draining || releaseFrame == Time.frameCount))
		{
			return false;
		}
		if (!Valid(candidate) || ModifiersHeld)
		{
			return true;
		}
		observed = candidate;
		observedFrame = Time.frameCount;
		if (!gesture.Active && UnityEngine.Input.GetKey((KeyCode)116))
		{
			Begin(candidate);
		}
		if (gesture.Active)
		{
			return !((object)owner == (object)candidate);
		}
		return true;
	}

	private void Replay(GamePlayerOwner candidate, ECommand command)
	{
		replaying = true;
		try
		{
			((InputNode)candidate).TranslateCommand(command);
		}
		finally
		{
			replaying = false;
		}
	}

	public void Update()
	{
		if (!plugin.Options.Enabled.Value || !NativeSettings.IsToggle)
		{
			Cancel(suppressUntilRelease: false);
			ResetMouse();
			return;
		}
		PollMouseCapture();
		bool key = UnityEngine.Input.GetKey((KeyCode)116);
		if (draining)
		{
			if (Application.isFocused && !key)
			{
				draining = false;
				releaseFrame = Time.frameCount;
				owner = null;
				firearm = null;
			}
			return;
		}
		if (!gesture.Active && UnityEngine.Input.GetKeyDown((KeyCode)116) && Time.frameCount - observedFrame <= 1)
		{
			Begin(observed);
		}
		if (!gesture.Active)
		{
			return;
		}
		if (!Valid(owner) || Time.frameCount - observedFrame > 2 || ((PlayerOwner)owner).Player.HandsController != firearm || ModifiersHeld || UnityEngine.Input.GetKeyDown((KeyCode)27))
		{
			Cancel(suppressUntilRelease: true);
			return;
		}
		if (WheelOpen)
		{
			plugin.Menu.Move(UnityEngine.Input.GetAxisRaw("Mouse X"), UnityEngine.Input.GetAxisRaw("Mouse Y"));
			switch (WheelClick.Decide(key, UnityEngine.Input.GetMouseButtonDown(0), UnityEngine.Input.GetMouseButtonDown(1), plugin.Menu.HasSelection, plugin.Options.CloseOnTRelease.Value))
			{
			case WheelClickAction.Confirm:
			{
				bool flag;
				try
				{
					flag = plugin.Menu.Commit(((PlayerOwner)owner).Player, firearm);
				}
				catch (Exception e)
				{
					// Leave the menu open: the state it was in before the click is still what the
					// player sees, so a second click can try again.
					plugin.TransientFault("InputController.Commit", e);
					return;
				}
				plugin.Debug("TDW LMB TOGGLE applied=" + flag + "; menu remains open");
				return;
			}
			default:
				plugin.Debug(UnityEngine.Input.GetMouseButtonDown(1) ? "TDW RMB CANCEL" : "TDW T RELEASE: close without action");
				Cancel(suppressUntilRelease: true);
				return;
			case WheelClickAction.None:
				break;
			}
		}
		switch (gesture.Step(Time.realtimeSinceStartupAsDouble, key, plugin.Options.CloseOnTRelease.Value))
		{
		case GestureResult.Open:
			try
			{
				plugin.Menu.Open(((PlayerOwner)owner).Player, firearm);
				if (owner != null)
				{
					mouseOwner = owner;
				}
				Replay(owner, (ECommand)2);
				if (UnityEngine.Input.GetMouseButton(0))
				{
					leftMouse.Capture();
				}
				PollMouseCapture();
				plugin.Debug("TDW OPEN");
			}
			catch (Exception e)
			{
				// The gesture has already advanced to its opened state, so leaving it there would
				// strand the state machine: the wheel would never open again. Reset it instead.
				plugin.TransientFault("InputController.Open", e);
				Cancel(suppressUntilRelease: false);
			}
			break;
		case GestureResult.ShortPress:
			releaseFrame = Time.frameCount;
			try
			{
				Replay(owner, (ECommand)38);
				break;
			}
			finally
			{
				owner = null;
				firearm = null;
			}
		case GestureResult.ReleaseWheel:
		case GestureResult.Cancel:
			releaseFrame = Time.frameCount;
			plugin.Menu.Close();
			owner = null;
			firearm = null;
			plugin.Debug("TDW CLOSE without action");
			break;
		}
	}

	public void Cancel(bool suppressUntilRelease)
	{
		mouseOwner = null;
		if (gesture.Active || draining || plugin.Menu.IsOpen)
		{
			bool active = gesture.Active;
			gesture.Reset();
			plugin.Menu?.Close();
			if (active)
			{
				plugin.Debug("TDW CLOSE");
			}
			draining = suppressUntilRelease && (active || draining) && (UnityEngine.Input.GetKey((KeyCode)116) || !Application.isFocused);
			if (suppressUntilRelease && active)
			{
				releaseFrame = Time.frameCount;
			}
			if (!draining)
			{
				owner = null;
				firearm = null;
			}
		}
	}

	private void ResetMouse()
	{
		leftMouse.Reset();
		rightMouse.Reset();
		mouseOwner = null;
	}
}
