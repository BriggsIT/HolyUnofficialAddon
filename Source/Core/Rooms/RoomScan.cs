using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Events;
using HolyAddon.Core.Updates;
using UnityEngine;

namespace HolyAddon.Core.Rooms;

// One room as the Rooms Overview shows it.
internal class RoomInfo
{
	public roomScript Room;

	public string Name = "";

	public int Group;

	public int Staff;

	public int Desks;

	public string Job = "";

	// 0-1, or -1 when the job has no progress to show.
	public float Progress = -1f;

	public bool Working;

	public List<string> Auto = new List<string>();

	public List<string> Support = new List<string>();

	public List<string> Warnings = new List<string>();
}

// Reads every room: its job and progress (the game's own tasks, Holy's room tasks, the addon's projects), what it
// does by itself (repeat, auto research...), who supports whom (the game's support links and Holy's studio links),
// and what needs a look (idle and why, staffing, jobs at risk).
internal static class RoomScan
{
	public const int All = 0;

	public const int NeedsALook = 9;

	public static readonly string[] GroupNames = new string[10] { "All", "Development", "Research", "Studios", "Marketing", "Support", "Hardware", "Production", "Training", "Needs a Look" };

	private static readonly Dictionary<string, string> JobNames = new Dictionary<string, string>
	{
		{ "taskGame", "Developing" },
		{ "taskEngine", "Engine" },
		{ "taskUpdate", "Update" },
		{ "taskF2PUpdate", "F2P update" },
		{ "taskForschung", "Research" },
		{ "taskAutoForschung", "Auto research" },
		{ "taskForschungWait", "Research" },
		{ "taskMarketing", "Campaign" },
		{ "taskMarketingSpezial", "Special campaign" },
		{ "taskMarktforschung", "Market research" },
		{ "taskMitarbeitersuche", "Employee search" },
		{ "taskFankampagne", "Fan campaign" },
		{ "taskSupport", "Answering calls" },
		{ "taskFanshop", "Merch shop" },
		{ "taskBugfixing", "Bug fixing" },
		{ "taskGameplayVerbessern", "Gameplay improvement" },
		{ "taskGrafikVerbessern", "Graphics improvement" },
		{ "taskSoundVerbessern", "Sound improvement" },
		{ "taskAnimationVerbessern", "Motion capture" },
		{ "taskSpielbericht", "Game report" },
		{ "taskPolishing", "Polishing" },
		{ "taskContractWork", "Contract work" },
		{ "taskContractWait", "Waiting for a contract" },
		{ "taskWait", "Waiting for a game" },
		{ "taskKonsole", "Console development" },
		{ "taskKonsoleReduceCosts", "Cutting console costs" },
		{ "taskKonsoleHaltbarkeit", "Console durability" },
		{ "taskTraining", "Training course" },
		{ "taskProduction", "Production" },
		{ "taskArcadeProduction", "Arcade production" }
	};

	// Professions (characterScript.beruf) that suit each room type, as the game's staff AI sees it.
	private static readonly Dictionary<int, int[]> Fits = new Dictionary<int, int[]>
	{
		{ 1, new int[4] { 0, 1, 2, 3 } },
		{ 2, new int[1] { 7 } },
		{ 3, new int[1] { 5 } },
		{ 4, new int[1] { 2 } },
		{ 5, new int[1] { 3 } },
		{ 6, new int[1] { 4 } },
		{ 7, new int[1] { 4 } },
		{ 8, new int[1] { 6 } },
		{ 10, new int[1] { 1 } },
		{ 17, new int[1] { 6 } }
	};

	private static readonly Dictionary<Type, FieldInfo[]> fieldCache = new Dictionary<Type, FieldInfo[]>();

	private static readonly Dictionary<Type, MethodInfo> percentCache = new Dictionary<Type, MethodInfo>();

	public static int GroupOf(int typ)
	{
		switch (typ)
		{
		case 1:
			return 1;
		case 2:
			return 2;
		case 3:
		case 4:
		case 5:
		case 10:
			return 3;
		case 6:
			return 4;
		case 7:
			return 5;
		case 8:
			return 6;
		case 14:
		case 17:
			return 7;
		case 13:
			return 8;
		default:
			return -1;
		}
	}

	private static bool Staffed(int typ)
	{
		return typ != 14;
	}

