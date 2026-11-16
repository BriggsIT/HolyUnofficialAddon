using System;
using System.Collections.Generic;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Design;
using HolyAddon.Core.Industry;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// The design assistant's fit colours on the game design screen (Docs/Design-Assistant-Plan.md), from what your studio
// knows (Core/Design/DesignKnowledge):
// - genre picker: a main genre by how it goes with the sub-genre, topics and age group you've picked; a sub-genre by
//   whether it's a good one for the main genre; the reasons in the tooltip;
// - topics and age groups: the game's own colours (the colour-blind palette swaps them);
// - design sliders: the value in the fit colour when it's the right one, else in the misfit colour with the right one
//   beside it, as the game shows it;
// - platforms: by how the genre sells on the platform's kind (the stars the game shows in the genre's tooltip), in
//   the platform picker and on the platform page.
[HarmonyPatch]
internal static class DesignColours
{
	private static bool On => DesignKnowledge.On;

	private static bool ColourBlind => Plugin.DesignColourBlind.Value;

	private static Color GameColour(int i, Color fallback)
	{
		GUI_Main gui = G.Gui;
		if ((bool)gui && gui.colors != null && i < gui.colors.Length)
		{
			return gui.colors[i];
		}
		return fallback;
	}

	// Backgrounds of picker items: the game's topic colours, or light blue and orange.
	public static Color GoodBack
	{
		get
		{
			if (!ColourBlind)
			{
				return GameColour(7, new Color(0.62f, 0.88f, 0.55f));
			}
			return new Color(0.6f, 0.78f, 1f);
		}
	}

	public static Color BadBack
	{
		get
		{
			if (!ColourBlind)
			{
				return GameColour(8, new Color(1f, 0.6f, 0.55f));
			}
			return new Color(1f, 0.72f, 0.38f);
		}
	}

	// Text: the game's green and red ("<color=green>"), or blue and orange.
	public static string GoodHex
	{
		get
		{
			if (!ColourBlind)
			{
				return "green";
			}
			return "#1d5fc4";
		}
	}

	public static string BadHex
	{
		get
		{
			if (!ColourBlind)
			{
				return "red";
			}
			return "#c45a00";
		}
	}


	public static string Paint(string text, int fit)
	{
		if (fit == 0)
		{
			return text;
		}
		return "<color=" + ((fit > 0) ? GoodHex : BadHex) + ">" + text + "</color>";
	}

	private static Menu_DevGame Menu
	{
		get
		{
			Menu_DevGame menu = ExtraTopicsMenu.Menu;
			if (!menu || !menu.gameObject.activeInHierarchy)
			{
				return null;
			}
			return menu;
		}
	}

