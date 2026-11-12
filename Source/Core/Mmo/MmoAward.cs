using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Mmo;

// "Best Ongoing Game" at the yearly Mad Games Awards (first week of December). The game's own awards judge each
// game once, in the year it came out; this one is for MMOs that have been running for a year or more, yours and
// your rivals'. It looks at the year since the last ceremony: subscribers gained, new content (updates, projects
// and MMO addons), interest, rating and size. Shown after the game's own awards.
[HarmonyPatch]
internal static class MmoAward
{
	public const int MinWeeks = 52;

	public const long MinSubs = 1000L;

	public const int Hype = 50;

	public const int Interest = 15;

	public const int IpPoints = 40;

	public const int StudioPoints = 40;

	private static string pending;

	internal class Entry
	{
		public gameScript Game;

		public float Score;

		public float Growth;

		public bool GrowthKnown;

		public int Updates;

		public int Addons;

		// Updates and projects count once, MMO addons twice.
		public int Content => Updates + 2 * Addons;
	}

	private static bool Contender(gameScript g)
	{
		if ((bool)g && g.gameTyp == 1 && !g.typ_mmoaddon && !g.typ_bundle && !g.typ_budget && !g.typ_goty && !g.typ_bundleAddon && g.isOnMarket && !g.inDevelopment && g.weeksOnMarket >= MinWeeks)
		{
			return g.abonnements >= MinSubs;
		}
		return false;
	}