	public static List<RoomInfo> Scan()
	{
		List<RoomInfo> list = new List<RoomInfo>();
		roomScript[] rooms = (G.MS ? G.MS.arrayRoomScripts : null);
		if (rooms == null)
		{
			return list;
		}
		List<roomScript> shown = rooms.Where((roomScript r) => (bool)r && r.myID >= 100 && GroupOf(r.typ) > 0).OrderBy((roomScript r) => GroupOf(r.typ)).ThenBy((roomScript r) => r.typ).ThenBy((roomScript r) => r.myID).ToList();
		Dictionary<int, string> names = Names(rooms);
		Dictionary<int, int> staff = new Dictionary<int, int>();
		Dictionary<int, List<int>> professions = new Dictionary<int, List<int>>();
		characterScript[] chars = (G.MS ? G.MS.arrayCharactersScripts : null);
		if (chars != null)
		{
			foreach (characterScript c in chars)
			{
				if ((bool)c && c.roomID >= 0)
				{
					staff[c.roomID] = (staff.TryGetValue(c.roomID, out var n) ? n : 0) + 1;
					if (!professions.TryGetValue(c.roomID, out var p))
					{
						p = (professions[c.roomID] = new List<int>());
					}
					p.Add(c.beruf);
				}
			}
		}
		Dictionary<int, int> desks = new Dictionary<int, int>();
		objectScript[] objects = (G.MS ? G.MS.arrayObjectScripts : null);
		if (objects != null)
		{
			foreach (objectScript o in objects)
			{
				if ((bool)o && o.isArbeitsplatz)
				{
					desks[o.myRoomID] = (desks.TryGetValue(o.myRoomID, out var d) ? d : 0) + 1;
				}
			}
		}
		Dictionary<int, List<string>> studioLinks = StudioLinks(names);
		foreach (roomScript r in shown)
		{
			RoomInfo info = new RoomInfo
			{
				Room = r,
				Name = (names.TryGetValue(r.myID, out var name) ? name : "Room"),
				Group = GroupOf(r.typ),
				Staff = (staff.TryGetValue(r.myID, out var s) ? s : 0),
				Desks = (desks.TryGetValue(r.myID, out var dk) ? dk : 0)
			};
			try
			{
				Job(info, names);
				Supports(info, names, studioLinks);
				Warn(info, professions.TryGetValue(r.myID, out var prof) ? prof : new List<int>(), names);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("Rooms overview, " + info.Name + ": " + ex.Message);
			}
			list.Add(info);
		}
		return list;
	}

	// "Support room 2": the room's own name, else its type and its number among rooms of that type.
	private static Dictionary<int, string> Names(roomScript[] rooms)
	{
		Dictionary<int, string> map = new Dictionary<int, string>();
		roomDataScript rd = (G.MainObj ? G.MainObj.GetComponent<roomDataScript>() : null);
		foreach (IGrouping<int, roomScript> type in from r in rooms
			where (bool)r && r.myID >= 100
			group r by r.typ)
		{
			int i = 0;
			foreach (roomScript r2 in type.OrderBy((roomScript x) => x.myID))
			{
				i++;
				map[r2.myID] = (string.IsNullOrEmpty(r2.myName) ? (((bool)rd ? rd.GetName(r2.typ) : "Room") + " " + i) : r2.myName);
			}
		}
		return map;
	}

	private static void Job(RoomInfo info, Dictionary<int, string> names)
	{
		roomScript r = info.Room;
		if (r.taskID == -1 || !r.taskGameObject)
		{
			info.Job = "No job";
			return;
		}
		info.Working = true;
		UpdateProject p = GameUpdates.ProjectInRoom(r);
		if (p != null)
		{
			info.Job = (string.IsNullOrEmpty(p.Title) ? GameUpdates.KindName(p.Kind) : p.Title);
			info.Progress = p.Fraction;
			return;
		}
		CopyProtectProject cp = CopyProtectDev.Project;
		if (cp != null && cp.TaskId == r.taskID)
		{
			info.Job = "Copy protection";
			info.Progress = cp.Fraction;
			return;
		}
		object holy = RoomSupport.HolyTask(r);
		if (holy != null)
		{
			info.Job = HolyCompat.Get<string>(holy, "Name") ?? "Holy task";
			info.Progress = Mathf.Clamp01(HolyCompat.Get<float>(holy, "Progress") / Mathf.Max(1f, HolyCompat.Get<float>(holy, "Needed")));
			return;
		}
		taskUnterstuetzen support = r.taskGameObject.GetComponent<taskUnterstuetzen>();
		if ((bool)support)
		{
			roomScript target = support.rS_;
			info.Job = "Supporting " + (((bool)target && names.TryGetValue(target.myID, out var n)) ? n : "another room");
			float? progress = (target ? Progress(target) : null);
			info.Progress = progress ?? (-1f);
			return;
		}
		foreach (MonoBehaviour mb in r.taskGameObject.GetComponents<MonoBehaviour>())
		{
			if (!mb || !JobNames.TryGetValue(mb.GetType().Name, out var label))
			{
				continue;
			}
			string detail = Detail(mb);
			info.Job = label + ((detail != null) ? (": " + detail) : "");
			// Permanent jobs (calls, merch) never finish: no bar.
			string type = mb.GetType().Name;
			info.Progress = ((type == "taskSupport" || type == "taskFanshop") ? (-1f) : Percent(mb));
			Automations(info, mb);
			return;
		}
		info.Job = "Working";
	}

