using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Updates;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HolyAddon.Core.Landmarks;

// Landmarks tied into the game's systems (Docs/Landmarks-Release2-Plan.md, part C). A "landmark game" here is a
// landmark or a milestone entry (Super Mario 64), by anyone; a "landmark series" is one whose first game is a landmark.
// - System seller: a landmark exclusive's console sales count double (the game's own exclusive sales: a studio's
//   console through BonusSellsExklusiv, yours in SellPlayer).
// - Platform deals: a landmark full exclusive on a studio's console earns a bonus from its maker at release; a timed
//   exclusive's deal pays 1.5x (TimedExclusives.PayDeal).
// - Publishers: a landmark earns 20% more per copy from its publisher (publisherScript.GetShare, read as it sells, and
//   shown in the publisher list); the Publishers Bid event offers 1.5x (IndustryEvents).
// - The long tail: a landmark's saturation grows 40% slower.
// - The series: sequels start with more hype, the IP is worth 1.5x (IpValueOf, not a hook on GetIpWert), the fan shop
//   sells 30% more.
// - Copycats: for a year after a landmark comes out, studios' new games lean to its genre and topic.
// - Awards: landmarks get a bonus in Game of the Year and Players' Choice, and the Hall of Fame (AwardsShow).
internal static partial class Landmarks
{
	private static HashSet<int> landmarkGames;

	private static HashSet<int> seriesGames;

	private static int cacheKey = int.MinValue;

	private static void Cache()
	{
		int key = S.Made.Count * 100003 + S.Entries.Count * 7 + IndustryCore.Now;
		if (landmarkGames != null && key == cacheKey)
		{
			return;
		}
		cacheKey = key;
		landmarkGames = new HashSet<int>();
		seriesGames = new HashSet<int>();
		foreach (LandmarkRecord r in S.Made.Values)
		{
			if (r.GameId >= 0 && !r.Missed)
			{
				landmarkGames.Add(r.GameId);
				seriesGames.Add(r.GameId);
			}
		}
		foreach (LandmarkRecord r2 in S.Entries.Values)
		{
			if (r2.GameId >= 0 && !r2.Short && Entry(r2.Id)?.Milestone == true)
			{
				landmarkGames.Add(r2.GameId);
			}
		}
	}

	// A landmark or a milestone entry (by anyone).
	public static bool IsLandmark(gameScript g)
	{
		if (!On || !g || (S.Made.Count == 0 && S.Entries.Count == 0))
		{
			return false;
		}
		Cache();
		return landmarkGames.Contains(g.myID);
	}

	// A game of a series whose first game is a landmark.
	public static bool IsLandmarkSeries(gameScript g)
	{
		if (!On || !g || S.Made.Count == 0)
		{
			return false;
		}
		Cache();
		if (!seriesGames.Contains(g.myID))
		{
			return seriesGames.Contains(g.mainIP);
		}
		return true;
	}

	// ---- Selling: the long tail, the publisher's rate, consoles ----

	private static gameScript selling;

