using System.Text.RegularExpressions;

namespace TacticalDeviceWheel.Core;

public static class RangeReadoutText
{
	private static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.CultureInvariant);

	private static readonly Regex Number = new Regex("^[0-9]+([.,][0-9]+)?$", RegexOptions.CultureInvariant);

	public static string Format(string nativeText, UiLanguage language)
	{
		if (string.IsNullOrEmpty(nativeText) || nativeText.Length > 1024)
		{
			return NoReading(language);
		}
		string text = Tags.Replace(nativeText, "").Trim();
		if (text.Length > 12 || !Number.IsMatch(text))
		{
			return NoReading(language);
		}
		return text + Localization.Pick(language, " m", " 米");
	}

	public static string NoReading(UiLanguage language)
	{
		return Localization.Pick(language, "No reading", "无读数");
	}

	public static string Off(UiLanguage language)
	{
		return Localization.Pick(language, "Off", "关闭");
	}
}
