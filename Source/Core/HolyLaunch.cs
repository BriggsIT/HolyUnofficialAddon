using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Store;
using HolyAddon.Core.Updates;

namespace HolyAddon.Core;

// The Holy Expansion's Launch Planner (a Marketing room button): wishlists for every game in development, early
// access, review embargoes and a launch boost of up to +120% fading over 4 weeks. When it's on, it's the one launch
// system: your store adds its users' wishlists to Holy's count instead of keeping its own, early access is Holy's, and
// the store's own launch boost goes (Core/Store/StorePlus). Holy only wrote launches to its own log; here they make
// the news, so you can see what your wishlists did.
[HarmonyPatch]
internal static class HolyLaunch
{
	// Holy's own numbers (HolyExpansion.Systems.Launch).
	public const int EarlyAccessFrom = 30;

	public const float EarlyAccessWork = 150f;

	public const int EarlyAccessPrice = 20;

	public const int BoostWeeks = 4;

	private const int MarketingRoom = 6;

	private static bool looked;

	private static MethodInfo forGame;

	private static MethodInfo onGetter;

	private static MethodInfo canEarlyAccess;

	private static MethodInfo startTask;

	private static MethodInfo startNow;

	private static MethodInfo freeRooms;

	private static MethodInfo problem;

	private static MethodInfo roomName;

	private static MethodInfo duration;

	private static MethodInfo ofKind;

	private static readonly HashSet<int> announced = new HashSet<int>();

	private static void Look()
	{
		if (looked)
		{
			return;
		}
		looked = true;
		Type launch = HolyCompat.HolyType("HolyExpansion.Systems.Launch");
		Type tasks = HolyCompat.HolyType("HolyExpansion.Systems.RoomTasks");
		if (launch == null || tasks == null)
		{
			return;
		}
		forGame = AccessTools.Method(launch, "For");
		onGetter = AccessTools.PropertyGetter(launch, "On");
		canEarlyAccess = AccessTools.Method(launch, "CanEarlyAccess");
		startTask = AccessTools.Method(launch, "StartEarlyAccessTask");
		startNow = AccessTools.Method(launch, "StartEarlyAccess");
		freeRooms = AccessTools.Method(tasks, "FreeRooms");
		problem = AccessTools.Method(tasks, "Problem");
		roomName = AccessTools.Method(tasks, "RoomName");
		duration = AccessTools.Method(tasks, "Duration");
		ofKind = AccessTools.Method(tasks, "OfKind");
		if (forGame == null || onGetter == null || canEarlyAccess == null || startTask == null)
		{
			forGame = null;
			Plugin.Log.LogWarning("Holy's Launch Planner wasn't found: the store keeps its own wishlists and early access");
		}
	}

	// Holy is installed and its "Launch planner" setting is on (single player).
	public static bool On
	{
		get
		{
			if (!G.Singleplayer)
			{
				return false;
			}
			Look();
			if (forGame == null)
			{
				return false;
			}
			try
			{
				return (bool)onGetter.Invoke(null, null);
			}
			catch
			{
				return false;
			}
		}
	}

	// Holy's record for the game (LaunchGame), or null.
	private static object For(gameScript g, bool create)
	{
		if (!g || !On)
		{
			return null;
		}
		try
		{
			return forGame.Invoke(null, new object[2] { g.myID, create });
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Holy's Launch Planner: " + ex.Message);
			return null;
		}
	}

	public static long Wishlists(gameScript g)
	{
		return HolyCompat.Get<long>(For(g, create: false), "Wishlists");
	}

	public static bool InEarlyAccess(gameScript g)
	{
		return HolyCompat.Get<bool>(For(g, create: false), "EarlyAccess");
	}

	public static long EarlyAccessPlayers(gameScript g)
	{
		return HolyCompat.Get<long>(For(g, create: false), "EaPlayers");
	}

	public static long EarlyAccessRevenue(gameScript g)
	{
		return HolyCompat.Get<long>(For(g, create: false), "EaRevenue");
	}

	// Your store's wishlists go into Holy's count.
	public static void AddWishlists(gameScript g, long n)
	{
		if (n <= 0)
		{
			return;
		}
		object l = For(g, create: true);
		if (l != null)
		{
			HolyCompat.Set(l, "Wishlists", HolyCompat.Get<long>(l, "Wishlists") + n);
		}
	}