	// ---- Genre picker ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Item_DevGame_Genre), "SetData")]
	private static void GenreItem(Item_DevGame_Genre __instance)
	{
		Menu_DevGame menu = Menu;
		if (!On || !menu || menu.typ_contractGame)
		{
			return;
		}
		try
		{
			List<string> notes = new List<string>();
			int fit = ((__instance.genreArt == 0) ? MainGenreFit(menu, __instance.myID, notes) : SubGenreFit(menu, __instance.myID, notes));
			Image back = __instance.GetComponent<Image>();
			if ((bool)back && fit != 0)
			{
				back.color = ((fit > 0) ? GoodBack : BadBack);
			}
			if (notes.Count > 0 && (bool)__instance.tooltip_)
			{
				tooltip tip = __instance.tooltip_;
				tip.c = tip.c + "\n\n<b>Design assistant</b>\n" + string.Join("\n", notes);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Design colours (genre): " + ex.Message);
		}
	}

	// A main genre: does it go with the sub-genre, the topics and the age group already picked (what's known).
	private static int MainGenreFit(Menu_DevGame menu, int genre, List<string> notes)
	{
		int good = 0;
		int bad = 0;
		void Note(int f, string yes, string no)
		{
			if (f > 0)
			{
				good++;
				notes.Add(Paint(yes, 1));
			}
			else if (f < 0)
			{
				bad++;
				notes.Add(Paint(no, -1));
			}
		}
		int sub = menu.g_GameSubGenre;
		if (sub >= 0 && sub != genre)
		{
			string name = IndustryCore.GenreName(sub);
			Note(DesignKnowledge.ComboFit(genre, sub), "Goes with " + name + " as its sub-genre", "Doesn't go with " + name + " as its sub-genre");
		}
		foreach (int topic in ExtraTopicsMenu.Others(menu))
		{
			if (topic >= 0)
			{
				string name2 = Core.Topics.ExtraTopics.TopicName(topic);
				Note(DesignKnowledge.TopicFit(topic, genre), "Suits " + name2, "Doesn't suit " + name2);
			}
		}
		int age = menu.g_GameZielgruppe;
		if (age >= 0 && (bool)G.TS)
		{
			string name3 = G.TS.GetGameZielgruppe(age);
			Note(DesignKnowledge.AgeFit(genre, age), "Right for " + name3, "Not for " + name3);
		}
		if (bad > 0)
		{
			return -1;
		}
		return (good > 0) ? 1 : 0;
	}

	private static int SubGenreFit(Menu_DevGame menu, int genre, List<string> notes)
	{
		int main = menu.g_GameMainGenre;
		if (main < 0 || main == genre)
		{
			return 0;
		}
		string name = IndustryCore.GenreName(main);
		int fit = DesignKnowledge.ComboFit(main, genre);
		if (fit > 0)
		{
			notes.Add(Paint("A good sub-genre for " + name, 1));
		}
		else if (fit < 0)
		{
			notes.Add(Paint("Not a good sub-genre for " + name, -1));
		}
		else
		{
			notes.Add("Not known yet: the review report of a " + name + " game of yours lists its good sub-genres.");
		}
		return fit;
	}

	// ---- Topic and age group pickers: the game's colours, swapped for the colour-blind palette ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Item_DevGame_Theme), "SetData")]
	private static void TopicItem(Item_DevGame_Theme __instance)
	{
		if (!On || !ColourBlind || __instance.fitGenre == 0)
		{
			return;
		}
		Image back = __instance.GetComponent<Image>();
		if ((bool)back)
		{
			back.color = ((__instance.fitGenre > 0) ? GoodBack : BadBack);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame_Zielgruppe), "UpdateGUI")]
	private static void AgeGroups(Menu_DevGame_Zielgruppe __instance)
	{
		GUI_Main gui = G.Gui;
		if (!On || !ColourBlind || !gui || gui.colors == null || gui.colors.Length <= 8)
		{
			return;
		}
		for (int i = 1; i <= 5 && i < __instance.uiObjects.Length; i++)
		{
			Image img = (__instance.uiObjects[i] ? __instance.uiObjects[i].GetComponent<Image>() : null);
			if ((bool)img)
			{
				if (Same(img.color, gui.colors[4]))
				{
					img.color = GoodBack;
				}
				else if (Same(img.color, gui.colors[8]))
				{
					img.color = BadBack;
				}
			}
		}
	}

	private static bool Same(Color a, Color b)
	{
		return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.01f;
	}

	// ---- Design sliders: the value in the fit colour, or the misfit colour with the right value beside it ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "UpdateDesignSettings")]
	private static void Sliders(Menu_DevGame __instance)
	{
		genres gen = IndustryCore.Genres;
		int main = __instance.g_GameMainGenre;
		int sub = __instance.g_GameSubGenre;
		if (!On || !gen || main < 0)
		{
			return;
		}
		try
		{
			for (int i = 0; i < __instance.g_Designschwerpunkt.Length && i < __instance.uiDesignschwerpunkte.Length; i++)
			{
				int v = __instance.g_Designschwerpunkt[i];
				bool known = gen.GetFocusKnown(i, main, sub);
				Show(ValueText(__instance.uiDesignschwerpunkte[i]), v, known ? gen.GetFocus(i, main, sub) : (-1), !known && gen.GetFocusTested(i, main, sub, v));
			}
			for (int j = 0; j < __instance.g_Designausrichtung.Length && j < __instance.uiDesignausrichtung.Length; j++)
			{
				int v2 = __instance.g_Designausrichtung[j];
				bool known2 = gen.GetAlignKnown(j, main, sub);
				Show(ValueText(__instance.uiDesignausrichtung[j]), v2, known2 ? gen.GetAlign(j, main, sub) : (-1), !known2 && gen.GetAlignTested(j, main, sub, v2));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Design colours (sliders): " + ex.Message);
		}
	}

	private static Text ValueText(GameObject row)
	{
		if (!row || row.transform.childCount <= 2 || row.transform.GetChild(2).childCount == 0)
		{
			return null;
		}
		return row.transform.GetChild(2).GetChild(0).GetComponent<Text>();
	}

	private static void Show(Text t, int value, int right, bool testedWrong)
	{
		if (!t)
		{
			return;
		}
		if (right >= 0)
		{
			t.text = ((value == right) ? Paint(value.ToString(), 1) : (Paint(value.ToString(), -1) + " " + Paint("[" + right + "]", 1)));
		}
		else if (testedWrong)
		{
			t.text = Paint(value.ToString(), -1);
		}
	}
}
