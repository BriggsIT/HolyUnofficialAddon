using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Industry;

// Fan modding, on top of The Holy Expansion's Communities (from 1993: modders for your games with Mod Support or a
// Level Editor, a "Mod of the month", and modding tools and tournaments as Support room jobs):
// - Holy's "Mod of the month" starts the Mod Team decision (IndustryEvents3D): hire the modders, back them, or buy
//   the mod and sell it as a paid add-on (a real add-on game, made here).
// - A Support room can be set (its room menu toggle) to start Holy's modding tools and tournaments by itself, ahead
//   of answering calls: the calls stop while the job runs and start again when it's done.
// Reflection, like Core/HolyLaunch. Without Holy's Communities the Mod Team still comes up, on its own terms.
internal static class IndustryCommunity
{
	private static bool looked;

	private static MethodInfo forGame;

	private static MethodInfo startTools;

	private static MethodInfo startTourney;

	private static MethodInfo toolsBlocker;

	private static MethodInfo tourneyBlocker;

	private static MethodInfo preparing;

	private static MethodInfo applyTools;

	private static MethodInfo onGetter;

	private static MethodInfo toolsCost;

	private static IndustryState S => IndustryCore.State;

	private static void Look()
	{
		if (looked)
		{
			return;
		}
		looked = true;
		Type t = HolyCompat.HolyType("HolyExpansion.Systems.Communities");
		if (t == null)
		{
			return;
		}
		forGame = AccessTools.Method(t, "For");
		startTools = AccessTools.Method(t, "StartTools");
		startTourney = AccessTools.Method(t, "StartTournament");
		toolsBlocker = AccessTools.Method(t, "ToolsBlocker");
		tourneyBlocker = AccessTools.Method(t, "TournamentBlocker");
		preparing = AccessTools.Method(t, "Preparing");
		applyTools = AccessTools.Method(t, "ApplyTools");
		onGetter = AccessTools.PropertyGetter(t, "On");
		toolsCost = AccessTools.Method(t, "ToolsCost");
		if (forGame == null || startTools == null || startTourney == null || toolsBlocker == null || tourneyBlocker == null || preparing == null)
		{
			forGame = null;
			Plugin.Log.LogWarning("Holy's Communities weren't found: no Support room automation, and the Mod Team comes up on its own");
		}
	}

	// Holy is installed and its Communities are on.
	public static bool On
	{
		get
		{
			Look();
			if (forGame == null || !G.Singleplayer)
			{
				return false;
			}
			try
			{
				return onGetter == null || (bool)onGetter.Invoke(null, null);
			}
			catch
			{
				return false;
			}
		}
	}

	private static object Call(MethodInfo m, params object[] args)
	{
		try
		{
			return m?.Invoke(null, args);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Holy's Communities: " + (ex.InnerException ?? ex).Message);
			return null;
		}
	}

	private static object Record(gameScript g, bool create)
	{
		if (!On || !g)
		{
			return null;
		}
		return Call(forGame, g.myID, create);
	}

	public static long Modders(gameScript g)
	{
		return HolyCompat.Get<long>(Record(g, create: false), "Modders");
	}

	public static void ScaleModders(gameScript g, float factor)
	{
		object c = Record(g, create: true);
		if (c != null)
		{
			HolyCompat.Set(c, "Modders", (long)((float)HolyCompat.Get<long>(c, "Modders") * factor));
		}
	}

	public static bool HasTools(gameScript g)
	{
		return HolyCompat.Get<bool>(Record(g, create: false), "ModTools");
	}

	// Holy's own release of modding tools (its effects and news), without the Support room job.
	public static void GiveTools(gameScript g)
	{
		if (On && applyTools != null && !HasTools(g))
		{
			Call(applyTools, g);
		}
	}

	// What Holy charges for a game's official modding tools (its Communities window and Support room job), or the same
	// price on Holy's curve without it.
	public static long ToolsCost()
	{
		Look();
		if (toolsCost != null && On && Call(toolsCost) is long cost && cost > 0)
		{
			return cost;
		}
		return Prices.Scaled(60000.0);
	}

	// ---- Make It Official: priced and rated like the game's own add-ons ----

	private static Menu_Dev_AddonDo addonMenu;

