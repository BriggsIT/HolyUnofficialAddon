using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Updates;

// One timed exclusive: developed for every chosen platform, released on one first, the rest follow later.
internal class TimedExclusive
{
	public int GameId;

	public int First;

	public int Months;

	// The platforms that come later, taken out of the game's slots at release.
	public List<int> Later = new List<int>();

	public bool Released;

	public int ReleaseWeek = -1;

	public int EndWeek = -1;

	public long Deal;
}

internal class TimedExclusiveState
{
	public List<TimedExclusive> Games = new List<TimedExclusive>();
}

// Timed exclusives, like the console makers' own: picked in the new-game screen (UI/TimedExclusiveMenu), the game
// is made for all its platforms but released on one of them first, as a real exclusive (the game's exclusive sales
// base, extra console sales, popularity). The platform's maker pays a deal at release, a share of the subsidy a full
// exclusive gets, by the window's length. When the window ends the game comes out on the others: like a finished
// port, with a launch boost on them.
[HarmonyPatch]
internal static class TimedExclusives
{
	private const string SaveKey = "mgt2.editor.timedexclusive";

	public static readonly int[] Windows = new int[5] { 3, 6, 12, 18, 24 };

	private static TimedExclusiveState S = new TimedExclusiveState();

	public static bool On
	{
		get
		{
			if (Plugin.TimedExclusives.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static IReadOnlyList<TimedExclusive> All => S.Games;

	public static TimedExclusive For(int gameId)
	{
		return S.Games.FirstOrDefault((TimedExclusive t) => t.GameId == gameId);
	}

	// Still waiting for its other platforms (in development, or released inside its window).
	public static TimedExclusive Active(gameScript g)
	{
		if (!g)
		{
			return null;
		}
		return For(g.myID);
	}

	// What share of a full exclusive's subsidy the maker pays, by window length.
	public static float DealShare(int months)
	{
		if (months <= 3)
		{
			return 0.15f;
		}
		if (months <= 6)
		{
			return 0.25f;
		}
		if (months <= 12)
		{
			return 0.4f;
		}
		if (months <= 18)
		{
			return 0.5f;
		}
		return 0.6f;
	}

	// The deal as a share of development costs (0 for your own platforms).
	public static float DealRate(platformScript p, int months)
	{
		if (!p || !p.OwnerIsNPC())
		{
			return 0f;
		}
		return p.GetExklusivBonus() / 100f * DealShare(months);
	}

	public static string Names(IEnumerable<int> ids)
	{
		return string.Join(", ", from id in ids
			select GameUpdates.Platform(id) into p
			where (bool)p
			select p.GetName());
	}

	// From the new-game screen: called once the game exists.
	public static void Register(gameScript g, int first, int months)
	{
		if (!g || first < 0 || !g.gamePlatform.Contains(first) || g.gamePlatform.Count((int id) => id != -1) < 2)
		{
			return;
		}
		S.Games.RemoveAll((TimedExclusive t) => t.GameId == g.myID);
		S.Games.Add(new TimedExclusive
		{
			GameId = g.myID,
			First = first,
			Months = months
		});
		GameUpdates.Touch();
		Plugin.Log.LogInfo("Timed exclusive: " + g.GetNameSimple() + " first on " + Names(new int[1] { first }) + " for " + months + " months");
	}

	public static void Cancel(gameScript g)
	{
		if ((bool)g && S.Games.RemoveAll((TimedExclusive t) => t.GameId == g.myID && !t.Released) > 0)
		{
			GameUpdates.Touch();
		}
	}

	// At release: take the later platforms out, so the game goes out as an exclusive on the first one.
	[HarmonyPrefix]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void BeforeRelease(gameScript __instance)
	{
		TimedExclusive t = Active(__instance);
		if (t == null || t.Released)
		{
			return;
		}
		try
		{
			gameScript g = __instance;
			t.Later.Clear();
			for (int i = 0; i < g.gamePlatform.Length; i++)
			{
				int id = g.gamePlatform[i];
				if (id != -1 && id != t.First)
				{
					t.Later.Add(id);
					g.gamePlatform[i] = -1;
					g.gamePlatformScript[i] = null;
				}
			}
			if (!g.gamePlatform.Contains(t.First) || t.Later.Count == 0)
			{
				Restore(g, t);
				S.Games.Remove(t);
				return;
			}
			// Keep the first platform in the first slot (the game card shows that one).
			int at = Array.IndexOf(g.gamePlatform, t.First);
			if (at > 0 && g.gamePlatform[0] == -1)
			{
				g.gamePlatform[0] = t.First;
				g.gamePlatform[at] = -1;
				g.gamePlatformScript[at] = null;
			}
			t.Released = true;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Timed exclusive release: " + ex);
		}
	}

	private static void Restore(gameScript g, TimedExclusive t)
	{
		foreach (int id in t.Later)
		{
			int free = Array.IndexOf(g.gamePlatform, -1);
			if (free >= 0)
			{
				g.gamePlatform[free] = id;
			}
		}
		t.Later.Clear();
	}

	// The game's own exclusive check at release only covers games made for one platform; make sure ours counts.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void AfterRelease(gameScript __instance)
	{
		TimedExclusive t = Active(__instance);
		if (t == null || !t.Released || __instance.exklusiv)
		{
			return;
		}
		gameScript g = __instance;
		if (!g.retro && !g.arcade && !g.handy)
		{
			g.exklusiv = true;
			g.herstellerExklusiv = false;
			platformScript p = GameUpdates.Platform(t.First);
			if ((bool)p)
			{
				p.exklusivGames++;
			}
		}
	}

	// ---- Weekly: the deal on release day, the other platforms when the window ends ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly()
	{
		if (!G.Singleplayer || S.Games.Count == 0)
		{
			return;
		}
		try
		{
			int now = CopyProtectDev.AbsWeek;
			foreach (TimedExclusive t in S.Games.ToList())
			{
				gameScript g = GameUpdates.FindGame(t.GameId);
				if (!g)
				{
					S.Games.Remove(t);
					continue;
				}
				if (!t.Released)
				{
					continue;
				}
				if (!g.isOnMarket)
				{
					S.Games.Remove(t);
					continue;
				}
				if (t.ReleaseWeek < 0 && g.releaseDate <= 0)
				{
					t.ReleaseWeek = now;
					t.EndWeek = now + Mathf.RoundToInt((float)t.Months * 52f / 12f);
					PayDeal(g, t);
				}
				if (t.EndWeek >= 0 && now >= t.EndWeek)
				{
					Finish(g, t);
					S.Games.Remove(t);
				}
			}
			GameUpdates.Touch();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Timed exclusives week: " + ex);
		}
	}

	private static void PayDeal(gameScript g, TimedExclusive t)
	{
		platformScript p = GameUpdates.Platform(t.First);
		float rate = DealRate(p, t.Months);
		if (rate <= 0f)
		{
			return;
		}
		long deal = (long)((float)GameUpdates.DevCost(g) * rate);
		if (deal <= 0)
		{
			return;
		}
		t.Deal = deal;
		G.MS.Earn(deal, 1);
		CopyProtectDev.Report("<b>" + g.GetNameWithTag() + "</b> is out as a " + t.Months + "-month " + p.GetName() + " exclusive.\n\n" + p.GetManufacturer() + " pays " + G.Money(deal) + " for it (" + Mathf.RoundToInt(rate * 100f) + "% of its development costs). It comes to " + Names(t.Later) + " when the deal ends.");
	}

	// The window is over: out on the other platforms (those still on the market), like a finished port.
	private static void Finish(gameScript g, TimedExclusive t)
	{
		List<int> coming = t.Later.Where((int id) => (bool)GameUpdates.Platform(id) && !GameUpdates.Platform(id).vomMarktGenommen && !g.gamePlatform.Contains(id)).ToList();
		if (coming.Count == 0)
		{
			return;
		}
		int[] after = (int[])g.gamePlatform.Clone();
		List<int> placed = new List<int>();
		foreach (int id in coming)
		{
			int free = Array.IndexOf(after, -1);
			if (free < 0)
			{
				break;
			}
			after[free] = id;
			placed.Add(id);
		}
		if (placed.Count == 0)
		{
			return;
		}
		float boost = GameUpdates.LaunchBoostFor(g, after);
		List<int> before = g.gamePlatform.Where((int id) => id != -1).ToList();
		for (int i = 0; i < g.gamePlatform.Length; i++)
		{
			g.gamePlatform[i] = after[i];
			g.gamePlatformScript[i] = null;
		}
		g.FindMyPlatforms();
		foreach (int id in placed)
		{
			platformScript p = GameUpdates.Platform(id);
			if ((bool)p)
			{
				p.games++;
				if (p.ownerID != G.MS.myID)
				{
					g.herstellerExklusiv = false;
				}
			}
		}
		if (g.exklusiv)
		{
			g.exklusiv = false;
			foreach (int id2 in before)
			{
				platformScript old = GameUpdates.Platform(id2);
				if ((bool)old && old.exklusivGames > 0)
				{
					old.exklusivGames--;
				}
			}
		}
		GameExtras extras = GameUpdates.Extras(g.myID, create: true);
		extras.LaunchBoost = boost;
		extras.LaunchWeek = CopyProtectDev.AbsWeek;
		g.AddHype(10f);
		if ((bool)g.gameTab_)
		{
			g.gameTab_.UpdateData();
		}
		platformScript first = GameUpdates.Platform(t.First);
		CopyProtectDev.Report("<b>" + g.GetNameWithTag() + "</b> is out now on " + Names(placed) + " after its " + t.Months + "-month " + (first ? first.GetName() : "") + " exclusivity.\n\nIt gets a launch boost of " + GameUpdates.Pct(boost) + " on them, fading over " + GameUpdates.LaunchWeeks + " weeks, and hype +10.");
		if ((bool)G.Gui)
		{
			G.Gui.AddHistory(g.GetNameSimple() + " is out on " + Names(placed) + ".");
		}
	}

	// The game card's tooltip says what's coming.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "GetTooltip")]
	private static void Tooltip(gameScript __instance, ref string __result)
	{
		try
		{
			TimedExclusive t = (G.Singleplayer ? Active(__instance) : null);
			if (t == null)
			{
				return;
			}
			platformScript first = GameUpdates.Platform(t.First);
			string firstName = (first ? first.GetName() : "?");
			if (!t.Released)
			{
				__result = __result + "\n<color=#1565c0>Timed exclusive: first on " + firstName + " for " + t.Months + " months.</color>";
			}
			else if (t.EndWeek >= 0)
			{
				int weeks = Mathf.Max(0, t.EndWeek - CopyProtectDev.AbsWeek);
				__result = __result + "\n<color=#1565c0>Timed " + firstName + " exclusive: comes to " + Names(t.Later) + " in " + ((weeks == 1) ? "1 week" : (weeks + " weeks")) + ".</color>";
			}
		}
		catch (Exception)
		{
		}
	}

	// ---- Saving ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "SaveHistory")]
	private static void Save(ES3Writer writer)
	{
		if (!G.Singleplayer)
		{
			return;
		}
		try
		{
			writer.Write<string>(SaveKey, JsonConvert.SerializeObject(S));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Saving timed exclusives failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new TimedExclusiveState();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<TimedExclusiveState>(___es3file.Load<string>(SaveKey)) : null) ?? new TimedExclusiveState();
			S.Games = S.Games ?? new List<TimedExclusive>();
			foreach (TimedExclusive t in S.Games)
			{
				t.Later = t.Later ?? new List<int>();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading timed exclusives failed: " + ex);
			S = new TimedExclusiveState();
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new TimedExclusiveState();
	}
}