	// The progress of a room's job (0-1), whoever's job it is; null when there's nothing to measure.
	public static float? Progress(roomScript r)
	{
		if (!r || !r.taskGameObject || r.taskID == -1)
		{
			return null;
		}
		float? own = RoomSupport.Progress(r);
		if (own.HasValue)
		{
			return own;
		}
		foreach (MonoBehaviour mb in r.taskGameObject.GetComponents<MonoBehaviour>())
		{
			if ((bool)mb && JobNames.ContainsKey(mb.GetType().Name))
			{
				float p = Percent(mb);
				return (p >= 0f) ? new float?(p) : null;
			}
		}
		return null;
	}

	private static FieldInfo[] Fields(Type t)
	{
		if (!fieldCache.TryGetValue(t, out var f))
		{
			f = (fieldCache[t] = t.GetFields(BindingFlags.Instance | BindingFlags.Public));
		}
		return f;
	}

	private static T Field<T>(MonoBehaviour mb, string name)
	{
		FieldInfo f = Fields(mb.GetType()).FirstOrDefault((FieldInfo x) => x.Name == name && typeof(T).IsAssignableFrom(x.FieldType));
		if (f == null)
		{
			return default(T);
		}
		object v = f.GetValue(mb);
		if (v is T t)
		{
			return t;
		}
		return default(T);
	}

	private static string Detail(MonoBehaviour mb)
	{
		gameScript g = Field<gameScript>(mb, "gS_");
		if ((bool)g)
		{
			return g.GetNameSimple();
		}
		platformScript p = Field<platformScript>(mb, "pS_");
		if ((bool)p)
		{
			return p.GetName();
		}
		return null;
	}

	private static float Percent(MonoBehaviour mb)
	{
		Type t = mb.GetType();
		if (!percentCache.TryGetValue(t, out var m))
		{
			m = (percentCache[t] = t.GetMethod("GetProzent", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null));
		}
		if (m == null || m.ReturnType != typeof(float))
		{
			return -1f;
		}
		try
		{
			float v = (float)m.Invoke(mb, null);
			return (v < 0f) ? (-1f) : Mathf.Clamp01(v / 100f);
		}
		catch
		{
			return -1f;
		}
	}

	private static void Automations(RoomInfo info, MonoBehaviour mb)
	{
		string type = mb.GetType().Name;
		if (type == "taskSupport" || type == "taskFanshop")
		{
			info.Auto.Add("Permanent");
			return;
		}
		if (type == "taskAutoForschung" || Field<bool>(mb, "autoForschung"))
		{
			info.Auto.Add("Auto research");
		}
		if (Field<bool>(mb, "automatic"))
		{
			info.Auto.Add(type switch
			{
				"taskForschung" => "Auto-continue",
				"taskProduction" => "Auto re-order",
				"taskContractWork" => "Next contract",
				"taskSpielbericht" => "Next game",
				_ => "Repeat",
			});
		}
		if (Field<bool>(mb, "produceAutomatikAllGames"))
		{
			info.Auto.Add("All games");
		}
		if (Field<bool>(mb, "automaticWait"))
		{
			info.Auto.Add("Waits if needed");
		}
		if (Field<bool>(mb, "stopAutomatic"))
		{
			info.Auto.Add("Stops at max hype");
		}
	}

