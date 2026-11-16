using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Landmarks;

// A landmark series of yours whose real games have run out (the user's design, 16 Nov 2026; plan doc
// Docs/Landmarks-Release2-Plan.md, "Series complete"):
// - Its real games have run out when each later game has come out (yours, or a studio's before the series was yours)
//   or its years have passed. Checked once a week.
// - A congratulations pop-up (the landmark pop-up, Showing "complete:<id>") says how many of its real games you made,
//   with extra praise for all of them.
// - From then on its new sequels and spin-offs are your own games. Sequels get no extra starting hype
//   (LandmarkTieIns.SequelHype), and there's no real game left to become. They show pictures from the series' real
//   games (PictureFor). The IP keeps its 1.5x value and the fan shop bonus.
internal static partial class Landmarks
{
	public const string CompletePrefix = "complete:";

	private static int seriesChecked = int.MinValue;

	private static void CheckSeriesDone()
	{
		try
		{
			int year = G.MS.year;
			foreach (LandmarkRecord r in YourSeries())
			{
				LandmarkDef d = Def(r.Id);
				if (d == null || d.Entries.Count == 0 || S.Complete.ContainsKey(d.Id) || !d.Entries.All((LandmarkEntry e) => EntryDone(e, year)))
				{
					continue;
				}
				S.Complete[d.Id] = year * 12 + G.MS.month;
				completeKey = -1;
				if (!S.Celebrate.Contains(CompletePrefix + d.Id))
				{
					S.Celebrate.Add(CompletePrefix + d.Id);
				}
				Tally(d, out var yours, out var made, out var total);
				IndustryCore.Feed("The real " + d.Name + " series has run its course: you made " + yours + " of its " + total + " games.");
				Plugin.Log.LogInfo("Landmarks: the " + d.Name + " series is complete (" + yours + " of " + total + " yours, " + made + " made)");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Landmark series check: " + ex.Message);
		}
	}

	// Out (by anyone), or its years have passed with nobody making it. One still being made isn't done.
	private static bool EntryDone(LandmarkEntry e, int year)
	{
		LandmarkRecord er = EntryRecord(e);
		if (er != null && er.Released)
		{
			return true;
		}
		if (er != null && !Scrapped(er))
		{
			return false;
		}
		return year > e.Year + Window;
	}

	// The series' real games (the landmark and its later games): how many are yours, how many came out at all.
	public static void Tally(LandmarkDef d, out int yours, out int made, out int total)
	{
		LandmarkRecord r = Record(d.Id);
		total = 1 + d.Entries.Count;
		yours = ((r != null && r.Player) ? 1 : 0);
		made = ((r != null && r.Released && !r.Missed) ? 1 : 0);
		foreach (LandmarkEntry e in d.Entries)
		{
			LandmarkRecord er = EntryRecord(e);
			if (er != null && er.Released)
			{
				made++;
				if (er.Player)
				{
					yours++;
				}
			}
		}
	}

	// The landmark whose series this game is in (its main IP is the landmark game), or null.
	internal static LandmarkDef SeriesDef(gameScript g)
	{
		if (!g)
		{
			return null;
		}
		gameScript main = ((g.mainIP >= 0 && g.mainIP != g.myID) ? GameUpdates.FindGame(g.mainIP) : g);
		return Of(main);
	}

	public static bool SeriesDone(gameScript g)
	{
		if ((bool)g && S.Complete.Count != 0)
		{
			LandmarkDef d = SeriesDef(g);
			if (d != null)
			{
				return S.Complete.ContainsKey(d.Id);
			}
		}
		return false;
	}

	// ---- Pictures for the series' later games ----

	private static int completeKey = -1;

	private static readonly Dictionary<int, LandmarkDef> completeMains = new Dictionary<int, LandmarkDef>();

	private static readonly Dictionary<string, List<string>> seriesPictures = new Dictionary<string, List<string>>();

	// A game started after its series was complete, and not one of its real games: a picture from the series'.
	private static Sprite LaterGamePicture(gameScript g)
	{
		if (S.Complete.Count == 0 || g.mainIP < 0)
		{
			return null;
		}
		int key = S.Complete.Count * 1000 + S.Made.Count;
		if (key != completeKey)
		{
			completeKey = key;
			completeMains.Clear();
			foreach (string id in S.Complete.Keys)
			{
				LandmarkRecord r = Record(id);
				gameScript main = ((r != null) ? SeriesOf(r) : null);
				LandmarkDef d = Def(id);
				if ((bool)main && d != null)
				{
					completeMains[main.myID] = d;
				}
			}
		}
		if (!completeMains.TryGetValue(g.mainIP, out var series) || !S.Complete.TryGetValue(series.Id, out var when) || g.date_start_year * 12 + g.date_start_month < when)
		{
			return null;
		}
		if (!seriesPictures.TryGetValue(series.Id, out var pool))
		{
			pool = new List<string>(Pictures(series));
			foreach (LandmarkEntry e in series.Entries)
			{
				pool.AddRange(EntryPictures(e));
			}
			seriesPictures[series.Id] = pool;
		}
		if (pool.Count != 0)
		{
			return LoadPicture(pool[(g.myID & 0x7FFFFFFF) % pool.Count]);
		}
		return null;
	}
}
