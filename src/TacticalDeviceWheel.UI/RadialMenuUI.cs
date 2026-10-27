using System;
using System.Collections.Generic;
using TacticalDeviceWheel.Core;
using TacticalDeviceWheel.Devices;
using UnityEngine;
using UnityEngine.UI;

namespace TacticalDeviceWheel.UI;

internal sealed class RadialMenuUI : IDisposable
{
	private sealed class Entry
	{
		public WedgeGraphic Wedge;

		public Image Icon;

		public Image DeviceIcon;

		public Text State;

		public Text FunctionLabel;

		public TacticalCapability Capability;

		public bool? On;

		public bool? Available;

		public string Reading;
	}

	private readonly Plugin plugin;

	private GameObject root;

	private GameObject content;

	private RectTransform wheel;

	private RectTransform indicator;

	private Font font;

	private Text center;

	private Text centerDevice;

	private readonly List<Entry> entries = new List<Entry>();

	private int previousSelection = -2;

	private int previousNativeMode = -1;

	private bool previousDeviceActive;

	private string previousReading;

	private bool previousFlashing;

	private bool previousRestoring;

	private float radius;

	private UiLanguage language;

	private string EmptyText => L("No usable tactical devices\nRight-click to close", "当前武器无可用设备\n右键关闭");

	private string L(string english, string chinese)
	{
		return Localization.Pick(language, english, chinese);
	}

	public RadialMenuUI(Plugin plugin)
	{
		this.plugin = plugin;
	}

	private RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
	{
		GameObject val = new GameObject(name, new Type[1] { typeof(RectTransform) });
		val.transform.SetParent(parent, false);
		RectTransform val2 = (RectTransform)val.transform;
		Vector2 val3 = new Vector2(0.5f, 0.5f);
		val2.pivot = val3;
		Vector2 anchorMin = (val2.anchorMax = val3);
		val2.anchorMin = anchorMin;
		val2.sizeDelta = size;
		val2.anchoredPosition = position;
		return val2;
	}

	private Text Label(string name, Transform parent, string value, Vector2 size, Vector2 position, int fontSize)
	{
		Text obj = ((Component)Rect(name, parent, size, position)).gameObject.AddComponent<Text>();
		obj.font = font;
		obj.text = value;
		obj.fontSize = fontSize;
		obj.alignment = (TextAnchor)4;
		((Graphic)obj).color = new Color(0.88f, 0.91f, 0.89f);
		((Graphic)obj).raycastTarget = false;
		obj.supportRichText = false;
		obj.horizontalOverflow = (HorizontalWrapMode)0;
		obj.verticalOverflow = (VerticalWrapMode)0;
		return obj;
	}

	private Image Picture(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position)
	{
		Image obj = ((Component)Rect(name, parent, size, position)).gameObject.AddComponent<Image>();
		obj.sprite = sprite;
		obj.preserveAspect = true;
		((Graphic)obj).raycastTarget = false;
		((Behaviour)obj).enabled = (object)sprite != null;
		return obj;
	}

	private static Color NameColor(DisplayName name)
	{
		if (!name.Rgba.HasValue)
		{
			return new Color(0.88f, 0.91f, 0.89f);
		}
		uint value = name.Rgba.Value;
		return new Color((float)((value >> 24) & 0xFFu) / 255f, (float)((value >> 16) & 0xFFu) / 255f, (float)((value >> 8) & 0xFFu) / 255f, (float)(value & 0xFFu) / 255f);
	}