	// A Marketing room is preparing its early access (Holy's room task).
	public static bool Preparing(gameScript g)
	{
		Look();
		if (ofKind == null || !g)
		{
			return false;
		}
		try
		{
			if (ofKind.Invoke(null, new object[1] { "earlyaccess" }) is IEnumerable tasks)
			{
				foreach (object t in tasks)
				{
					if (HolyCompat.Get<int>(t, "GameId") == g.myID)
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}
		return false;
	}

	// The fastest free Marketing room with staff (Holy's own pick for room work), or null.
	public static roomScript FreeRoom()
	{
		Look();
		if (freeRooms == null || problem == null)
		{
			return null;
		}
		try
		{
			IEnumerable rooms = freeRooms.Invoke(null, new object[1] { MarketingRoom }) as IEnumerable;
			return rooms?.Cast<roomScript>().FirstOrDefault((roomScript r) => (bool)r && problem.Invoke(null, new object[2] { r, MarketingRoom }) == null);
		}
		catch
		{
			return null;
		}
	}

	public static float Progress(gameScript g)
	{
		if (!(g.devPointsStart_Gesamt > 0.01f))
		{
			return 0f;
		}
		return UnityEngine.Mathf.Clamp(g.GetProzentGesamt(), 0f, 100f);
	}

	// Why it can't go into early access now (null: it can). "room" is a free Marketing room, or null.
	public static string EarlyAccessProblem(gameScript g, roomScript room)
	{
		if (!g || !g.inDevelopment)
		{
			return "Only games in development can go into early access.";
		}
		if (g.typ_addon || g.typ_mmoaddon)
		{
			return "Early access is for new games, not add-ons.";
		}
		if (InEarlyAccess(g))
		{
			return "It stays in early access until launch.";
		}
		if (Preparing(g))
		{
			return "A Marketing room is preparing its early access.";
		}
		bool can;
		try
		{
			can = (bool)canEarlyAccess.Invoke(null, new object[1] { g });
		}
		catch
		{
			can = false;
		}
		if (!can)
		{
			return "It needs to be at least " + EarlyAccessFrom + "% done (" + UnityEngine.Mathf.FloorToInt(Progress(g)) + "% now).";
		}
		if (!room)
		{
			return "No free Marketing room with staff to prepare it.";
		}
		return null;
	}

	// Starts Holy's early-access job in the fastest free Marketing room. Returns what happened, or the problem.
	public static string StartEarlyAccess(gameScript g, out bool started)
	{
		started = false;
		roomScript room = FreeRoom();
		string why = EarlyAccessProblem(g, room);
		if (why != null)
		{
			return why;
		}
		try
		{
			if (startTask.Invoke(null, new object[2] { g, room }) is string error)
			{
				return error;
			}
		}
		catch (Exception ex)
		{
			return "Holy's Launch Planner couldn't start it: " + ex.Message;
		}
		started = true;
		string where = ((roomName != null) ? ((string)roomName.Invoke(null, new object[1] { room })) : "A Marketing room");
		string time = ((duration != null) ? ((string)duration.Invoke(null, new object[2] { EarlyAccessWork, room })) : "");
		return where + " is preparing early access for " + g.GetNameSimple() + (string.IsNullOrEmpty(time) ? "." : (" (" + time + ")."));
	}

	// A game that was in early access on your store before the merge carries on in Holy's early access.
	public static void ContinueEarlyAccess(gameScript g)
	{
		if (!g || startNow == null || InEarlyAccess(g))
		{
			return;
		}
		try
		{
			startNow.Invoke(null, new object[1] { g });
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Holy's early access for " + g.GetNameSimple() + ": " + ex.Message);
		}
	}

	// Launch boost right now (Holy fades it over 4 weeks).
	public static float BoostNow(object l)
	{
		int weeks = HolyCompat.Get<int>(l, "BoostWeeks");
		if (weeks <= 0)
		{
			return 0f;
		}
		return HolyCompat.Get<float>(l, "LaunchBoost") * (float)weeks / (float)BoostWeeks;
	}

	// Your games Holy has launched (newest first), with its record for each.
	public static List<KeyValuePair<gameScript, object>> Launched(int max)
	{
		List<KeyValuePair<gameScript, object>> list = new List<KeyValuePair<gameScript, object>>();
		if (!On || !(HolyCompat.Get<IDictionary>(HolyCompat.HolyState("Launch"), "Games") is IDictionary games))
		{
			return list;
		}
		foreach (DictionaryEntry e in games)
		{
			if (e.Key is int id && HolyCompat.Get<bool>(e.Value, "Launched"))
			{
				gameScript g = GameUpdates.FindGame(id);
				if ((bool)g)
				{
					list.Add(new KeyValuePair<gameScript, object>(g, e.Value));
				}
			}
		}
		return list.OrderByDescending((KeyValuePair<gameScript, object> x) => x.Key.date_year * 12 + x.Key.date_month).Take(max).ToList();
	}

	public static object Record(gameScript g)
	{
		return For(g, create: false);
	}

	// After Holy's own launch code (it runs first): the launch makes the news, with what the wishlists did.
	[HarmonyPostfix]
	[HarmonyPriority(0)]
	[HarmonyPatch(typeof(gameScript), "SetOnMarket")]
	private static void AfterLaunch(gameScript __instance)
	{
		try
		{
			gameScript g = __instance;
			if (!g || !g.IsMyGame() || announced.Contains(g.myID))
			{
				return;
			}
			object l = For(g, create: false);
			if (l == null || !HolyCompat.Get<bool>(l, "Launched"))
			{
				return;
			}
			long wish = HolyCompat.Get<long>(l, "Wishlists");
			long ea = HolyCompat.Get<long>(l, "EaPlayers");
			if (wish <= 0 && ea <= 0)
			{
				return;
			}
			announced.Add(g.myID);
			float boost = HolyCompat.Get<float>(l, "LaunchBoost");
			StoreGame d = StorePlus.Data(g);
			long fromStore = ((d != null && StoreOps.State.Built) ? Math.Min(d.Wishlists, wish) : 0);
			string text = "<b>" + g.GetNameSimple() + "</b> launches with " + MmoUiText.Num(wish) + " wishlists" + ((fromStore > 0) ? (" (" + MmoUiText.Num(fromStore) + " from " + StoreOps.State.Name + ")") : "") + ((boost > 0.01f) ? (": sales +" + UnityEngine.Mathf.RoundToInt(boost * 100f) + "% at launch, fading over " + BoostWeeks + " weeks" + ((g.reviewTotal < 60) ? " (half, as reviews are under 60%)." : ".")) : ".") + ((ea > 0) ? (" " + MmoUiText.Num(ea) + " early-access players already have it.") : "");
			if ((bool)G.Gui)
			{
				G.Gui.CreateTopNewsInfo(text);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Launch news: " + ex.Message);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		announced.Clear();
	}
}
