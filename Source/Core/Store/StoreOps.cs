using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Store;

// A rival digital store: when it opens (and closes), its cut, and how strong it gets.
internal class RivalStore
{
	public string Id;

	public string Name;

	public string About;

	public int OpenYear;

	public int OpenMonth = 1;

	public int CloseYear;

	public int Fee;

	// Publisher stores only sell their owner's games: they take players but never your money.
	public bool OwnGamesOnly;

	public float Peak;

	public float RampYears;
}

// A store feature: a development room project that makes the store more attractive.
internal class StoreFeature
{
	public int Id;

	public string Name;

	public string About;

	public int Year;

	public float Work;

	public long Cost;

	public float Appeal;

	public float Reputation;

	public float Active;
}

internal class StoreState
{
	public bool Built;

	public string Name = "";

	public int Logo = 1;

	public int BuiltWeek = -1;

	public int BuiltYear;

	public List<int> Features = new List<int>();

	public int Commission = 30;

	// Share of digital PC buyers: yours, each rival's, and those buying straight from the studios.
	public float Share;

	public Dictionary<string, float> Rivals = new Dictionary<string, float>();

	public float Direct = 1f;

	public bool Started;

	public long Accounts;

	public long Active;

	public float Reputation = 60f;

	public float Trust = 50f;

	public float Coverage = 0.2f;

	public float Outage;

	// Your games sold only on your store.
	public List<int> Exclusive = new List<int>();

	public List<float> ShareHistory = new List<float>();

	public List<long> AccountHistory = new List<long>();

	public int MonthKey = -1;

	public long CommissionMonth;

	public long FeesMonth;

	public long ServerMonth;

	public long CommissionLast;

	public long FeesLast;

	public long ServerLast;

	public long CommissionTotal;

	public long FeesTotal;

	public List<string> Announced = new List<string>();

	public long NextMilestone = 1000000L;
}

// Your own digital PC store, and the rival stores around it. From 2003 the market for digital PC buyers is split
// between buying straight from the studios (fading as stores take over), the rival stores (opening on a
// real-life-like timeline) and, once you build one, your store. Your store earns a commission on other studios'
// digital PC sales (their weekly sales and prices come from the game), helps your own games, and needs servers.
// Your games sold through rival stores pay those stores their cut (optional).
[HarmonyPatch]
internal static class StoreOps
{
	private const string SaveKey = "mgt2.editor.store";

	public const int OpenYear = 2003;

	public static readonly int[] Commissions = new int[4] { 12, 20, 25, 30 };

	public const int LogoCount = 12;

	public const float BuildWork = 450f;

	public const long BuildCost = 600000L;