	public void Show(IReadOnlyList<TacticalCapability> capabilities, DisplayName weapon)
	{
		EnsureRoot();
		Hide();
		language = plugin.Options.Language.Value;
		root.SetActive(true);
		content = new GameObject("WheelContent", new Type[1] { typeof(RectTransform) });
		content.transform.SetParent(root.transform, false);
		wheel = (RectTransform)content.transform;
		RectTransform obj = wheel;
		RectTransform obj2 = wheel;
		RectTransform obj3 = wheel;
		Vector2 val = new Vector2(0.5f, 0.5f);
		obj3.pivot = val;
		Vector2 anchorMin = (obj2.anchorMax = val);
		obj.anchorMin = anchorMin;
		float num = 1080f * (float)Screen.width / (float)Screen.height;
		radius = Mathf.Min(new float[3]
		{
			380f * plugin.Options.Scale.Value,
			455f,
			num / 2f - 35f
		});
		float num2 = radius * 0.4f;
		int count = capabilities.Count;
		for (int i = 0; i < count; i++)
		{
			TacticalCapability tacticalCapability = capabilities[i];
			float num3 = 360f * (float)i / (float)count;
			WedgeGraphic wedgeGraphic = ((Component)Rect("Sector-" + i, (Transform)(object)wheel, Vector2.one * radius * 2f, Vector2.zero)).gameObject.AddComponent<WedgeGraphic>();
			wedgeGraphic.InnerRadius = num2;
			wedgeGraphic.OuterRadius = radius;
			wedgeGraphic.CenterAngle = num3;
			wedgeGraphic.SweepAngle = 360f / (float)count;
			((Graphic)wedgeGraphic).raycastTarget = false;
			((Graphic)wedgeGraphic).SetVerticesDirty();
			float num4 = radius * 0.7f;
			Vector2 position = new Vector2(Mathf.Sin(num3 * (MathF.PI / 180f)), Mathf.Cos(num3 * (MathF.PI / 180f))) * num4;
			float num5 = Mathf.Clamp(2f * num4 * Mathf.Sin(MathF.PI / (float)Mathf.Max(count, 3)) - 12f, 45f, 190f);
			int num6 = ((count <= 12) ? 17 : ((count <= 20) ? 13 : 10));
			float num7 = ((count <= 12) ? 48 : ((count <= 20) ? 34 : 24));
			float num8 = Mathf.Min(36f * plugin.Options.DeviceIconScale.Value, num5 * 0.48f);
			float num9 = num8 * 25f / 36f;
			float num10 = Mathf.Max(36f, num9);
			float num11 = 40f;
			num7 = Mathf.Max(12f, Mathf.Min(new float[3]
			{
				num7 * plugin.Options.FunctionIconScale.Value,
				num5 - 4f,
				radius * 0.56f - num10 - num11 - 29f
			}));
			float num12 = num7 + num11 + num10 + 29f;
			RectTransform parent = Rect("Labels-" + i, (Transform)(object)wheel, new Vector2(num5, num12), position);
			float num13 = num12 / 2f - num7 / 2f;
			Image icon = Picture("Function", (Transform)(object)parent, plugin.Icons.Get(tacticalCapability.IconKey), Vector2.one * num7, new Vector2(0f, num13));
			num13 -= num7 / 2f + 3f + num11 / 2f;
			Text functionLabel = Label("Capability", (Transform)(object)parent, tacticalCapability.DisplayLabel(language), new Vector2(num5, num11), new Vector2(0f, num13), (tacticalCapability.Type == "Rangefinder") ? Mathf.Max(10, num6 - 2) : num6);
			num13 -= num11 / 2f + 3f + num10 / 2f;
			ItemIcon itemIcon = tacticalCapability.Device.ItemIcon;
			Image deviceIcon = Picture("Device", (Transform)(object)parent, ((itemIcon != null) ? itemIcon.Sprite : null) ?? plugin.Icons.Get("Device"), new Vector2(num8, num9), new Vector2((num8 - num5) / 2f, num13));
			float num14 = num5 - num8 - 6f;
			((Graphic)Label("DeviceName", (Transform)(object)parent, tacticalCapability.Device.Name, new Vector2(num14, num10), new Vector2((num8 + 6f) / 2f, num13), Mathf.Max(10, num6 - 3))).color = NameColor(tacticalCapability.Device.DisplayName);
			num13 -= num10 / 2f + 13f;
			Text state = Label("State", (Transform)(object)parent, "", new Vector2(num5, 20f), new Vector2(0f, num13), Mathf.Max(10, num6 - 3));
			entries.Add(new Entry
			{
				Wedge = wedgeGraphic,
				Icon = icon,
				DeviceIcon = deviceIcon,
				State = state,
				FunctionLabel = functionLabel,
				Capability = tacticalCapability
			});
		}
		center = Label("Selection", (Transform)(object)wheel, (count == 0) ? EmptyText : L("Tactical devices\nMove mouse to select", "战术设备\n移动鼠标选择"), new Vector2(num2 * 1.8f, num2), new Vector2(0f, 22f), 20);
		center.resizeTextForBestFit = true;
		center.resizeTextMinSize = 11;
		center.resizeTextMaxSize = 20;
		centerDevice = Label("SelectionDevice", (Transform)(object)wheel, "", new Vector2(num2 * 1.8f, 46f), new Vector2(0f, (0f - num2) * 0.38f), 16);
		((Graphic)Label("Title", (Transform)(object)wheel, "TDW " + plugin.Info.Metadata.Version + "  /  " + weapon.PlainText, new Vector2(radius * 2f, 40f), new Vector2(0f, radius + 30f), 18)).color = NameColor(weapon);
		Label("Hint", (Transform)(object)wheel, L("Move to select · LMB toggle · RMB close · ", "移动鼠标选择 · 左键切换 · 右键关闭 · ") + (plugin.Options.CloseOnTRelease.Value ? L("Release T: close", "松 T 关闭") : L("Release T: stay open", "松 T 保持")), new Vector2(790f, 32f), new Vector2(0f, 0f - radius - 26f), 16);
		indicator = Rect("VirtualDirection", (Transform)(object)wheel, new Vector2(7f, 7f), Vector2.zero);
		Image obj4 = ((Component)indicator).gameObject.AddComponent<Image>();
		((Graphic)obj4).color = new Color(1f, 0.84f, 0.48f);
		((Graphic)obj4).raycastTarget = false;
		Refresh(-1, Vector2.zero);
	}

