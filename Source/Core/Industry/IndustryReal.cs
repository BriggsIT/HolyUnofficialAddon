using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// The game's own levers that events pull instead of flat sales percentages (Docs/Events-RealSystems-Plan.md):
// - Saturation (gameScript.realsticPower, retail games): sales x(1 - it). It grows once a game has sold to most of the
//   players who want it. Lowering it brings lapsed buyers back; raising it shortens the game's shelf life.
// - Real copies sold (a rental chain's or a card maker's order): counted in the game's totals, revenue and charts, and
//   taken from your warehouse when you publish it boxed.
// - Stock and production: the warehouse (lagerbestand) and the production room (taskProduction.Work).
// - The Holy Expansion's Movie & TV deals, share price and esports leagues, by reflection.
[HarmonyPatch]
internal static class IndustryReal
{
	private static IndustryState S => IndustryCore.State;

	// ---- Saturation ----

	public static void Saturate(gameScript g, float delta)
	{
		if ((bool)g && g.gameTyp == 0)
		{
			g.realsticPower = Mathf.Clamp(g.realsticPower + delta, 0f, 0.98f);
		}
	}

	// ---- Real copies ----

	// What the game earned per copy so far (the price after the shops' cut), or its standard price.
	public static float PerCopy(gameScript g)
	{
		if (!g)
		{
			return 0f;
		}
		if (g.sellsTotal > 0 && g.umsatzTotal > 0)
		{
			return (float)g.umsatzTotal / (float)g.sellsTotal;
		}
		return (g.verkaufspreis != null && g.verkaufspreis.Length > 0) ? (float)g.verkaufspreis[0] : 0f;
	}

	// Copies bought outright at "price" each: counted as sales of the game (its totals, its revenue and the charts),
	// from the warehouse when you publish it boxed (only what's there). Returns the copies sold.
	public static long SellCopies(gameScript g, long copies, float price)
	{
		if (!g || copies <= 0 || !G.MS)
		{
			return 0L;
		}
		if (g.publisherID == G.MS.myID && g.retailVersion && g.lagerbestand != null && g.lagerbestand.Length > 0)
		{
			copies = Math.Min(copies, g.lagerbestand[0]);
			if (copies <= 0)
			{
				return 0L;
			}
			g.lagerbestand[0] -= copies;
			Restock();
		}
		long money = (long)((float)copies * Mathf.Max(0f, price));
		g.sellsTotal += copies;
		g.sellsTotalStandard += copies;
		g.umsatzTotal += money;
		if (money > 0)
		{
			G.MS.Earn(money, 3);
		}
		return copies;
	}

	// ---- Stock ----

	public static bool Boxed(gameScript g)
	{
		if ((bool)g && (bool)G.MS && g.publisherID == G.MS.myID && g.retailVersion)
		{
			return g.lagerbestand != null;
		}
		return false;
	}

	public static long Stock(gameScript g)
	{
		if (!Boxed(g))
		{
			return 0L;
		}
		return g.GetLagerbestand();
	}

	// Copies back into the warehouse, as far as there's room. Returns how many fitted.
	public static long AddStock(gameScript g, long copies)
	{
		if (!Boxed(g) || copies <= 0 || g.lagerbestand.Length == 0)
		{
			return 0L;
		}
		long room = Math.Max(0L, G.MS.games_.GetFreeLagerplatz());
		long fit = Math.Min(copies, room);
		if (fit > 0)
		{
			g.lagerbestand[0] += fit;
			Restock();
		}
		return fit;
	}

	// Every copy of it in the warehouse is gone.
	public static long ClearStock(gameScript g)
	{
		if (!Boxed(g))
		{
			return 0L;
		}
		long gone = g.GetLagerbestand();
		for (int i = 0; i < g.lagerbestand.Length; i++)
		{
			g.lagerbestand[i] = 0L;
		}
		Restock();
		return gone;
	}

	private static void Restock()
	{
		try
		{
			G.MS.games_.LagerplatzVerteilen();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Warehouse update: " + ex.Message);
		}
	}

	// ---- Production (A Cartridge Shortage) ----

