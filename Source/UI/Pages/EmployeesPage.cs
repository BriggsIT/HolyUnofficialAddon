using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// Every employee with their eight skills and motivation. Click a row to edit that employee.
internal class EmployeesPage : Page
{
	private const int SortName = 0;

	private const int SortProfession = 1;

	private const int SortBest = 2;

	private const int SortMotivation = 3;

	private const int SortFirstSkill = 4;

	private string search = "";

	private int sort;

	private ListView list;

	private float rowWidth;

	private Text countLabel;

	private float refillAt;

	private readonly ConfirmButton maxSkills = new ConfirmButton("Max all skills");

	private readonly ConfirmButton maxMotivation = new ConfirmButton("Max all motivation");

	public override string Title => "Employees";

	public override string Icon => "iconPersonal";

	public override float Width => 940f;

	public override float Height => 720f;

	private float BodyHeight(EditorWindow w)
	{
		return w.Height - 75f;
	}

	// Column layout inside a row; the list's rows start about 7px right of the list itself.
	private static float NameWidth => 214f;

	private static float ColumnStart => NameWidth + 8f;

	private static float ColumnStep(float rowWidth)
	{
		return (rowWidth - ColumnStart - 4f) / 9f;
	}

	public override void Build(EditorWindow w)
	{
		BackButton(w);
		w.Input(search, "Search by name...", 116f, 7f, 240f, (string v) =>
		{
			search = v;
			refillAt = Time.unscaledTime + 0.25f;
		});
		w.Label("Sort by", 374f, 9f, 70f, 30f);
		List<string> options = new List<string> { G.Text(183, "Name"), G.Text(1018, "Profession"), G.Text(1764, "Best Skill"), Employees.MotivationName };
		for (int i = 0; i < Employees.SkillCount; i++)
		{
			options.Add(Employees.SkillName(i));
		}
		w.Dropdown(444f, 9f, 230f, options, sort, (int v) =>
		{
			sort = v;
			Fill();
		});
		countLabel = w.Label("", w.Width - 250f, 9f, 236f, 30f, TextAnchor.MiddleRight, bold: true);
		float listY = 92f;
		float listHeight = BodyHeight(w) - listY - 58f;
		list = w.List(13f, listY, w.Width - 26f, listHeight);
		rowWidth = w.Width - 26f - 38f;
		float step = ColumnStep(rowWidth);
		const float rowOffset = 20f;
		w.Label(G.Text(183, "Name"), rowOffset + 8f, 52f, NameWidth - 8f, 36f, TextAnchor.LowerLeft, bold: true, null, 13);
		for (int i = 0; i <= Employees.SkillCount; i++)
		{
			string header = (i < Employees.SkillCount) ? Employees.SkillName(i) : Employees.MotivationName;
			w.Label(header, rowOffset + ColumnStart + step * i, 52f, step - 4f, 36f, TextAnchor.LowerCenter, bold: true, null, 11);
		}
		float buttonsY = BodyHeight(w) - 48f;
		maxSkills.Create(w, 14f, buttonsY, 230f, "Set every skill to 100 for everyone in the list (the search box narrows it down).", () =>
		{
			foreach (characterScript c in Shown())
			{
				for (int i = 0; i < Employees.SkillCount; i++)
				{
					Employees.SetSkill(c, i, 100f);
				}
			}
			Fill();
		});
		maxMotivation.Create(w, 254f, buttonsY, 230f, "Set motivation to 100 for everyone in the list (the search box narrows it down).", () =>
		{
			foreach (characterScript c in Shown())
			{
				Employees.SetMotivation(c, 100f);
			}
			Fill();
		});
		w.Label("Click an employee to edit them.", 500f, buttonsY, w.Width - 514f, 38f, TextAnchor.MiddleRight);
		Fill();
	}

	public override void Tick(EditorWindow w)
	{
		if (refillAt > 0f && Time.unscaledTime >= refillAt)
		{
			refillAt = 0f;
			Fill();
		}
		maxSkills.Tick();
		maxMotivation.Tick();
	}