	private void EnsureRoot()
	{
		if (!((object)root != null))
		{
			font = Font.CreateDynamicFontFromOSFont(new string[4] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 24);
			root = new GameObject("TDW-Canvas", new Type[3]
			{
				typeof(RectTransform),
				typeof(Canvas),
				typeof(CanvasScaler)
			});
			UnityEngine.Object.DontDestroyOnLoad(root);
			Canvas component = root.GetComponent<Canvas>();
			component.renderMode = (RenderMode)0;
			component.sortingOrder = 32700;
			CanvasScaler component2 = root.GetComponent<CanvasScaler>();
			component2.uiScaleMode = (UnityEngine.UI.CanvasScaler.ScaleMode)1;
			component2.referenceResolution = new Vector2(1920f, 1080f);
			component2.screenMatchMode = (UnityEngine.UI.CanvasScaler.ScreenMatchMode)0;
			component2.matchWidthOrHeight = 1f;
			root.SetActive(false);
		}
	}

	public void Refresh(int selected, Vector2 vector)
	{
		if ((object)content == null)
		{
			return;
		}
		bool flag = false;
		for (int i = 0; i < entries.Count; i++)
		{
			Entry entry = entries[i];
			bool isOn = entry.Capability.IsOn;
			bool canToggle = entry.Capability.CanToggle;
			string text = ((!(entry.Capability.Type == "Rangefinder")) ? null : entry.Capability.Device.RangeReadout?.Value);
			if (text != entry.Reading)
			{
				entry.Reading = text;
				entry.FunctionLabel.text = entry.Capability.DisplayLabel(language) + (string.IsNullOrEmpty(text) ? "" : ("\n" + text));
			}
			if (i == selected && (entry.On != isOn || entry.Available != canToggle))
			{
				flag = true;
			}
			if (entry.On != isOn || entry.Available != canToggle)
			{
				Text state = entry.State;
				string obj = (isOn ? L("● ON", "● 开启") : L("○ OFF", "○ 关闭"));
				object obj2;
				if (!canToggle)
				{
					StrobeController strobes = entry.Capability.Device.Strobes;
					obj2 = ((strobes != null && strobes.IsRestoring(entry.Capability.Device.InstanceId)) ? L(" · Restoring", " · 恢复中") : (entry.Capability.IsLinked ? L(" · Linked", " · 联动") : L(" · Unavailable", " · 不可组合")));
				}
				else
				{
					obj2 = "";
				}
				state.text = obj + (string?)obj2;
				((Graphic)entry.State).color = (isOn ? new Color(0.64f, 0.95f, 0.95f) : new Color(0.54f, 0.59f, 0.6f));
				((Graphic)entry.Icon).color = (Color)(isOn ? Color.white : new Color(0.65f, 0.69f, 0.7f, 0.72f));
				entry.On = isOn;
				entry.Available = canToggle;
			}
			entry.Wedge.SetState(isOn, i == selected);
			ItemIcon itemIcon = entry.Capability.Device.ItemIcon;
			Sprite val = ((itemIcon != null) ? itemIcon.Sprite : null);
			if ((object)val != null && (object)entry.DeviceIcon.sprite != (object)val)
			{
				entry.DeviceIcon.sprite = val;
				((Behaviour)entry.DeviceIcon).enabled = true;
			}
		}
		EFT.InventoryLogic.LightComponent selectedComponent = ((selected >= 0 && selected < entries.Count) ? entries[selected].Capability.Device.Component : null);
		int num = ((selectedComponent != null) ? selectedComponent.SelectedMode : (-1));
		bool flag2 = selectedComponent != null && selectedComponent.IsActive;
		string text2 = ((selected >= 0 && selected < entries.Count) ? entries[selected].Reading : null);
		TacticalDevice tacticalDevice = ((selected >= 0 && selected < entries.Count) ? entries[selected].Capability.Device : null);
		bool valueOrDefault = (tacticalDevice?.Strobes?.IsRunning(tacticalDevice.InstanceId)).GetValueOrDefault();
		bool valueOrDefault2 = (tacticalDevice?.Strobes?.IsRestoring(tacticalDevice.InstanceId)).GetValueOrDefault();
		if (selected != previousSelection || flag || num != previousNativeMode || flag2 != previousDeviceActive || text2 != previousReading || valueOrDefault != previousFlashing || valueOrDefault2 != previousRestoring)
		{
			previousSelection = selected;
			previousNativeMode = num;
			previousDeviceActive = flag2;
			previousReading = text2;
			previousFlashing = valueOrDefault;
			previousRestoring = valueOrDefault2;
			if (selected >= 0 && selected < entries.Count)
			{
				TacticalCapability capability = entries[selected].Capability;
				center.text = capability.DisplayLabel(language) + (string.IsNullOrEmpty(text2) ? "" : ("\n" + text2)) + "\n" + capability.ActionHint(language);
				centerDevice.text = capability.Device.Name;
				((Graphic)centerDevice).color = NameColor(capability.Device.DisplayName);
			}
			else
			{
				center.text = ((entries.Count == 0) ? EmptyText : L("Tactical devices\nMove to select\nLMB: toggle", "战术设备\n移动鼠标选择\n左键切换"));
				centerDevice.text = "";
			}
		}
		indicator.anchoredPosition = vector * radius * 0.96f;
	}

	public void Hide()
	{
		entries.Clear();
		previousSelection = -2;
		previousReading = null;
		if ((object)content != null)
		{
			content.SetActive(false);
			UnityEngine.Object.Destroy((UnityEngine.Object)content);
			content = null;
		}
		if ((object)root != null)
		{
			root.SetActive(false);
		}
	}

	public void Dispose()
	{
		Hide();
		if ((object)root != null)
		{
			UnityEngine.Object.Destroy((UnityEngine.Object)root);
		}
		if ((object)font != null)
		{
			UnityEngine.Object.Destroy((UnityEngine.Object)font);
		}
	}
}