	public static void LimitProduction(gameScript g, int weeks, bool block, float surcharge)
	{
		if ((bool)g)
		{
			S.ProductionLimits.RemoveAll((ProductionLimit l) => l.GameId == g.myID);
			S.ProductionLimits.Add(new ProductionLimit
			{
				GameId = g.myID,
				Expires = IndustryCore.Now + weeks,
				Block = block,
				Surcharge = surcharge
			});
		}
	}

	private static ProductionLimit LimitFor(gameScript g)
	{
		if (!g || S.ProductionLimits == null || S.ProductionLimits.Count == 0)
		{
			return null;
		}
		return S.ProductionLimits.FirstOrDefault((ProductionLimit l) => l.GameId == g.myID && l.Expires > IndustryCore.Now);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(taskProduction), "Work")]
	private static bool BeforeProduction(taskProduction __instance, out long __state)
	{
		__state = -1L;
		try
		{
			ProductionLimit limit = LimitFor(__instance.gS_);
			if (limit == null)
			{
				return true;
			}
			if (limit.Block)
			{
				return false;
			}
			__state = __instance.gS_.GetLagerbestand();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Production limit: " + ex.Message);
		}
		return true;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(taskProduction), "Work")]
	private static void AfterProduction(taskProduction __instance, long __state)
	{
		try
		{
			if (__state < 0 || !__instance.gS_)
			{
				return;
			}
			ProductionLimit limit = LimitFor(__instance.gS_);
			long made = __instance.gS_.GetLagerbestand() - __state;
			if (limit != null && made > 0 && limit.Surcharge > 0f)
			{
				long extra = (long)((float)made * __instance.gS_.GetProduktionskosten(0) * limit.Surcharge);
				if (extra > 0)
				{
					__instance.gS_.costs_production += extra;
					G.MS.Pay(extra, 21);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Production surcharge: " + ex.Message);
		}
	}

	// ---- The Holy Expansion's toggles ----

	public static bool HolyOn(string toggle)
	{
		try
		{
			Type config = HolyCompat.HolyType("HolyExpansion.Core.HolyConfig");
			FieldInfo f = config?.GetField(toggle, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (f?.GetValue(null) is ConfigEntry<bool> entry)
			{
				return entry.Value && G.Singleplayer;
			}
		}
		catch
		{
		}
		return false;
	}

	// ---- Share price (Holy's Stock, only once you've gone public) ----

	public static bool Public
	{
		get
		{
			object stock = (HolyOn("Stock") ? HolyCompat.HolyState("Stock") : null);
			return HolyCompat.Get<bool>(stock, "Public");
		}
	}

	// The share price moves by "factor" (1.15 = +15%). Returns false when you aren't on Holy's stock market.
	public static bool MoveSharePrice(float factor)
	{
		object stock = (HolyOn("Stock") ? HolyCompat.HolyState("Stock") : null);
		if (stock == null || !HolyCompat.Get<bool>(stock, "Public"))
		{
			return false;
		}
		float price = HolyCompat.Get<float>(stock, "SharePrice");
		HolyCompat.Set(stock, "SharePrice", Mathf.Max(0.5f, price * factor));
		return true;
	}

	// ---- Esports leagues (Holy) ----

	// Every league of yours gains (or loses) popularity: more viewers, income and sales. Returns how many.
	public static int EsportsPopularity(float delta)
	{
		object state = (HolyOn("Esports") ? HolyCompat.HolyState("Esports") : null);
		IList leagues = HolyCompat.Get<IList>(state, "Leagues");
		if (leagues == null)
		{
			return 0;
		}
		int n = 0;
		foreach (object l in leagues)
		{
			float pop = HolyCompat.Get<float>(l, "Popularity");
			HolyCompat.Set(l, "Popularity", Mathf.Clamp(pop + delta, 0.3f, 3f));
			n++;
		}
		return n;
	}

	// ---- Online interest (MMO and free-to-play) ----

	// The game's own interest in each online game (it multiplies MMO / F2P sales and new players). Returns how many.
	public static int OnlineInterest(float delta)
	{
		int n = 0;
		foreach (gameScript g in IndustryCore.AllGames())
		{
			if (!g.isOnMarket || g.releaseDate > 0)
			{
				continue;
			}
			if (g.gameTyp == 1)
			{
				g.AddMMOInteresse(delta);
				n++;
			}
			else if (g.gameTyp == 2)
			{
				g.AddF2PInteresse(delta);
				n++;
			}
		}
		return n;
	}

	// ---- A Cartoon: an animated series of your IP ----

	private static readonly string[] Results = new string[4] { "", "a hit", "solid", "a flop" };

	// With Holy's Movie & TV deals on, the series is Holy's deal (Holy premieres it and judges it: sales, IP points and
	// fans for the whole series); otherwise the addon premieres it the same way, with the game's own levers.
	// kind: Holy's (0 a film, 1 a TV series, 2 an animated series). A film takes longer, as in Holy.
	public static void StartCartoon(gameScript ip, string studio, long fee, bool coProduced, int kind = 2)
	{
		if (!ip)
		{
			return;
		}
		int weeks = ((kind == 0) ? UnityEngine.Random.Range(60, 100) : UnityEngine.Random.Range(44, 80));
		bool holy = AddHolyDeal(ip, studio, fee, weeks, kind);
		S.CartoonDeals.Add(new CartoonDeal
		{
			IpGameId = ip.myID,
			Studio = studio,
			ReleaseWeek = IndustryCore.Now + weeks,
			CoProduced = coProduced,
			Holy = holy,
			Kind = kind
		});
	}

	public static readonly string[] ScreenKinds = new string[3] { "film", "TV series", "animated series" };

	public static int CartoonWeeks(gameScript ip)
	{
		CartoonDeal d = S.CartoonDeals.LastOrDefault((CartoonDeal x) => (bool)ip && x.IpGameId == ip.myID);
		return (d == null) ? 0 : Math.Max(0, d.ReleaseWeek - IndustryCore.Now);
	}

	public static bool CartoonRunning(gameScript ip)
	{
		if ((bool)ip && S.CartoonDeals.Any((CartoonDeal d) => d.IpGameId == ip.myID && d.Result == 0))
		{
			return true;
		}
		return HolyBusy(ip);
	}

	private static bool AddHolyDeal(gameScript ip, string studio, long fee, int weeks, int kind)
	{
		try
		{
			if (!HolyOn("Media"))
			{
				return false;
			}
			object media = HolyCompat.HolyState("Media");
			IList deals = HolyCompat.Get<IList>(media, "Deals");
			Type dealType = HolyCompat.HolyType("HolyExpansion.Systems.MediaDeal");
			if (deals == null || dealType == null)
			{
				return false;
			}
			object d = Activator.CreateInstance(dealType);
			HolyCompat.Set(d, "IpGameId", ip.myID);
			HolyCompat.Set(d, "IpName", ip.GetIpName());
			HolyCompat.Set(d, "Studio", studio);
			HolyCompat.Set(d, "Kind", Mathf.Clamp(kind, 0, 2));
			HolyCompat.Set(d, "Fee", fee);
			HolyCompat.Set(d, "StartWeek", HolyAbsWeek());
			HolyCompat.Set(d, "ReleaseWeek", HolyAbsWeek() + weeks);
			deals.Add(d);
			return true;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Holy's Movie & TV deal: " + ex.Message);
			return false;
		}
	}

	// Holy counts weeks the same way (Util.AbsWeek), but ask it in case it differs.
	private static int HolyAbsWeek()
	{
		try
		{
			PropertyInfo p = HolyCompat.HolyType("HolyExpansion.Core.Util")?.GetProperty("AbsWeek", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (p != null)
			{
				return (int)p.GetValue(null);
			}
		}
		catch
		{
		}
		return IndustryCore.Now;
	}

	// Holy already has an unfinished deal or an offer for this IP.
	private static bool HolyBusy(gameScript ip)
	{
		if (!ip || !HolyOn("Media"))
		{
			return false;
		}
		object media = HolyCompat.HolyState("Media");
		IList deals = HolyCompat.Get<IList>(media, "Deals");
		IList offers = HolyCompat.Get<IList>(media, "Offers");
		if (deals != null && deals.Cast<object>().Any((object d) => HolyCompat.Get<int>(d, "IpGameId") == ip.myID && HolyCompat.Get<int>(d, "Result") == 0))
		{
			return true;
		}
		return offers != null && offers.Cast<object>().Any((object o) => HolyCompat.Get<int>(o, "IpGameId") == ip.myID);
	}

	private static int HolyResult(CartoonDeal c)
	{
		object media = HolyCompat.HolyState("Media");
		IList deals = HolyCompat.Get<IList>(media, "Deals");
		if (deals == null)
		{
			return 3;
		}
		object d = deals.Cast<object>().LastOrDefault((object x) => HolyCompat.Get<int>(x, "IpGameId") == c.IpGameId && HolyCompat.Get<int>(x, "Kind") == c.Kind);
		return (d == null) ? 3 : HolyCompat.Get<int>(d, "Result");
	}

	// The addon's own premiere, Holy's formula: the IP's fame and its games' reviews, plus luck.
	private static int Premiere(CartoonDeal c, gameScript ip)
	{
		List<gameScript> games = IndustryCore.AllGames().Where((gameScript g) => g.mainIP == c.IpGameId && g.reviewTotal > 0).ToList();
		float review = ((games.Count > 0) ? ((float)games.Average((gameScript g) => g.reviewTotal)) : 60f);
		float score = Mathf.Clamp01(ip.ipPunkte / 1000f) * 0.35f + review / 100f * 0.35f + UnityEngine.Random.value * 0.45f;
		genres gen = IndustryCore.Genres;
		long fans = (gen ? gen.GetAmountFans() : 0);
		if (score > 0.72f)
		{
			ip.AddIpPoints(150f);
			G.MS.AddFans((int)Mathf.Min((float)fans / 10f + 20000f, 2000000f), ip.maingenre);
			foreach (gameScript g2 in games.Where((gameScript g) => g.isOnMarket))
			{
				Saturate(g2, -0.3f);
				g2.AddHype(15f);
			}
			return 1;
		}
		if (score > 0.45f)
		{
			ip.AddIpPoints(50f);
			G.MS.AddFans((int)Mathf.Min((float)fans / 40f + 5000f, 400000f), ip.maingenre);
			foreach (gameScript g3 in games.Where((gameScript g) => g.isOnMarket))
			{
				Saturate(g3, -0.1f);
			}
			return 2;
		}
		ip.AddIpPoints(-80f);
		return 3;
	}

	// A co-produced series' merchandise: the IP's fan shop demand starts fresh (the game counts every item sold
	// against the fans who'd buy one) and its review points are topped up.
	private static void Merchandise(gameScript ip)
	{
		if (ip.merchGesamtSells != null)
		{
			for (int i = 0; i < ip.merchGesamtSells.Length; i++)
			{
				ip.merchGesamtSells[i] = 0;
			}
		}
		ip.merchGesamtReviewPoints = Mathf.Max(ip.merchGesamtReviewPoints, 90f);
	}

	// ---- Weekly ----

	public static void Weekly()
	{
		S.ProductionLimits.RemoveAll((ProductionLimit l) => l.Expires <= IndustryCore.Now);
		foreach (CartoonDeal c in S.CartoonDeals.Where((CartoonDeal x) => x.Result == 0 && IndustryCore.Now >= x.ReleaseWeek + (x.Holy ? 1 : 0)).ToList())
		{
			gameScript ip = GameUpdates.FindGame(c.IpGameId);
			if (!ip)
			{
				c.Result = 3;
				continue;
			}
			if (c.Holy)
			{
				c.Result = HolyResult(c);
				if (c.Result == 0)
				{
					continue;
				}
			}
			else
			{
				c.Result = Premiere(c, ip);
				string text = "The " + ip.GetIpName() + " " + ScreenKinds[Mathf.Clamp(c.Kind, 0, 2)] + " by " + c.Studio + " is out: " + Results[c.Result] + ((c.Result == 1) ? ". Lapsed players pick the games up again." : ((c.Result == 3) ? ". Fans of the games aren't happy." : "."));
				IndustryCore.News(text);
				IndustryCore.Feed(text);
			}
			if (c.CoProduced && c.Result != 3)
			{
				Merchandise(ip);
				IndustryCore.Feed("The " + ip.GetIpName() + " " + ScreenKinds[Mathf.Clamp(c.Kind, 0, 2)] + " has the shops wanting its merchandise: your fan shop's demand for it starts fresh.");
			}
		}
		S.CartoonDeals.RemoveAll((CartoonDeal x) => x.Result != 0 && IndustryCore.Now - x.ReleaseWeek > 104);
	}
}
