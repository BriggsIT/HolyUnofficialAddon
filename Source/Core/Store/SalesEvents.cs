using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Store;

internal class SalesState
{
	// Your games' discount for each sale event, by "<event>-<year>" (index into SalesEvents.Discounts).
	public Dictionary<string, int> Plans = new Dictionary<string, int>();

	// The sale your games are in now.
	public string Now;

	public string Event = "";

	public int Discount;

	// True when every big store runs it (Summer and Winter); false when only your store does.
	public bool Wide = true;

	public int WeeksLeft;

	// Players waiting for the next sale: your games sell a little less for a while.
	public int FatigueWeeks;

	// The sales your games joined: when, and how much of their buyers each one reached.
	public List<SaleMark> Marks = new List<SaleMark>();

	// Join every sale with your plans, no pop-up.
	public bool AutoJoin;

	// Your games' discount in Summer and Winter when you haven't planned one.
	public int AutoDiscount = 2;

	// The event the pop-up asked about, and the last one answered.
	public string Asked;

	public string Decided;

	// The Holy Expansion's Seasonal Sales state was taken over (once per save).
	public bool Imported;

	public List<SaleRecord> History = new List<SaleRecord>();
}

internal class SaleMark
{
	public int Week;

	public float Weight = 1f;
}

// How one sale went for your games.
internal class SaleRecord
{
	public string Id;

	public string Name;

	public int Year;

	public int Discount;

	public bool Wide = true;

	public int Games;

	// Your eligible games' copies in the week before the sale.
	public long BaseWeekly;

	public long Units;

	public long Revenue;

	public int Weeks;

	public bool Fatigue;
}

// Sales Events: one system for seasonal sales (with The Holy Expansion, it takes over Holy's Seasonal Sales).
// - Summer (June week 3) and Winter (December week 2) run in every big digital store; your games join with a
//   discount (25/50/75%), as in Holy: each copy sells for less, many more copies sell (+2.2 x the discount, inside
//   Holy's +250% cap), and joining too many sales in a year makes players wait for the next one.
// - Once you own a store, it runs its own Spring and Autumn sales too, and its sale depth for other studios' games
//   is set alongside (Core/Store/StorePlus). In a sale only your store runs, your games' discount reaches only the
//   buyers on your store.
// - A pop-up opens as each sale starts, in your store's colours.
[HarmonyPatch]
internal static class SalesEvents
{
	private const string SaveKey = "mgt2.editor.sales";

	public const int MinWeeksOnMarket = 8;

	public static readonly int[] Discounts = new int[4] { 0, 25, 50, 75 };

	public static readonly string[] DiscountNames = new string[4] { "Full Price", "25% Off", "50% Off", "75% Off" };

	// Extra copies per discount: Holy's numbers (25% off = +55%, 50% = +110%, 75% = +165%).
	public const float BonusPerDiscount = 2.2f;

	public const float FatiguePenalty = -0.08f;

	public const int FatigueWeeks = 8;

	// Sales joined within a year (48 weeks) before players start waiting for the next one.
	public const float FatigueSales = 3f;

	private static SalesState S = new SalesState();

	// Holy applies our price cut itself (SalesBonus.RegisterPrice); without Holy we do it here.
	public static bool HolyPrices;

	private static bool shown;

	public static SalesState State => S;

