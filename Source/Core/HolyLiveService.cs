using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.Updates;

namespace HolyAddon.Core;

// The Holy Expansion's live service: with "Start the next season automatically" on, Holy began the next season
// instantly at the end of the last one, skipping the 250 work points a season normally takes in a Marketing room.
// Now the automatic season goes through a Marketing room like a manual one: Holy's own season task starts in the
// fastest free Marketing room (Holy charges for it as usual), or waits for one to come free.
[HarmonyPatch]
internal static class HolyLiveService
{
	private static bool looked;

	private static Type live;

	private static MethodInfo forGame;

	private static MethodInfo preparing;

	private static MethodInfo startTask;

	private static MethodInfo seasonCost;

	private static MethodInfo liveOn;

	private static MethodInfo freeRooms;

	private static MethodInfo problem;

	private static MethodInfo roomName;

	private static MethodInfo duration;

	// Games already told they're waiting for a room (once each).
	private static readonly HashSet<int> waiting = new HashSet<int>();

	private static void Look()
	{
		if (looked)
		{
			return;
		}
		looked = true;
		live = AccessTools.TypeByName("HolyExpansion.Systems.LiveService");
		Type tasks = AccessTools.TypeByName("HolyExpansion.Systems.RoomTasks");
		if (live == null || tasks == null)
		{
			live = null;
			return;
		}
		forGame = AccessTools.Method(live, "For");
		preparing = AccessTools.Method(live, "Preparing");
		startTask = AccessTools.Method(live, "StartSeasonTask");
		seasonCost = AccessTools.Method(live, "SeasonCost");
		liveOn = AccessTools.PropertyGetter(live, "On");
		freeRooms = AccessTools.Method(tasks, "FreeRooms");
		problem = AccessTools.Method(tasks, "Problem");
		roomName = AccessTools.Method(tasks, "RoomName");
		duration = AccessTools.Method(tasks, "Duration");
		if (forGame == null || preparing == null || startTask == null || seasonCost == null || freeRooms == null || problem == null)
		{
			live = null;
		}
	}

	public static void Apply(Harmony harmony)
	{
		Look();
		MethodInfo start = live?.GetMethod("StartSeason", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
		if (start == null)
		{
			return;
		}
		try
		{
			harmony.Patch(start, new HarmonyMethod(typeof(HolyLiveService), "StartSeasonPrefix"));
			Plugin.Log.LogInfo("Holy's automatic live-service seasons are prepared in a Marketing room");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Could not change Holy's automatic live-service seasons: " + ex.Message);
		}
	}

	private static bool StartSeasonPrefix(gameScript g)
	{
		if (!Plugin.LiveServiceInRoom.Value || !G.Singleplayer)
		{
			return true;
		}
		try
		{
			Renew(g);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Live-service season: " + ex.Message);
			return true;
		}
		return false;
	}

	// The next season's task in the fastest free Marketing room with staff; false while it waits for one.
	private static bool Renew(gameScript g)
	{
		object l = forGame.Invoke(null, new object[1] { g.myID });
		if (l == null)
		{
			return false;
		}
		if (preparing.Invoke(null, new object[1] { g }) != null)
		{
			waiting.Remove(g.myID);
			return true;
		}
		Traverse t = Traverse.Create(l);
		int monetization = t.Field("Monetization").GetValue<int>();
		int season = t.Field("Season").GetValue<int>() + 1;
		string name = "Season " + season + " of <b>" + g.GetNameSimple() + "</b>";
		List<roomScript> rooms = ((freeRooms.Invoke(null, new object[1] { 6 }) as IEnumerable)?.Cast<roomScript>() ?? Enumerable.Empty<roomScript>()).Where((roomScript r) => (bool)r && problem.Invoke(null, new object[2] { r, 6 }) == null).ToList();
		if (rooms.Count == 0)
		{
			if (waiting.Add(g.myID))
			{
				News(name + " waits for a free Marketing room to prepare it.");
			}
			return false;
		}
		roomScript room = rooms[0];
		if (startTask.Invoke(null, new object[3] { g, monetization, room }) is string error)
		{
			if (waiting.Add(g.myID))
			{
				News(name + " can't be prepared yet: " + error);
			}
			return false;
		}
		waiting.Remove(g.myID);
		string where = ((roomName != null) ? ((string)roomName.Invoke(null, new object[1] { room })) : "A Marketing room");
		string time = ((duration != null) ? ((string)duration.Invoke(null, new object[2] { 250f, room })) : "");
		News(name + ": " + where + " prepares it" + (string.IsNullOrEmpty(time) ? "." : (" (" + time + ").")));
		return true;
	}

	// Seasons waiting for a room (or for money) start as soon as they can.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly()
	{
		if (!Plugin.LiveServiceInRoom.Value || !G.Singleplayer)
		{
			return;
		}
		try
		{
			Look();
			if (live == null || (liveOn != null && !(bool)liveOn.Invoke(null, null)))
			{
				return;
			}
			if (!(HolyCompat.Get<IList>(HolyCompat.HolyState("LiveService"), "Games") is IList games))
			{
				return;
			}
			foreach (object l in games.Cast<object>().ToList())
			{
				Traverse t = Traverse.Create(l);
				if (!t.Field("AutoRenew").GetValue<bool>() || t.Field("WeeksLeft").GetValue<int>() > 0)
				{
					continue;
				}
				gameScript g = GameUpdates.FindGame(t.Field("GameId").GetValue<int>());
				if ((bool)g && g.isOnMarket && G.MS.money >= (long)seasonCost.Invoke(null, new object[1] { g }))
				{
					Renew(g);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Live-service seasons: " + ex.Message);
		}
	}

	private static void News(string text)
	{
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsInfo(text);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		waiting.Clear();
	}
}
