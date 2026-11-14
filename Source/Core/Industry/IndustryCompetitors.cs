using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// Competitors (rival merge, step 2): the studios you meet most in the market right now, as opposed to rivals (how a
// studio feels about you). About four, picked again every year and after each of your releases, scored on:
// - making games in your genres (your last few games' and the ones you're making),
// - being about your size,
// - having something out or coming right now.
// Allies can be competitors too: competing isn't hating. Their fights are industry decisions
// (IndustryEventsCompetitors: Release Clash, Talent Race, Price War, Same Idea), at most one every FightGap weeks;
// fights that go badly cost standing, so a competitor you keep clashing with can turn into a rival.
[HarmonyPatch]
internal static class IndustryCompetitors
{
	public const int Count = 4;

	public const int FightGap = 8;

	private static IndustryState S => IndustryCore.State;

	private static int Now => IndustryCore.Now;

	public static bool On => IndustryCore.On;

	private static bool Open(publisherScript p)
	{
		if ((bool)p && !p.isPlayer && p.isUnlocked && !p.Geschlossen())
		{
			return !p.IsTochterfirma();
		}
		return false;
	}

	public static bool IsCompetitor(publisherScript p)
	{
		if ((bool)p && S.Competitors != null)
		{
			return S.Competitors.Contains(p.myID);
		}
		return false;
	}

	public static List<publisherScript> All()
	{
		return (S.Competitors ?? new List<int>()).Select(IndustryCore.Studio).Where(Open).ToList();
	}

	// A competitor fight may start now (each fight's own cooldown comes on top).
	public static bool FightReady()
	{
		if (On && Now >= S.CompetitorFightAfter)
		{
			return All().Count > 0;
		}
		return false;
	}

	// Your size: your studio's own stars where the game keeps them, else from your studio level.
	public static float MyStars()
	{
		if (!G.MS)
		{
			return 20f;
		}
		if (!G.MS.myPubS_)
		{
			return G.MS.GetStudioLevel(G.MS.studioPoints) * 10;
		}
		return G.MS.myPubS_.stars;
	}

	// The same players: one game's genre is the other's main or second genre.
	public static bool SameAudience(gameScript a, gameScript b)
	{
		if (!a || !b || a.maingenre < 0)
		{
			return false;
		}
		if (a.maingenre != b.maingenre && a.maingenre != b.subgenre)
		{
			if (a.subgenre >= 0)
			{
				return a.subgenre == b.maingenre;
			}
			return false;
		}
		return true;
	}

	public static void Weekly()
	{
		if (S.Competitors.Count == 0 || S.CompetitorsStale || Now >= S.CompetitorsNext)
		{
			Refresh();
		}
		else
		{
			S.Competitors.RemoveAll((int id) => !Open(IndustryCore.Studio(id)));
		}
		Clashes();
	}

	// One of your games is out: the market around you has moved, so pick again next week.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void Released(gameScript __instance)
	{
		if ((bool)__instance && __instance.IsMyGame())
		{
			S.CompetitorsStale = true;
		}
	}

	public static void Refresh()
	{
		S.CompetitorsStale = false;
		S.CompetitorsNext = Now + 52;
		List<int> before = S.Competitors.ToList();
		S.Competitors = (from x in Rank()
			select x.Key.myID).Take(Count).ToList();
		if (before.Count == 0)
		{
			return;
		}
		foreach (int id in S.Competitors.Where((int id) => !before.Contains(id)))
		{
			publisherScript p = IndustryCore.Studio(id);
			if ((bool)p)
			{
				IndustryCore.Feed(p.GetName() + " is now one of your competitors (" + Why(p) + ").");
			}
		}
	}

	// Your genres, weighted (1 = what you make most): your last five games out and everything you're making now.
	private static Dictionary<int, float> MyGenres()
	{
		Dictionary<int, float> w = new Dictionary<int, float>();
		void Add(int genre, float amount)
		{
			if (genre >= 0)
			{
				w[genre] = (w.TryGetValue(genre, out var had) ? had : 0f) + amount;
			}
		}
		foreach (gameScript g in (from x in IndustryCore.MyGamesOnMarket()
			where !x.typ_bundle && !x.typ_bundleAddon && !x.typ_goty && !x.typ_budget
			orderby x.date_year * 12 + x.date_month descending
			select x).Take(5))
		{
			Add(g.maingenre, 1f);
			Add(g.subgenre, 0.5f);
		}
		foreach (gameScript g2 in IndustryCore.MyGamesInDevelopment())
		{
			Add(g2.maingenre, 1.5f);
			Add(g2.subgenre, 0.75f);
		}
		float max = ((w.Count > 0) ? w.Values.Max() : 1f);
		return w.ToDictionary((KeyValuePair<int, float> kv) => kv.Key, (KeyValuePair<int, float> kv) => kv.Value / max);
	}

