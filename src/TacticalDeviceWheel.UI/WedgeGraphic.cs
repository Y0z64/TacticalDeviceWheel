using System;
using UnityEngine;
using UnityEngine.UI;

namespace TacticalDeviceWheel.UI;

public sealed class WedgeGraphic : MaskableGraphic
{
	public float InnerRadius;

	public float OuterRadius;

	public float CenterAngle;

	public float SweepAngle;

	private bool selected;

	private bool active;

	public void SetState(bool on, bool chosen)
	{
		if (on != active || chosen != selected)
		{
			active = on;
			selected = chosen;
			((Graphic)this).SetVerticesDirty();
		}
	}

	private static Vector2 Point(float radius, float degrees)
	{
		float num = degrees * (MathF.PI / 180f);
		return new Vector2(Mathf.Sin(num), Mathf.Cos(num)) * radius;
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		vh.Clear();
		float num = Mathf.Min(1.1f, SweepAngle * 0.04f);
		float num2 = CenterAngle - SweepAngle / 2f + num / 2f;
		float num3 = SweepAngle - num;
		int num4 = Mathf.Max(4, Mathf.CeilToInt(num3 / 4f));
		Color color = (selected ? new Color(0.23f, 0.21f, 0.14f, 0.96f) : (active ? new Color(0.08f, 0.17f, 0.18f, 0.94f) : new Color(0.045f, 0.055f, 0.062f, 0.91f)));
		Color color2 = (selected ? new Color(1f, 0.84f, 0.48f, 1f) : new Color(0.58f, 0.65f, 0.66f, active ? 0.65f : 0.25f));
		for (int i = 0; i < num4; i++)
		{
			float degrees = num2 + num3 * (float)i / (float)num4;
			float degrees2 = num2 + num3 * (float)(i + 1) / (float)num4;
			Quad(vh, Point(InnerRadius, degrees), Point(OuterRadius, degrees), Point(OuterRadius, degrees2), Point(InnerRadius, degrees2), color);
			float num5 = ((!selected) ? 1 : 3);
			Quad(vh, Point(OuterRadius - num5, degrees), Point(OuterRadius, degrees), Point(OuterRadius, degrees2), Point(OuterRadius - num5, degrees2), color2);
			Quad(vh, Point(InnerRadius, degrees), Point(InnerRadius + num5, degrees), Point(InnerRadius + num5, degrees2), Point(InnerRadius, degrees2), color2);
		}
		float num6 = Mathf.Min(0.6f, num3 * 0.03f);
		Quad(vh, Point(InnerRadius, num2), Point(OuterRadius, num2), Point(OuterRadius, num2 + num6), Point(InnerRadius, num2 + num6), color2);
		float num7 = num2 + num3;
		Quad(vh, Point(InnerRadius, num7 - num6), Point(OuterRadius, num7 - num6), Point(OuterRadius, num7), Point(InnerRadius, num7), color2);
	}

	private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
	{
		int currentVertCount = vh.currentVertCount;
		vh.AddVert((Vector2)(a), (Color32)(color), (Vector4)(Vector2.zero));
		vh.AddVert((Vector2)(b), (Color32)(color), (Vector4)(Vector2.zero));
		vh.AddVert((Vector2)(c), (Color32)(color), (Vector4)(Vector2.zero));
		vh.AddVert((Vector2)(d), (Color32)(color), (Vector4)(Vector2.zero));
		vh.AddTriangle(currentVertCount, currentVertCount + 1, currentVertCount + 2);
		vh.AddTriangle(currentVertCount, currentVertCount + 2, currentVertCount + 3);
	}
}
