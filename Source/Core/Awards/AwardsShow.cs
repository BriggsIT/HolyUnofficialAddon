using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using HolyAddon.UI.Pages;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Awards;

// One nominee: a game or a company.
internal class AwardEntry
{
	public int GameId = -1;

	public int CompanyId = -1;

	public string Name = "";

	public string Studio = "";

	public string Stat = "";

	public float Score;

	public bool Mine;
}

// One category of one ceremony. Nominees[0] won.
internal class AwardResult
{
	public string Id = "";

	public string Name = "";

	public string About = "";

	public bool Dishonour;

	public List<AwardEntry> Nominees = new List<AwardEntry>();

	// What you got for winning it (empty if you didn't).
	public string Prize = "";
}

internal class AwardShowRecord
{
	public int Year;

	public List<AwardResult> Results = new List<AwardResult>();

	public string Summary = "";
}

internal class AwardsState
{
	public int LastYear = -1;

	public List<AwardShowRecord> Shows = new List<AwardShowRecord>();
}

// The MGH Games Awards (December, first week), rebuilt: 12 awards with up to five nominees each, plus the game's
// Worst Game of the Year, shown on a ceremony screen of the addon's instead of the game's.
//
// Balance: the game's own categories (Game of the Year, Best Graphics, Best Soundtrack, Developer and Publisher of
// the Year, Worst Game) keep exactly the game's prizes, records, achievements and Game of the Year Edition unlock.
// The new categories give smaller prizes, less again for a game that already won something that night, and
// everything you win in one ceremony is capped at what sweeping the game's own five awards could ever pay
// (fans, sales boost, studio points), so twelve categories never pay more than the game's six did.
[HarmonyPatch]
internal static class AwardsShow
{
	private const string SaveKey = "mgt2.editor.awards";

	private static AwardsState S = new AwardsState();

	public static AwardShowRecord Current;