	public static readonly List<RivalStore> RivalStores = new List<RivalStore>
	{
		new RivalStore
		{
			Id = "diesel", Name = "Diesel", OpenYear = 2003, OpenMonth = 9, Fee = 30, Peak = 100f, RampYears = 8f,
			About = "The big one. Started as a way to patch one studio's games and became where most PC players buy. Huge Summer and Winter sales."
		},
		new RivalStore
		{
			Id = "pixelport", Name = "Pixelport", OpenYear = 2006, OpenMonth = 4, CloseYear = 2016, Fee = 25, Peak = 12f, RampYears = 2f,
			About = "A general store that never quite caught on."
		},
		new RivalStore
		{
			Id = "cog", Name = "COG", OpenYear = 2008, OpenMonth = 9, Fee = 30, Peak = 18f, RampYears = 4f,
			About = "Classic Old Games: old favourites made to run on today's PCs, and no DRM on anything."
		},
		new RivalStore
		{
			Id = "skirmish", Name = "Skirmish.net", OpenYear = 2009, OpenMonth = 3, OwnGamesOnly = true, Peak = 14f, RampYears = 2f,
			About = "A publisher's own launcher. Sells only its owner's games."
		},
		new RivalStore
		{
			Id = "modest", Name = "Modest Bundle", OpenYear = 2010, OpenMonth = 5, Fee = 15, Peak = 10f, RampYears = 2f,
			About = "Bundles of games for whatever you want to pay, with part of it going to charity."
		},
		new RivalStore
		{
			Id = "genesis", Name = "Genesis", OpenYear = 2011, OpenMonth = 6, OwnGamesOnly = true, Peak = 16f, RampYears = 1f,
			About = "A big publisher's store for its own games."
		},
		new RivalStore
		{
			Id = "scratch", Name = "scratch.io", OpenYear = 2013, OpenMonth = 3, Fee = 10, Peak = 9f, RampYears = 4f,
			About = "The indie corner of the internet. Studios pick their own cut, most leave it at 10%."
		},
		new RivalStore
		{
			Id = "nebula", Name = "Nebula Arcade", OpenYear = 2014, OpenMonth = 10, CloseYear = 2024, Fee = 20, Peak = 10f, RampYears = 2f,
			About = "A slick store that undercut the big one on fees."
		},
		new RivalStore
		{
			Id = "mythic", Name = "Mythic Games Store", OpenYear = 2018, OpenMonth = 12, Fee = 12, Peak = 45f, RampYears = 3f,
			About = "Takes only 12%, pays for exclusives and gives a free game away every week."
		},
		new RivalStore
		{
			Id = "stadio", Name = "Stadio", OpenYear = 2020, OpenMonth = 1, CloseYear = 2023, Fee = 15, Peak = 6f, RampYears = 1f,
			About = "Games streamed from the cloud, nothing to download."
		},
		new RivalStore
		{
			Id = "passline", Name = "Passline", OpenYear = 2022, OpenMonth = 4, Fee = 20, Peak = 16f, RampYears = 3f,
			About = "Subscription first: a monthly fee for a big library."
		},
		new RivalStore
		{
			Id = "curio", Name = "Curio", OpenYear = 2025, OpenMonth = 2, Fee = 15, Peak = 12f, RampYears = 3f,
			About = "A store that picks games for you."
		},
		new RivalStore
		{
			Id = "horizon", Name = "Horizon Hall", OpenYear = 2028, OpenMonth = 6, Fee = 20, Peak = 10f, RampYears = 3f,
			About = "Built around VR and shared virtual spaces."
		}
	};

	public static readonly List<StoreFeature> Features = new List<StoreFeature>
	{
		new StoreFeature { Id = 0, Name = "User reviews", Year = 2003, Work = 140f, Cost = 150000L, Appeal = 6f, Reputation = 4f, About = "Players rate and review games. Buyers trust the store more." },
		new StoreFeature { Id = 1, Name = "Friends and chat", Year = 2003, Work = 160f, Cost = 180000L, Appeal = 5f, Active = 0.05f, About = "Friends lists, chat and invites. Players come back more often." },
		new StoreFeature { Id = 2, Name = "Automatic updates", Year = 2003, Work = 120f, Cost = 120000L, Appeal = 4f, Reputation = 3f, About = "Games patch themselves. Fewer headaches for players and studios." },
		new StoreFeature { Id = 3, Name = "Regional pricing", Year = 2005, Work = 150f, Cost = 220000L, Appeal = 5f, Reputation = 2f, About = "Fair prices in every country. More players around the world sign up." },
		new StoreFeature { Id = 4, Name = "Achievements", Year = 2007, Work = 180f, Cost = 260000L, Appeal = 6f, Active = 0.08f, About = "Goals to chase in every game. Players stay active longer." },
		new StoreFeature { Id = 5, Name = "Cloud saves", Year = 2008, Work = 220f, Cost = 380000L, Appeal = 6f, Reputation = 4f, About = "Saves follow players to any PC. One of the things players expect from a store." }
	};

	private static StoreState S = new StoreState();

	private static PropertyInfo holyRunning;

	private static bool holyLooked;

	public static StoreState State => S;