	private List<characterScript> Shown()
	{
		IEnumerable<characterScript> source = Employees.All();
		string term = search.Trim();
		if (term.Length > 0)
		{
			source = source.Where((characterScript c) => (c.myName ?? "").IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
		}
		IOrderedEnumerable<characterScript> ordered = sort switch
		{
			SortProfession => source.OrderBy((characterScript c) => c.beruf).ThenBy((characterScript c) => c.myName),
			SortBest => source.OrderByDescending(Employees.BestSkill),
			SortMotivation => source.OrderByDescending((characterScript c) => c.s_motivation),
			>= SortFirstSkill => source.OrderByDescending((characterScript c) => Employees.Skill(c, sort - SortFirstSkill)),
			_ => source.OrderBy((characterScript c) => c.myName),
		};
		return ordered.ThenBy((characterScript c) => c.myID).ToList();
	}

	private void Fill()
	{
		if (list == null || !list.Scroll)
		{
			return;
		}
		list.Clear();
		List<characterScript> shown = Shown();
		int total = Employees.All().Count;
		if ((bool)countLabel)
		{
			string count = G.Text(184, "<NUM> Employees").Replace("<NUM>", total.ToString());
			countLabel.text = (shown.Count == total) ? count : (shown.Count + " / " + count);
		}
		float step = ColumnStep(rowWidth);
		for (int n = 0; n < shown.Count; n++)
		{
			characterScript c = shown[n];
			int index = n;
			RowView row = list.Row(40f, () => EditorWindow.Main.Push(new EmployeePage(shown, index)), Employees.Tooltip(c));
			row.Top(c.myName, 8f, NameWidth - 8f);
			Text info = row.Bottom(Employees.Profession(c) + (Employees.IsSick(c) ? "  ·  sick" : ""), 8f, NameWidth - 8f);
			if (Employees.IsSick(c))
			{
				info.color = UIKit.Red;
			}
			row.Line(NameWidth + 2f);
			for (int i = 0; i <= Employees.SkillCount; i++)
			{
				float value = (i < Employees.SkillCount) ? Employees.Skill(c, i) : c.s_motivation;
				string tip = (i == c.beruf) ? ("Main skill of a " + Employees.Profession(c)) : null;
				row.Bar(ColumnStart + step * i, step - 8f, 22f, value / 100f, UIKit.ValueColor(value), Mathf.RoundToInt(value).ToString() + ((i == c.beruf) ? "*" : ""), tip);
			}
		}
		if (shown.Count == 0)
		{
			RowView row = list.Row(40f);
			row.Text((total == 0) ? "You have no employees yet." : "No employees match your search.", 8f, row.W - 16f, bold: false, TextAnchor.MiddleCenter);
		}
	}
}

// A button that needs a second click within a few seconds, for actions that change many employees at once.
internal class ConfirmButton
{
	private readonly string label;

	private Text text;

	private float armedUntil;

	public ConfirmButton(string label)
	{
		this.label = label;
	}

	public void Create(EditorWindow w, float x, float y, float width, string tooltip, Action action)
	{
		armedUntil = 0f;
		Button button = w.BigButton(label, x, y, width, 38f, () =>
		{
			if (Time.unscaledTime < armedUntil)
			{
				armedUntil = 0f;
				Show();
				action();
			}
			else
			{
				armedUntil = Time.unscaledTime + 4f;
				Show();
			}
		}, enabled: true, tooltip);
		text = button.transform.Find("Text").GetComponent<Text>();
	}

	public void Tick()
	{
		if (armedUntil > 0f && Time.unscaledTime >= armedUntil)
		{
			armedUntil = 0f;
			Show();
		}
	}

	private void Show()
	{
		if ((bool)text)
		{
			text.text = (armedUntil > 0f) ? "Click again to confirm" : label;
		}
	}
}
