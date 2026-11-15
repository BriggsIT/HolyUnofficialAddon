using System;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core.Updates;

namespace HolyAddon.Core.Industry;

// Industry events that carry on into the game's own screens.
// - Share a Stand?: when you book your stand at the summer trade fair (Menu_MesseSelect), the studio you agreed with
//   pays half of it and shows its game there. If the fair goes by without you, the deal lapses.
[HarmonyPatch]
internal static class IndustryHooks
{
	private static IndustryState S => IndustryCore.State;

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_MesseSelect), "BUTTON_OK")]
	private static void BeforeStand(out long __state)
	{
		__state = (G.MS ? G.MS.money : 0L);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_MesseSelect), "BUTTON_OK")]
	private static void StandBooked(long __state)
	{
		try
		{
			StandShare share = S.Stand;
			if (share == null || !G.MS || share.Year != G.MS.year)
			{
				return;
			}
			long paid = __state - G.MS.money;
			if (paid <= 0)
			{
				// Nothing booked (no game picked): the game asked again.
				return;
			}
			S.Stand = null;
			publisherScript p = IndustryCore.Studio(share.StudioId);
			if (!p || p.Geschlossen())
			{
				return;
			}
			long half = paid / 2;
			G.MS.Pay(-half, 17);
			gameScript theirs = GameUpdates.FindGame(share.GameId);
			theirs?.AddHype(3f);
			foreach (gameScript g in IndustryCore.MyGamesInDevelopment())
			{
				g.AddHype(3f);
			}
			IndustryCore.Change(p, 8, "Shared your trade-fair stand");
			string text = "<b>" + p.GetName() + "</b> pays half your trade-fair stand (" + G.Money(half) + ")" + (theirs ? (" and shows " + theirs.GetNameSimple() + " next to your games") : "") + ". Hype +3 for both.";
			IndustryCore.News(text);
			IndustryCore.Feed(text.Replace("<b>", "").Replace("</b>", ""));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Shared trade-fair stand failed: " + ex);
		}
	}

	// Cover Disc: the unfinished demo is remembered when the reviews come in (the game's own review score, set just
	// before it goes on the market).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void DemoRemembered(gameScript __instance)
	{
		try
		{
			if (!__instance || S.CoverDiscs == null || !S.CoverDiscs.Remove(__instance.myID))
			{
				return;
			}
			if (UnityEngine.Random.value < 0.35f && __instance.reviewTotal > 10)
			{
				int drop = UnityEngine.Random.Range(3, 7);
				__instance.reviewTotal = Math.Max(10, __instance.reviewTotal - drop);
				string text = "Reviewers still remember the rough cover-disc demo of <b>" + __instance.GetNameSimple() + "</b>: its review score is " + drop + " points lower.";
				IndustryCore.News(text);
				IndustryCore.Feed(text.Replace("<b>", "").Replace("</b>", ""));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Cover disc at release failed: " + ex);
		}
	}

	// ---- Engine Request: a studio's next game on your engine ----

	// The game's own engine pick for an NPC game (publisherScript.FindEngineForGameNew), done for your engine when a
	// studio has a deal: the same fields the game sets, with the agreed share of the game's profit (paid by the game's
	// own engine statement when the game leaves the market). The fee was paid when the deal was made.
	[HarmonyPrefix]
	[HarmonyPatch(typeof(publisherScript), "FindEngineForGameNew")]
	private static bool LicensedEngine(publisherScript __instance, gameScript gS_)
	{
		try
		{
			EngineDeal deal = S.EngineDeals?.FirstOrDefault((EngineDeal d) => d.StudioId == __instance.myID);
			if (deal == null || !gS_ || !G.MS)
			{
				return true;
			}
			S.EngineDeals.Remove(deal);
			engineScript e = G.MS.arrayEnginesScripts?.FirstOrDefault((engineScript x) => (bool)x && x.myID == deal.EngineId);
			engineFeatures ef = GameUpdates.EF;
			if (!e || !ef || e.ownerID != G.MS.myID || e.updating || !e.Complete())
			{
				return true;
			}
			gS_.engineID = e.myID;
			gS_.engineS_ = e;
			gS_.engineGewinnbeteiligung = deal.Share;
			for (int i = 0; i < 4; i++)
			{
				gS_.engineFeature_DevDone[i] = true;
			}
			gS_.gameEngineFeature[0] = e.GetBestFeature(ef.GetTypGrafik());
			gS_.gameEngineFeature[1] = e.GetBestFeature(ef.GetTypSound());
			gS_.gameEngineFeature[2] = e.GetBestFeature(ef.GetTypKI());
			gS_.gameEngineFeature[3] = e.GetBestFeature(ef.GetTypPhysik());
			for (int j = 0; j < 4; j++)
			{
				gS_.costs_entwicklung += ef.GetDevCosts(gS_.gameEngineFeature[j]);
			}
			IndustryCore.Feed(__instance.GetName() + " starts its next game on " + e.GetName() + ", your engine.");
			return false;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Engine deal failed: " + ex);
			return true;
		}
	}

	// ---- The Holy Expansion's weak console launches ----

	// Holy can give a console you bought an early dev kit for a weak launch (DevKits.Flop: it halves the machines it
	// will ever sell). The "sells out at launch" headline must never praise one of those.
	public static void WatchHolyFlops(Harmony harmony)
	{
		try
		{
			Type devKits = HolyCompat.HolyType("HolyExpansion.Systems.DevKits");
			System.Reflection.MethodInfo flop = ((devKits != null) ? AccessTools.Method(devKits, "Flop") : null);
			if (flop != null)
			{
				harmony.Patch(flop, null, new HarmonyMethod(typeof(IndustryHooks), "Flopped"));
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Watching Holy's console launches: " + ex.Message);
		}
	}

	private static void Flopped(platformScript __0)
	{
		if ((bool)__0 && S.FloppedConsoles != null && !S.FloppedConsoles.Contains(__0.myID))
		{
			S.FloppedConsoles.Add(__0.myID);
		}
	}

	// A console Holy may still give a weak launch (an early dev kit bought, its launch not rolled yet), or did.
	public static bool WeakLaunch(platformScript p)
	{
		if (!p)
		{
			return false;
		}
		if (S.FloppedConsoles != null && S.FloppedConsoles.Contains(p.myID))
		{
			return true;
		}
		try
		{
			object kits = HolyCompat.HolyState("DevKits");
			if (kits == null)
			{
				return false;
			}
			System.Collections.IEnumerable early = HolyCompat.Get<System.Collections.IEnumerable>(kits, "EarlyKits");
			System.Collections.IEnumerable rolled = HolyCompat.Get<System.Collections.IEnumerable>(kits, "FlopRolled");
			bool inEarly = early != null && Contains(early, p.myID);
			bool done = rolled != null && Contains(rolled, p.myID);
			return inEarly && !done;
		}
		catch
		{
			return false;
		}
	}

	private static bool Contains(System.Collections.IEnumerable list, int id)
	{
		foreach (object o in list)
		{
			if (o is int i && i == id)
			{
				return true;
			}
		}
		return false;
	}

	public static void Weekly()
	{
		if (!G.MS)
		{
			return;
		}
		StandShare share = S.Stand;
		// The fair is in July's first week: by the end of July (or another year) it's gone by without you.
		if (share != null && (G.MS.year != share.Year || G.MS.month >= 8))
		{
			S.Stand = null;
			publisherScript p = IndustryCore.Studio(share.StudioId);
			IndustryCore.Change(p, -6, "You skipped the trade fair after promising them half a stand");
			if ((bool)p)
			{
				IndustryCore.Feed("You didn't go to the trade fair, so " + p.GetName() + " missed it too.");
			}
		}
		IndustryCommunity.Weekly();
		try
		{
			IndustryReal.Weekly();
			Topics.ExtraTopics.Weekly();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Industry weekly (stock, production, cartoons): " + ex.Message);
		}
		// Holy named a mod of the month for one of your games: the Mod Team decision, when nothing else is waiting.
		if (S.Pending == null && S.ModHits != null && S.ModHits.Count > 0 && IndustryCore.Quiet() && (!S.LastFired.TryGetValue("modteam", out var last) || IndustryCore.Now - last >= 26))
		{
			ForceIfReady("modteam");
		}
		try
		{
			IndustryEvents.RestoreOldExits();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Restoring cancelled consoles: " + ex.Message);
		}
		// A console maker may quit hardware: rolled once a year from 2000 to 2009, and it only happens to a maker
		// that's really struggling (the event's own conditions).
		if (S.Pending == null && IndustryCore.Year >= 2000 && IndustryCore.Year <= 2009 && !S.LastFired.ContainsKey("hwquit") && IndustryCore.Quiet() && (!S.Cooldowns.TryGetValue("hwquit:roll", out var rolled) || IndustryCore.Now - rolled >= 48))
		{
			S.Cooldowns["hwquit:roll"] = IndustryCore.Now;
			if (UnityEngine.Random.value < IndustryEvents.HardwareQuitChance)
			{
				ForceIfReady("hwquit");
			}
		}
		// A closed studio's series is free to sell: the Closure Auction, when nothing else is waiting.
		if (S.Pending == null && IndustryCore.Year >= 1985 && IndustryCore.Quiet() && (!S.LastFired.TryGetValue("auction", out var lastAuction) || IndustryCore.Now - lastAuction >= IndustryEvents.AuctionGap))
		{
			ForceIfReady("auction");
		}
	}

	private static void ForceIfReady(string id)
	{
		try
		{
			IndustryEventDef d = IndustryEvents.Find(id);
			IndustryContext c = new IndustryContext();
			if (d != null && d.Setup(c))
			{
				IndustryCore.Force(new IndustryPending
				{
					Id = id,
					C = c,
					Week = IndustryCore.Now
				});
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Industry event " + id + ": " + ex.Message);
		}
	}
}