	public static bool On
	{
		get
		{
			if (Plugin.AwardsCeremony.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static AwardsState State => S;

	// ---- Prizes ----

	private class Prize
	{
		public float FansShare;

		public int FansBase;

		public int FansCap;

		public float Sales;

		public int Studio;

		public int Ip;

		public int NpcIp;

		public int Hype;

		public int Interest;

		public bool Game;

		// One of the game's own categories: paid in full, as the game pays it.
		public bool Classic;
	}

	private static readonly Dictionary<string, Prize> Prizes = new Dictionary<string, Prize>
	{
		["goty"] = new Prize { FansShare = 0.05f, FansBase = 3000, FansCap = 40000, Sales = 0.1f, Studio = 75, Ip = 100, NpcIp = 100, Hype = 100, Game = true, Classic = true },
		["graphics"] = new Prize { FansShare = 0.02f, FansBase = 1000, FansCap = 40000, Sales = 0.05f, Studio = 40, Ip = 40, NpcIp = 40, Hype = 50, Game = true, Classic = true },
		["sound"] = new Prize { FansShare = 0.02f, FansBase = 1000, FansCap = 40000, Sales = 0.05f, Studio = 40, Ip = 40, NpcIp = 40, Hype = 50, Game = true, Classic = true },
		["developer"] = new Prize { FansShare = 0.045f, FansBase = 2500, FansCap = 40000, Sales = 0.2f, Studio = 100, Classic = true },
		["publisher"] = new Prize { FansShare = 0.04f, FansBase = 2500, FansCap = 40000, Sales = 0.2f, Studio = 100, Classic = true },
		["players"] = new Prize { FansShare = 0.02f, FansBase = 1000, FansCap = 20000, Sales = 0.04f, Studio = 30, Ip = 30, NpcIp = 30, Hype = 50, Game = true },
		["gameplay"] = new Prize { FansShare = 0.015f, FansBase = 750, FansCap = 15000, Sales = 0.03f, Studio = 30, Ip = 30, NpcIp = 30, Hype = 40, Game = true },
		["ongoing"] = new Prize { FansShare = 0.015f, FansBase = 750, FansCap = 15000, Sales = 0.02f, Studio = 30, Ip = 40, NpcIp = 40, Hype = 50, Interest = 15, Game = true },
		["controls"] = new Prize { FansShare = 0.01f, FansBase = 500, FansCap = 10000, Sales = 0.02f, Studio = 20, Ip = 20, NpcIp = 20, Hype = 25, Game = true },
		["debut"] = new Prize { FansShare = 0.01f, FansBase = 500, FansCap = 10000, Sales = 0.02f, Studio = 20, Ip = 20, NpcIp = 20, Hype = 30, Game = true },
		["indie"] = new Prize { FansShare = 0.01f, FansBase = 500, FansCap = 10000, Sales = 0.02f, Studio = 20, Ip = 20, NpcIp = 20, Hype = 30, Game = true },
		["family"] = new Prize { FansShare = 0.01f, FansBase = 500, FansCap = 10000, Sales = 0.02f, Studio = 20, Ip = 20, NpcIp = 20, Hype = 25, Game = true }
	};

	// The order prizes are paid in: the game's own categories first, so the ceremony caps never cut them.
	private static readonly string[] PayOrder = new string[12] { "goty", "graphics", "sound", "developer", "publisher", "players", "gameplay", "ongoing", "controls", "debut", "indie", "family" };

	// The order they're presented in (the big one last, as at real shows).
	private static readonly string[] ShowOrder = new string[13] { "debut", "indie", "family", "controls", "sound", "graphics", "gameplay", "ongoing", "players", "developer", "publisher", "worst", "goty" };

	public const float SalesCap = 0.6f;

	public const int StudioCap = 355;

	public const int NominationHype = 5;

	// ---- The ceremony ----

	// The game holds its awards in the first week of December (mainScript.MonatlicheUpdates).
	[HarmonyPrefix]
	[HarmonyPatch(typeof(mainScript), "MadGamesAward")]
	private static bool RunShow(mainScript __instance, bool force)
	{
		if (!On || (!force && (__instance.month != 12 || __instance.week != 1)))
		{
			return true;
		}
		if (!force && S.LastYear == __instance.year)
		{
			return false;
		}
		AwardShowRecord show;
		List<gameScript> pool;
		Dictionary<string, List<object>> picks;
		try
		{
			show = Judge(__instance, out pool, out picks);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Awards: judging failed, the game's own ceremony runs instead: " + ex);
			return true;
		}
		try
		{
			Pay(__instance, show, picks);
			Record(__instance, show, pool, picks);
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogError("Awards: handing out prizes failed: " + ex2);
		}
		S.LastYear = __instance.year;
		S.Shows.Add(show);
		Current = show;
		try
		{
			if ((bool)__instance.settings_ && __instance.settings_.hideAwards)
			{
				AwardEntry goty = show.Results.FirstOrDefault((AwardResult r) => r.Id == "goty")?.Nominees.FirstOrDefault();
				if ((bool)G.Gui && goty != null)
				{
					G.Gui.CreateTopNewsInfo("MGH Games Awards " + show.Year + ": Game of the Year goes to " + goty.Name + " (" + goty.Studio + ")");
				}
			}
			else
			{
				EditorWindow.Awards.Open();
				Sound(50);
			}
		}
		catch (Exception ex3)
		{
			Plugin.Log.LogError("Awards: showing the ceremony failed: " + ex3);
		}
		return false;
	}

	public static void Sound(int id)
	{
		try
		{
			GameObject sfx = GameObject.Find("SFX");
			if ((bool)sfx)
			{
				sfx.GetComponent<sfxScript>()?.PlaySound(id, force: false);
			}
		}
		catch (Exception)
		{
		}
	}

	// ---- Judging ----

	// The games the game's own awards look at: released since the last ceremony, full games only (Menu_Awards.CheckGame).
	private static bool Eligible(gameScript g)
	{
		if ((bool)g && !g.warBeiAwards && !g.inDevelopment && !g.schublade && !g.pubAngebot && !g.auftragsspiel && (g.typ_standard || g.typ_nachfolger || g.typ_remaster || g.typ_spinoff) && !g.typ_addon && !g.typ_addonStandalone && !g.typ_mmoaddon && !g.typ_bundle && !g.typ_budget && !g.typ_bundleAddon && !g.typ_goty)
		{
			return g.weeksOnMarket > 0;
		}
		return false;
	}

	private static bool BaseType(gameScript g)
	{
		if (g.typ_standard || g.typ_nachfolger || g.typ_remaster || g.typ_spinoff)
		{
			if (!g.typ_contractGame)
			{
				return !g.auftragsspiel;
			}
		}
		return false;
	}

	private static int ReleaseMonth(gameScript g)
	{
		return g.date_year * 12 + g.date_month;
	}

	private static AwardEntry GameEntry(gameScript g, float score, string stat)
	{
		return new AwardEntry
		{
			GameId = g.myID,
			Name = g.GetNameSimple(),
			Studio = g.GetDeveloperName(),
			Score = score,
			Stat = stat,
			Mine = (g.developerID == G.MS.myID)
		};
	}

	private static AwardEntry CompanyEntry(publisherScript p, float score, string stat)
	{
		return new AwardEntry
		{
			CompanyId = p.myID,
			Name = p.GetName(),
			Studio = "",
			Score = score,
			Stat = stat,
			Mine = (p.myID == G.MS.myID)
		};
	}

	private static string Pct(float v)
	{
		return Mathf.RoundToInt(v) + "%";
	}

	private static AwardResult Category(string id, string name, string about, IEnumerable<KeyValuePair<object, AwardEntry>> ranked, Dictionary<string, List<object>> picks, int take = 5)
	{
		List<KeyValuePair<object, AwardEntry>> top = ranked.Take(take).ToList();
		picks[id] = top.Select((KeyValuePair<object, AwardEntry> t) => t.Key).ToList();
		return new AwardResult
		{
			Id = id,
			Name = name,
			About = about,
			Dishonour = (id == "worst"),
			Nominees = top.Select((KeyValuePair<object, AwardEntry> t) => t.Value).ToList()
		};
	}

	private static IEnumerable<KeyValuePair<object, AwardEntry>> Rank(IEnumerable<gameScript> games, Func<gameScript, float> score, Func<gameScript, string> stat)
	{
		return from g in games
			let s = score(g)
			orderby s descending, g.reviewTotal descending, g.sellsTotal descending
			select new KeyValuePair<object, AwardEntry>(g, GameEntry(g, s, stat(g)));
	}

	private static AwardShowRecord Judge(mainScript ms, out List<gameScript> pool, out Dictionary<string, List<object>> picks)
	{
		gameScript[] all = ms.games_.arrayGamesScripts.Where((gameScript g) => (bool)g).ToArray();
		pool = all.Where(Eligible).ToList();
		picks = new Dictionary<string, List<object>>();
		AwardShowRecord show = new AwardShowRecord { Year = ms.year };
		List<gameScript> games = pool;
		// Each studio's first full game, by release month.
		Dictionary<int, int> firstRelease = new Dictionary<int, int>();
		foreach (gameScript g in all)
		{
			if (BaseType(g) && !g.inDevelopment && g.date_year > 0 && g.developerID >= 0)
			{
				int m = ReleaseMonth(g);
				if (!firstRelease.TryGetValue(g.developerID, out var first) || m < first)
				{
					firstRelease[g.developerID] = m;
				}
			}
		}
		List<AwardResult> results = new List<AwardResult>();
		results.Add(Category("goty", "Game of the Year", "The best game of the year: its review score and what players think of it.", Rank(games, (gameScript g) => (float)g.reviewTotal + g.GetUserReviewPercent(), (gameScript g) => "Rated " + g.reviewTotal + "%, players " + Pct(g.GetUserReviewPercent())), picks));
		results.Add(Category("gameplay", "Best Gameplay", "The highest gameplay score of the year.", Rank(games, (gameScript g) => g.reviewGameplay, (gameScript g) => "Gameplay " + g.reviewGameplay + "%"), picks));
		results.Add(Category("graphics", "Best Graphics", "The highest graphics score of the year.", Rank(games, (gameScript g) => g.reviewGrafik, (gameScript g) => "Graphics " + g.reviewGrafik + "%"), picks));
		results.Add(Category("sound", "Best Soundtrack", "The highest sound score of the year.", Rank(games, (gameScript g) => g.reviewSound, (gameScript g) => "Sound " + g.reviewSound + "%"), picks));
		results.Add(Category("controls", "Best Controls", "The highest controls score of the year.", Rank(games, (gameScript g) => g.reviewSteuerung, (gameScript g) => "Controls " + g.reviewSteuerung + "%"), picks));
		results.Add(Category("players", "Players' Choice", "The game players loved most: user reviews, and how many bought it.", Rank(games, (gameScript g) => g.GetUserReviewPercent() + 15f * Mathf.Log10(1f + (float)g.sellsTotal / 1000f), (gameScript g) => "Players " + Pct(g.GetUserReviewPercent()) + ", " + MmoUi.Short(g.sellsTotal) + " sold"), picks));
		results.Add(Category("debut", "Best Debut", "The best first game from a studio.", Rank(games.Where((gameScript g) => firstRelease.TryGetValue(g.developerID, out var first) && ReleaseMonth(g) <= first), (gameScript g) => g.reviewTotal, (gameScript g) => "Rated " + g.reviewTotal + "%"), picks));
		results.Add(Category("indie", "Best Independent Game", "The best small game (size A or smaller) its studio published itself.", Rank(games.Where((gameScript g) => g.publisherID == g.developerID && g.gameSize <= 2), (gameScript g) => g.reviewTotal, (gameScript g) => "Rated " + g.reviewTotal + "%, size " + MmoOps.SizeName(g.gameSize)), picks));
		results.Add(Category("family", "Best Family Game", "The best game for children or everyone, rated 12 or under.", Rank(games.Where((gameScript g) => (g.gameZielgruppe == 0 || g.gameZielgruppe == 4) && g.usk <= 2), (gameScript g) => g.reviewTotal, (gameScript g) => "Rated " + g.reviewTotal + "%, " + g.GetZielgruppeString()), picks));
		List<MmoAward.Entry> mmos = (MmoOps.On ? MmoAward.Judge() : new List<MmoAward.Entry>());
		results.Add(Category("ongoing", "Best Ongoing Game", "MMOs out for a year or more: players gained this year, new content, interest, rating and size.", mmos.Select((MmoAward.Entry e) => new KeyValuePair<object, AwardEntry>(e, GameEntry(e.Game, e.Score, MmoAward.Describe(e)))), picks));
		Companies(ms, pool, out var developers, out var publishers);
		results.Add(Category("developer", "Developer of the Year", "The studio whose games scored best this year.", developers, picks));
		results.Add(Category("publisher", "Publisher of the Year", "The publisher that sold the most good games this year.", publishers, picks));
		gameScript goty = (picks["goty"].Count > 0) ? ((gameScript)picks["goty"][0]) : null;
		results.Add(Category("worst", "Worst Game of the Year", "A dishonour: the lowest review score of the year, under 60%.", from g in games
			where g.reviewTotal < 60 && g != goty
			orderby g.reviewTotal
			select new KeyValuePair<object, AwardEntry>(g, GameEntry(g, g.reviewTotal, "Rated " + g.reviewTotal + "%")), picks));
		foreach (string id in ShowOrder)
		{
			AwardResult r = results.FirstOrDefault((AwardResult x) => x.Id == id);
			if (r != null)
			{
				show.Results.Add(r);
			}
		}
		return show;
	}

	// Developer and Publisher of the Year, scored as the game scores them: review points of the year's games for
	// studios, copies sold for publishers; your own games count only from 80%.
	private static void Companies(mainScript ms, List<gameScript> pool, out List<KeyValuePair<object, AwardEntry>> developers, out List<KeyValuePair<object, AwardEntry>> publishers)
	{
		Dictionary<publisherScript, long> dev = new Dictionary<publisherScript, long>();
		Dictionary<publisherScript, long> pub = new Dictionary<publisherScript, long>();
		foreach (gameScript g in pool)
		{
			if (!g.devS_)
			{
				g.FindMyDeveloper();
			}
			publisherScript d = g.devS_;
			if ((bool)d && (!d.isPlayer || g.reviewTotal >= 80))
			{
				dev[d] = (dev.TryGetValue(d, out var v) ? v : 0) + g.reviewTotal;
			}
			if (g.handy || g.gameTyp == 2)
			{
				continue;
			}
			if (!g.pS_)
			{
				g.FindMyPublisher();
			}
			publisherScript p = g.pS_;
			if ((bool)p && (!p.isPlayer || g.reviewTotal >= 80))
			{
				pub[p] = (pub.TryGetValue(p, out var v2) ? v2 : 0) + g.sellsTotal;
			}
		}
		developers = (from kv in dev
			where kv.Value > 0
			orderby kv.Value descending
			select new KeyValuePair<object, AwardEntry>(kv.Key, CompanyEntry(kv.Key, kv.Value, kv.Value.ToString("N0") + " review points"))).ToList();
		publishers = (from kv in pub
			where kv.Value > 0
			orderby kv.Value descending
			select new KeyValuePair<object, AwardEntry>(kv.Key, CompanyEntry(kv.Key, kv.Value, MmoUi.Short(kv.Value) + " copies sold"))).ToList();
	}

	// ---- Prizes ----

	private static int Fans(int myFans, Prize p, float share)
	{
		float fans = (float)myFans * p.FansShare;
		if (fans > (float)p.FansCap)
		{
			fans = p.FansCap + UnityEngine.Random.Range(0, p.FansCap / 4);
		}
		return Mathf.RoundToInt((fans + (float)p.FansBase) * share);
	}

	// The most a sweep of the game's own five awards could give you in fans (with its cap and random top-up).
	private static int ClassicFansCap(int myFans)
	{
		int total = 0;
		foreach (string id in new string[5] { "goty", "graphics", "sound", "developer", "publisher" })
		{
			Prize p = Prizes[id];
			total += Mathf.RoundToInt(Mathf.Min((float)myFans * p.FansShare, (float)(p.FansCap + p.FansCap / 4)) + (float)p.FansBase);
		}
		return total;
	}

	private static void Pay(mainScript ms, AwardShowRecord show, Dictionary<string, List<object>> picks)
	{
		int myFans = ms.genres_.GetAmountFans();
		int fansLeft = ClassicFansCap(myFans);
		float salesLeft = SalesCap;
		int studioLeft = StudioCap;
		Dictionary<gameScript, int> wins = new Dictionary<gameScript, int>();
		int totalFans = 0;
		int totalStudio = 0;
		float totalSales = 0f;
		ms.awardBonus = 0;
		ms.awardBonusAmount = 0f;
		foreach (string id in PayOrder)
		{
			AwardResult r = show.Results.FirstOrDefault((AwardResult x) => x.Id == id);
			if (r == null || !picks.TryGetValue(id, out var list) || list.Count == 0)
			{
				continue;
			}
			Prize p = Prizes[id];
			object winner = list[0];
			gameScript g = winner as gameScript ?? (winner as MmoAward.Entry)?.Game;
			publisherScript company = winner as publisherScript;
			bool mine = r.Nominees[0].Mine;
			// Diminishing returns: a game's second award of the night counts half, the third a third...
			float share = 1f;
			if ((bool)g)
			{
				int already = (wins.TryGetValue(g, out var n) ? n : 0);
				wins[g] = already + 1;
				if (!p.Classic)
				{
					share = 1f / (float)(already + 1);
				}
			}
			if ((bool)g)
			{
				g.AddHype((float)p.Hype * share);
				if (id == "goty")
				{
					g.goty = true;
				}
			}
			if (!mine)
			{
				if ((bool)g && g.OwnerIsNPC() && p.NpcIp > 0 && (bool)IpLinks.Holder(g))
				{
					g.AddIpPoints((float)p.NpcIp * share);
				}
				Count(ms, id, g, company, mine: false);
				continue;
			}
			int fans = Mathf.Min(Fans(myFans, p, share), Mathf.Max(0, fansLeft));
			float sales = Mathf.Min(p.Sales * share, Mathf.Max(0f, salesLeft));
			int studio = Mathf.Min(Mathf.RoundToInt((float)p.Studio * share), Mathf.Max(0, studioLeft));
			int ip = Mathf.RoundToInt((float)p.Ip * share);
			fansLeft -= fans;
			salesLeft -= sales;
			studioLeft -= studio;
			if (fans > 0)
			{
				ms.AddFans(fans, -1);
			}
			if (sales > 0f)
			{
				ms.awardBonus = 20;
				ms.awardBonusAmount += sales;
			}
			if (studio > 0)
			{
				ms.AddStudioPoints(studio);
			}
			if ((bool)g && ip > 0 && (bool)IpLinks.Holder(g))
			{
				g.AddIpPoints(ip);
			}
			if ((bool)g && p.Interest > 0)
			{
				g.AddMMOInteresse(p.Interest);
			}
			Count(ms, id, g, company, mine: true);
			totalFans += fans;
			totalStudio += studio;
			totalSales += sales;
			List<string> parts = new List<string>();
			if (fans > 0)
			{
				parts.Add("fans +" + ms.GetMoney(fans, showDollar: false));
			}
			if ((bool)g && p.Hype > 0)
			{
				parts.Add("hype +" + Mathf.RoundToInt((float)p.Hype * share));
			}
			if (sales > 0f)
			{
				parts.Add("+" + Mathf.RoundToInt(sales * 100f) + "% sales for all your games (20 weeks)");
			}
			if (studio > 0)
			{
				parts.Add("studio points +" + studio);
			}
			if (ip > 0)
			{
				parts.Add("IP points +" + ip);
			}
			if (p.Interest > 0)
			{
				parts.Add("interest +" + p.Interest);
			}
			if (id == "goty")
			{
				parts.Add("a Game of the Year Edition once it leaves the market");
			}
			r.Prize = string.Join(", ", parts);
		}
		// The dishonour, as the game hands it out.
		AwardResult worst = show.Results.FirstOrDefault((AwardResult x) => x.Id == "worst");
		if (worst != null && picks.TryGetValue("worst", out var bad) && bad.Count > 0)
		{
			gameScript g2 = (gameScript)bad[0];
			g2.AddHype(-50f);
			if (worst.Nominees[0].Mine)
			{
				int lost = Fans(myFans, new Prize { FansShare = 0.05f, FansBase = 2000, FansCap = 40000 }, 1f);
				ms.AddFans(-lost, -1);
				worst.Prize = "fans -" + ms.GetMoney(lost, showDollar: false) + ", hype -50";
				Achievement(ms, 40);
			}
			if (!g2.devS_)
			{
				g2.FindMyDeveloper();
			}
			ms.AddAwards(5, g2.devS_);
		}
		// Nominations get a little attention too.
		foreach (AwardResult r2 in show.Results.Where((AwardResult x) => !x.Dishonour))
		{
			foreach (AwardEntry e in r2.Nominees.Skip(1).Where((AwardEntry x) => x.Mine && x.GameId >= 0))
			{
				gameScript g3 = GameUpdates.FindGame(e.GameId);
				if ((bool)g3)
				{
					g3.AddHype(NominationHype);
				}
			}
		}
		int won = show.Results.Count((AwardResult x) => !x.Dishonour && x.Nominees.Count > 0 && x.Nominees[0].Mine);
		int nominated = show.Results.Sum((AwardResult x) => (!x.Dishonour) ? x.Nominees.Count((AwardEntry e) => e.Mine) : 0);
		show.Summary = (won > 0) ? ("You won " + won + ((won == 1) ? " award" : " awards") + " from " + nominated + ((nominated == 1) ? " nomination" : " nominations") + ": fans +" + ms.GetMoney(totalFans, showDollar: false) + ", +" + Mathf.RoundToInt(totalSales * 100f) + "% sales for all your games for 20 weeks, studio points +" + totalStudio + ".") : ((nominated > 0) ? ("You had " + nominated + ((nominated == 1) ? " nomination" : " nominations") + " but no wins this year.") : "None of your games were nominated this year.");
		if ((bool)G.Gui)
		{
			foreach (gameScript g4 in wins.Keys)
			{
				if ((bool)g4.gameTab_)
				{
					g4.gameTab_.UpdateData();
				}
			}
		}
	}

	// The game's own records for its categories: company award counts, your award counters and achievements.
	private static void Count(mainScript ms, string id, gameScript g, publisherScript company, bool mine)
	{
		if ((bool)g && !g.devS_)
		{
			g.FindMyDeveloper();
		}
		switch (id)
		{
		case "graphics":
			ms.AddAwards(0, g ? g.devS_ : null);
			if (mine)
			{
				ms.award_Grafik++;
				Achievement(ms, 38);
			}
			break;
		case "sound":
			ms.AddAwards(1, g ? g.devS_ : null);
			if (mine)
			{
				ms.award_Sound++;
				Achievement(ms, 39);
			}
			break;
		case "goty":
			ms.AddAwards(4, g ? g.devS_ : null);
			if (mine)
			{
				ms.award_GOTY++;
				Achievement(ms, 35);
			}
			break;
		case "developer":
			if ((bool)company)
			{
				company.awards[2]++;
			}
			if (mine)
			{
				ms.award_Studio++;
				Achievement(ms, 36);
			}
			break;
		case "publisher":
			if ((bool)company)
			{
				company.awards[3]++;
			}
			if (mine)
			{
				ms.award_Publisher++;
				Achievement(ms, 37);
			}
			break;
		}
	}

	private static void Achievement(mainScript ms, int id)
	{
		try
		{
			if ((bool)ms.achScript_)
			{
				ms.achScript_.SetAchivement(id);
			}
		}
		catch (Exception)
		{
		}
	}

	// What the game keeps about its ceremonies: its awards history (the game's six categories) and which games
	// have had their turn.
	private static void Record(mainScript ms, AwardShowRecord show, List<gameScript> pool, Dictionary<string, List<object>> picks)
	{
		int Id(string key)
		{
			if (!picks.TryGetValue(key, out var list) || list.Count == 0)
			{
				return -1;
			}
			if (list[0] is gameScript g)
			{
				return g.myID;
			}
			if (list[0] is publisherScript p)
			{
				return p.myID;
			}
			return -1;
		}
		ms.AddMadGameConvetionVerlauf(Id("graphics"), Id("sound"), Id("developer"), Id("publisher"), Id("goty"), Id("worst"));
		foreach (gameScript g in pool)
		{
			g.warBeiAwards = true;
		}
		if (MmoOps.On && picks.TryGetValue("ongoing", out var mmo))
		{
			MmoAward.Close(show.Year, mmo.OfType<MmoAward.Entry>().ToList());
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
			Plugin.Log.LogError("Saving awards failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new AwardsState();
		Current = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<AwardsState>(___es3file.Load<string>(SaveKey)) : null) ?? new AwardsState();
			S.Shows = S.Shows ?? new List<AwardShowRecord>();
			Current = S.Shows.LastOrDefault();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading awards failed: " + ex);
			S = new AwardsState();
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new AwardsState();
		Current = null;
	}
}
