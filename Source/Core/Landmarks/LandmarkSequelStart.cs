using System;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Updates;
using HolyAddon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.Core.Landmarks;

// Choosing a landmark series' next game when you start the sequel (the user's design, 16 Nov 2026; plan doc
// Docs/Landmarks-Release2-Plan.md, "Sequels chosen at the start"):
// - Picking a game of your landmark series in the game's Sequel list opens the landmark pop-up over the design screen:
//   the series' next real game (the earliest open entry whose years have come), what it needs to be received as it
//   was (its review bar, and out by 5 years after its year), and its rewards.
// - Make It: the sequel is that game from the start, with its name and pictures. It stays that game whatever its
//   review, so the series always goes on. With the bar and on time it gets the entry's rewards at release; otherwise
//   it doesn't.
// - Just a Sequel: an ordinary sequel, and the offer when it's finished doesn't come up.
// - Closing the pop-up leaves the choice for when it's finished (the old offer), as for sequels started before this.
internal static partial class Landmarks
{
	// The choice made on this design screen: the entry (Make It) or null (Just a Sequel).
	private class StartPlan
	{
		public Menu_DevGame Menu;

		public LandmarkEntry Entry;
	}

	// A sequel takes a year or more to make, so it can be started a year before the entry could come out.
	private const int StartLead = 1;

	private static StartPlan startPlan;

	private static bool sequelStarting;

	private static gameScript sequelCreated;

	// Every design screen starts with no choice made (InitNachfolger calls Init first, then asks).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "Init")]
	private static void DesignScreenOpened()
	{
		startPlan = null;
		if (Offer != null && Offer.AtStart)
		{
			Offer = null;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitNachfolger")]
	private static void SequelSetUp(Menu_DevGame __instance, int orgID_)
	{
		try
		{
			LandmarkOffer o = StartCheck(__instance, orgID_);
			if (o != null)
			{
				Offer = o;
				EditorWindow.Landmark.Open();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Landmark sequel check failed: " + ex);
		}
	}

	// The series' next real game, if this is a sequel of your landmark series and one's years have come.
	private static LandmarkOffer StartCheck(Menu_DevGame menu, int orgId)
	{
		if (!On || !G.MS || !menu || menu.typ_contractGame)
		{
			return null;
		}
		gameScript org = GameUpdates.FindGame(orgId);
		gameScript main = (org ? GameUpdates.FindGame((org.mainIP >= 0) ? org.mainIP : org.myID) : null);
		LandmarkDef d = Of(main);
		if (d == null || d.Mmo || d.Entries.Count == 0 || main.ownerID != G.MS.myID)
		{
			return null;
		}
		FixLines();
		bool[] used = InUse();
		int year = G.MS.year;
		LandmarkEntry next = (from e in d.Entries
			where year >= e.From - StartLead && year <= e.Year + Window && EntryOpen(e, used)
			orderby e.Year
			select e).FirstOrDefault();
		if (next == null)
		{
			return null;
		}
		return new LandmarkOffer
		{
			Def = d,
			Entry = next,
			AtStart = true,
			DevMenu = menu,
			Original = org
		};
	}

	// A start offer whose design screen has closed is dropped (so it never shows over something else).
	public static void DropStaleStart()
	{
		LandmarkOffer o = Offer;
		if (o != null && o.AtStart && (!o.DevMenu || !o.DevMenu.gameObject.activeInHierarchy))
		{
			Offer = null;
		}
	}

	// Make It: the design screen shows the entry's name now; the game becomes it when development starts.
	public static string CommitAtStart()
	{
		LandmarkOffer o = Offer;
		Offer = null;
		if (o == null || !o.AtStart || o.Entry == null || !o.DevMenu || !o.DevMenu.gameObject.activeInHierarchy)
		{
			return "Too late: the design screen has closed.";
		}
		LandmarkEntry e = o.Entry;
		startPlan = new StartPlan
		{
			Menu = o.DevMenu,
			Entry = e
		};
		try
		{
			o.DevMenu.uiObjects[0].GetComponent<InputField>().text = e.Name;
		}
		catch
		{
		}
		return "Your sequel is <b>" + e.Name + "</b>, the next " + e.Of.Name + " game" + (e.Milestone ? ", a landmark in its own right" : "") + ". To be received as it was, it needs a review of " + e.Bar + "% or more and to come out by " + (e.Year + Window) + ".";
	}

	// Just a Sequel: an ordinary sequel, with no offer when it's finished.
	public static string DeclineAtStart()
	{
		LandmarkOffer o = Offer;
		Offer = null;
		if (o != null && o.AtStart && (bool)o.DevMenu)
		{
			startPlan = new StartPlan
			{
				Menu = o.DevMenu,
				Entry = null
			};
		}
		return "It's an ordinary sequel." + ((o?.Entry != null) ? (" " + o.Entry.Name + " stays open for a later one.") : "");
	}

	// ---- Development starts: the choice goes onto the new game ----

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_DevGame), "BUTTON_Start")]
	private static void SequelStarting(Menu_DevGame __instance, out bool __state)
	{
		__state = startPlan != null && startPlan.Menu == __instance && __instance.typ_nachfolger;
		sequelStarting = __state;
		sequelCreated = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(games), "CreateNewGame")]
	private static void SequelCreated(gameScript __result)
	{
		if (sequelStarting)
		{
			sequelCreated = __result;
		}
	}

	// (BUTTON_Start can stop before making the game, a missing platform say: then the choice waits for the next try.)
	[HarmonyFinalizer]
	[HarmonyPatch(typeof(Menu_DevGame), "BUTTON_Start")]
	private static Exception SequelStarted(bool __state, Exception __exception)
	{
		sequelStarting = false;
		if (__state && (bool)sequelCreated && __exception == null)
		{
			try
			{
				ApplyStartPlan(sequelCreated, startPlan.Entry);
				startPlan = null;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Landmark sequel start: " + ex);
			}
		}
		sequelCreated = null;
		return __exception;
	}

	private static void ApplyStartPlan(gameScript g, LandmarkEntry e)
	{
		if (e == null)
		{
			if (!S.Declined.Contains(g.myID))
			{
				S.Declined.Add(g.myID);
			}
			return;
		}
		FixLines();
		bool[] used = InUse();
		if (!EntryOpen(e, used))
		{
			IndustryCore.Feed(e.Name + " was taken by then: your sequel is an ordinary one.");
			return;
		}
		int i = EntryLine(e);
		if (used != null && i >= 0 && i < used.Length)
		{
			used[i] = true;
		}
		g.SetMyName(e.Name);
		g.myNameTeil1 = e.Name;
		S.Entries[e.Key] = new LandmarkRecord
		{
			Id = e.Key,
			GameId = g.myID,
			StudioId = G.MS.myID,
			Player = true,
			Committed = true
		};
		IndustryCore.Feed("You're making " + e.Name + ", the next " + e.Of.Name + " game.");
		Plugin.Log.LogInfo("Landmarks: your sequel (game " + g.myID + ") is " + e.Key + " from the start");
	}

	// A sequel you chose at the start and then scrapped frees its entry again.
	private static bool Scrapped(LandmarkRecord r)
	{
		if (r != null && r.Player && r.Committed && !r.Released)
		{
			return !GameObject.Find("GAME_" + r.GameId);
		}
		return false;
	}
}