	// Each studio's games that count now: out within the last year, or coming.
	private static Dictionary<int, List<gameScript>> TheirGames()
	{
		Dictionary<int, List<gameScript>> by = new Dictionary<int, List<gameScript>>();
		foreach (gameScript g in IndustryCore.AllGames())
		{
			if (g.developerID >= 0 && !g.IsMyGame() && !g.typ_contractGame && !g.typ_bundle && !g.typ_bundleAddon && ((g.inDevelopment && !g.isOnMarket) || (g.isOnMarket && g.weeksOnMarket <= 52)))
			{
				if (!by.TryGetValue(g.developerID, out var list))
				{
					list = (by[g.developerID] = new List<gameScript>());
				}
				list.Add(g);
			}
		}
		return by;
	}

	private static bool Coming(gameScript g)
	{
		if (g.inDevelopment)
		{
			return !g.isOnMarket;
		}
		return false;
	}

	// The studios that meet you most in the market, best first.
	public static List<KeyValuePair<publisherScript, float>> Rank()
	{
		List<KeyValuePair<publisherScript, float>> scored = new List<KeyValuePair<publisherScript, float>>();
		Dictionary<int, float> mine = MyGenres();
		if (mine.Count == 0)
		{
			return scored;
		}
		float myStars = MyStars();
		Dictionary<int, List<gameScript>> theirs = TheirGames();
		foreach (publisherScript p in IndustryCore.Studios())
		{
			if (!p.developer || !theirs.TryGetValue(p.myID, out var games))
			{
				continue;
			}
			float overlap = 0f;
			foreach (gameScript g in games)
			{
				overlap += (mine.TryGetValue(g.maingenre, out var a) ? a : 0f) + 0.5f * (mine.TryGetValue(g.subgenre, out var b) ? b : 0f);
			}
			if (p.fanGenre >= 0 && mine.TryGetValue(p.fanGenre, out var fan))
			{
				overlap += 0.5f * fan;
			}
			overlap = Mathf.Min(overlap, 3f);
			if (overlap <= 0.01f)
			{
				continue;
			}
			float size = Mathf.Max(0.15f, 1f - Mathf.Abs(p.stars - myStars) / 40f);
			bool now = games.Any((gameScript g) => Coming(g) || g.weeksOnMarket <= 12);
			scored.Add(new KeyValuePair<publisherScript, float>(p, overlap * size * (now ? 1.3f : 1f)));
		}
		return scored.OrderByDescending((KeyValuePair<publisherScript, float> kv) => kv.Value).ToList();
	}

	private static int whyFrame = -1;

	private static Dictionary<int, float> whyMine;

	private static Dictionary<int, List<gameScript>> whyTheirs;

	// Why they're a competitor, short: "Strategy, Simulation · your size · game out now". The game scans are shared
	// by all the calls in one frame (the Industry window asks for every competitor at once).
	public static string Why(publisherScript p)
	{
		if (!p)
		{
			return "";
		}
		if (Time.frameCount != whyFrame || whyMine == null || whyTheirs == null)
		{
			whyFrame = Time.frameCount;
			whyMine = MyGenres();
			whyTheirs = TheirGames();
		}
		Dictionary<int, float> mine = whyMine;
		whyTheirs.TryGetValue(p.myID, out var games);
		games = games ?? new List<gameScript>();
		List<string> genres = (from g in games
			where mine.ContainsKey(g.maingenre)
			group g by g.maingenre into x
			orderby x.Count() descending
			select IndustryCore.GenreName(x.Key)).Take(2).ToList();
		if (genres.Count == 0 && p.fanGenre >= 0 && mine.ContainsKey(p.fanGenre))
		{
			genres.Add(IndustryCore.GenreName(p.fanGenre));
		}
		List<string> parts = new List<string>();
		if (genres.Count > 0)
		{
			parts.Add(string.Join(", ", genres));
		}
		float diff = p.stars - MyStars();
		parts.Add((Mathf.Abs(diff) <= 15f) ? "your size" : ((diff > 0f) ? "bigger than you" : "smaller than you"));
		if (games.Any((gameScript g) => g.isOnMarket && g.weeksOnMarket <= 12))
		{
			parts.Add("game out now");
		}
		else if (games.Any((gameScript g) => Coming(g) && g.angekuendigt))
		{
			parts.Add("game announced");
		}
		else if (games.Any(Coming))
		{
			parts.Add("game coming");
		}
		return string.Join("  ·  ", parts);
	}

