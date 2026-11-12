using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Store;

// One of your store's yearly sale events.
internal class StoreSaleEvent
{
	public string Id;

	public string Name;

	public int Month;

	public int Week;

	public int Weeks;

	public int From;

	public int DefaultDepth;
}

// Your store, phase 2: its own sale events, a front page with spots for your games or sold to studios, wishlists
// and early access for your games in development, your MMOs in the launcher, and storefronts on your consoles.
// All of it feeds the store model in StoreOps (appeal, active players, accounts, commission, your games' sales).
[HarmonyPatch]
internal static class StorePlus
{
	private static StoreState S => StoreOps.State;

	private static int Year => StoreOps.Year;

	private static int NowWeek => CopyProtectDev.AbsWeek;

	private static int MonthKey
	{
		get
		{
			if (!G.MS)
			{
				return 0;
			}
			return G.MS.year * 12 + G.MS.month;
		}
	}

	// ---- Sale events ----

	public static readonly StoreSaleEvent[] Events = new StoreSaleEvent[4]
	{
		new StoreSaleEvent
		{
			Id = "spring", Name = "Spring Sale", Month = 3, Week = 3, Weeks = 2, From = 2004, DefaultDepth = 1
		},
		// Summer and Winter run in every big store, on The Holy Expansion's dates (Core/Store/SalesEvents).
		new StoreSaleEvent
		{
			Id = "summer", Name = "Summer Sale", Month = 6, Week = 3, Weeks = 2, From = 2003, DefaultDepth = 2
		},
		new StoreSaleEvent
		{
			Id = "autumn", Name = "Autumn Sale", Month = 11, Week = 4, Weeks = 1, From = 2004, DefaultDepth = 1
		},
		new StoreSaleEvent
		{
			Id = "winter", Name = "Winter Sale", Month = 12, Week = 2, Weeks = 2, From = 2003, DefaultDepth = 2
		}
	};

	public static readonly string[] DepthNames = new string[4] { "Skip it", "Light: about 20% off", "Standard: about 40% off", "Deep: about 65% off" };

	public static readonly string[] DepthShort = new string[4] { "skipped", "light", "standard", "deep" };

	private static readonly float[] SaleAppeal = new float[4] { 1f, 1.08f, 1.15f, 1.25f };

	private static readonly float[] SaleActive = new float[4] { 1f, 1.15f, 1.3f, 1.45f };

	private static readonly float[] SaleUnits = new float[4] { 1f, 1.5f, 2.2f, 3.2f };

	private static readonly float[] SalePrice = new float[4] { 1f, 0.8f, 0.6f, 0.35f };

	private static readonly float[] SaleReputation = new float[4] { 0f, 3f, 6f, 9f };

	private static readonly float[] SaleTrust = new float[4] { 0f, 0f, 0.4f, 1.2f };

	private static readonly float[] SaleSignUps = new float[4] { 0f, 0.0015f, 0.003f, 0.006f };

	private static readonly float[] SaleOwn = new float[4] { 0f, 0.1f, 0.2f, 0.35f };

	private static int WeekOfYear(int month, int week)
	{
		return (month - 1) * 4 + (week - 1);
	}

	public static string Key(StoreSaleEvent e, int year)
	{
		return e.Id + "-" + year;
	}

	// The event running this week (by the calendar, whether or not your store joins it), and the year it belongs to.
	public static StoreSaleEvent Current(out int year)
	{
		year = 0;
		if (!G.MS)
		{
			return null;
		}
		int now = G.MS.year * 48 + WeekOfYear(G.MS.month, G.MS.week);
		foreach (StoreSaleEvent e in Events)
		{
			for (int y = G.MS.year - 1; y <= G.MS.year; y++)
			{
				int start = y * 48 + WeekOfYear(e.Month, e.Week);
				if (now >= start && now < start + e.Weeks)
				{
					year = y;
					return e;
				}
			}
		}
		return null;
	}

	public static int Depth(StoreSaleEvent e, int year)
	{
		if (S.SalePlans.TryGetValue(Key(e, year), out var d))
		{
			return Mathf.Clamp(d, 0, 3);
		}
		if (year < e.From)
		{
			return 0;
		}
		return e.DefaultDepth;
	}

	public static void SetDepth(StoreSaleEvent e, int year, int depth)
	{
		S.SalePlans[Key(e, year)] = Mathf.Clamp(depth, 0, 3);
		GameUpdates.Touch();
	}

	// How deep your store's sale goes this week (0 = none).
	public static int DepthNow()
	{
		if (!S.Built)
		{
			return 0;
		}
		StoreSaleEvent e = Current(out int y);
		if (e == null || y < e.From)
		{
			return 0;
		}
		return Depth(e, y);
	}

	public static float AppealFactor()
	{
		return SaleAppeal[DepthNow()];
	}

	public static float ActiveFactor()
	{
		return SaleActive[DepthNow()];
	}

	// Units and price of other studios' games sold through your store during a sale.
	public static float CommissionFactor()
	{
		int d = DepthNow();
		return SaleUnits[d] * SalePrice[d];
	}