	// Holy's studio links (QA, Graphics, Sound and Motion Capture rooms working for a Development room), by room id.
	private static Dictionary<int, List<string>> StudioLinks(Dictionary<int, string> names)
	{
		Dictionary<int, List<string>> map = new Dictionary<int, List<string>>();
		IList links = HolyCompat.Get<IList>(HolyCompat.HolyState("StudioSupport"), "Links");
		if (links == null)
		{
			return map;
		}
		foreach (object l in links)
		{
			int studio = HolyCompat.Get<int>(l, "StudioId");
			int team = HolyCompat.Get<int>(l, "TeamId");
			if (names.TryGetValue(studio, out var s) && names.TryGetValue(team, out var d))
			{
				Add(map, studio, "→ " + d + " (studio link)");
				Add(map, team, "← " + s + " (studio link)");
			}
		}
		return map;
	}

	private static void Add(Dictionary<int, List<string>> map, int id, string line)
	{
		if (!map.TryGetValue(id, out var list))
		{
			list = (map[id] = new List<string>());
		}
		list.Add(line);
	}

	private static void Supports(RoomInfo info, Dictionary<int, string> names, Dictionary<int, List<string>> studioLinks)
	{
		roomScript r = info.Room;
		taskUnterstuetzen t = (r.taskGameObject ? r.taskGameObject.GetComponent<taskUnterstuetzen>() : null);
		studioLinks.TryGetValue(r.myID, out var links);
		// One line per link: a Holy studio link replaces the plain line for the same room.
		void Line(string line)
		{
			if (!info.Support.Contains(line) && (links == null || !links.Contains(line + " (studio link)")))
			{
				info.Support.Add(line);
			}
		}
		if ((bool)t && (bool)t.rS_ && names.TryGetValue(t.rS_.myID, out var target))
		{
			Line("→ " + target);
		}
		foreach (roomScript s in RoomSupport.Supporters(r))
		{
			if (names.TryGetValue(s.myID, out var n))
			{
				Line("← " + n);
			}
		}
		if (links != null)
		{
			info.Support.AddRange(links.Distinct().Where((string l) => !info.Support.Contains(l)));
		}
	}

	private static void Warn(RoomInfo info, List<int> professions, Dictionary<int, string> names)
	{
		roomScript r = info.Room;
		List<string> w = info.Warnings;
		// Staffing.
		if (Staffed(r.typ))
		{
			if (info.Staff == 0)
			{
				w.Add("No staff");
				info.Working = false;
				return;
			}
			if (info.Desks > 0 && info.Staff > info.Desks)
			{
				w.Add(info.Staff + " staff, only " + info.Desks + " desks");
			}
			if (Fits.TryGetValue(r.typ, out var fit) && !professions.Any((int b) => fit.Contains(b)))
			{
				w.Add("Nobody here is a " + string.Join(" or ", fit.Select(Profession)).ToLower());
			}
		}
		// Idle, and why.
		if (r.pause)
		{
			w.Add("Paused");
			info.Working = false;
		}
		if (r.taskID == -1 || !r.taskGameObject)
		{
			w.Add("Idle: no job");
			info.Working = false;
			return;
		}
		GameObject task = r.taskGameObject;
		if ((bool)task.GetComponent<taskSupport>() && r.KeineAnrufe())
		{
			Idle(info, "Idle: no calls right now");
		}
		else if ((bool)task.GetComponent<taskMarketing>() && r.WaitForMinimumHype())
		{
			Idle(info, "Waiting: hype is close to its maximum");
		}
		else if ((bool)task.GetComponent<taskForschungWait>())
		{
			Idle(info, "Waiting for money to research");
		}
		else if ((bool)task.GetComponent<taskContractWait>())
		{
			Idle(info, "Waiting for a contract");
		}
		else if ((bool)task.GetComponent<taskWait>())
		{
			Idle(info, "Waiting for a game to report on");
		}
		else if ((bool)task.GetComponent<taskArcadeProduction>() && r.KeineAutomatenBestellungen())
		{
			Idle(info, "Idle: no arcade orders");
		}
		else if ((bool)task.GetComponent<taskBugfixing>())
		{
			gameScript g = task.GetComponent<taskBugfixing>().gS_;
			if ((bool)g && g.points_bugs <= 0f)
			{
				Idle(info, "Idle: the game has no bugs left");
			}
		}
		else
		{
			taskUnterstuetzen t = task.GetComponent<taskUnterstuetzen>();
			if ((bool)t && (bool)t.rS_ && (t.rS_.taskID == -1 || !t.rS_.taskGameObject))
			{
				Idle(info, "Idle: the room it supports has no job");
			}
		}
		// Jobs at risk.
		ContractAtRisk(info);
		ConventionAtRisk(info);
	}