	private static float saturation;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static void SellStart(gameScript __instance)
	{
		selling = null;
		if (IsLandmark(__instance))
		{
			selling = __instance;
			saturation = __instance.realsticPower;
		}
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(gameScript), "SellGame")]
	private static Exception SellEnd(Exception __exception)
	{
		if ((bool)selling)
		{
			float grown = selling.realsticPower - saturation;
			if (grown > 0f)
			{
				selling.realsticPower = saturation + grown * 0.6f;
			}
		}
		selling = null;
		return __exception;
	}

	// A landmark's publisher pays it more per copy (and the publisher list shows it).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(publisherScript), "GetShare")]
	private static void Share(publisherScript __instance, ref int __result)
	{
		if (LandmarkDeal(__instance))
		{
			__result = Mathf.RoundToInt((float)__result * 1.2f);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(publisherScript), "GetShareExklusiv")]
	private static void ShareExclusive(publisherScript __instance, ref int __result)
	{
		if (LandmarkDeal(__instance))
		{
			__result = Mathf.RoundToInt((float)__result * 1.2f);
		}
	}

	// The publisher list (Menu_Dev_SelectPublisher) open for a landmark: its rates show the landmark's.
	private static Menu_Dev_SelectPublisher picking;

	private static gameScript pickingFor;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_SelectPublisher), "Init")]
	private static void PublisherList(Menu_Dev_SelectPublisher __instance, gameScript game_)
	{
		picking = __instance;
		pickingFor = game_;
	}

	private static bool LandmarkDeal(publisherScript p)
	{
		if ((bool)selling)
		{
			if (selling.IsMyGame())
			{
				return selling.publisherID == p.myID;
			}
			return false;
		}
		if ((bool)picking && picking.gameObject.activeInHierarchy && (bool)pickingFor)
		{
			return IsLandmark(pickingFor);
		}
		return false;
	}

	// A studio's console: a landmark exclusive's console sales count double.
	[HarmonyPrefix]
	[HarmonyPatch(typeof(platformScript), "BonusSellsExklusiv")]
	private static void SystemSellerNpc(platformScript __instance, ref int i)
	{
		if ((bool)selling && i > 0 && __instance.IsVerfuegbar() && __instance.OwnerIsNPC())
		{
			selling.exklusivKonsolenSells += ((selling.gameTyp == 2) ? (i / 5) : i);
			i *= 2;
		}
	}

	// Your console: the same, with its sales and costs (as the game's own exclusive sales pay them).
	[HarmonyPrefix]
	[HarmonyPatch(typeof(platformScript), "SellPlayer")]
	private static void SystemSellerBefore(platformScript __instance, out Dictionary<gameScript, long> __state)
	{
		__state = null;
		if (!On || (S.Made.Count == 0 && S.Entries.Count == 0))
		{
			return;
		}
		foreach (gameScript g in IndustryCore.AllGames())
		{
			if (g.isOnMarket && (g.exklusiv || g.herstellerExklusiv) && g.gamePlatform != null && g.gamePlatform.Contains(__instance.myID) && IsLandmark(g))
			{
				__state = __state ?? new Dictionary<gameScript, long>();
				__state[g] = g.exklusivKonsolenSells;
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(platformScript), "SellPlayer")]
	private static void SystemSellerAfter(platformScript __instance, Dictionary<gameScript, long> __state)
	{
		if (__state == null)
		{
			return;
		}
		try
		{
			int extra = 0;
			foreach (KeyValuePair<gameScript, long> kv in __state)
			{
				if ((bool)kv.Key)
				{
					long d = kv.Key.exklusivKonsolenSells - kv.Value;
					if (d > 0)
					{
						kv.Key.exklusivKonsolenSells += d;
						extra += (int)d;
					}
				}
			}
			if (extra <= 0 || !G.MS)
			{
				return;
			}
			__instance.units += extra;
			__instance.sellsPerWeek[0] += extra;
			G.MS.AddVerkaufsverlaufKonsolen(extra);
			long cost = (long)extra * (long)__instance.GetAktuellProductionsCosts();
			G.MS.Pay(cost, 23);
			__instance.costs_production += cost;
			long income = (long)extra * (long)__instance.verkaufspreis;
			G.MS.Earn(income, 9);
			__instance.umsatzTotal += income;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Landmark console sales: " + ex.Message);
		}
	}

	// ---- The series ----

	// An IP's value with the landmark series' 1.5x. The game's GetIpWert can't be hooked: it returns a long, and on this
	// 32-bit game a hooked long comes back as garbage (StutterFinder.SafeReturn; 0.33.0's postfix made a 236-point IP
	// cost $1.09 billion at the Closure Auction). So the game's IP screens call this instead (IpScreens), and so do
	// the addon's own prices. publisherScript.GetFirmenwert also returns a long, so a company's value keeps the plain one.
	public static long IpValueOf(gameScript g)
	{
		long value = g.GetIpWert();
		if (value > 0 && IsLandmarkSeries(g))
		{
			value = (long)((double)value * 1.5);
		}
		return value;
	}

	// The game's IP market, sale result, IP lists and IP tooltip read the value through IpValueOf.
	[HarmonyPatch]
	private static class IpScreens
	{
		private static readonly string[][] Callers = new string[9][]
		{
			new string[2] { "gameScript", "GetTooltipIP" },
			new string[2] { "Item_IpHandel_Buy", "SetData" },
			new string[2] { "Item_IpHandel_Sell", "SetData" },
			new string[2] { "Item_MyGames_MyIPs", "SetData" },
			new string[2] { "Menu_Result_IpVerkauf", "Init" },
			new string[2] { "Menu_W_IpHandel_Buy", "Init" },
			new string[2] { "Menu_W_IpHandel_Buy", "BUTTON_Yes" },
			new string[2] { "Menu_Stats_ShowBestIPs", "ShowDaten" },
			new string[2] { "Menu_Stats_ShowMyIPs", "ShowDaten" }
		};

		private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
		{
			foreach (string[] c in Callers)
			{
				Type t = AccessTools.TypeByName(c[0]);
				foreach (System.Reflection.MethodInfo m in (t != null) ? AccessTools.GetDeclaredMethods(t).Where((System.Reflection.MethodInfo x) => x.Name == c[1]) : Enumerable.Empty<System.Reflection.MethodInfo>())
				{
					// Only methods safe to hook here (they return nothing, a reference or a small value).
					if (Performance.StutterFinder.SafeReturn(m))
					{
						yield return m;
					}
				}
			}
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code, System.Reflection.MethodBase __originalMethod)
		{
			System.Reflection.MethodInfo from = AccessTools.Method(typeof(gameScript), "GetIpWert");
			System.Reflection.MethodInfo to = AccessTools.Method(typeof(Landmarks), "IpValueOf");
			int swapped = 0;
			foreach (CodeInstruction ci in code)
			{
				if ((ci.opcode == System.Reflection.Emit.OpCodes.Callvirt || ci.opcode == System.Reflection.Emit.OpCodes.Call) && ci.operand as System.Reflection.MethodInfo == from)
				{
					ci.opcode = System.Reflection.Emit.OpCodes.Call;
					ci.operand = to;
					swapped++;
				}
				yield return ci;
			}
			if (swapped == 0)
			{
				Plugin.Log.LogWarning("Landmark IP value: " + __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name + " no longer reads GetIpWert, so it shows the plain value");
			}
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "GetHypeNachfolger")]
	private static void SequelHype(gameScript __instance, ref int __result)
	{
		// (Not once the series' real games have run out: LandmarkSeriesDone.)
		if (IsLandmarkSeries(__instance) && !SeriesDone(__instance))
		{
			__result += 10;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SellMerchandise")]
	private static void FanShop(gameScript __instance)
	{
		if (__instance.merchBestellungen == null || !IsLandmarkSeries(__instance))
		{
			return;
		}
		for (int j = 0; j < __instance.merchBestellungen.Length; j++)
		{
			__instance.merchBestellungen[j] = Mathf.RoundToInt((float)__instance.merchBestellungen[j] * 1.3f);
		}
	}

	// ---- At release: the platform maker's bonus, copycats, an anniversary remaster ----

	// A landmark of yours out as a full exclusive on a studio's console: its maker pays for the system seller.
	internal static void PlatformBonus(gameScript g, string name)
	{
		try
		{
			if (!g.exklusiv || TimedExclusives.Active(g) != null || g.gamePlatform == null || g.gamePlatform.Length == 0)
			{
				return;
			}
			platformScript p = GameUpdates.Platform(g.gamePlatform[0]);
			if (!p || !p.OwnerIsNPC())
			{
				return;
			}
			float rate = 0.1f + p.GetExklusivBonus() / 200f;
			long pay = (long)((float)GameUpdates.DevCost(g) * rate);
			if (pay <= 0)
			{
				return;
			}
			G.MS.Earn(pay, 2);
			string text = p.GetManufacturer() + " pays " + G.Money(pay) + " for " + name + ", a landmark exclusive on " + p.GetName() + ": it will sell their consoles.";
			IndustryCore.News(text);
			IndustryCore.Feed(text);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Landmark platform bonus: " + ex.Message);
		}
	}

	// A landmark is out: studios start copying it for a year.
	internal static void StartCopycats(gameScript g, string name)
	{
		if (!g || g.maingenre < 0)
		{
			return;
		}
		S.CopyGenre = g.maingenre;
		S.CopyTopic = g.gameMainTheme;
		S.CopyUntil = IndustryCore.Now + 48;
		S.CopyOf = name;
		IndustryCore.Feed("Studios rush to copy " + name + ": expect a wave of " + IndustryCore.GenreGames(g.maingenre).ToLowerInvariant() + ".");
	}

	private static bool Copying
	{
		get
		{
			if (On && S.CopyGenre >= 0)
			{
				return IndustryCore.Now <= S.CopyUntil;
			}
			return false;
		}
	}

	// A studio's new game: one in four goes for the landmark's genre (not a subsidiary's, which you set).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(publisherScript), "SetGenre")]
	private static void CopyGenre(publisherScript __instance, gameScript script_)
	{
		try
		{
			if (!Copying || !script_ || __instance.IsTochterfirma() || Random.value >= 0.25f)
			{
				return;
			}
			genres gen = __instance.genres_ ?? IndustryCore.Genres;
			if ((bool)gen && S.CopyGenre < gen.genres_UNLOCK.Length && gen.genres_UNLOCK[S.CopyGenre])
			{
				script_.maingenre = S.CopyGenre;
				if (script_.subgenre == S.CopyGenre)
				{
					script_.subgenre = -1;
				}
			}
		}
		catch
		{
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(publisherScript), "SetTheme")]
	private static void CopyTopic(publisherScript __instance, gameScript script_)
	{
		if (!Copying || !script_ || S.CopyTopic < 0 || script_.maingenre != S.CopyGenre || __instance.IsTochterfirma() || Random.value >= 0.5f)
		{
			return;
		}
		script_.gameMainTheme = S.CopyTopic;
		if (script_.gameSubTheme == S.CopyTopic)
		{
			script_.gameSubTheme = -1;
		}
	}

	// Your remaster of a landmark series, planned at its anniversary: an anniversary launch.
	private static void AnniversaryRemaster(gameScript g)
	{
		if (!g.typ_remaster || !G.MS || g.developerID != G.MS.myID || !S.Remasters.TryGetValue(g.mainIP, out var until))
		{
			return;
		}
		S.Remasters.Remove(g.mainIP);
		if (G.MS.year > until)
		{
			return;
		}
		g.AddHype(30f);
		genres gen = IndustryCore.Genres;
		int fans = (int)Mathf.Clamp((gen ? ((float)gen.GetAmountFans()) : 0f) * 0.02f, 1000f, 200000f);
		G.MS.AddFans(fans, g.maingenre);
		string text = "<b>" + g.GetNameSimple() + "</b> gets an anniversary launch: hype +30, and fans of the original come back.";
		IndustryCore.News(text);
		IndustryCore.Feed(text.Replace("<b>", "").Replace("</b>", ""));
	}

	// Plan an anniversary remaster of your landmark (the anniversary event): it counts until the year given.
	internal static void PlanRemaster(gameScript landmark, int untilYear)
	{
		if ((bool)landmark)
		{
			S.Remasters[(landmark.mainIP >= 0) ? landmark.mainIP : landmark.myID] = untilYear;
		}
	}

	// ---- For the landmark events (IndustryEventsLandmarks) ----

	// The series' main game (its IP) of a landmark record.
	internal static gameScript SeriesOf(LandmarkRecord r)
	{
		gameScript g = ((r != null && r.GameId >= 0) ? GameUpdates.FindGame(r.GameId) : null);
		if (!g)
		{
			return null;
		}
		if (g.mainIP < 0 || g.mainIP == g.myID)
		{
			return g;
		}
		return GameUpdates.FindGame(g.mainIP) ?? g;
	}

	// Landmarks that are out, whose series you own now (claimed, or bought since).
	internal static List<LandmarkRecord> YourSeries()
	{
		if (!On || !G.MS)
		{
			return new List<LandmarkRecord>();
		}
		return S.Made.Values.Where((LandmarkRecord r) => r.Released && !r.Missed && r.Year > 0 && (bool)SeriesOf(r) && SeriesOf(r).ownerID == G.MS.myID && !SeriesOf(r).inDevelopment).ToList();
	}

	internal static string TitleOf(LandmarkRecord r)
	{
		LandmarkDef d = Def(r.Id);
		if (d != null)
		{
			return d.Title;
		}
		gameScript g = GameUpdates.FindGame(r.GameId);
		if (!g)
		{
			return "the landmark";
		}
		return g.GetNameSimple();
	}

	// The studio that made it in history (still in business), or -1.
	internal static int HistoricStudio(LandmarkRecord r)
	{
		LandmarkDef d = Def(r.Id);
		if (d == null || string.IsNullOrEmpty(d.Studio))
		{
			return -1;
		}
		return StudioByName(d.Studio);
	}

	// ---- Awards ----

	// A landmark's nomination bonus (Game of the Year, Players' Choice).
	public static float AwardBonus(gameScript g)
	{
		if (!IsLandmark(g))
		{
			return 0f;
		}
		return 8f;
	}

	// The Hall of Fame: landmarks (and milestone entries) out ten or more years, best first, not in it yet.
	public static List<gameScript> HallOfFameCandidates(int year)
	{
		List<KeyValuePair<string, gameScript>> list = new List<KeyValuePair<string, gameScript>>();
		foreach (LandmarkRecord r in S.Made.Values)
		{
			gameScript g = ((r.Released && !r.Missed && r.Year > 0 && r.Year <= year - 10 && !S.HallOfFame.Contains(r.Id)) ? GameUpdates.FindGame(r.GameId) : null);
			if ((bool)g)
			{
				list.Add(new KeyValuePair<string, gameScript>(r.Id, g));
			}
		}
		foreach (LandmarkRecord r2 in S.Entries.Values)
		{
			if (!r2.Short && Entry(r2.Id)?.Milestone == true)
			{
				gameScript g2 =((r2.Released && r2.Year > 0 && r2.Year <= year - 10 && !S.HallOfFame.Contains(r2.Id)) ? GameUpdates.FindGame(r2.GameId) : null);
				if ((bool)g2)
				{
					list.Add(new KeyValuePair<string, gameScript>(r2.Id, g2));
				}
			}
		}
		return (from kv in list
			orderby kv.Value.reviewTotal descending
			select kv.Value).ToList();
	}

	// The inducted game (the first candidate) joins the Hall of Fame for good.
	public static void Inducted(gameScript g)
	{
		if (!g)
		{
			return;
		}
		LandmarkRecord r = S.Made.Values.FirstOrDefault((LandmarkRecord x) => x.GameId == g.myID) ?? S.Entries.Values.FirstOrDefault((LandmarkRecord x) => x.GameId == g.myID);
		if (r != null && !S.HallOfFame.Contains(r.Id))
		{
			S.HallOfFame.Add(r.Id);
		}
	}
}