	// This year's contenders, best first.
	public static List<Entry> Judge()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return new List<Entry>();
		}
		MmoState s = MmoOps.State;
		int year = G.MS.year;
		List<Entry> list = new List<Entry>();
		foreach (gameScript g in all.Where(Contender))
		{
			Entry e = new Entry
			{
				Game = g
			};
			long baseSubs = -1L;
			if (s.AwardBase.TryGetValue(g.myID, out var b) && b != null)
			{
				baseSubs = b.Subs;
				e.Updates = Math.Max(0, g.amountUpdates - b.Updates);
				e.Addons = Math.Max(0, g.amountMMOAddons - b.Addons);
			}
			else
			{
				// No numbers from last year (the first ceremony with the addon): your MMOs have a weekly record; for
				// everyone, MMO addons that came out this year count as new content.
				MmoGame m = MmoOps.Find(g.myID);
				if (m?.History != null && m.History.Count >= 52)
				{
					baseSubs = m.History[m.History.Count - 52];
				}
				e.Addons = MmoOps.Addons(g).Count((gameScript a) => a.date_year == year);
			}
			if (baseSubs >= 0)
			{
				e.GrowthKnown = true;
				e.Growth = Mathf.Clamp((float)(g.abonnements - baseSubs) / Mathf.Max(1000f, baseSubs), -1f, 1.5f);
			}
			e.Score = 30f * e.Growth + 0.3f * g.mmoInteresse + 0.25f * (float)g.reviewTotal + 8f * Mathf.Log10(1f + (float)g.abonnements / 1000f) + 4f * (float)Mathf.Min(6, e.Content);
			list.Add(e);
		}
		return list.OrderByDescending((Entry e) => e.Score).ToList();
	}

	// Numbers for next year's judging: every MMO on the market now.
	private static void Remember()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		MmoState s = MmoOps.State;
		s.AwardBase = new Dictionary<int, AwardBase>();
		if (all == null)
		{
			return;
		}
		foreach (gameScript g in all)
		{
			if ((bool)g && g.gameTyp == 1 && !g.typ_mmoaddon && g.isOnMarket && !g.inDevelopment)
			{
				s.AwardBase[g.myID] = new AwardBase
				{
					Subs = g.abonnements,
					Updates = g.amountUpdates,
					Addons = g.amountMMOAddons
				};
			}
		}
	}

	public static List<AwardWin> WinsOf(int gameId)
	{
		return MmoOps.State.Wins.Where((AwardWin w) => w.GameId == gameId).OrderBy((AwardWin w) => w.Year).ToList();
	}

	public static string WinsText(int gameId)
	{
		List<AwardWin> wins = WinsOf(gameId);
		if (wins.Count == 0)
		{
			return "not yet";
		}
		if (wins.Count <= 2)
		{
			return string.Join(", ", wins.Select((AwardWin w) => w.Year.ToString()));
		}
		// Short enough for the dashboard: how many, and the latest.
		return wins.Count + " wins, last " + wins.Max((AwardWin w) => w.Year);
	}

	public static AwardWin LastWin()
	{
		return MmoOps.State.Wins.OrderByDescending((AwardWin w) => w.Year).FirstOrDefault();
	}

	private static bool Mine(gameScript g)
	{
		return g.developerID == G.MS.myID || g.ownerID == G.MS.myID;
	}

	// The addon's own awards ceremony (Core/Awards/AwardsShow) judges this award as one of its categories and pays
	// its prize; this records the result here (the winner for the MMO's page, next year's numbers).
	public static void Close(int year, List<Entry> ranked)
	{
		MmoState s = MmoOps.State;
		s.AwardYear = year;
		Remember();
		if (ranked == null || ranked.Count == 0)
		{
			return;
		}
		gameScript g = ranked[0].Game;
		s.Wins.Add(new AwardWin
		{
			Year = year,
			GameId = g.myID,
			Game = g.GetNameSimple(),
			Studio = g.GetDeveloperName(),
			Mine = Mine(g)
		});
	}

	public static string Describe(Entry e)
	{
		return Why(e);
	}

	// The game opens its awards in the first week of December (mainScript.MadGamesAward). Only used with the game's
	// own ceremony screen (the addon's awards ceremony switched off).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "MadGamesAward")]
	private static void Ceremony(mainScript __instance)
	{
		if (!MmoOps.On || Awards.AwardsShow.On)
		{
			return;
		}
		try
		{
			GameObject menu = (((bool)G.Gui && G.Gui.uiObjects != null && G.Gui.uiObjects.Length > 143) ? G.Gui.uiObjects[143] : null);
			MmoState s = MmoOps.State;
			if (!menu || !menu.activeSelf || s.AwardYear == __instance.year)
			{
				return;
			}
			s.AwardYear = __instance.year;
			List<Entry> ranked = Judge();
			Remember();
			if (ranked.Count == 0)
			{
				return;
			}
			Entry win = ranked[0];
			gameScript g = win.Game;
			bool mine = Mine(g);
			s.Wins.Add(new AwardWin
			{
				Year = __instance.year,
				GameId = g.myID,
				Game = g.GetNameSimple(),
				Studio = g.GetDeveloperName(),
				Mine = mine
			});
			g.AddHype(Hype);
			string prize = "";
			if (mine)
			{
				float fans = (float)__instance.genres_.GetAmountFans() * 0.02f;
				if (fans > 40000f)
				{
					fans = 40000 + UnityEngine.Random.Range(0, 10000);
				}
				int newFans = Mathf.RoundToInt(fans + 1000f);
				__instance.AddFans(newFans, -1);
				__instance.AddStudioPoints(StudioPoints);
				g.AddMMOInteresse(Interest);
				if ((bool)MmoOps.IpOf(g))
				{
					g.AddIpPoints(IpPoints);
				}
				prize = "\n\nHype +" + Hype + ", interest +" + Interest + ", fans +" + G.MS.GetMoney(newFans, showDollar: false) + ", studio points +" + StudioPoints + ((bool)MmoOps.IpOf(g) ? (", IP points +" + IpPoints) : "") + ".";
			}
			else if (g.OwnerIsNPC() && (bool)MmoOps.IpOf(g))
			{
				g.AddIpPoints(IpPoints);
			}
			if ((bool)g.gameTab_)
			{
				g.gameTab_.UpdateData();
			}
			GameUpdates.Touch();
			string news = "Best Ongoing Game " + __instance.year + ": " + g.GetNameSimple() + " (" + g.GetDeveloperName() + ")";
			Entry myBest = ranked.FirstOrDefault((Entry e) => Mine(e.Game));
			if (myBest == null)
			{
				// You had no MMO in the running: a news line is enough.
				if ((bool)G.Gui)
				{
					G.Gui.CreateTopNewsInfo(news);
				}
				return;
			}
			string text = "<b>Best Ongoing Game " + __instance.year + "</b>\n\n";
			if (mine)
			{
				text += "<color=blue><b>" + g.GetNameWithTag() + "</b></color> wins this year's award for the best game still going strong. " + Why(win) + prize;
				G.Gui.AddHistory(news);
			}
			else
			{
				int place = ranked.IndexOf(myBest) + 1;
				text += "<b>" + g.GetNameWithTag() + "</b> [" + g.GetDeveloperName() + "] wins this year's award for the best game still going strong. " + Why(win) + "\n\nYour best: " + myBest.Game.GetNameSimple() + ", in " + Ordinal(place) + " place of " + ranked.Count + ". " + Why(myBest);
			}
			string others = string.Join("\n", from e in ranked.Skip(1).Take(2)
				select (ranked.IndexOf(e) + 1) + ". " + e.Game.GetNameSimple() + " [" + e.Game.GetDeveloperName() + "]");
			if (others.Length > 0)
			{
				text += "\n\nRunners-up:\n" + others;
			}
			text += "\n\nFor MMOs out a year or more, judged on the past year: players gained, new content, interest, rating and size.";
			pending = text;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Best Ongoing Game award: " + ex);
		}
	}

	private static string Why(Entry e)
	{
		List<string> parts = new List<string>();
		parts.Add(MmoUiText.Num(e.Game.abonnements) + " subscribers" + (e.GrowthKnown ? (" (" + ((e.Growth >= 0f) ? "+" : "") + Mathf.RoundToInt(e.Growth * 100f) + "% this year)") : ""));
		if (e.Updates > 0)
		{
			parts.Add(e.Updates + ((e.Updates == 1) ? " update" : " updates") + " this year");
		}
		if (e.Addons > 0)
		{
			parts.Add(e.Addons + ((e.Addons == 1) ? " MMO addon" : " MMO addons") + " this year");
		}
		parts.Add("interest " + Mathf.RoundToInt(e.Game.mmoInteresse));
		parts.Add("rated " + e.Game.reviewTotal + "%");
		return string.Join(", ", parts) + ".";
	}

	private static string Ordinal(int n)
	{
		int tens = n % 100;
		if (tens >= 11 && tens <= 13)
		{
			return n + "th";
		}
		return (n % 10) switch
		{
			1 => n + "st",
			2 => n + "nd",
			3 => n + "rd",
			_ => n + "th",
		};
	}

	// After the game's own awards close.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Awards), "BUTTON_Abbrechen")]
	private static void AwardsClosed(Menu_Awards __instance)
	{
		if (pending == null || __instance.gameObject.activeSelf)
		{
			return;
		}
		string text = pending;
		pending = null;
		CopyProtectDev.Report(text);
	}
}
