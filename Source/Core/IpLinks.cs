using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace HolyAddon.Core;

// A game's link to its IP (gameScript.mainIP: the id of the IP's first game).
//
// The game's own lookup (gameScript.FindMainIpScript, used by GetIpIsInArchiv, GetIpName, GetIpBekanntheit...)
// forgets the link for good (mainIP = -1) when it can't find that game in the game list. While a save loads that
// list isn't complete yet, so anything asking then cuts games off their IPs. The addon's 0.11.5 to 0.11.7 did
// that through the game cards. The addon asks here instead, which never changes the link, and repairs links cut
// that way: every game you develop gets one (a new game its own, sequels, spin-offs, remasters, ports and addons
// their parent's), so a game of yours without one lost it.
[HarmonyPatch]
internal static class IpLinks
{
	private static float repairAt = -1f;

	// The IP's first game, or null. Never changes the link.
	public static gameScript Holder(gameScript g)
	{
		if (!g || g.mainIP == -1)
		{
			return null;
		}
		if ((bool)g.script_mainIP && g.script_mainIP.myID == g.mainIP)
		{
			return g.script_mainIP;
		}
		if (g.mainIP == g.myID)
		{
			return g;
		}
		gameScript[] all = (((bool)G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : null);
		if (all == null)
		{
			return null;
		}
		foreach (gameScript x in all)
		{
			if ((bool)x && x.myID == g.mainIP)
			{
				g.script_mainIP = x;
				return x;
			}
		}
		return null;
	}

	public static bool Archived(gameScript g)
	{
		gameScript holder = Holder(g);
		if ((bool)holder)
		{
			return holder.archiv_ip;
		}
		return false;
	}

	// ---- Repair ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Loaded()
	{
		// Once the game has finished building everything.
		repairAt = Time.unscaledTime + 2f;
	}

	public static void Tick()
	{
		if (repairAt >= 0f && Time.unscaledTime >= repairAt)
		{
			repairAt = -1f;
			Repair();
		}
	}

	// The week's sales use IPs: make sure a pending repair has run.
	[HarmonyPrefix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void BeforeWeek()
	{
		if (repairAt >= 0f)
		{
			repairAt = -1f;
			Repair();
		}
	}

	// The games the addon looked up (GameUpdates.BaseEligible): your own, released, not addons or bundles.
	private static bool Repairable(gameScript g, int me)
	{
		if ((bool)g && g.mainIP == -1 && g.ownerID == me && g.developerID == me && !g.inDevelopment && !g.typ_bundle && !g.typ_budget && !g.typ_bundleAddon && !g.typ_goty && !g.typ_addon && !g.typ_addonStandalone && !g.typ_mmoaddon && !g.typ_contractGame && !g.auftragsspiel)
		{
			return !g.pubAngebot;
		}
		return false;
	}

	public static int Repair()
	{
		if (!G.MS || !G.Singleplayer)
		{
			return 0;
		}
		try
		{
			gameScript[] all = UnityEngine.Object.FindObjectsOfType<gameScript>(includeInactive: true);
			Dictionary<int, gameScript> byId = new Dictionary<int, gameScript>();
			foreach (gameScript g in all)
			{
				if ((bool)g && !byId.ContainsKey(g.myID))
				{
					byId[g.myID] = g;
				}
			}
			int me = G.MS.myID;
			List<string> names = new List<string>();
			foreach (gameScript g in all.Where((gameScript x) => Repairable(x, me)).ToList())
			{
				int ip = IpFor(g, byId, 0);
				if (ip != -1)
				{
					g.mainIP = ip;
					g.script_mainIP = null;
					names.Add(g.GetNameSimple());
				}
			}
			if (names.Count > 0)
			{
				Plugin.Log.LogWarning("Reconnected " + names.Count + " of your games to their IPs (cut off while a save loaded): " + string.Join(", ", names));
			}
			return names.Count;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("IP repair: " + ex);
			return 0;
		}
	}

	// The IP a game got when it was made: its parent's (port of, sequel/spin-off/remaster of, addon for), or its
	// own if it has none. -1 when its parent can't be found.
	private static int IpFor(gameScript g, Dictionary<int, gameScript> byId, int depth)
	{
		int parent = ((g.portID != -1) ? g.portID : g.originalGameID);
		if (parent == -1 || parent == g.myID)
		{
			return g.myID;
		}
		if (depth > 30 || !byId.TryGetValue(parent, out var p) || !p)
		{
			return -1;
		}
		if (p.mainIP != -1)
		{
			return p.mainIP;
		}
		return IpFor(p, byId, depth + 1);
	}
}
