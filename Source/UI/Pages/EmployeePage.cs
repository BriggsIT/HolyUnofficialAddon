using System.Collections.Generic;
using HolyAddon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// One employee: a slider per skill plus motivation. Values are written to the employee as the slider moves.
internal class EmployeePage : Page
{
	private readonly List<characterScript> order;

	private readonly int index;

	private readonly characterScript c;

	// What the employee looked like when this page opened, for "Undo changes".
	private readonly float[] original = new float[Employees.SkillCount + 1];

	private readonly int originalKrank;

	public EmployeePage(List<characterScript> order, int index)
	{
		this.order = order;
		this.index = index;
		c = order[index];
		for (int i = 0; i < Employees.SkillCount; i++)
		{
			original[i] = Employees.Skill(c, i);
		}
		original[Employees.SkillCount] = c.s_motivation;
		originalKrank = c.krank;
	}

	public override string Title => (bool)c ? c.myName : "Employee";

	public override string Icon => "iconPersonal";

	public override float Width => 660f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		if (!c)
		{
			return;
		}
		BackButton(w);
		w.Label(c.myName, 116f, 8f, w.Width - 116f - 116f, 32f, TextAnchor.MiddleLeft, bold: true, null, 20);
		bool canStep = order.Count > 1;
		w.SmallButton("<", w.Width - 102f, 10f, 40f, () => Step(w, -1), canStep, "Previous employee", null, 28f);
		w.SmallButton(">", w.Width - 56f, 10f, 40f, () => Step(w, 1), canStep, "Next employee", null, 28f);
		int best = c.GetBestSkill();
		float y = 48f;
		y += w.Facts(14f, y, w.Width - 28f, new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV(G.Text(1018, "Profession"), Employees.Profession(c)),
			EditorWindow.KV(G.Text(127, "Salary"), G.Money(c.GetGehalt())),
			EditorWindow.KV(G.Text(1764, "Best Skill"), (best >= 0 && best < Employees.SkillCount) ? Employees.SkillName(best) : "-"),
			EditorWindow.KV("Health", Employees.IsSick(c) ? "<color=red>Sick</color>" : "Healthy")
		}, 2) + 8f;
		w.Header("Skills", 14f, y, w.Width - 28f);
		y += 30f;
		for (int i = 0; i < Employees.SkillCount; i++)
		{
			int skill = i;
			bool main = skill == c.beruf;
			Text name = w.Label(Employees.SkillName(skill), 24f, y, 200f, 30f, TextAnchor.MiddleLeft, main);
			if (main)
			{
				name.color = UIKit.Blue;
				UIKit.SetTooltip(name.gameObject, "Main skill of a " + Employees.Profession(c) + ": it can grow all the way to 100.");
			}
			else
			{
				string cap = Employees.NaturalCap(c, skill).ToString("0");
				UIKit.SetTooltip(name.gameObject, "Outside their profession this skill normally stops at " + cap + ", and the game pushes it back to " + cap + " whenever they use it. " + (Plugin.KeepEditedSkills.Value ? "With \"Keep edited skills\" on (Game settings page), a higher value you set here stays." : "\"Keep edited skills\" is off, so a higher value drops back the first time they use this skill."));
			}
			w.Slider(228f, y, w.Width - 228f - 20f, 0f, 100f, Employees.Skill(c, skill), (float v) => Employees.SetSkill(c, skill, v), null, colorByValue: true);
			y += 34f;
		}
		y += 6f;
		w.Header(Employees.MotivationName, 14f, y, w.Width - 28f);
		y += 30f;
		Text motivation = w.Label(Employees.MotivationName, 24f, y, 200f, 30f);
		UIKit.SetTooltip(motivation.gameObject, "How happy the employee is. At 0 they hand in their notice.");
		w.Slider(228f, y, w.Width - 228f - 20f, 0f, 100f, c.s_motivation, (float v) => Employees.SetMotivation(c, v), (float v) => Mathf.RoundToInt(v) + "%", colorByValue: true);
		y += 40f;
		w.Header("Perks", 14f, y, w.Width - 28f);
		y += 28f;
		Perks(w, y);
		float buttonsY = w.Height - 75f - 50f;
		float bw = (w.Width - 28f - 30f) / 4f;
		w.BigButton("Max Skills", 14f, buttonsY, bw, 40f, () =>
		{
			for (int i = 0; i < Employees.SkillCount; i++)
			{
				Employees.SetSkill(c, i, 100f);
			}
			w.Rebuild();
		}, enabled: true, "Set all eight skills to 100.");
		w.BigButton("Max Motivation", 14f + (bw + 10f), buttonsY, bw, 40f, () =>
		{
			Employees.SetMotivation(c, 100f);
			w.Rebuild();
		}, enabled: true, "Set motivation to 100%.");
		w.BigButton("Cure Sickness", 14f + (bw + 10f) * 2f, buttonsY, bw, 40f, () =>
		{
			c.krank = 0;
			w.Rebuild();
		}, Employees.IsSick(c), Employees.IsSick(c) ? "Make the employee healthy again right away." : "This employee isn't sick.");
		w.BigButton("Undo Changes", 14f + (bw + 10f) * 3f, buttonsY, bw, 40f, () =>
		{
			for (int i = 0; i < Employees.SkillCount; i++)
			{
				Employees.SetSkill(c, i, original[i]);
			}
			Employees.SetMotivation(c, original[Employees.SkillCount]);
			c.krank = originalKrank;
			w.Rebuild();
		}, enabled: true, "Put this employee back the way they were when you opened this page.");
	}

	// The game's own perk icons (with their tooltips), read-only for now.
	private void Perks(EditorWindow w, float y)
	{
		GameObject strip = new GameObject("Perks", typeof(RectTransform));
		strip.transform.SetParent(w.Body, worldPositionStays: false);
		UIKit.Place(strip, 24f, y, w.Width - 48f, 36f);
		HorizontalLayoutGroup layout = strip.AddComponent<HorizontalLayoutGroup>();
		layout.spacing = 6f;
		layout.childAlignment = TextAnchor.MiddleLeft;
		layout.childControlWidth = false;
		layout.childControlHeight = false;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
		bool any = false;
		if (c.perks != null)
		{
			foreach (bool perk in c.perks)
			{
				any |= perk;
			}
		}
		if (any && (bool)G.Gui)
		{
			G.Gui.CreatePerkIcons(c, strip.transform);
		}
		else
		{
			w.Label("No perks", 24f, y, 200f, 36f);
		}
	}

	private void Step(EditorWindow w, int direction)
	{
		for (int n = 1; n <= order.Count; n++)
		{
			int next = ((index + direction * n) % order.Count + order.Count) % order.Count;
			if ((bool)order[next])
			{
				w.Replace(new EmployeePage(order, next));
				return;
			}
		}
	}

	public override void Tick(EditorWindow w)
	{
		// The employee left or was fired while the page was open (only possible if the game keeps running in menus).
		if (!c)
		{
			w.Back();
		}
	}
}
