using FirearmController = EFT.Player.FirearmController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT;
using EFT.Visual;
using HarmonyLib;
using TMPro;
using TacticalDeviceWheel.Core;
using TacticalDeviceWheel.Devices;
using UnityEngine;

namespace TacticalDeviceWheel.Compatibility;

internal static class ModeDiagnostics
{
	// 4.1.5: FirearmController.Firearms (public) -> Firearms._tacticalComboVisualControllers (public)

	private static readonly FieldInfo Modes = AccessTools.Field(typeof(TacticalComboVisualController), "_ligthbeamsTransforms");

	private static readonly FieldInfo RangeCanvas = AccessTools.Field(typeof(TacticalRangeFinderController), "_canvas");

	private static readonly FieldInfo RangeText = AccessTools.Field(typeof(TacticalRangeFinderController), "_textOnDisplay");

	public static VisualSnapshot Snapshot(FirearmController controller, Func<string, bool> ignoreStatusIndicators = null)
	{
		VisualSnapshot visualSnapshot = new VisualSnapshot();
		try
		{
			if (Modes == null)
			{
				throw new MissingFieldException("Verified tactical visual fields are unavailable.");
			}
			TacticalComboVisualController[] array = controller.Firearms?._tacticalComboVisualControllers;
			if (array == null)
			{
				throw new InvalidOperationException("Weapon tactical visuals are not initialized.");
			}
			visualSnapshot.Devices = (from v in array
				where (object)v != null && v.LightMod != null
				select ReadDevice(v, ignoreStatusIndicators?.Invoke(v.LightMod.Item.StringTemplateId) ?? false)).ToArray();
		}
		catch (Exception ex)
		{
			visualSnapshot.Error = ex.ToString();
		}
		return visualSnapshot;
	}

	private static DeviceVisuals ReadDevice(TacticalComboVisualController view, bool ignoreStatusIndicators)
	{
		DeviceVisuals deviceVisuals = new DeviceVisuals
		{
			InstanceId = view.LightMod.Item.Id,
			TemplateId = view.LightMod.Item.StringTemplateId
		};
		try
		{
			if (!(Modes.GetValue(view) is List<Transform> { Count: not 0 } list))
			{
				throw new InvalidOperationException("No ordered visual mode nodes.");
			}
			IkLight[] infrared = ((Component)view).GetComponentsInChildren<IkLight>(true);
			TacticalRangeFinderController[] rangefinders = ((Component)view).GetComponentsInChildren<TacticalRangeFinderController>(true);
			deviceVisuals.Modes = list.Select((Transform node, int index) => ReadMode(node, index, infrared, rangefinders, ignoreStatusIndicators)).ToArray();
		}
		catch (Exception ex)
		{
			deviceVisuals.Error = ex.ToString();
		}
		return deviceVisuals;
	}