	private static void Idle(RoomInfo info, string why)
	{
		info.Warnings.Add(why);
		info.Working = false;
	}

	private static string Profession(int b)
	{
		string fallback = b switch
		{
			0 => "Game Designer",
			1 => "Programmer",
			2 => "Graphics Artist",
			3 => "Sound Artist",
			4 => "Office Worker",
			5 => "Game Tester",
			6 => "Technician",
			7 => "Researcher",
			_ => "Specialist",
		};
		return G.Text(137 + b, fallback);
	}

	// A contract that won't be done before its deadline at the room's pace (supporting rooms included).
	private static void ContractAtRisk(RoomInfo info)
	{
		taskContractWork t = info.Room.taskGameObject.GetComponent<taskContractWork>();
		if (!t || !t.contract_)
		{
			return;
		}
		int weeks = t.contract_.zeitInWochen;
		float rate = Pace(info.Room);
		if (rate <= 0f || weeks <= 0)
		{
			return;
		}
		int needed = Mathf.CeilToInt(t.pointsLeft / rate);
		if (needed > weeks)
		{
			info.Warnings.Add("At risk: about " + needed + " weeks of work, " + weeks + " left (penalty " + G.Money(t.contract_.strafe) + ")");
		}
	}

	// The convention being prepared in this Support room, if it won't be ready when the doors open.
	private static void ConventionAtRisk(RoomInfo info)
	{
		if (info.Room.typ != 7 || !StudioCon.On)
		{
			return;
		}
		ConPlan p = StudioCon.State.Plan;
		if (p == null || !p.Started || p.Agency || p.Prepared)
		{
			return;
		}
		object task = HolyRooms.Find(p.Year);
		if (task == null || HolyRooms.RoomOf(task) != info.Room)
		{
			return;
		}
		float ready = StudioCon.ReadyOnTheDay(p);
		if (ready < 0.95f)
		{
			info.Warnings.Add("At risk: about " + Mathf.RoundToInt(ready * 100f) + "% ready when " + StudioCon.FullName(p.Year) + " opens");
		}
	}

	// Work points a week from the room's staff and the rooms supporting it (the game's work formula).
	public static float Pace(roomScript r)
	{
		float total = 0f;
		foreach (roomScript room in new List<roomScript> { r }.Concat(RoomSupport.Supporters(r)))
		{
			foreach (characterScript c in CopyProtectDev.Staff(room))
			{
				float speed = UpdateRoom.Speed(c) / Mathf.Max(0.01f, G.MS.speedSetting);
				float desk = (c.objectUsingS_ ? (((float)c.objectUsingS_.qualitaet - 1f) / 10f + 1f) : 1f);
				float pressure = ((G.MS.personal_druck == 1) ? 0.8f : ((G.MS.personal_druck == 2) ? 0.6f : 1f));
				total += speed * (0.1f + Skill(c, r.typ) * 0.1f) / 2f * desk * pressure;
			}
		}
		return total * 0.95f;
	}

	private static float Skill(characterScript c, int typ)
	{
		return typ switch
		{
			2 => c.s_forschen,
			3 => c.s_gamedesign,
			4 => c.s_grafik,
			5 => c.s_sound,
			6 => c.s_pr,
			7 => c.s_pr,
			8 => c.s_technik,
			17 => c.s_technik,
			_ => c.s_programmieren,
		};
	}

	// ---- Jumping to a room ----

	private static roomScript jumpTo;

	private static readonly AccessTools.FieldRef<roomScript, roomButtonScript> RoomButtons = AccessTools.FieldRefAccess<roomScript, roomButtonScript>("rbS_");

	// Closes the overview, then (next frame) moves the camera to the room and opens its menu.
	public static void Jump(roomScript r)
	{
		jumpTo = r;
	}

	public static void Tick()
	{
		if (!jumpTo || !G.Gui || G.Gui.menuOpen)
		{
			return;
		}
		roomScript r = jumpTo;
		jumpTo = null;
		try
		{
			Transform rig = (G.Gui.camera_ ? G.Gui.camera_.transform.parent : null);
			if ((bool)rig)
			{
				rig.position = new Vector3(r.uiPos.x, rig.position.y, r.uiPos.z);
			}
			roomButtonScript rb = RoomButtons(r);
			if ((bool)rb)
			{
				rb.BUTTON_Main();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Rooms overview: jumping to a room: " + ex.Message);
		}
	}
}
