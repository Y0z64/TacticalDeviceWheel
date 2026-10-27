using System.Collections.Generic;
using TacticalDeviceWheel.Core;
using UnityEngine;

namespace TacticalDeviceWheel.Devices;

internal sealed class RangeReadout
{
	private readonly TacticalDevice device;

	private readonly Dictionary<int, RangeDisplaySource[]> modes;

	private readonly PeriodicGate clock = new PeriodicGate();

	private int lastMode = -1;

	private string lastRaw;

	private UiLanguage lastLanguage;

	public string Value { get; private set; } = "";


	public int SourceCount { get; }

	public RangeReadout(TacticalDevice device, Dictionary<int, RangeDisplaySource[]> modes)
	{
		this.device = device;
		this.modes = modes;
		foreach (RangeDisplaySource[] value in modes.Values)
		{
			SourceCount += value.Length;
		}
	}

	public void Poll(double now, bool enabled, float refreshHz, UiLanguage language)
	{
		if (language != lastLanguage)
		{
			lastLanguage = language;
			lastRaw = null;
		}
		if (!enabled || !device.Component.IsActive)
		{
			Value = (enabled ? RangeReadoutText.Off(language) : "");
			lastMode = -1;
			lastRaw = null;
			clock.Reset();
			return;
		}
		int selectedMode = device.Component.SelectedMode;
		if (selectedMode != lastMode)
		{
			lastMode = selectedMode;
			lastRaw = null;
			Value = "";
			clock.Reset();
		}
		if (!clock.Take(now, refreshHz))
		{
			return;
		}
		if (modes.TryGetValue(selectedMode, out var value))
		{
			RangeDisplaySource[] array = value;
			foreach (RangeDisplaySource rangeDisplaySource in array)
			{
				if (!((object)rangeDisplaySource.Controller == null) && ((Behaviour)rangeDisplaySource.Controller).isActiveAndEnabled && !((object)rangeDisplaySource.Text == null))
				{
					string text = rangeDisplaySource.Text.text;
					if (text != lastRaw || Value.Length == 0)
					{
						lastRaw = text;
						Value = RangeReadoutText.Format(text, language);
					}
					return;
				}
			}
		}
		lastRaw = null;
		Value = RangeReadoutText.NoReading(language);
	}
}