	// ---- Release Clash: their game set for your week ----

	public static bool Clashing(gameScript g)
	{
		if ((bool)g)
		{
			return S.Clashes.Any((ReleaseClash c) => c.GameId == g.myID);
		}
		return false;
	}

	// Their game comes out in your game's week: an NPC studio releases when its countdown runs out.
	public static void SetForYourWeek(publisherScript p, int weeks)
	{
		if ((bool)p)
		{
			p.newGameInWeeks = Mathf.Max(1, weeks);
			p.newGameInWeeksORG = Mathf.Max(p.newGameInWeeksORG, p.newGameInWeeks);
		}
	}

	public static void AddClash(gameScript mine, gameScript theirs, publisherScript p, int edge)
	{
		if ((bool)mine && (bool)theirs && (bool)p)
		{
			S.Clashes.RemoveAll((ReleaseClash c) => c.GameId == mine.myID);
			S.Clashes.Add(new ReleaseClash
			{
				GameId = mine.myID,
				TheirGameId = theirs.myID,
				StudioId = p.myID,
				Edge = edge,
				Expires = Now + Mathf.Max(0, mine.releaseDate) + 12
			});
		}
	}

	private static bool Out(gameScript g)
	{
		if (g.isOnMarket && g.releaseDate <= 0)
		{
			return g.reviewTotal > 0;
		}
		return false;
	}

	// Once both games are out, the better-reviewed one wins the launch weeks.
	private static void Clashes()
	{
		foreach (ReleaseClash c in S.Clashes.ToList())
		{
			gameScript mine = GameUpdates.FindGame(c.GameId);
			gameScript theirs = GameUpdates.FindGame(c.TheirGameId);
			publisherScript p = IndustryCore.Studio(c.StudioId);
			if (!mine || !theirs || !p || Now > c.Expires)
			{
				S.Clashes.Remove(c);
				continue;
			}
			if (!Out(mine) || !Out(theirs))
			{
				continue;
			}
			S.Clashes.Remove(c);
			if (Mathf.Abs(mine.weeksOnMarket - theirs.weeksOnMarket) > 3)
			{
				continue;
			}
			int ours = mine.reviewTotal + c.Edge;
			string score = mine.reviewTotal + "%" + ((c.Edge > 0) ? (" + your marketing") : "") + " against " + theirs.reviewTotal + "%";
			string text;
			if (ours >= theirs.reviewTotal)
			{
				IndustryCore.AddMod(mine, 0.1f, 6);
				IndustryCore.AddMod(theirs, -0.1f, 6);
				IndustryCore.Change(p, -3, "Lost the release clash to " + mine.GetNameSimple());
				text = "Release clash: <b>" + mine.GetNameSimple() + "</b> beat " + p.GetName() + "'s " + theirs.GetNameSimple() + " (" + score + "). Players picked yours: sales +10% for 6 weeks.";
			}
			else
			{
				IndustryCore.AddMod(mine, -0.1f, 6);
				IndustryCore.AddMod(theirs, 0.1f, 6);
				IndustryCore.Change(p, -1, "Won the release clash against " + mine.GetNameSimple());
				text = "Release clash: " + p.GetName() + "'s <b>" + theirs.GetNameSimple() + "</b> beat " + mine.GetNameSimple() + " (" + score + "). Sales -10% for 6 weeks.";
			}
			IndustryCore.News(text);
			IndustryCore.Feed(text.Replace("<b>", "").Replace("</b>", ""));
		}
	}
}