	public static bool On
	{
		get
		{
			if (Plugin.MergedSales.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	private static int Now => CopyProtectDev.AbsWeek;

	// Before the first Summer Sale's year there's nothing to plan.
	public static string LockReason()
	{
		if (!Plugin.MergedSales.Value)
		{
			return "Sales Events are switched off in the addon's settings.";
		}
		if ((bool)G.MS && G.MS.year < HolyCompat.SeasonalYear())
		{
			return "The Summer and Winter Sales start in " + HolyCompat.SeasonalYear() + ".";
		}
		return null;
	}

	// ---- The calendar ----

	public static bool Wide(StoreSaleEvent e)
	{
		if (!(e.Id == "summer"))
		{
			return e.Id == "winter";
		}
		return true;
	}

	public static int FirstYear(StoreSaleEvent e)
	{
		if (!Wide(e))
		{
			return e.From;
		}
		return HolyCompat.SeasonalYear();
	}

	// Your store holds this event (its own sale, for every game it sells).
	public static bool StoreRuns(StoreSaleEvent e, int year)
	{
		if (StoreOps.On && StoreOps.State.Built)
		{
			return year >= e.From;
		}
		return false;
	}

	// The event happens at all: Summer and Winter everywhere, Spring and Autumn only on your store.
	public static bool Runs(StoreSaleEvent e, int year)
	{
		if (Wide(e))
		{
			return year >= FirstYear(e);
		}
		return StoreRuns(e, year);
	}

	private static int WeekOfYear(int month, int week)
	{
		return (month - 1) * 4 + (week - 1);
	}

	// The event starting this very week, if any.
	public static StoreSaleEvent Starting(out int year)
	{
		year = 0;
		if (!G.MS)
		{
			return null;
		}
		foreach (StoreSaleEvent e in StorePlus.Events)
		{
			if (e.Month == G.MS.month && e.Week == G.MS.week && Runs(e, G.MS.year))
			{
				year = G.MS.year;
				return e;
			}
		}
		return null;
	}

	// The next year of events that happen, soonest first, with the weeks until each starts (0 = running).
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
		foreach (StoreSaleEvent e in StorePlus.Events)
		{
			for (int y = G.MS.year - 1; y <= G.MS.year + 1; y++)
			{
				int start = y * 48 + WeekOfYear(e.Month, e.Week);
				if (Runs(e, y) && start + e.Weeks > now && start - now < 48)
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

	public static StoreSaleEvent Find(string key, out int year)
	{
		year = 0;
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}
		int dash = key.LastIndexOf('-');
		if (dash <= 0 || !int.TryParse(key.Substring(dash + 1), out year))
		{
			return null;
		}
		string id = key.Substring(0, dash);
		return StorePlus.Events.FirstOrDefault((StoreSaleEvent e) => e.Id == id);
	}

	// Your games' discount for an event: your plan, else your usual one for Summer and Winter, else full price.
	public static int Planned(StoreSaleEvent e, int year)
	{
		if (S.Plans.TryGetValue(StorePlus.Key(e, year), out var d))
		{
			return Mathf.Clamp(d, 0, 3);
		}
		if (!Wide(e))
		{
			return 0;
		}
		return Mathf.Clamp(S.AutoDiscount, 0, 3);
	}

	public static void Plan(StoreSaleEvent e, int year, int discount)
	{
		S.Plans[StorePlus.Key(e, year)] = Mathf.Clamp(discount, 0, 3);
		GameUpdates.Touch();
	}

	// ---- Your games ----

	public static bool Eligible(gameScript g)
	{
		if ((bool)g && g.isOnMarket && g.gameTyp == 0 && !g.arcade && g.weeksOnMarket >= MinWeeksOnMarket)
		{
			return g.IsMyGame();
		}
		return false;
	}

	public static List<gameScript> MyGames()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return new List<gameScript>();
		}
		return all.Where(Eligible).ToList();
	}

	public static bool Running
	{
		get
		{
			if (S.WeeksLeft > 0)
			{
				return S.Discount > 0;
			}
			return false;
		}
	}

	// How much of a game's buyers a sale reaches: all of them when every store runs it; in a sale only your store
	// runs, its digital PC buyers who shop with you (all of them for your store exclusives).
	public static float Reach(gameScript g, bool wide)
	{
		if (wide)
		{
			return 1f;
		}
		float pc = StoreOps.PcFraction(g);
		if (pc <= 0f || !StoreOps.State.Built)
		{
			return 0f;
		}
		float share = (StoreOps.IsExclusive(g) ? 1f : StoreOps.State.Share);
		return Mathf.Clamp01(pc * StoreOps.DigitalShare() * share);
	}

	// Average reach over your games (for the pop-up's estimates and the fatigue count).
	public static float AverageReach(List<gameScript> games, bool wide)
	{
		if (wide)
		{
			return 1f;
		}
		if (games.Count == 0)
		{
			return 0f;
		}
		long weekly = games.Sum(Weekly);
		if (weekly <= 0)
		{
			return games.Average((gameScript g) => Reach(g, wide: false));
		}
		return games.Sum((gameScript g) => (float)Weekly(g) * Reach(g, wide: false)) / (float)weekly;
	}

	public static long Weekly(gameScript g)
	{
		if (g.sellsPerWeek == null || g.sellsPerWeek.Length == 0)
		{
			return 0L;
		}
		return Math.Max(0, g.sellsPerWeek[0]);
	}

	// What the sale adds to a game's sales (into Holy's total and its cap when Holy is installed).
	public static float Bonus(gameScript g)
	{
		if (!On || !Eligible(g))
		{
			return 0f;
		}
		if (Running)
		{
			return (float)Discounts[S.Discount] / 100f * BonusPerDiscount * Reach(g, S.Wide);
		}
		if (S.FatigueWeeks > 0)
		{
			return FatiguePenalty;
		}
		return 0f;
	}

	// The share of the price a buyer pays this week.
	public static float PriceFactor(gameScript g)
	{
		if (!On || !Running || !Eligible(g))
		{
			return 1f;
		}
		return 1f - (float)Discounts[S.Discount] / 100f * Reach(g, S.Wide);
	}

	// Money from a sale next to a normal week, before other bonuses and Holy's cap (1 = the same).
	public static float MoneyFactor(int discount, float reach)
	{
		float d = (float)Discounts[Mathf.Clamp(discount, 0, 3)] / 100f * reach;
		return (1f - d) * (1f + BonusPerDiscount * d);
	}

	// Sales your games joined in the last year, weighted by how many of their buyers each reached.
	public static float SalesLastYear()
	{
		return S.Marks.Where((SaleMark m) => Now - m.Week < 48).Sum((SaleMark m) => m.Weight);
	}

	public static SaleRecord Current()
	{
		if (S.Now == null)
		{
			return null;
		}
		return S.History.LastOrDefault((SaleRecord r) => r.Id == S.Now);
	}

	// ---- Deciding ----

	// The pop-up's (or your plan's) answer for the event starting now: your games' discount and your store's depth.
	public static string Decide(string key, int discount, int storeDepth)
	{
		StoreSaleEvent e = Find(key, out int year);
		if (e == null)
		{
			return null;
		}
		S.Decided = key;
		discount = Mathf.Clamp(discount, 0, 3);
		S.Plans[key] = discount;
		List<string> parts = new List<string>();
		bool store = StoreRuns(e, year);
		if (store)
		{
			StorePlus.SetDepth(e, year, storeDepth);
			StorePlus.SyncSale();
			if (storeDepth > 0)
			{
				parts.Add("<b>" + StoreOps.State.Name + "</b> runs a " + StorePlus.DepthShort[storeDepth] + " sale");
			}
			else
			{
				parts.Add("<b>" + StoreOps.State.Name + "</b> sits it out");
			}
		}
		List<gameScript> games = MyGames();
		if (discount > 0 && games.Count > 0)
		{
			Start(e, year, discount, games);
			parts.Add(games.Count + ((games.Count == 1) ? " of your games is " : " of your games are ") + Discounts[discount] + "% off");
		}
		else if (games.Count > 0)
		{
			parts.Add("your games stay at full price");
		}
		GameUpdates.Touch();
		string text = "<b>" + e.Name + "</b>: " + string.Join(", ", parts) + ".";
		StoreOps.News(text);
		return text;
	}

	private static void Start(StoreSaleEvent e, int year, int discount, List<gameScript> games)
	{
		bool wide = Wide(e);
		string key = StorePlus.Key(e, year);
		int started = year * 48 + WeekOfYear(e.Month, e.Week);
		int now = G.MS.year * 48 + WeekOfYear(G.MS.month, G.MS.week);
		S.Now = key;
		S.Event = e.Name;
		S.Discount = discount;
		S.Wide = wide;
		S.WeeksLeft = Mathf.Max(1, e.Weeks - Mathf.Max(0, now - started));
		S.Marks.Add(new SaleMark
		{
			Week = Now,
			Weight = AverageReach(games, wide)
		});
		if (S.Marks.Count > 16)
		{
			S.Marks.RemoveAt(0);
		}
		S.History.Add(new SaleRecord
		{
			Id = key,
			Name = e.Name,
			Year = year,
			Discount = discount,
			Wide = wide,
			Games = games.Count,
			BaseWeekly = games.Sum(Weekly)
		});
		if (S.History.Count > 30)
		{
			S.History.RemoveAt(0);
		}
	}

	private static void End()
	{
		SaleRecord r = Current();
		if (SalesLastYear() >= FatigueSales - 0.001f)
		{
			S.FatigueWeeks = FatigueWeeks;
			if (r != null)
			{
				r.Fatigue = true;
			}
		}
		if (r != null)
		{
			string normal = ((r.BaseWeekly > 0 && r.Weeks > 0) ? (" (about " + MmoUiText.Num(r.BaseWeekly * r.Weeks) + " at full price)") : "");
			StoreOps.News("<b>" + r.Name + "</b> is over: your games sold " + MmoUiText.Num(r.Units) + " copies at " + Discounts[r.Discount] + "% off" + normal + "." + (r.Fatigue ? " Players have seen a lot of sales this year and start waiting for the next one." : ""));
		}
		S.Now = null;
		S.Discount = 0;
		S.WeeksLeft = 0;
	}

	// ---- Each week ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly(mainScript __instance)
	{
		try
		{
			if (!On)
			{
				return;
			}
			Ready();
			if (S.WeeksLeft > 0)
			{
				SaleRecord r = Current();
				if (r != null)
				{
					r.Weeks++;
				}
				S.WeeksLeft--;
				if (S.WeeksLeft == 0)
				{
					End();
				}
			}
			else if (S.FatigueWeeks > 0)
			{
				S.FatigueWeeks--;
			}
			StoreSaleEvent e = Starting(out int year);
			if (e == null)
			{
				return;
			}
			string key = StorePlus.Key(e, year);
			if (S.Asked == key || S.Decided == key)
			{
				return;
			}
			if (MyGames().Count == 0 && !StoreRuns(e, year))
			{
				return;
			}
			S.Asked = key;
			shown = false;
			if (S.AutoJoin)
			{
				Decide(key, Planned(e, year), StorePlus.Depth(e, year));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Sales Events: " + ex);
		}
	}

	// The event waiting for your answer, if its pop-up should show.
	public static string Waiting
	{
		get
		{
			if (!On || S.Asked == null || S.Decided == S.Asked)
			{
				return null;
			}
			StoreSaleEvent e = Find(S.Asked, out int year);
			if (e == null || !G.MS)
			{
				return null;
			}
			int start = year * 48 + WeekOfYear(e.Month, e.Week);
			int now = G.MS.year * 48 + WeekOfYear(G.MS.month, G.MS.week);
			if (now < start || now >= start + e.Weeks)
			{
				return null;
			}
			return S.Asked;
		}
	}

	// Opens the pop-up once nothing else is on screen; closing it goes ahead with what it shows.
	public static void Tick()
	{
		if (!On)
		{
			return;
		}
		string waiting = Waiting;
		if (waiting != null && !shown && (bool)G.Gui && !G.Gui.menuOpen && !EditorWindow.SalePopup.IsOpen)
		{
			shown = true;
			EditorWindow.SalePopup.Open();
			Awards.AwardsShow.Sound(50);
		}
		else if (shown && waiting != null && !EditorWindow.SalePopup.IsOpen)
		{
			shown = false;
			StoreSaleEvent e = Find(waiting, out int year);
			if (e != null)
			{
				if (UI.Pages.SalePopupPage.Picks(waiting, out int games, out int store))
				{
					Decide(waiting, games, store);
				}
				else
				{
					Decide(waiting, Planned(e, year), StorePlus.Depth(e, year));
				}
			}
		}
	}

	// ---- Taking over Holy's Seasonal Sales ----

	// Once per save: Holy's auto-join setting, the sales it ran this year, and a sale it has running.
	public static void Ready()
	{
		if (S.Imported || !On)
		{
			return;
		}
		S.Imported = true;
		try
		{
			Type state = AccessTools.TypeByName("HolyExpansion.Core.HolyState");
			object current = state?.GetField("Current", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
			object seasonal = ((current != null) ? state.GetField("Seasonal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(current) : null);
			if (seasonal == null)
			{
				return;
			}
			Traverse t = Traverse.Create(seasonal);
			S.AutoJoin = t.Field("AutoJoin").GetValue<bool>();
			S.AutoDiscount = Mathf.Clamp(t.Field("AutoDiscount").GetValue<int>(), 1, 3);
			List<int> weeks = t.Field("SaleWeeks").GetValue<List<int>>();
			if (weeks != null)
			{
				foreach (int w in weeks)
				{
					if (Now - w < 48 && !S.Marks.Any((SaleMark m) => m.Week == w))
					{
						S.Marks.Add(new SaleMark
						{
							Week = w
						});
					}
				}
			}
			S.FatigueWeeks = Mathf.Max(S.FatigueWeeks, t.Field("FatigueWeeks").GetValue<int>());
			int left = t.Field("WeeksLeft").GetValue<int>();
			int discount = t.Field("Discount").GetValue<int>();
			if (left > 0 && discount > 0 && S.WeeksLeft == 0)
			{
				S.Now = "holy-" + G.MS.year;
				S.Event = t.Field("Event").GetValue<string>() ?? "Sale";
				S.Discount = Mathf.Clamp(discount, 1, 3);
				S.Wide = true;
				S.WeeksLeft = left;
				List<gameScript> games = MyGames();
				S.History.Add(new SaleRecord
				{
					Id = S.Now,
					Name = S.Event,
					Year = G.MS.year,
					Discount = S.Discount,
					Games = games.Count,
					BaseWeekly = games.Sum(Weekly)
				});
			}
			t.Field("WeeksLeft").SetValue(0);
			t.Field("Discount").SetValue(0);
			Plugin.Log.LogInfo("Sales Events took over Holy's Seasonal Sales" + ((S.WeeksLeft > 0) ? (" (its " + S.Event + " carries on)") : ""));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not take over Holy's Seasonal Sales: " + ex.Message);
		}
	}

	// ---- Selling: the price cut (without Holy) and the sale's tally ----

	internal class SellState
	{
		public int[] Prices;

		public int[] Discounted;

		public SaleRecord Record;

		public long Sold;

		public long Money;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static void SellBefore(gameScript __instance, out SellState __state)
	{
		__state = null;
		gameScript g = __instance;
		if (!On || !g.isOnMarket || !Running || !Eligible(g))
		{
			return;
		}
		__state = new SellState
		{
			Record = Current(),
			Sold = g.sellsTotal,
			Money = g.umsatzTotal
		};
		float f = (HolyPrices ? 1f : PriceFactor(g));
		if (f < 0.999f && g.verkaufspreis != null)
		{
			__state.Prices = (int[])g.verkaufspreis.Clone();
			for (int i = 0; i < g.verkaufspreis.Length; i++)
			{
				g.verkaufspreis[i] = Math.Max(1, (int)((float)g.verkaufspreis[i] * f));
			}
			__state.Discounted = (int[])g.verkaufspreis.Clone();
		}
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static Exception SellAfter(gameScript __instance, SellState __state, Exception __exception)
	{
		if (__state == null)
		{
			return __exception;
		}
		try
		{
			int[] prices = __instance.verkaufspreis;
			if (__state.Prices != null && prices != null)
			{
				for (int i = 0; i < prices.Length && i < __state.Prices.Length; i++)
				{
					if (prices[i] == __state.Discounted[i])
					{
						prices[i] = __state.Prices[i];
					}
				}
			}
			if (__state.Record != null)
			{
				__state.Record.Units += Math.Max(0L, __instance.sellsTotal - __state.Sold);
				__state.Record.Revenue += Math.Max(0L, __instance.umsatzTotal - __state.Money);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Sales Events: " + ex.Message);
		}
		return __exception;
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
			Plugin.Log.LogError("Saving the sales events failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new SalesState();
		shown = false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<SalesState>(___es3file.Load<string>(SaveKey)) : null) ?? new SalesState();
			S.Plans = S.Plans ?? new Dictionary<string, int>();
			S.Marks = S.Marks ?? new List<SaleMark>();
			S.History = S.History ?? new List<SaleRecord>();
			S.Event = S.Event ?? "";
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading the sales events failed: " + ex);
			S = new SalesState();
		}
		shown = false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new SalesState();
		shown = false;
	}
}
