using System.Collections.Generic;
using UnityEngine;

namespace HolyAddon.Core;

// Reads and writes the game's employee (characterScript) values. Skill order matches the game's own: beruf 0-7.
internal static class Employees
{
	public const int SkillCount = 8;

	private static readonly string[] SkillFallback = new string[8] { "Game Design", "Programming", "Graphics Design", "Music & Sound", "Marketing & Support", "Game Testing", "Hardware & Engineering", "Research" };

	private static readonly string[] ProfessionFallback = new string[8] { "Game Designer", "Programmer", "Graphics Artist", "Sound Artist", "Office Worker", "Game Tester", "Technician", "Researcher" };

	public static string SkillName(int skill)
	{
		return G.Text(119 + skill, SkillFallback[skill]);
	}

	public static string MotivationName => G.Text(190, "Motivation");

	public static string Profession(characterScript c)
	{
		if (c.beruf < 0 || c.beruf >= ProfessionFallback.Length)
		{
			return G.Text(137 + c.beruf, "Employee");
		}
		return G.Text(137 + c.beruf, ProfessionFallback[c.beruf]);
	}

	public static List<characterScript> All()
	{
		List<characterScript> list = new List<characterScript>();
		characterScript[] array = ((bool)G.MS) ? G.MS.arrayCharactersScripts : null;
		if (array == null)
		{
			return list;
		}
		foreach (characterScript c in array)
		{
			if ((bool)c)
			{
				list.Add(c);
			}
		}
		return list;
	}

	public static float Skill(characterScript c, int skill)
	{
		return skill switch
		{
			0 => c.s_gamedesign,
			1 => c.s_programmieren,
			2 => c.s_grafik,
			3 => c.s_sound,
			4 => c.s_pr,
			5 => c.s_gametests,
			6 => c.s_technik,
			7 => c.s_forschen,
			_ => 0f,
		};
	}

	public static void SetSkill(characterScript c, int skill, float value)
	{
		value = Mathf.Clamp(value, 0f, 100f);
		switch (skill)
		{
		case 0:
			c.s_gamedesign = value;
			break;
		case 1:
			c.s_programmieren = value;
			break;
		case 2:
			c.s_grafik = value;
			break;
		case 3:
			c.s_sound = value;
			break;
		case 4:
			c.s_pr = value;
			break;
		case 5:
			c.s_gametests = value;
			break;
		case 6:
			c.s_technik = value;
			break;
		case 7:
			c.s_forschen = value;
			break;
		}
	}

	public static void SetMotivation(characterScript c, float value)
	{
		c.s_motivation = Mathf.Clamp(value, 0f, 100f);
	}

	public static float BestSkill(characterScript c)
	{
		float best = 0f;
		for (int i = 0; i < SkillCount; i++)
		{
			best = Mathf.Max(best, Skill(c, i));
		}
		return best;
	}

	// Where the game stops a skill from growing on its own: 100 in the employee's profession, otherwise 50 (60 with perk 15).
	public static float NaturalCap(characterScript c, int skill)
	{
		mainScript ms = G.MS;
		if (skill == c.beruf || ((bool)ms && ms.settings_sandbox && ms.sandbox_mitarbeiterSkill100))
		{
			return 100f;
		}
		if (c.perks != null && c.perks.Length > 15 && c.perks[15])
		{
			return 60f;
		}
		return 50f;
	}

	public static bool IsSick(characterScript c)
	{
		return c.krank > 0;
	}

	public static string Tooltip(characterScript c)
	{
		try
		{
			return c.GetTooltip();
		}
		catch
		{
			return c.myName;
		}
	}
}