	private static VisualMode ReadMode(Transform node, int index, IkLight[] infrared, TacticalRangeFinderController[] rangefinders, bool ignoreStatusIndicators)
	{
		VisualMode result = new VisualMode
		{
			Mode = index
		};
		try
		{
			if ((object)node == null)
			{
				throw new InvalidOperationException("Null mode transform.");
			}
			result.Node = ((UnityEngine.Object)node).name;
			List<RangeDisplaySource> rangeSources = new List<RangeDisplaySource>();
			result.Rangefinders = ((IEnumerable<TacticalRangeFinderController>)rangefinders).Select((Func<TacticalRangeFinderController, object>)delegate(TacticalRangeFinderController r)
			{
				object? obj4 = RangeCanvas?.GetValue(r);
				Component val2 = (Component)((obj4 is Component) ? obj4 : null);
				object? obj5 = RangeText?.GetValue(r);
				TMP_Text val3 = (TMP_Text)((obj5 is TMP_Text) ? obj5 : null);
				bool flag2 = OwnedWithinMode((Component)(object)r, node);
				bool flag3 = OwnedWithinMode(val2, node);
				bool flag4 = OwnedWithinMode((Component)(object)val3, node);
				bool flag5 = RangefinderModeEvidence.IsControlled(((Behaviour)r).enabled, flag2, flag3, flag4);
				if (flag5)
				{
					result.RangefinderCount++;
				}
				if (flag5 && (object)val3 != null)
				{
					rangeSources.Add(new RangeDisplaySource
					{
						Controller = r,
						Text = val3
					});
				}
				return new
				{
					node = RelativePath(((Component)r).transform, null),
					enabled = ((Behaviour)r).enabled,
					controllerOwned = flag2,
					canvasOwned = flag3,
					textOwned = flag4,
					controlledByMode = flag5,
					canvas = (((object)val2 != null) ? RelativePath(val2.transform, null) : null),
					text = (((object)val3 != null) ? RelativePath(val3.transform, null) : null)
				};
			}).ToArray();
			result.RangeSources = rangeSources.ToArray();
			LaserBeam[] componentsInChildren = ((Component)node).GetComponentsInChildren<LaserBeam>(true);
			Light[] componentsInChildren2 = ((Component)node).GetComponentsInChildren<Light>(true);
			result.NodePaths = (from t in ((Component)node).GetComponentsInChildren<Transform>(true)
				select RelativePath(t, node)).ToArray();
			result.MaterialDetails = (from m in componentsInChildren.SelectMany((LaserBeam b) => (IEnumerable<Material>)(object)new Material[2] { b.BeamMaterial, b.PointMaterial })
				where (object)m != null
				select m).Distinct().Select((Func<Material, object>)delegate(Material m)
			{
				Color val = (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.clear);
				string name = ((UnityEngine.Object)m).name;
				Shader shader6 = m.shader;
				return new
				{
					name = name,
					shader = ((shader6 != null) ? ((UnityEngine.Object)shader6).name : null),
					keywords = m.shaderKeywords,
					color = ((!m.HasProperty("_Color")) ? null : new float[4] { val.r, val.g, val.b, val.a }),
					factor = (m.HasProperty("_Factor") ? new float?(m.GetFloat("_Factor")) : null)
				};
			}).ToArray();
			result.ComponentTypes = (from c in ((Component)node).GetComponentsInChildren<Component>(true)
				where (object)c != null
				select ((object)c).GetType().FullName).Distinct().ToArray();
			LaserBeam[] array = componentsInChildren.Where((LaserBeam b) => ((Behaviour)b).enabled && EnabledWithinMode(((Component)b).transform, node)).ToArray();
			result.LaserCount = array.Length;
			result.Lasers = componentsInChildren.Select(delegate(LaserBeam b)
			{
				string text = RelativePath(((Component)b).transform, node);
				bool flag = infrared.Any((IkLight ik) => (object)ik != null && ((Behaviour)ik).enabled && (object)ik.Light != null && ((Component)ik.Light).transform.IsChildOf(((Component)b).transform));
				int num;
				if (!flag)
				{
					string[] array2 = new string[5];
					Material beamMaterial = b.BeamMaterial;
					object obj;
					if (beamMaterial == null)
					{
						obj = null;
					}
					else
					{
						Shader shader = beamMaterial.shader;
						obj = ((shader != null) ? ((UnityEngine.Object)shader).name : null);
					}
					array2[0] = (string)obj;
					Material beamMaterial2 = b.BeamMaterial;
					array2[1] = ((beamMaterial2 != null) ? ((UnityEngine.Object)beamMaterial2).name : null);
					Material pointMaterial = b.PointMaterial;
					object obj2;
					if (pointMaterial == null)
					{
						obj2 = null;
					}
					else
					{
						Shader shader2 = pointMaterial.shader;
						obj2 = ((shader2 != null) ? ((UnityEngine.Object)shader2).name : null);
					}
					array2[2] = (string)obj2;
					Material pointMaterial2 = b.PointMaterial;
					array2[3] = ((pointMaterial2 != null) ? ((UnityEngine.Object)pointMaterial2).name : null);
					array2[4] = text;
					num = (int)SpectrumMarkers.Read(array2);
				}
				else
				{
					num = 2;
				}
				EmitterSpectrum emitterSpectrum = (EmitterSpectrum)num;
				LaserObservation obj3 = new LaserObservation
				{
					Node = text,
					Enabled = ((Behaviour)b).enabled,
					ActiveWithinMode = EnabledWithinMode(((Component)b).transform, node)
				};
				Material beamMaterial3 = b.BeamMaterial;
				obj3.Material = ((beamMaterial3 != null) ? ((UnityEngine.Object)beamMaterial3).name : null);
				Material beamMaterial4 = b.BeamMaterial;
				object shader3;
				if (beamMaterial4 == null)
				{
					shader3 = null;
				}
				else
				{
					Shader shader4 = beamMaterial4.shader;
					shader3 = ((shader4 != null) ? ((UnityEngine.Object)shader4).name : null);
				}
				obj3.Shader = (string)shader3;
				Material pointMaterial3 = b.PointMaterial;
				obj3.PointMaterial = ((pointMaterial3 != null) ? ((UnityEngine.Object)pointMaterial3).name : null);
				Material pointMaterial4 = b.PointMaterial;
				object pointShader;
				if (pointMaterial4 == null)
				{
					pointShader = null;
				}
				else
				{
					Shader shader5 = pointMaterial4.shader;
					pointShader = ((shader5 != null) ? ((UnityEngine.Object)shader5).name : null);
				}
				obj3.PointShader = (string)pointShader;
				obj3.BeamSize = b.BeamSize;
				obj3.UsePointLight = b.UsePointLight;
				obj3.Spectrum = emitterSpectrum;
				obj3.Evidence = (flag ? "EFT.Visual.IkLight references laser-owned Light" : ((emitterSpectrum != 0) ? "Runtime asset-name spectrum marker (inferred; see names)" : "Unmarked laser; requires profile evidence"));
				return obj3;
			}).ToArray();
			result.VisibleLaserCount = result.Lasers.Count((LaserObservation l) => l.Enabled && l.ActiveWithinMode && l.Spectrum == EmitterSpectrum.Visible);
			result.InfraredLaserCount = result.Lasers.Count((LaserObservation l) => l.Enabled && l.ActiveWithinMode && l.Spectrum == EmitterSpectrum.Infrared);
			result.UnknownLaserCount = result.Lasers.Count((LaserObservation l) => l.Enabled && l.ActiveWithinMode && l.Spectrum == EmitterSpectrum.Unknown);
			result.Lights = componentsInChildren2.Select(delegate(Light l)
			{
				NativeLightObservation nativeLightObservation = new NativeLightObservation
				{
					Node = RelativePath(((Component)l).transform, node),
					Enabled = ((Behaviour)l).enabled
				};
				LightType type = l.type;
				nativeLightObservation.Type = type.ToString();
				nativeLightObservation.LaserOwned = HasLaserAncestor(((Component)l).transform, node);
				nativeLightObservation.InfraredByIkLight = IsIr(l);
				nativeLightObservation.ActiveWithinMode = EnabledWithinMode(((Component)l).transform, node);
				nativeLightObservation.Intensity = l.intensity;
				nativeLightObservation.Range = l.range;
				nativeLightObservation.SpotAngle = l.spotAngle;
				nativeLightObservation.Color = new float[4]
				{
					l.color.r,
					l.color.g,
					l.color.b,
					l.color.a
				};
				return nativeLightObservation;
			}).ToArray();
			NativeLightSummary nativeLightSummary = NativeLightRules.Summarize(result.Lights, ignoreStatusIndicators);
			result.WhiteLightCount = nativeLightSummary.WhiteLights;
			result.InfraredLightCount = nativeLightSummary.InfraredLights;
			result.IndependentSpotLightCount = nativeLightSummary.WhiteLights + nativeLightSummary.InfraredLights;
			result.UnsupportedLights = nativeLightSummary.Unsupported.ToArray();
			result.UnsupportedLightCount = result.UnsupportedLights.Length;
			result.IgnoredStatusIndicators = nativeLightSummary.IgnoredIndicators.ToArray();
		}
		catch (Exception ex)
		{
			result.Error = ex.ToString();
		}
		return result;
		bool IsIr(Light light)
		{
			return infrared.Any((IkLight ik) => (object)ik != null && ((Behaviour)ik).enabled && (object)ik.Light == (object)light);
		}
	}

	private static bool OwnedWithinMode(Component component, Transform mode)
	{
		if ((object)component != null && component.transform.IsChildOf(mode))
		{
			return EnabledWithinMode(component.transform, mode);
		}
		return false;
	}

	private static string RelativePath(Transform child, Transform mode)
	{
		List<string> list = new List<string>();
		Transform val = child;
		while ((object)val != null)
		{
			list.Add(((UnityEngine.Object)val).name);
			if ((object)val == (object)mode)
			{
				break;
			}
			val = val.parent;
		}
		list.Reverse();
		return string.Join("/", list);
	}

	private static bool EnabledWithinMode(Transform child, Transform mode)
	{
		Transform val = child;
		while ((object)val != null && (object)val != (object)mode)
		{
			if (!((Component)val).gameObject.activeSelf)
			{
				return false;
			}
			val = val.parent;
		}
		return true;
	}

	private static bool HasLaserAncestor(Transform child, Transform mode)
	{
		Transform val = child;
		while ((object)val != null)
		{
			if ((object)((Component)val).GetComponent<LaserBeam>() != null)
			{
				return true;
			}
			if ((object)val == (object)mode)
			{
				break;
			}
			val = val.parent;
		}
		return false;
	}
}