	public static float ReputationBonus()
	{
		return SaleReputation[DepthNow()];
	}

	// The big rival stores hold Summer and Winter sales too: buyers drift their way unless yours runs.
	public static bool RivalSaleNow()
	{
		StoreSaleEvent e = Current(out int _);
		if (e != null)
		{
			if (!(e.Id == "summer"))
			{
				return e.Id == "winter";
			}
			return true;
		}
		return false;
	}

	// The next year of events, soonest first: event, year, weeks until it starts (0 = running).
	public static List<KeyValuePair<StoreSaleEvent, int>> Upcoming(out List<int> weeksAway)
	{
		weeksAway = new List<int>();
		List<KeyValuePair<StoreSaleEvent, int>> list = new List<KeyValuePair<StoreSaleEvent, int>>();
		if (!G.MS)
		{
			return list;
		}
		int now = G.MS.year * 48 + WeekOfYear(G.MS.month, G.MS.week);
		List<Tuple<StoreSaleEvent, int, int>> all = new List<Tuple<StoreSaleEvent, int, int>>();
		foreach (StoreSaleEvent e in Events)
		{
			for (int y = G.MS.year - 1; y <= G.MS.year + 1; y++)
			{
				int start = y * 48 + WeekOfYear(e.Month, e.Week);
				if (y >= e.From && start + e.Weeks > now && start - now < 48)
				{
					all.Add(Tuple.Create(e, y, Mathf.Max(0, start - now)));
				}
			}
		}
		foreach (Tuple<StoreSaleEvent, int, int> t in all.OrderBy((Tuple<StoreSaleEvent, int, int> t) => t.Item3))
		{
			list.Add(new KeyValuePair<StoreSaleEvent, int>(t.Item1, t.Item2));
			weeksAway.Add(t.Item3);
		}
		return list;
	}

	public static StoreSaleRecord Record(string id)
	{
		return S.SaleHistory.LastOrDefault((StoreSaleRecord r) => r.Id == id);
	}

	// Starts or ends your store's sale to match the calendar and its depth (also right after the sale pop-up's answer).
	public static void SyncSale()
	{
		if (!S.Built)
		{
			return;
		}
		StoreSaleEvent e = Current(out int y);
		int depth = ((e != null) ? DepthNow() : 0);
		string id = ((e != null && depth > 0) ? Key(e, y) : null);
		if (id == S.SaleNow)
		{
			StoreSaleRecord same = ((id != null) ? Record(id) : null);
			if (same != null)
			{
				same.Depth = depth;
			}
			return;
		}
		if (S.SaleNow != null)
		{
			StoreSaleRecord done = Record(S.SaleNow);
			// Called off in the week it started (the pop-up's answer): it never ran.
			if (done != null && done.StartWeek == NowWeek && done.Commission == 0 && done.Accounts == 0)
			{
				S.SaleHistory.Remove(done);
			}
			else if (done != null)
			{
				StoreOps.News("<b>" + S.Name + "</b>'s " + done.Name + " is over: " + G.Money(done.Commission) + " in commission, " + MmoUiText.Num(done.Accounts) + " new accounts.");
			}
		}
		if (id != null)
		{
			StoreSaleRecord again = Record(id);
			if (again != null)
			{
				again.Depth = depth;
			}
			else
			{
				S.SaleHistory.Add(new StoreSaleRecord
				{
					Id = id,
					Name = e.Name,
					Year = y,
					Depth = depth,
					StartWeek = NowWeek
				});
				if (S.SaleHistory.Count > 40)
				{
					S.SaleHistory.RemoveAt(0);
				}
				// With sales events on, the sale's pop-up (or its answer in the news) says it instead.
				if (!SalesEvents.On)
				{
					StoreOps.News("<b>" + S.Name + "</b>'s " + e.Name + " starts: " + DepthNames[depth].ToLower() + " for " + e.Weeks + ((e.Weeks == 1) ? " week." : " weeks."));
				}
			}
		}
		S.SaleNow = id;
	}

	private static void SaleWeek()
	{
		SyncSale();
		if (S.SaleNow == null)
		{
			return;
		}
		int depth = DepthNow();
		long signups = (long)((float)StoreOps.DigitalPcPlayers() * SaleSignUps[depth]);
		S.Accounts += signups;
		StoreSaleRecord r = Record(S.SaleNow);
		if (r != null)
		{
			r.Accounts += signups;
		}
		S.Trust = Mathf.Clamp(S.Trust - SaleTrust[depth], 0f, 100f);
	}

	// Commission earned during a sale goes on its record.
	public static void NoteCommission(long amount)
	{
		if (S.SaleNow != null)
		{
			StoreSaleRecord r = Record(S.SaleNow);
			if (r != null)
			{
				r.Commission += amount;
			}
		}
	}

	// ---- Front page ----

	public const int Slots = 4;

	public static readonly string[] SlotNames = new string[4] { "Featured", "Spotlight 1", "Spotlight 2", "Spotlight 3" };

