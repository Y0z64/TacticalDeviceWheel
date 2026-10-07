using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TacticalDeviceWheel.UI;

internal sealed class IconManager : IDisposable
{
	private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

	private readonly List<Texture2D> textures = new List<Texture2D>();

	private readonly HashSet<string> fallbackKeys = new HashSet<string>();

	private readonly Plugin plugin;

	public IconManager(Plugin plugin)
	{
		this.plugin = plugin;
	}

	public void Preload()
	{
		string[] array = new string[10] { "WhiteLight", "WhiteLightStrobe", "VisibleLaser", "IRLaser", "IRLaserLow", "IRLaserHigh", "IRIlluminator", "Rangefinder", "GenericMode", "Device" };
		foreach (string name in array)
		{
			Get(name);
		}
	}

	public Sprite Get(string name)
	{
		if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains("/") || name.Contains("\\"))
		{
			plugin.Warning("Invalid icon key; falling back to GenericMode.");
			name = "GenericMode";
		}
		if (sprites.TryGetValue(name, out var value))
		{
			plugin.Debug("Capability icon cached: " + name + ", sprite=" + ((object)value != null) + ", fallback=" + fallbackKeys.Contains(name) + ", path=" + Path.Combine(plugin.RootPath, "resources", "icons", "png", name + ".png"));
			return value;
		}
		string fullPath = Path.GetFullPath(Path.Combine(plugin.RootPath, "resources", "icons", "png", name + ".png"));
		plugin.Debug("Loading capability icon: " + name + ". Icon path: " + fullPath);
		Texture2D val = null;
		try
		{
			if (!File.Exists(fullPath))
			{
				throw new FileNotFoundException("Icon PNG is missing.", fullPath);
			}
			val = new Texture2D(2, 2, (TextureFormat)4, false);
			if (!ImageConversion.LoadImage(val, File.ReadAllBytes(fullPath), true))
			{
				throw new InvalidDataException("Unity LoadImage returned false.");
			}
			((Texture)val).filterMode = (FilterMode)1;
			((Texture)val).wrapMode = (TextureWrapMode)1;
			Sprite val2 = Sprite.Create(val, new Rect(0f, 0f, (float)((Texture)val).width, (float)((Texture)val).height), new Vector2(0.5f, 0.5f));
			if ((object)val2 == null)
			{
				throw new InvalidOperationException("Sprite.Create returned null.");
			}
			textures.Add(val);
			sprites[name] = val2;
			plugin.Debug("Icon loaded successfully: " + name + " (" + ((Texture)val).width + "x" + ((Texture)val).height + ")");
			return val2;
		}
		catch (Exception ex)
		{
			if ((object)val != null)
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)val);
			}
			plugin.Warning("Failed to load capability icon: " + name + ". Path: " + fullPath + ". " + ex?.ToString() + ((name == "GenericMode") ? " No fallback icon available." : " Falling back to GenericMode."));
			Sprite val3 = ((name == "GenericMode") ? null : Get("GenericMode"));
			fallbackKeys.Add(name);
			sprites[name] = val3;
			return val3;
		}
	}

	public void Dispose()
	{
		HashSet<Sprite> hashSet = new HashSet<Sprite>();
		foreach (Sprite value in sprites.Values)
		{
			if ((object)value != null && hashSet.Add(value))
			{
				UnityEngine.Object.Destroy((UnityEngine.Object)value);
			}
		}
		foreach (Texture2D texture in textures)
		{
			UnityEngine.Object.Destroy((UnityEngine.Object)texture);
		}
		sprites.Clear();
		textures.Clear();
		fallbackKeys.Clear();
	}
}