	// The game's add-on screen (Menu_Dev_AddonDo) prices each kind of content as a share of the main game's development
	// cost (capped at $100M), and adds a share of add-on quality for each (an add-on with none sells 40% less). A fan
	// mod counts as two kinds of content: buying it costs what making that add-on would, and it sells like one.
	public static void AddonOffer(gameScript main, out long price, out float quality)
	{
		long devCost = Math.Min(100000000L, Math.Max(0L, main ? main.GetGesamteEntwicklungskosten() : 0L));
		float costShare = 0.2f;
		quality = 0.2f;
		try
		{
			if (!addonMenu)
			{
				addonMenu = Resources.FindObjectsOfTypeAll<Menu_Dev_AddonDo>().FirstOrDefault();
				if ((bool)addonMenu && addonMenu.devCostsPercent != null && addonMenu.devPointsPercent != null)
				{
					Plugin.Log.LogInfo("The game's add-on content: cost shares " + string.Join(", ", addonMenu.devCostsPercent.Select((float v) => v.ToString("0.###"))) + "; quality " + string.Join(", ", addonMenu.devPointsPercent.Select((float v) => v.ToString("0.###"))));
				}
			}
			float[] costs = (addonMenu ? addonMenu.devCostsPercent : null);
			float[] points = (addonMenu ? addonMenu.devPointsPercent : null);
			if (costs != null && points != null && costs.Length >= 2 && points.Length == costs.Length)
			{
				int a = UnityEngine.Random.Range(0, costs.Length);
				int b = (a + UnityEngine.Random.Range(1, costs.Length)) % costs.Length;
				costShare = costs[a] + costs[b];
				quality = Mathf.Clamp(points[a] + points[b], 0f, 0.4f);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("The game's add-on prices weren't found: " + ex.Message);
		}
		price = Math.Max(Prices.Scaled(20000.0), Prices.Round((double)devCost * (double)costShare));
	}

	public static void Buzz(gameScript g, int weeks)
	{
		object c = Record(g, create: true);
		if (c != null)
		{
			HolyCompat.Set(c, "BuzzWeeks", Math.Max(HolyCompat.Get<int>(c, "BuzzWeeks"), weeks));
		}
	}

	// ---- Holy's "Mod of the month": it names the mod (ModName) right before announcing it ----

	public static void Hook(Harmony harmony)
	{
		try
		{
			Type t = HolyCompat.HolyType("HolyExpansion.Systems.Communities");
			MethodInfo name = ((t != null) ? AccessTools.Method(t, "ModName") : null);
			if (name != null)
			{
				harmony.Patch(name, null, new HarmonyMethod(typeof(IndustryCommunity), "ModOfTheMonth"));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Watching Holy's mods of the month: " + ex.Message);
		}
	}

	private static void ModOfTheMonth(gameScript __0, string __result)
	{
		if ((bool)__0 && !string.IsNullOrEmpty(__result) && S.ModHits != null)
		{
			S.ModHits.Add(new ModHit
			{
				GameId = __0.myID,
				Name = __result,
				Week = IndustryCore.Now
			});
			if (S.ModHits.Count > 8)
			{
				S.ModHits.RemoveAt(0);
			}
		}
	}

	// ---- Support rooms on community work ----

	public static bool Auto(roomScript room)
	{
		if ((bool)room && S.AutoSupport != null)
		{
			return S.AutoSupport.Contains(room.myID);
		}
		return false;
	}

	public static void SetAuto(roomScript room, bool on)
	{
		if (!room)
		{
			return;
		}
		S.AutoSupport.Remove(room.myID);
		if (on)
		{
			S.AutoSupport.Add(room.myID);
		}
	}

	private static roomScript Room(int id)
	{
		roomScript[] all = (G.MS ? G.MS.arrayRoomScripts : null);
		return all?.FirstOrDefault((roomScript r) => (bool)r && r.myID == id);
	}

	public static void Weekly()
	{
		if (S.AutoSupport == null || S.AutoSupport.Count == 0 || !On)
		{
			return;
		}
		foreach (int id in S.AutoSupport.ToList())
		{
			roomScript room = Room(id);
			if (!room || room.typ != 7)
			{
				S.AutoSupport.Remove(id);
				S.PausedCalls.Remove(id);
				continue;
			}
			try
			{
				Tend(room);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Support room community work: " + ex.Message);
			}
		}
	}

	private static void Tend(roomScript room)
	{
		// Calls paused for Holy's job: back to the phones once it's done (or was cancelled).
		if (S.PausedCalls.Contains(room.myID))
		{
			if (room.taskID == -1)
			{
				StartCalls(room);
				S.PausedCalls.Remove(room.myID);
			}
			return;
		}
		bool onCalls = (bool)room.GetTaskSupport();
		if (room.taskID != -1 && !onCalls)
		{
			// Busy with something else (a fan campaign, the convention): leave it.
			return;
		}
		List<gameScript> mine = IndustryCore.MyGamesOnMarket();
		gameScript tools = (from g in mine
			where Call(toolsBlocker, g) == null && Call(preparing, g) == null
			orderby g.sellsPerWeek[0] descending
			select g).FirstOrDefault();
		gameScript tourney = ((!tools) ? (from g in mine
			where Call(tourneyBlocker, g) == null && Call(preparing, g) == null
			orderby g.sellsPerWeek[0] descending
			select g).FirstOrDefault() : null);
		gameScript pick = (tools ? tools : tourney);
		if (!pick)
		{
			return;
		}
		if (onCalls)
		{
			StopCalls(room);
		}
		string problem = Call(tools ? startTools : startTourney, pick, room) as string;
		if (problem != null && room.taskID == -1)
		{
			if (onCalls)
			{
				StartCalls(room);
			}
			return;
		}
		if (onCalls)
		{
			S.PausedCalls.Add(room.myID);
		}
		IndustryCore.Feed((string.IsNullOrEmpty(room.myName) ? "A Support room" : room.myName) + " starts " + (tools ? "modding tools" : "a tournament") + " for " + pick.GetNameSimple() + (onCalls ? ", and the phones wait." : "."));
	}

	// The base game's "answer calls" job, as its own menu stops and starts it.
	private static void StopCalls(roomScript room)
	{
		taskSupport t = room.GetTaskSupport();
		if ((bool)t)
		{
			t.gameObject.name = "Task_done";
			t.Abbrechen();
		}
		room.taskID = -1;
		room.taskGameObject = null;
	}

	private static void StartCalls(roomScript room)
	{
		if (!G.Gui || room.taskID != -1)
		{
			return;
		}
		taskSupport t = G.Gui.AddTask_Support();
		t.Init(false);
		room.taskID = t.myID;
		room.taskGameObject = t.gameObject;
	}

	// ---- Make It Official: the mod as a paid add-on, a real add-on game released today ----

	public static string AddonProblem(gameScript main)
	{
		if (!main || !main.isOnMarket)
		{
			return "The game is no longer on sale.";
		}
		if (main.gameTyp != 0 || main.typ_addon || main.typ_addonStandalone || main.typ_mmoaddon || main.arcade || main.handy)
		{
			return "Only for a boxed or download game of your own (MMOs get expansion packs in MMO Management).";
		}
		if (!main.HatEinePlattformInternet())
		{
			return "It needs a platform that can sell add-ons online.";
		}
		return null;
	}

	public static gameScript MakeAddon(gameScript main, string modName, long cost, float quality)
	{
		try
		{
			gameScript a = UnityEngine.Object.Instantiate(main.gameObject).GetComponent<gameScript>();
			G.MS.games_.InitMMOtoF2PGame(a);
			a.gameTab_ = null;
			a.SetMyName(main.GetNameSimple() + ": " + modName);
			a.gameTyp = 0;
			a.typ_standard = false;
			a.typ_nachfolger = false;
			a.typ_remaster = false;
			a.typ_spinoff = false;
			a.typ_budget = false;
			a.typ_goty = false;
			a.typ_bundle = false;
			a.typ_bundleAddon = false;
			a.typ_addonStandalone = false;
			a.typ_mmoaddon = false;
			a.typ_addon = true;
			a.originalGameID = main.myID;
			a.mainIP = main.mainIP;
			a.portID = -1;
			a.developerID = G.MS.myID;
			a.publisherID = G.MS.myID;
			a.ownerID = G.MS.myID;
			a.pS_ = null;
			a.warBeiAwards = true;
			a.weeksOnMarket = 0;
			a.releaseDate = 0;
			a.vorbestellungen = 0;
			a.date_year = G.MS.year;
			a.date_month = G.MS.month;
			a.commercialHit = false;
			a.commercialFlop = false;
			a.addonQuality = quality;
			a.amountAddons = 0;
			a.amountMMOAddons = 0;
			a.amountUpdates = 0;
			a.bonusSellsAddons = 0f;
			a.bonusSellsUpdates = 0f;
			a.nachfolger_created = false;
			a.remaster_created = false;
			a.budget_created = false;
			a.goty_created = false;
			a.mmoTOf2p_created = false;
			a.f2pConverted = false;
			a.digitalVersion = true;
			a.retailVersion = false;
			a.sellsTotal = 0L;
			a.sellsTotalOnline = 0L;
			a.sellsTotalStandard = 0L;
			a.sellsTotalDeluxe = 0L;
			a.sellsTotalCollectors = 0L;
			a.umsatzTotal = 0L;
			a.umsatzInApp = 0L;
			a.umsatzAbos = 0L;
			a.costs_entwicklung = cost;
			a.costs_mitarbeiter = 0L;
			a.costs_marketing = 0L;
			a.costs_enginegebuehren = 0L;
			a.costs_server = 0L;
			a.costs_production = 0L;
			a.costs_updates = 0L;
			if (a.sellsPerWeek != null)
			{
				for (int i = 0; i < a.sellsPerWeek.Length; i++)
				{
					a.sellsPerWeek[i] = 0;
				}
			}
			if (a.lagerbestand != null)
			{
				for (int j = 0; j < a.lagerbestand.Length; j++)
				{
					a.lagerbestand[j] = 0L;
				}
			}
			if (a.verkaufspreis != null)
			{
				for (int k = 0; k < a.verkaufspreis.Length; k++)
				{
					a.verkaufspreis[k] = Mathf.Max(1, main.verkaufspreis[k] / 2);
				}
			}
			a.userPositiv = 0;
			a.userNegativ = 0;
			a.hype = Mathf.Max(main.hype * 0.5f, 10f);
			a.reviewTotal = Mathf.Clamp(main.reviewTotal + UnityEngine.Random.Range(-8, 6), 35, 95);
			a.SetOnMarket();
			GameUpdates.Touch();
			Plugin.Log.LogInfo("Mod made official: " + a.GetNameSimple() + " (game " + a.myID + ") for " + main.GetNameSimple() + " (game " + main.myID + ")");
			return a;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Making the mod official failed: " + ex);
			return null;
		}
	}
}
