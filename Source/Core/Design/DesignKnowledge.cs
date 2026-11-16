using System.Collections.Generic;
using HarmonyLib;
using HolyAddon.Core.Industry;
using UnityEngine;

namespace HolyAddon.Core.Design;

// What your studio knows about how a design fits, by the game's own rules (Docs/Design-Assistant-Plan.md):
// - a topic's fit with a genre, and an age group's fit with a genre: once you've had the review report (spielbericht)
//   of a game of yours in that genre with that topic (main or sub) or that age group (Menu_DevGame_Theme.FitGenre,
//   Menu_DevGame_Zielgruppe.FitTargetGroup);
// - a genre's good sub-genres and its work priorities: once you've had any review report in that genre (the report
//   lists them, Menu_QA_Spielbericht.Init);
// - the design sliders' right values: per genre and sub-genre (genres.GetFocusKnown, GetAlignKnown).
// "Reveal every fit" (the design assistant's setting) makes all of it known, the way the game's sandbox switches do.
[HarmonyPatch]
internal static class DesignKnowledge
{
	private class Known
	{
		public readonly HashSet<long> Topics = new HashSet<long>();

		public readonly HashSet<long> Ages = new HashSet<long>();

		public readonly HashSet<int> Genres = new HashSet<int>();
	}

	private static Known cache;

	private static int cacheFrame = -100;

	public static bool On
	{
		get
		{
			if (Plugin.DesignAssistant.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static bool Reveal
	{
		get
		{
			if (On)
			{
				return Plugin.DesignRevealAll.Value;
			}
			return false;
		}
	}

	private static long Key(int a, int b)
	{
		return (long)a * 100000L + b;
	}

	// One pass over the games, kept for half a second (pickers ask for every item at once).
	private static Known Now()
	{
		if (cache != null && Time.frameCount - cacheFrame < 30)
		{
			return cache;
		}
		Known k = new Known();
		mainScript ms = G.MS;
		if ((bool)ms)
		{
			foreach (gameScript g in IndustryCore.AllGames())
			{
				if (!g.spielbericht || (g.ownerID != ms.myID && g.developerID != ms.myID) || g.maingenre < 0)
				{
					continue;
				}
				k.Genres.Add(g.maingenre);
				k.Ages.Add(Key(g.maingenre, g.gameZielgruppe));
				if (g.gameMainTheme >= 0)
				{
					k.Topics.Add(Key(g.maingenre, g.gameMainTheme));
				}
				if (g.gameSubTheme >= 0)
				{
					k.Topics.Add(Key(g.maingenre, g.gameSubTheme));
				}
			}
		}
		cache = k;
		cacheFrame = Time.frameCount;
		return k;
	}

	private static bool SandboxFits
	{
		get
		{
			mainScript ms = G.MS;
			if ((bool)ms && ms.settings_sandbox)
			{
				return ms.sandbox_fitTopicToGenre;
			}
			return false;
		}
	}

	private static themes Themes
	{
		get
		{
			if (!G.MainObj)
			{
				return null;
			}
			return G.MainObj.GetComponent<themes>();
		}
	}

	// 1: fits, -1: doesn't, 0: not known yet.
	public static int TopicFit(int topic, int genre)
	{
		themes th = Themes;
		if (!th || topic < 0 || genre < 0)
		{
			return 0;
		}
		if (!Reveal && !SandboxFits && !Now().Topics.Contains(Key(genre, topic)))
		{
			return 0;
		}
		if (!th.IsThemesFitWithGenre(topic, genre))
		{
			return -1;
		}
		return 1;
	}

	public static int AgeFit(int genre, int group)
	{
		genres gen = IndustryCore.Genres;
		if (!gen || genre < 0 || group < 0)
		{
			return 0;
		}
		if (!Reveal && !SandboxFits && !Now().Ages.Contains(Key(genre, group)))
		{
			return 0;
		}
		if (!gen.IsTargetGroup(genre, group))
		{
			return -1;
		}
		return 1;
	}

	// Learned from any review report in the genre: its good sub-genres and its work priorities.
	public static bool GenreKnown(int genre)
	{
		if (genre < 0)
		{
			return false;
		}
		if (!Reveal)
		{
			return Now().Genres.Contains(genre);
		}
		return true;
	}

	public static int ComboFit(int main, int sub)
	{
		genres gen = IndustryCore.Genres;
		if (!gen || main < 0 || sub < 0 || main == sub || !GenreKnown(main))
		{
			return 0;
		}
		if (!gen.IsGenreCombination(main, sub))
		{
			return -1;
		}
		return 1;
	}

	// ---- Reveal every fit: the game's own pickers and sliders show everything, as with its sandbox switches ----

	private static bool[] Open()
	{
		mainScript ms = G.MS;
		if (!Reveal || !ms)
		{
			return null;
		}
		bool[] old = new bool[3] { ms.settings_sandbox, ms.sandbox_fitTopicToGenre, ms.sandbox_bekannteKonzeptEinstellungen };
		ms.settings_sandbox = true;
		ms.sandbox_fitTopicToGenre = true;
		ms.sandbox_bekannteKonzeptEinstellungen = true;
		return old;
	}

	private static void Close(bool[] old)
	{
		mainScript ms = G.MS;
		if (old != null && (bool)ms)
		{
			ms.settings_sandbox = old[0];
			ms.sandbox_fitTopicToGenre = old[1];
			ms.sandbox_bekannteKonzeptEinstellungen = old[2];
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_DevGame_Theme), "FitGenre")]
	private static void TopicBefore(out bool[] __state)
	{
		__state = Open();
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(Menu_DevGame_Theme), "FitGenre")]
	private static void TopicAfter(bool[] __state)
	{
		Close(__state);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_DevGame_Zielgruppe), "FitTargetGroup")]
	private static void AgeBefore(out bool[] __state)
	{
		__state = Open();
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(Menu_DevGame_Zielgruppe), "FitTargetGroup")]
	private static void AgeAfter(bool[] __state)
	{
		Close(__state);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(genres), "GetFocusKnown")]
	private static void FocusKnown(int maingenre, ref bool __result)
	{
		if (!__result && maingenre >= 0 && Reveal)
		{
			__result = true;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(genres), "GetAlignKnown")]
	private static void AlignKnown(int maingenre, ref bool __result)
	{
		if (!__result && maingenre >= 0 && Reveal)
		{
			__result = true;
		}
	}
}