	public static void EnsureSlots()
	{
		while (S.FrontPage.Count < Slots)
		{
			S.FrontPage.Add(new FrontSlot());
		}
	}

	public static long SlotPrice(int i)
	{
		long price = (long)((float)S.Active * ((i == 0) ? 0.008f : 0.003f));
		return Math.Max(500L, price / 100 * 100);
	}

	private static float SlotWeight(int i)
	{
		if (i != 0)
		{
			return 0.5f;
		}
		return 1f;
	}

	public static bool SlotGameOk(FrontSlot slot)
	{
		gameScript g = GameUpdates.FindGame(slot.GameId);
		if (!g || !g.isOnMarket)
		{
			return false;
		}
		if (slot.Mode == 1)
		{
			return g.IsMyGame() && StoreOps.PcFraction(g) > 0f;
		}
		if (slot.Mode == 2)
		{
			return StoreOps.OthersGame(g);
		}
		return false;
	}

	// Your games that can go on the front page.
	public static List<gameScript> FrontCandidates()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : new gameScript[0]);
		return (from g in all
			where (bool)g && g.isOnMarket && g.IsMyGame() && g.gameTyp != 2 && !g.arcade && !g.handy && !g.typ_bundle && StoreOps.PcFraction(g) > 0f
			orderby g.weeksOnMarket
			select g).ToList();
	}

	public static string SetSlot(int i, int mode, int gameId)
	{
		EnsureSlots();
		if (i < 0 || i >= Slots)
		{
			return null;
		}
		FrontSlot slot = S.FrontPage[i];
		if (slot.Mode == 2 && mode != 2)
		{
			return "That spot is sold to " + StudioName(slot.StudioId) + " until the end of the month.";
		}
		slot.Mode = mode;
		slot.GameId = gameId;
		slot.StudioId = -1;
		slot.Until = -1;
		GameUpdates.Touch();
		return null;
	}

	public static string StudioName(int id)
	{
		publisherScript p = MmoBots.FindStudio(id);
		if (!p)
		{
			return "a studio";
		}
		return p.GetName();
	}

	private static void FrontWeek(bool monthly)
	{
		EnsureSlots();
		int month = MonthKey;
		List<gameScript> others = null;
		for (int i = 0; i < Slots; i++)
		{
			FrontSlot slot = S.FrontPage[i];
			if (slot.Mode != 0 && (!SlotGameOk(slot) || (slot.Mode == 2 && month > slot.Until)))
			{
				slot.Mode = 0;
				slot.GameId = -1;
				slot.StudioId = -1;
			}
			if (slot.Mode == 0 && monthly && S.SellSlots && UnityEngine.Random.value < 0.25f + S.Trust / 100f * 0.6f)
			{
				others = others ?? (from g in StoreOps.OthersGames()
					where g.sellsPerWeek != null && g.sellsPerWeek.Length != 0 && (!S.Signing || IsSigned(StudioOf(g)))
					orderby g.sellsPerWeek[0] descending
					select g).Take(30).ToList();
				gameScript pick = others.FirstOrDefault((gameScript g) => !S.FrontPage.Any((FrontSlot x) => x.GameId == g.myID));
				if ((bool)pick)
				{
					slot.Mode = 2;
					slot.GameId = pick.myID;
					slot.StudioId = ((pick.publisherID >= 0) ? pick.publisherID : pick.developerID);
					slot.Until = month;
				}
			}
			if (slot.Mode == 2)
			{
				long pay = SlotPrice(i);
				G.MS.Earn(pay, 2);
				S.PlacementMonth += pay;
				S.PlacementTotal += pay;
			}
		}
	}

	private static int SlotOf(gameScript g, int mode)
	{
		if (!g)
		{
			return -1;
		}
		for (int i = 0; i < S.FrontPage.Count; i++)
		{
			if (S.FrontPage[i].Mode == mode && S.FrontPage[i].GameId == g.myID)
			{
				return i;
			}
		}
		return -1;
	}

	// More of a studio's game sells through your store while it's on the front page.
	public static float FrontCommission(gameScript g)
	{
		int i = SlotOf(g, 2);
		if (i < 0)
		{
			return 1f;
		}
		return 1f + SlotWeight(i);
	}

	public static int SoldSlots()
	{
		return S.FrontPage.Count((FrontSlot s) => s.Mode == 2);
	}

	// ---- Wishlists and early access ----

	public const float EarlyAccessFrom = 40f;

	public const int LaunchWeeks = 8;

	public static StoreGame Data(gameScript g, bool create = false)
	{
		if (!g)
		{
			return null;
		}
		if (!S.Games.TryGetValue(g.myID, out var d) && create)
		{
			d = new StoreGame();
			S.Games[g.myID] = d;
		}
		return d;
	}

	private static bool BaseKind(gameScript g)
	{
		if (!g.typ_bundle && !g.typ_bundleAddon && !g.typ_budget && !g.typ_goty && !g.typ_contractGame && !g.pubOffer)
		{
			return !g.arcade && !g.handy;
		}
		return false;
	}

	// Your games in development that PC players can wishlist on your store.
	public static List<gameScript> InDevelopment()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : new gameScript[0]);
		return all.Where((gameScript g) => (bool)g && g.inDevelopment && g.ownerID == G.MS.myID && g.developerID == G.MS.myID && g.gameTyp != 2 && BaseKind(g) && StoreOps.PcFraction(g) > 0f).ToList();
	}

	public static string EarlyAccessProblem(gameScript g)
	{
		if (!S.Built)
		{
			return "Build your store first.";
		}
		if (!g || !g.inDevelopment)
		{
			return "Only games in development can be in early access.";
		}
		if (g.gameTyp != 0 || g.typ_mmoaddon || g.typ_addon || g.typ_addonStandalone)
		{
			return "Early access is for new games (not MMOs or addons).";
		}
		float done = ((g.devPointsStart_Gesamt > 0.01f) ? Mathf.Clamp(g.GetProzentGesamt(), 0f, 100f) : 0f);
		if (done < EarlyAccessFrom)
		{
			return "It needs to be at least " + Mathf.RoundToInt(EarlyAccessFrom) + "% done (" + Mathf.FloorToInt(done) + "% now).";
		}
		return null;
	}

	public static string SetEarlyAccess(gameScript g, bool on)
	{
		string problem = (on ? EarlyAccessProblem(g) : null);
		if (problem != null)
		{
			return problem;
		}
		StoreGame d = Data(g, create: true);
		if (d.EarlyAccess == on)
		{
			return null;
		}
		d.EarlyAccess = on;
		if (on && d.EaWeek < 0)
		{
			d.EaWeek = NowWeek;
			StoreOps.News("<b>" + g.GetNameSimple() + "</b> is out in early access on " + S.Name + ".");
		}
		GameUpdates.Touch();
		return null;
	}

	public static int EarlyAccessPrice(gameScript g)
	{
		return Mathf.Max(5, Mathf.RoundToInt((float)StoreOps.Price(g) * 0.7f));
	}

	private static void WishWeek()
	{
		foreach (gameScript g in InDevelopment())
		{
			StoreGame d = Data(g, create: true);
			float pc = StoreOps.PcFraction(g);
			float hype = Mathf.Clamp(g.hype, 0f, 100f) / 100f;
			long growth = (long)((float)S.Active * 0.0015f * (0.2f + hype) * pc);
			long cap = (long)((float)S.Active * 0.6f);
			d.Wishlists = Math.Max(d.Wishlists, Math.Min(cap, d.Wishlists + growth));
			if (!d.EarlyAccess)
			{
				continue;
			}
			if (EarlyAccessProblem(g) != null)
			{
				d.EarlyAccess = false;
				continue;
			}
			long units = (long)((float)S.Active * 0.0008f * (0.3f + hype) * pc * (1f + Mathf.Min(1f, (float)d.Wishlists / Mathf.Max(1f, S.Active))));
			long revenue = units * EarlyAccessPrice(g);
			if (revenue > 0)
			{
				G.MS.Earn(revenue, 3);
				S.EarlyAccessTotal += revenue;
			}
			d.EaUnits += units;
			d.EaRevenue += revenue;
			// Early access players find bugs nobody had seen yet; the team fixes them before release.
			g.points_bugsInvis = Mathf.Max(0f, g.points_bugsInvis * 0.95f);
			g.AddHype(0.3f);
		}
	}

	// At release: early access buyers count as sold, wishlists turn into a launch boost on your store.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void Released(gameScript __instance)
	{
		try
		{
			if (!StoreOps.On || !S.Built || !S.Games.TryGetValue(__instance.myID, out var d) || d.Released)
			{
				return;
			}
			gameScript g = __instance;
			d.Released = true;
			d.EarlyAccess = false;
			if (d.EaUnits > 0)
			{
				g.sellsTotal += d.EaUnits;
			}
			float bought = Mathf.Min(0.5f, (float)d.EaUnits / Mathf.Max(1f, d.Wishlists));
			d.LaunchBoost = Mathf.Clamp((float)d.Wishlists / Mathf.Max(20000f, S.Active) * 0.8f * (1f - bought), 0f, 0.35f);
			d.LaunchWeek = NowWeek;
			if (d.Wishlists > 0)
			{
				StoreOps.News("<b>" + g.GetNameSimple() + "</b> launches with " + MmoUiText.Num(d.Wishlists) + " wishlists on " + S.Name + ": PC sales +" + Mathf.RoundToInt(d.LaunchBoost * 100f) + "% for its first " + LaunchWeeks + " weeks." + ((d.EaUnits > 0) ? (" " + MmoUiText.Num(d.EaUnits) + " early access players already have it.") : ""));
			}
			GameUpdates.Touch();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Store release: " + ex.Message);
		}
	}

	public static float LaunchNow(StoreGame d)
	{
		if (d == null)
		{
			return 0f;
		}
		int weeks = NowWeek - d.LaunchWeek;
		if (weeks < 0 || weeks >= LaunchWeeks)
		{
			return 0f;
		}
		return d.LaunchBoost * (1f - (float)weeks / (float)LaunchWeeks);
	}

	// What phase 2 adds to one of your PC games' sales: launch boost, front page, the store's sale.
	public static float GameFactor(gameScript g, float pc)
	{
		float f = 1f + LaunchNow(Data(g)) * pc;
		int slot = SlotOf(g, 1);
		if (slot >= 0)
		{
			f += 0.5f * SlotWeight(slot) * S.Share * pc;
		}
		// With sales events on, your games' own discount does this (Core/Store/SalesEvents), not the store's sale.
		if (!SalesEvents.On)
		{
			f += SaleOwn[DepthNow()] * S.Share * pc;
		}
		return f;
	}

	// ---- Your MMOs in the launcher ----

	public static bool InLauncher(gameScript g)
	{
		if (!S.Built || !g || g.gameTyp != 1 || StoreOps.PcFraction(g) <= 0f)
		{
			return false;
		}
		if (S.Launcher.TryGetValue(g.myID, out var on))
		{
			return on;
		}
		return true;
	}

	public static void SetLauncher(gameScript g, bool on)
	{
		S.Launcher[g.myID] = on;
		GameUpdates.Touch();
	}

	// Players find your MMOs through your store, and the launcher keeps them patched and playing.
	public static float MmoFactor(gameScript g)
	{
		if (!InLauncher(g))
		{
			return 1f;
		}
		return 1f + 0.03f + 0.25f * S.Share * StoreOps.PcFraction(g);
	}

	private static void LauncherWeek()
	{
		long total = 0L;
		foreach (gameScript g in MmoOps.MyMmos())
		{
			if (InLauncher(g))
			{
				total += (long)((float)g.abonnements * StoreOps.PcFraction(g) * 0.9f);
			}
		}
		S.LauncherAccounts = total;
	}

	// ---- Console storefronts ----

	public const int ConsoleYear = 2005;

	public static List<platformScript> MyConsoles()
	{
		platformScript[] all = (G.MS ? G.MS.arrayPlatformsScripts : null);
		if (all == null)
		{
			return new List<platformScript>();
		}
		return all.Where((platformScript p) => (bool)p && p.ownerID == G.MS.myID && p.typ != 0 && p.isUnlocked && !p.vomMarktGenommen).ToList();
	}

	public static ConsoleStore ConsoleStoreFor(int platformId)
	{
		return S.Consoles.FirstOrDefault((ConsoleStore c) => c.PlatformId == platformId);
	}

	public static string ConsoleProblem(platformScript p)
	{
		if (!S.Built)
		{
			return "Build your PC store first.";
		}
		if (Year < ConsoleYear)
		{
			return "Console storefronts become possible in " + ConsoleYear + ".";
		}
		if (!p)
		{
			return "Pick one of your consoles.";
		}
		if (ConsoleStoreFor(p.myID) != null)
		{
			return p.GetName() + " already has your storefront.";
		}
		if (!p.internet)
		{
			return p.GetName() + " has no internet: players can't download anything on it.";
		}
		if (p.vomMarktGenommen)
		{
			return p.GetName() + " is off the market.";
		}
		return null;
	}

	public static UpdatePlan PlanConsole(platformScript p)
	{
		return new UpdatePlan
		{
			Problem = ConsoleProblem(p),
			Work = 220f,
			Cost = (long)Mathf.Round(400000f * (1f + (float)Mathf.Max(0, Year - ConsoleYear) * 0.05f) / 1000f) * 1000
		};
	}

	public static string CompleteConsole(UpdateProject p)
	{
		platformScript c = GameUpdates.Platform(p.PlatformId);
		if (!c || ConsoleStoreFor(p.PlatformId) != null)
		{
			return null;
		}
		S.Consoles.Add(new ConsoleStore
		{
			PlatformId = p.PlatformId,
			BuiltWeek = NowWeek
		});
		GameUpdates.Touch();
		return "<b>" + c.GetName() + "</b> now has " + S.Name + " built in.\n\nPlayers buy and download games right on the console. Other studios' digital sales on it pay you " + ConsoleCommission + "%.";
	}

	public const int ConsoleCommission = 30;

	// Digital buyers on consoles: they came later than on PC.
	public static float ConsoleDigital()
	{
		return StoreOps.DigitalShare() * Mathf.Clamp((float)(Year - ConsoleYear) / 8f + 0.25f, 0.25f, 1f);
	}

	// How much of a game's market this platform is (by market share among its platforms).
	private static float PlatformFraction(gameScript g, platformScript p)
	{
		float all = 0f;
		float mine = 0f;
		for (int i = 0; i < g.gamePlatform.Length; i++)
		{
			platformScript x = GameUpdates.Platform(g.gamePlatform[i]);
			if ((bool)x)
			{
				float s = Mathf.Max(0.1f, x.GetMarktanteil());
				all += s;
				if (x == p)
				{
					mine += s;
				}
			}
		}
		if (all <= 0f)
		{
			return 0f;
		}
		return mine / all;
	}

	public static long ConsoleCommissionWeek(platformScript p)
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null || !p)
		{
			return 0L;
		}
		float digital = ConsoleDigital();
		long total = 0L;
		foreach (gameScript g in all)
		{
			if ((bool)g && g.isOnMarket && !g.IsMyGame() && g.gamePlatform.Contains(p.myID) && g.sellsPerWeek != null && g.sellsPerWeek.Length != 0 && g.sellsPerWeek[0] > 0)
			{
				float units = (float)g.sellsPerWeek[0] * PlatformFraction(g, p) * digital;
				total += (long)(units * (float)StoreOps.Price(g) * (float)ConsoleCommission / 100f);
			}
		}
		return total;
	}

	private static void ConsoleWeek()
	{
		foreach (ConsoleStore c in S.Consoles)
		{
			platformScript p = GameUpdates.Platform(c.PlatformId);
			if (!p || p.vomMarktGenommen)
			{
				continue;
			}
			long pay = ConsoleCommissionWeek(p);
			if (pay > 0)
			{
				G.MS.Earn(pay, 2);
				c.CommissionTotal += pay;
				S.ConsoleMonth += pay;
				S.ConsoleTotal += pay;
			}
		}
	}

	// ---- Numbers for the store's pages ----

	// Copies of a game sold through your store this week (other studios' games by your catalogue's coverage).
	public static long StoreUnits(gameScript g)
	{
		if (!S.Built || !g || g.sellsPerWeek == null || g.sellsPerWeek.Length == 0 || g.sellsPerWeek[0] <= 0)
		{
			return 0L;
		}
		float pc = StoreOps.PcFraction(g);
		float units = (float)g.sellsPerWeek[0] * pc * StoreOps.DigitalShare();
		if (g.IsMyGame())
		{
			units *= (StoreOps.IsExclusive(g) ? 1f : S.Share);
		}
		else
		{
			units *= S.Share * CoverageFor(g) * SaleUnits[DepthNow()] * FrontCommission(g);
		}
		return (long)units;
	}

	// ---- Studios signing with your store ----

	// Your signing fee's choices: what a studio pays you once to sell its games on your store.
	public static readonly long[] FeeSteps = new long[9] { 0L, 25000L, 100000L, 250000L, 500000L, 1000000L, 2500000L, 5000000L, 10000000L };

	public const int MaxRequests = 8;

	public static int StudioOf(gameScript g)
	{
		if (!g)
		{
			return -1;
		}
		if (g.publisherID < 0)
		{
			return g.developerID;
		}
		return g.publisherID;
	}

	private static bool OwnSubsidiary(int studio)
	{
		publisherScript p = MmoBots.FindStudio(studio);
		if ((bool)p)
		{
			return p.IsMyTochterfirma();
		}
		return false;
	}

	public static bool IsSigned(int studio)
	{
		if (!S.Signed.ContainsKey(studio))
		{
			return OwnSubsidiary(studio);
		}
		return true;
	}

	// How much of a game your store carries: all of it once its studio has signed (before signing started, the
	// old share of the whole catalogue).
	public static float CoverageFor(gameScript g)
	{
		if (!S.Signing)
		{
			return S.Coverage;
		}
		if (!IsSigned(StudioOf(g)))
		{
			return 0f;
		}
		return 1f;
	}

	// What a studio's PC games would make through your store: a year for the studio (after your cut), a week for you.
	public static void StudioValue(List<gameScript> games, out long theirYear, out long yourWeek)
	{
		float their = 0f;
		float yours = 0f;
		float cut = (float)S.Commission / 100f;
		float digital = StoreOps.DigitalShare();
		foreach (gameScript g in games)
		{
			if (g.sellsPerWeek != null && g.sellsPerWeek.Length != 0)
			{
				float money = (float)g.sellsPerWeek[0] * StoreOps.PcFraction(g) * digital * S.Share * (float)StoreOps.Price(g);
				their += money * (1f - cut) * 52f;
				yours += money * cut;
			}
		}
		theirYear = (long)their;
		yourWeek = (long)yours;
	}

	// The chance a studio signs at this fee: sure when it's small next to what your store would earn it in half a
	// year, never quite zero (some studios want a place on every store).
	public static float AcceptChance(long fee, long theirYear)
	{
		if (fee <= 0)
		{
			return 1f;
		}
		return Mathf.Clamp(1f - (float)fee / ((float)theirYear * 0.5f + 1f), 0.05f, 1f);
	}

	public static bool IsBig(int studio, long yourWeek)
	{
		publisherScript p = MmoBots.FindStudio(studio);
		if (yourWeek < 25000)
		{
			if ((bool)p)
			{
				return p.stars >= 60f;
			}
			return false;
		}
		return true;
	}

	// Your share of the other studios' catalogue, by what it sells: the signed studios' part.
	public static float SignedWeight()
	{
		float all = 0f;
		float signed = 0f;
		foreach (KeyValuePair<int, List<gameScript>> kv in StudiosOnStore())
		{
			float w = 1f + (float)kv.Value.Sum((gameScript g) => (g.sellsPerWeek != null && g.sellsPerWeek.Length != 0) ? g.sellsPerWeek[0] : 0);
			all += w;
			if (IsSigned(kv.Key))
			{
				signed += w;
			}
		}
		if (all <= 0f)
		{
			return 0f;
		}
		return signed / all;
	}

	// Starting the signing system on a store that already ran: the studios it carried (its coverage, biggest
	// sellers first) count as signed, so nothing is lost.
	private static void StartSigning()
	{
		S.Signing = true;
		List<KeyValuePair<int, float>> studios = (from kv in StudiosOnStore()
			select new KeyValuePair<int, float>(kv.Key, 1f + (float)kv.Value.Sum((gameScript g) => (g.sellsPerWeek != null && g.sellsPerWeek.Length != 0) ? g.sellsPerWeek[0] : 0)) into kv
			orderby kv.Value descending
			select kv).ToList();
		float all = studios.Sum((KeyValuePair<int, float> kv) => kv.Value);
		float target = S.Coverage * all;
		float got = 0f;
		foreach (KeyValuePair<int, float> kv2 in studios)
		{
			if (got >= target)
			{
				break;
			}
			S.Signed[kv2.Key] = new SignedStudio
			{
				Week = NowWeek,
				Fee = 0L
			};
			got += kv2.Value;
		}
		Plugin.Log.LogInfo("Store: " + S.Signed.Count + " studios signed from its earlier reach");
	}

	// A new store opens with a few small studios on board.
	public static void LaunchPartners()
	{
		S.Signing = true;
		foreach (int id in (from kv in StudiosOnStore()
			orderby kv.Value.Count
			select kv.Key).Take(4))
		{
			S.Signed[id] = new SignedStudio
			{
				Week = NowWeek,
				Fee = 0L
			};
		}
	}

	public static StudioRequest NextPopup()
	{
		return S.Requests.FirstOrDefault((StudioRequest r) => r.Big && !r.Seen);
	}

	private static void StudiosWeek(bool monthly)
	{
		if (!S.Signing)
		{
			StartSigning();
		}
		Dictionary<int, List<gameScript>> studios = StudiosOnStore();
		// Old requests lapse.
		foreach (StudioRequest r in S.Requests.Where((StudioRequest r) => NowWeek - r.Week > 12 || IsSigned(r.StudioId)).ToList())
		{
			S.Requests.Remove(r);
			S.AskAgain[r.StudioId] = NowWeek + 13;
		}
		// Studios that stopped trusting you leave.
		if (monthly && S.Trust < 30f)
		{
			foreach (int id in S.Signed.Keys.ToList())
			{
				if (UnityEngine.Random.value < 0.04f)
				{
					S.Signed.Remove(id);
					S.AskAgain[id] = NowWeek + 26;
					StoreOps.News(StudioName(id) + " takes its games off " + S.Name + ": it no longer trusts your store.");
				}
			}
		}
		// New requests: studios weigh your fee against what your store would earn them.
		int asked = 0;
		foreach (KeyValuePair<int, List<gameScript>> kv in studios.OrderBy((KeyValuePair<int, List<gameScript>> _) => UnityEngine.Random.value))
		{
			if (asked >= 2 || S.Requests.Count >= MaxRequests)
			{
				break;
			}
			int id2 = kv.Key;
			if (IsSigned(id2) || S.Requests.Any((StudioRequest r) => r.StudioId == id2) || (S.AskAgain.TryGetValue(id2, out var again) && NowWeek < again))
			{
				continue;
			}
			StudioValue(kv.Value, out long theirYear, out long yourWeek);
			float chance = 0.03f * (0.4f + S.Trust / 100f) * (0.5f + Mathf.Min(1f, S.Share * 4f)) * AcceptChance(S.SigningFee, theirYear);
			if (UnityEngine.Random.value < chance)
			{
				bool big = IsBig(id2, yourWeek);
				S.Requests.Add(new StudioRequest
				{
					StudioId = id2,
					Week = NowWeek,
					Fee = S.SigningFee,
					Big = big
				});
				asked++;
				if (!big)
				{
					StoreOps.News(StudioName(id2) + " wants to sell its games on " + S.Name + " (Studios page).");
				}
			}
		}
	}

	public static StudioRequest RequestFor(int studio)
	{
		return S.Requests.FirstOrDefault((StudioRequest r) => r.StudioId == studio);
	}

	// Signs the studio at its request's fee.
	public static string Accept(StudioRequest r)
	{
		if (r == null || !S.Requests.Contains(r))
		{
			return "That request is gone.";
		}
		S.Requests.Remove(r);
		S.Signed[r.StudioId] = new SignedStudio
		{
			Week = NowWeek,
			Fee = r.Fee
		};
		if (r.Fee > 0)
		{
			G.MS.Earn(r.Fee, 2);
			S.SigningTotal += r.Fee;
		}
		GameUpdates.Touch();
		return StudioName(r.StudioId) + " signed" + ((r.Fee > 0) ? (" and paid " + G.Money(r.Fee)) : "") + ". Its PC games now sell on " + S.Name + ".";
	}

	// Asks for a different fee: the studio weighs it as it weighed yours; it walks away if it's too much.
	public static string Counter(StudioRequest r, long fee)
	{
		if (r == null || !S.Requests.Contains(r))
		{
			return "That request is gone.";
		}
		StudiosOnStore().TryGetValue(r.StudioId, out var games);
		StudioValue(games ?? new List<gameScript>(), out long theirYear, out long _);
		float chance = ((fee <= r.Fee) ? 1f : AcceptChance(fee, theirYear));
		if (UnityEngine.Random.value < chance)
		{
			r.Fee = fee;
			return Accept(r);
		}
		S.Requests.Remove(r);
		S.AskAgain[r.StudioId] = NowWeek + 13;
		GameUpdates.Touch();
		return StudioName(r.StudioId) + " won't pay " + G.Money(fee) + " and walks away. It may ask again in a few months.";
	}

	public static void Decline(StudioRequest r)
	{
		if (r != null && S.Requests.Remove(r))
		{
			S.AskAgain[r.StudioId] = NowWeek + 26;
			GameUpdates.Touch();
		}
	}

	// Ends a studio's deal: its games leave your store.
	public static void Drop(int studio)
	{
		if (S.Signed.Remove(studio))
		{
			S.AskAgain[studio] = NowWeek + 26;
			S.Trust = Mathf.Max(0f, S.Trust - 2f);
			GameUpdates.Touch();
		}
	}

	public static List<gameScript> StudioGames(int studio)
	{
		if (!StudiosOnStore().TryGetValue(studio, out var games))
		{
			return new List<gameScript>();
		}
		return games;
	}

	// The best sellers on your store this week: your PC games and other studios'.
	public static List<KeyValuePair<gameScript, long>> TopSellers(int count)
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : new gameScript[0]);
		return (from g in all
			where (bool)g && g.isOnMarket && !g.inDevelopment && g.gameTyp == 0 && !g.arcade && !g.handy && (g.IsMyGame() || StoreOps.OthersGame(g))
			select new KeyValuePair<gameScript, long>(g, StoreUnits(g)) into x
			where x.Value > 0
			orderby x.Value descending
			select x).Take(count).ToList();
	}

	// Studios selling on your store (other studios' PC games, by how much of the catalogue you carry).
	public static Dictionary<int, List<gameScript>> StudiosOnStore()
	{
		Dictionary<int, List<gameScript>> map = new Dictionary<int, List<gameScript>>();
		foreach (gameScript g in StoreOps.OthersGames())
		{
			int id = ((g.publisherID >= 0) ? g.publisherID : g.developerID);
			if (!map.TryGetValue(id, out var list))
			{
				list = (map[id] = new List<gameScript>());
			}
			list.Add(g);
		}
		return map;
	}

	public static int StudiosSigned()
	{
		if (S.Signing)
		{
			return S.Signed.Count;
		}
		return Mathf.RoundToInt((float)StudiosOnStore().Count * S.Coverage);
	}

	public static int GamesOnStore(out int yours)
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : new gameScript[0]);
		yours = all.Count((gameScript g) => (bool)g && g.isOnMarket && g.IsMyGame() && g.gameTyp == 0 && !g.arcade && !g.handy && g.digitalVersion && StoreOps.PcFraction(g) > 0f);
		List<gameScript> others = StoreOps.OthersGames();
		if (S.Signing)
		{
			return yours + others.Count((gameScript g) => IsSigned(StudioOf(g)));
		}
		return yours + Mathf.RoundToInt((float)others.Count * S.Coverage);
	}

	// Your store's place among the open stores by share of digital PC buyers.
	public static int Rank(out int stores)
	{
		List<float> shares = StoreOps.RivalStores.Where(StoreOps.IsOpen).Select((RivalStore r) => StoreOps.RivalShare(r.Id)).ToList();
		stores = shares.Count + 1;
		return 1 + shares.Count((float x) => x > S.Share);
	}

	// ---- Each week, from StoreOps ----

	public static void Week()
	{
		bool monthly = (bool)G.MS && G.MS.week == 1;
		StudiosWeek(monthly);
		SaleWeek();
		FrontWeek(monthly);
		WishWeek();
		LauncherWeek();
		ConsoleWeek();
	}

	public static void NewMonth()
	{
		S.PlacementLast = S.PlacementMonth;
		S.PlacementMonth = 0L;
		S.ConsoleLast = S.ConsoleMonth;
		S.ConsoleMonth = 0L;
		foreach (ConsoleStore c in S.Consoles)
		{
			c.CommissionLast = 0L;
		}
	}
}