	public static bool On
	{
		get
		{
			if (Plugin.StoreEnabled.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static bool FeesOn
	{
		get
		{
			if (On)
			{
				return Plugin.StoreRivalFees.Value;
			}
			return false;
		}
	}

	public static int Year
	{
		get
		{
			if (!G.MS)
			{
				return 1976;
			}
			return G.MS.year;
		}
	}

	public static bool Open => Year >= OpenYear;

	public static string LockReason()
	{
		if (!On)
		{
			return "Your digital store is switched off in the addon's settings.";
		}
		if (!Open)
		{
			return "Digital stores arrive in " + OpenYear + ", when games start selling as digital versions.";
		}
		return null;
	}

	// ---- Time ----

	private static float Now
	{
		get
		{
			if (!G.MS)
			{
				return 1976f;
			}
			return (float)G.MS.year + (float)(G.MS.month - 1) / 12f + (float)(G.MS.week - 1) / 52f;
		}
	}

	public static bool IsOpen(RivalStore r)
	{
		float now = Now;
		if (now < (float)r.OpenYear + (float)(r.OpenMonth - 1) / 12f)
		{
			return false;
		}
		if (r.CloseYear > 0)
		{
			return now < (float)r.CloseYear;
		}
		return true;
	}

	public static bool HasClosed(RivalStore r)
	{
		if (r.CloseYear > 0)
		{
			return Now >= (float)r.CloseYear;
		}
		return false;
	}

	// ---- The market ----

	private static float DirectAppeal()
	{
		return 120f * Mathf.Clamp(1f - (Now - (float)OpenYear) / 11f, 0.08f, 1f);
	}

	private static float RivalAppeal(RivalStore r)
	{
		if (!IsOpen(r))
		{
			return 0f;
		}
		float age = Now - ((float)r.OpenYear + (float)(r.OpenMonth - 1) / 12f);
		float ramp = Mathf.Clamp(age / Mathf.Max(0.1f, r.RampYears), 0.15f, 1f);
		return r.Peak * ramp;
	}

	public static bool HasFeature(int id)
	{
		return S.Features.Contains(id);
	}

	public static float YourAppeal()
	{
		if (!S.Built)
		{
			return 0f;
		}
		float appeal = 8f + Features.Where((StoreFeature f) => HasFeature(f.Id)).Sum((StoreFeature f) => f.Appeal);
		appeal += 25f * S.Coverage;
		appeal += (S.Reputation - 50f) * 0.4f;
		appeal += 6f * Mathf.Log10(1f + (float)S.Accounts / 100000f);
		appeal += 2f * (float)S.Exclusive.Count((int id) => (bool)GameUpdates.FindGame(id) && GameUpdates.FindGame(id).isOnMarket);
		appeal *= 1f - 0.6f * S.Outage;
		if (SaleRunning())
		{
			appeal *= 1.15f;
		}
		return Mathf.Max(1f, appeal);
	}

	// Shares of digital PC buyers right now (move toward their targets a little each week).
	private static void UpdateShares(bool jump)
	{
		Dictionary<string, float> appeal = RivalStores.ToDictionary((RivalStore r) => r.Id, RivalAppeal);
		float direct = DirectAppeal();
		float yours = YourAppeal();
		float total = direct + yours + appeal.Values.Sum();
		float rate = (jump ? 1f : 0.06f);
		S.Direct = Mathf.Lerp(S.Direct, direct / total, rate);
		S.Share = Mathf.Lerp(S.Share, yours / total, rate);
		foreach (RivalStore r in RivalStores)
		{
			S.Rivals.TryGetValue(r.Id, out var now);
			float target = appeal[r.Id] / total;
			S.Rivals[r.Id] = (IsOpen(r) ? Mathf.Lerp(now, target, (now <= 0f) ? Mathf.Max(rate, 0.25f) : rate) : 0f);
		}
		float sum = S.Direct + S.Share + S.Rivals.Values.Sum();
		if (sum > 0f)
		{
			S.Direct /= sum;
			S.Share /= sum;
			foreach (string id in S.Rivals.Keys.ToList())
			{
				S.Rivals[id] /= sum;
			}
		}
	}

	public static float RivalShare(string id)
	{
		if (!S.Rivals.TryGetValue(id, out var share))
		{
			return 0f;
		}
		return share;
	}

	// The part of digital PC buyers who shop in rival stores that sell other studios' games.
	public static float OpenRivalShare()
	{
		return RivalStores.Where((RivalStore r) => !r.OwnGamesOnly && IsOpen(r)).Sum((RivalStore r) => RivalShare(r.Id));
	}

	// Digital buyers among all buyers (the game's own curve; 0 before digital versions unlock).
	public static float DigitalShare()
	{
		if (!G.MS || !G.MS.games_)
		{
			return 0f;
		}
		return G.MS.games_.GetDigitalSells();
	}

	// Everyone with a PC who buys games digitally.
	public static long DigitalPcPlayers()
	{
		long users = 0L;
		platformScript[] all = (G.MS ? G.MS.arrayPlatformsScripts : null);
		if (all == null)
		{
			return 0L;
		}
		foreach (platformScript p in all)
		{
			if ((bool)p && p.typ == 0)
			{
				users += p.GetAktiveNutzer();
			}
		}
		return (long)((float)users * DigitalShare());
	}

	// How much of a game's market is on PC (by its platforms' market share).
	public static float PcFraction(gameScript g)
	{
		float pc = 0f;
		float all = 0f;
		int count = 0;
		int pcCount = 0;
		for (int i = 0; i < g.gamePlatform.Length; i++)
		{
			if (g.gamePlatform[i] == -1)
			{
				continue;
			}
			platformScript p = GameUpdates.Platform(g.gamePlatform[i]);
			if ((bool)p)
			{
				float share = p.GetMarktanteil();
				all += share;
				count++;
				if (p.typ == 0)
				{
					pc += share;
					pcCount++;
				}
			}
		}
		if (all > 0.01f)
		{
			return pc / all;
		}
		if (count <= 0)
		{
			return 0f;
		}
		return (float)pcCount / (float)count;
	}

	// ---- Sales events ----

	// The store sales: Holy's seasonal sales when it runs them, otherwise June and December.
	public static bool SaleRunning()
	{
		if (!holyLooked)
		{
			holyLooked = true;
			holyRunning = AccessTools.TypeByName("HolyExpansion.Systems.SeasonalSales")?.GetProperty("Running", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		}
		if (holyRunning != null)
		{
			try
			{
				return (bool)holyRunning.GetValue(null);
			}
			catch
			{
			}
		}
		if (!G.MS || !Open)
		{
			return false;
		}
		if (G.MS.month == 6 && G.MS.week >= 3)
		{
			return true;
		}
		if (G.MS.month == 12)
		{
			if (G.MS.week >= 2)
			{
				return G.MS.week <= 3;
			}
			return false;
		}
		return false;
	}

	// ---- Your games ----

	public static bool IsExclusive(gameScript g)
	{
		if ((bool)g && S.Built)
		{
			return S.Exclusive.Contains(g.myID);
		}
		return false;
	}

	public static bool CanBeExclusive(gameScript g)
	{
		if ((bool)g && S.Built && g.IsMyGame() && g.gameTyp == 0 && !g.arcade && !g.handy && g.digitalVersion)
		{
			return PcFraction(g) > 0f;
		}
		return false;
	}

	public static string SetExclusive(gameScript g, bool on)
	{
		if (!CanBeExclusive(g))
		{
			return "Only your PC games with a digital version can be exclusive to your store.";
		}
		if (on == IsExclusive(g))
		{
			return null;
		}
		if (on)
		{
			S.Exclusive.Add(g.myID);
			S.Reputation = Mathf.Max(0f, S.Reputation - 2f);
		}
		else
		{
			S.Exclusive.Remove(g.myID);
		}
		GameUpdates.Touch();
		return null;
	}

	// Your PC games on the front page of your store sell more; exclusives lose the buyers who stay in rival stores.
	public static float SalesFactor(gameScript g)
	{
		if (!On || !S.Built || !g || !g.IsMyGame() || g.gameTyp != 0 || g.arcade || g.handy)
		{
			return 1f;
		}
		float pc = PcFraction(g);
		if (pc <= 0f)
		{
			return 1f;
		}
		float factor = 1f + 0.35f * S.Share * pc;
		if (IsExclusive(g))
		{
			factor *= 1f - 0.7f * OpenRivalShare() * DigitalShare() * pc;
		}
		return factor;
	}

	// The cut a rival store takes from one of your games (Diesel's tiers from 2018, like the real thing).
	public static float RivalFee(RivalStore r, gameScript g)
	{
		if (r.OwnGamesOnly)
		{
			return 0f;
		}
		if (r.Id == "diesel" && Year >= 2018 && (bool)g)
		{
			long digital = g.sellsTotalOnline * Math.Max(1, g.verkaufspreis[3]);
			if (digital > 50000000)
			{
				return 0.2f;
			}
			if (digital > 10000000)
			{
				return 0.25f;
			}
		}
		return (float)r.Fee / 100f;
	}

	// The share of a game's digital PC money that goes to rival stores.
	public static float FeeRate(gameScript g)
	{
		if (!FeesOn || !Open || IsExclusive(g))
		{
			return 0f;
		}
		return RivalStores.Where(IsOpen).Sum((RivalStore r) => RivalShare(r.Id) * RivalFee(r, g));
	}

	private struct Before
	{
		public bool Ok;

		public long Online;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static void SellBefore(gameScript __instance, out Before __state)
	{
		__state = default(Before);
		gameScript g = __instance;
		if (On && Open && g.isOnMarket && g.gameTyp == 0 && !g.arcade && !g.handy && g.digitalVersion && g.IsMyGame())
		{
			__state = new Before
			{
				Ok = true,
				Online = g.sellsTotalOnline
			};
		}
	}

	// Digital PC copies sold this week: rival stores take their cut; exclusives bring new accounts instead.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static void SellAfter(gameScript __instance, Before __state)
	{
		if (!__state.Ok)
		{
			return;
		}
		try
		{
			gameScript g = __instance;
			long units = g.sellsTotalOnline - __state.Online;
			if (units <= 0)
			{
				return;
			}
			float pc = PcFraction(g);
			if (IsExclusive(g))
			{
				S.Accounts += (long)((float)units * pc * 0.4f);
				return;
			}
			long fee = (long)((float)units * (float)Math.Max(0, g.verkaufspreis[3]) * pc * FeeRate(g));
			if (fee > 0 && (bool)G.MS)
			{
				G.MS.Pay(fee, 13);
				S.FeesMonth += fee;
				S.FeesTotal += fee;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Store fees: " + ex);
		}
	}

	// ---- Other studios' games ----

	public static bool OthersGame(gameScript g)
	{
		if ((bool)g && g.isOnMarket && !g.inDevelopment && g.releaseDate <= 0 && g.gameTyp == 0 && !g.arcade && !g.handy && !g.IsMyGame() && !g.typ_bundle && !g.typ_bundleAddon)
		{
			return PcFraction(g) > 0f;
		}
		return false;
	}

	public static List<gameScript> OthersGames()
	{
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return new List<gameScript>();
		}
		return all.Where(OthersGame).ToList();
	}

	public static int Price(gameScript g)
	{
		if (g.verkaufspreis[3] > 0)
		{
			return g.verkaufspreis[3];
		}
		if (g.verkaufspreis[0] > 0)
		{
			return g.verkaufspreis[0];
		}
		return 30;
	}

	// What your store makes on a game this week.
	public static long CommissionOn(gameScript g)
	{
		if (!S.Built || g.sellsPerWeek == null || g.sellsPerWeek.Length == 0)
		{
			return 0L;
		}
		float units = (float)g.sellsPerWeek[0] * PcFraction(g) * DigitalShare() * S.Share * S.Coverage;
		if (SaleRunning())
		{
			units *= 1.6f;
		}
		return (long)(units * (float)Price(g) * (float)S.Commission / 100f);
	}

	// ---- Weekly ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly()
	{
		if (!On || !Open)
		{
			return;
		}
		try
		{
			NewMonth();
			Announce();
			UpdateShares(!S.Started);
			S.Started = true;
			if (S.Built)
			{
				WeekForStore();
			}
			S.ShareHistory.Add(S.Share);
			S.AccountHistory.Add(S.Accounts);
			Trim(S.ShareHistory);
			Trim(S.AccountHistory);
			GameUpdates.Touch();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Store week: " + ex);
		}
	}

	private static void Trim<T>(List<T> list)
	{
		if (list.Count > 156)
		{
			list.RemoveRange(0, list.Count - 156);
		}
	}

	private static void NewMonth()
	{
		int key = G.MS.year * 12 + G.MS.month;
		if (S.MonthKey == key)
		{
			return;
		}
		if (S.MonthKey >= 0)
		{
			S.CommissionLast = S.CommissionMonth;
			S.FeesLast = S.FeesMonth;
			S.ServerLast = S.ServerMonth;
		}
		S.CommissionMonth = 0L;
		S.FeesMonth = 0L;
		S.ServerMonth = 0L;
		S.MonthKey = key;
	}

	// News when rival stores open or close.
	private static void Announce()
	{
		foreach (RivalStore r in RivalStores)
		{
			string open = "open:" + r.Id;
			string close = "close:" + r.Id;
			if (IsOpen(r) && !S.Announced.Contains(open))
			{
				S.Announced.Add(open);
				if (S.Started)
				{
					News("<b>" + r.Name + "</b> opens its doors" + (r.OwnGamesOnly ? " (its owner's games only)." : (", taking " + r.Fee + "% of every sale.")) + " " + r.About);
				}
			}
			if (HasClosed(r) && !S.Announced.Contains(close))
			{
				S.Announced.Add(close);
				if (S.Started)
				{
					News("<b>" + r.Name + "</b> is closing down. Its players move to other stores.");
				}
			}
		}
	}

	private static void WeekForStore()
	{
		float activeBonus = 1f + Features.Where((StoreFeature f) => HasFeature(f.Id)).Sum((StoreFeature f) => f.Active);
		bool sale = SaleRunning();
		long active = (long)((float)DigitalPcPlayers() * S.Share * activeBonus * (sale ? 1.3f : 1f));
		S.Active = active;
		long target = (long)((float)active * 1.5f);
		if (target > S.Accounts)
		{
			S.Accounts += (long)((float)(target - S.Accounts) * 0.08f);
		}
		// Servers: one slot for every 25 active players, from your general-purpose server rooms.
		long need = active / 25;
		long placed = Servers(need);
		S.Outage = ((need > 0) ? Mathf.Clamp01((float)(need - placed) / (float)need) : 0f);
		long serverCost = active / 200;
		if (serverCost > 0)
		{
			G.MS.Pay(serverCost, 13);
			S.ServerMonth += serverCost;
		}
		float rep = 55f + Features.Where((StoreFeature f) => HasFeature(f.Id)).Sum((StoreFeature f) => f.Reputation) + (sale ? 5f : 0f) - 30f * S.Outage - Mathf.Min(15f, 3f * (float)S.Exclusive.Count);
		S.Reputation = Mathf.Clamp(Mathf.Lerp(S.Reputation, rep, 0.04f), 0f, 100f);
		float trust = 35f + (float)(30 - S.Commission) * 1.5f + Mathf.Min(30f, S.Share * 100f);
		S.Trust = Mathf.Clamp(Mathf.Lerp(S.Trust, trust, 0.03f), 0f, 100f);
		float coverage = Mathf.Clamp(0.15f + S.Trust / 100f * 0.6f + S.Share, 0.1f, 1f);
		S.Coverage = Mathf.Lerp(S.Coverage, coverage, 0.05f);
		long commission = OthersGames().Sum(CommissionOn);
		if (commission > 0)
		{
			G.MS.Earn(commission, 8);
			S.CommissionMonth += commission;
			S.CommissionTotal += commission;
		}
		while (S.Accounts >= S.NextMilestone)
		{
			News("<b>" + S.Name + "</b> passed " + MmoText(S.NextMilestone) + " accounts.");
			S.NextMilestone = ((S.NextMilestone < 10000000) ? (S.NextMilestone * 10) : (S.NextMilestone + 25000000));
		}
		if (S.Outage > 0.2f && UnityEngine.Random.value < 0.25f)
		{
			News("<b>" + S.Name + "</b> keeps going down: your servers can't keep up. Players are getting annoyed.");
		}
	}

	private static string MmoText(long n)
	{
		if (n >= 1000000)
		{
			return n / 1000000 + " million";
		}
		return n.ToString("N0");
	}

	private static long Servers(long need)
	{
		long left = need;
		roomScript[] rooms = (G.MS ? G.MS.arrayRoomScripts : null);
		if (rooms == null)
		{
			return 0L;
		}
		foreach (roomScript r in rooms)
		{
			if (left <= 0)
			{
				break;
			}
			if ((bool)r && r.typ == 15 && r.serverReservieren == 0)
			{
				left = r.SetAbos(left);
			}
		}
		return need - left;
	}

	public static void ServerSpace(out long free, out long capacity)
	{
		free = 0L;
		capacity = 0L;
		roomScript[] rooms = (G.MS ? G.MS.arrayRoomScripts : null);
		if (rooms == null)
		{
			return;
		}
		foreach (roomScript r in rooms)
		{
			if ((bool)r && r.typ == 15 && r.serverReservieren == 0)
			{
				capacity += r.GetServerplatz();
				free += r.GetFreeServerplatz();
			}
		}
	}

	public static void News(string text)
	{
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsInfo(text);
		}
	}

	// ---- Projects: building the store and its features ----

	public static StoreFeature Feature(int id)
	{
		return Features.FirstOrDefault((StoreFeature f) => f.Id == id);
	}

	public static UpdateProject Running()
	{
		return GameUpdates.Projects.FirstOrDefault((UpdateProject p) => GameUpdates.IsStore(p.Kind));
	}

	public static string FeatureProblem(StoreFeature f)
	{
		if (!S.Built)
		{
			return "Build your store first.";
		}
		if (HasFeature(f.Id))
		{
			return f.Name + " is already part of your store.";
		}
		if (Year < f.Year)
		{
			return f.Name + " becomes possible in " + f.Year + ".";
		}
		return null;
	}

	public static UpdatePlan PlanBuild()
	{
		return new UpdatePlan
		{
			Work = BuildWork,
			Cost = BuildCost
		};
	}

	public static UpdatePlan PlanFeature(StoreFeature f)
	{
		return new UpdatePlan
		{
			Problem = FeatureProblem(f),
			Work = f.Work,
			Cost = f.Cost
		};
	}

	// Checks before a store project starts (GameUpdates.Start).
	public static string ProjectProblem(UpdateProject p)
	{
		string locked = LockReason();
		if (locked != null)
		{
			return locked;
		}
		UpdateProject running = Running();
		if (running != null)
		{
			return "Your team is already working on " + running.Title + ".";
		}
		if (p.Kind == UpdateKind.StoreBuild)
		{
			if (S.Built)
			{
				return "Your store is already built.";
			}
			if (string.IsNullOrWhiteSpace(p.StoreName))
			{
				return "Give your store a name.";
			}
			return null;
		}
		StoreFeature f = Feature(p.Usk);
		if (f == null)
		{
			return "Unknown store feature.";
		}
		return FeatureProblem(f);
	}

	public static string Complete(UpdateProject p)
	{
		if (p.Kind == UpdateKind.StoreBuild)
		{
			if (S.Built)
			{
				return null;
			}
			S.Built = true;
			S.Name = (string.IsNullOrWhiteSpace(p.StoreName) ? "Your store" : p.StoreName.Trim());
			S.Logo = Mathf.Clamp(p.StoreLogo, 1, LogoCount);
			S.BuiltWeek = CopyProtectDev.AbsWeek;
			S.BuiltYear = Year;
			S.Share = 0f;
			S.Reputation = 60f;
			S.Trust = 50f;
			S.Coverage = 0.2f;
			GameUpdates.Touch();
			return "<b>" + S.Name + "</b> is open!\n\nPC players can now buy games from your store. It starts small: features, a fair commission, a big catalogue and enough servers make it grow. Your own PC games get a spot on its front page.";
		}
		StoreFeature f = Feature(p.Usk);
		if (f == null || HasFeature(f.Id))
		{
			return null;
		}
		S.Features.Add(f.Id);
		GameUpdates.Touch();
		return "<b>" + S.Name + "</b> now has " + f.Name.ToLower() + ".\n\n" + f.About;
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
			Plugin.Log.LogError("Saving the store failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new StoreState();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			S = ((___es3file != null && ___es3file.KeyExists(SaveKey)) ? JsonConvert.DeserializeObject<StoreState>(___es3file.Load<string>(SaveKey)) : null) ?? new StoreState();
			S.Features = S.Features ?? new List<int>();
			S.Rivals = S.Rivals ?? new Dictionary<string, float>();
			S.Exclusive = S.Exclusive ?? new List<int>();
			S.ShareHistory = S.ShareHistory ?? new List<float>();
			S.AccountHistory = S.AccountHistory ?? new List<long>();
			S.Announced = S.Announced ?? new List<string>();
			if (S.Built)
			{
				Plugin.Log.LogInfo("Store loaded: " + S.Name + ", " + S.Accounts + " accounts");
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading the store failed: " + ex);
			S = new StoreState();
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new StoreState();
	}
}
