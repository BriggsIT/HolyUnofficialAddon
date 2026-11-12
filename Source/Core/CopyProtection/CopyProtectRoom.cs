using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.UI;
using HolyAddon.UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.Core.CopyProtection;

// Marks a development room's task object ("Task_<id>") as our copy protection project.
internal class CopyProtectTask : MonoBehaviour
{
}

// The development room side: a "Develop copy protection" entry in the room's menu, the room's staff working on the
// project like on an engine, its progress window and busy menu, and the game's cancel-task dialog.
[HarmonyPatch]
internal static class CopyProtectRoom
{
	private const string EntryName = "Editor_CopyProtect";

	private const string DetailsName = "Editor_CopyProtectDetails";

	private static readonly MethodInfo WorkSpeed = AccessTools.Method(typeof(characterScript), "GetWorkSpeed");

	private static readonly MethodInfo GetWorkResult = AccessTools.Method(typeof(characterScript), "GetWorkResult");

	private static readonly MethodInfo CreatePop = AccessTools.Method(typeof(characterScript), "CreatePopInSeconds");

	private static readonly MethodInfo Learn = AccessTools.Method(typeof(characterScript), "Learn");

	private static readonly AccessTools.FieldRef<characterScript, bool> UiVisible = AccessTools.FieldRefAccess<characterScript, bool>("uiVisible");

	private static readonly AccessTools.FieldRef<characterScript, int> PopTechnik = AccessTools.FieldRefAccess<characterScript, int>("ePop_Technik");

	private static readonly AccessTools.FieldRef<roomScript, roomButtonScript> RoomButtons = AccessTools.FieldRefAccess<roomScript, roomButtonScript>("rbS_");

	private static readonly MethodInfo LerpBalken = AccessTools.Method(typeof(roomWindow), "LerpBalken");

	private static readonly AccessTools.FieldRef<Menu_W_Aufgabe_Abbrechen, roomScript> CancelRoom = AccessTools.FieldRefAccess<Menu_W_Aufgabe_Abbrechen, roomScript>("rS_");

	// Expected development points per week from a room's staff (programming skill, desks, crunch).
	// The room's pace, with the rooms supporting it (their staff work on its project too).
	public static float Rate(roomScript r)
	{
		float total = OwnRate(r);
		foreach (roomScript s in RoomSupport.Supporters(r))
		{
			total += OwnRate(s);
		}
		return total;
	}

	private static float OwnRate(roomScript r)
	{
		if (!r || !G.MS)
		{
			return 0f;
		}
		float pressure = ((G.MS.personal_druck == 1) ? 0.8f : ((G.MS.personal_druck == 2) ? 0.6f : 1f));
		float total = 0f;
		foreach (characterScript c in CopyProtectDev.Staff(r))
		{
			float speed = Convert.ToSingle(WorkSpeed.Invoke(c, null)) / Mathf.Max(0.01f, G.MS.speedSetting);
			float desk = (c.objectUsingS_ ? (((float)c.objectUsingS_.qualitaet - 1f) / 10f + 1f) : 1f);
			total += speed * (0.1f + c.s_programmieren * 0.1f) / 2f * desk * pressure;
		}
		return total * 0.95f;
	}

	public static void Attach(CopyProtectProject p, roomScript room)
	{
		p.RoomId = room.myID;
		GameObject go = new GameObject("Task_" + p.TaskId);
		go.AddComponent<CopyProtectTask>();
		room.taskID = p.TaskId;
		room.taskGameObject = go;
	}

	public static void Detach(CopyProtectProject p)
	{
		GameObject go = GameObject.Find("Task_" + p.TaskId);
		if ((bool)go && (bool)go.GetComponent<CopyProtectTask>())
		{
			go.name = "Task_done";
			UnityEngine.Object.Destroy(go);
		}
		foreach (roomScript r in CopyProtectDev.Rooms())
		{
			if (r.taskID == p.TaskId)
			{
				r.taskID = -1;
				r.taskGameObject = null;
			}
		}
	}

	// After loading: the game saved the room's task ID but not our task object.
	public static void Restore(CopyProtectProject p)
	{
		roomScript room = CopyProtectDev.FindRoom(p.RoomId);
		if ((bool)room && (room.taskID == -1 || room.taskID == p.TaskId) && !GameObject.Find("Task_" + p.TaskId))
		{
			Attach(p, room);
		}
		// Without its room the weekly check stops the project and refunds it.
	}

	// Leftover task objects from the game that was running before a load or a new game.
	public static void ClearTasks()
	{
		foreach (CopyProtectTask task in UnityEngine.Object.FindObjectsOfType<CopyProtectTask>())
		{
			task.gameObject.name = "Task_done";
			UnityEngine.Object.Destroy(task.gameObject);
		}
	}

	public static bool Ours(roomScript r)
	{
		if (!r || !r.taskGameObject || !r.taskGameObject.GetComponent<CopyProtectTask>())
		{
			return false;
		}
		CopyProtectProject project = CopyProtectDev.Project;
		if (project != null)
		{
			return project.TaskId == r.taskID;
		}
		return false;
	}

	// ---- Work ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(characterScript), "WORK_Engine")]
	private static void DevWork(characterScript __instance, roomScript rS_, ref bool __result)
	{
		if (__result || !Ours(rS_) || rS_.typ != 1)
		{
			return;
		}
		__result = true;
		try
		{
			characterScript c = __instance;
			float desk = (c.objectUsingS_ ? (((float)c.objectUsingS_.qualitaet - 1f) / 10f + 1f) : 1f);
			float points = UnityEngine.Random.Range(0.1f, c.s_programmieren * 0.1f) * desk;
			points = (float)GetWorkResult.Invoke(c, new object[1] { points });
			if (!c.settings_.disableWorkIcons && UiVisible(c))
			{
				c.StartCoroutine((IEnumerator)CreatePop.Invoke(c, new object[4] { PopTechnik(c), points, 0f, 13 }));
			}
			Learn.Invoke(c, new object[8] { false, true, false, false, false, false, false, false });
			CopyProtectDev.Work(points, rS_);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Copy protection work failed: " + ex);
		}
	}

	// ---- The room's progress window ----

	[HarmonyPrefix]
	[HarmonyPatch(typeof(roomScript), "UpdateWindowEngine")]
	private static bool DevWindow(roomScript __instance)
	{
		return !ShowWindow(__instance);
	}

	private static bool ShowWindow(roomScript r)
	{
		if (!Ours(r) || r.typ != 1)
		{
			return false;
		}
		CopyProtectProject p = CopyProtectDev.Project;
		roomButtonScript rb = RoomButtons(r);
		GameObject window = (((bool)rb && rb.uiWindows != null && rb.uiWindows.Length > 1) ? rb.uiWindows[1] : null);
		if (!window)
		{
			return true;
		}
		bool visible = OnScreen(r);
		if (window.activeSelf != visible)
		{
			window.SetActive(visible);
		}
		if (!visible)
		{
			return true;
		}
		roomWindow rw = window.GetComponent<roomWindow>();
		if (!rw || rw.uiObjects == null || rw.uiObjects.Length < 4)
		{
			return true;
		}
		float percent = p.Fraction * 100f;
		rw.uiObjects[0].GetComponent<Text>().text = p.Name;
		try
		{
			LerpBalken.Invoke(rw, new object[2]
			{
				rw.uiObjects[1],
				percent * 0.01f
			});
		}
		catch
		{
		}
		rw.uiObjects[2].GetComponent<Text>().text = Mathf.Floor(percent * 10f) / 10f + "%";
		Sprite sprite = CopyProtectDev.TechIcon(p.Tech);
		if ((bool)sprite)
		{
			rw.uiObjects[3].GetComponent<Image>().sprite = sprite;
		}
		return true;
	}

	private static bool OnScreen(roomScript r)
	{
		if (!r.myUI || !r.myUI.activeSelf || !r.camera_ || !G.Gui || G.Gui.disableRoomGUI)
		{
			return false;
		}
		Vector3 pos = r.uiPos;
		pos.y += 2f;
		Vector2 screen = r.camera_.WorldToScreenPoint(pos);
		if (screen.x >= -200f && screen.x < (float)(Screen.width + 200) && screen.y >= -200f)
		{
			return screen.y < (float)(Screen.height + 200);
		}
		return false;
	}

	// ---- The room's menu ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(roomButtonScript), "BUTTON_Main")]
	private static void RoomClicked(roomButtonScript __instance)
	{
		if (!__instance.rS_ || __instance.rS_.typ != 1 || __instance.uiOptions == null)
		{
			return;
		}
		try
		{
			EnsureEntry(__instance);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Copy protection room entry: " + ex);
		}
		// Placed here, after the copy protection entry exists, so the MMO entry always goes below it.
		try
		{
			HolyAddon.UI.MmoMenu.EnsureEntry(__instance);
		}
		catch (Exception ex3)
		{
			Plugin.Log.LogError("MMO room entry: " + ex3);
		}
		try
		{
			BusyPanel(__instance);
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogError("Copy protection busy menu: " + ex2);
		}
	}

	internal static string Method(Transform option)
	{
		Button button = option.GetComponent<Button>();
		if (!button || button.onClick.GetPersistentEventCount() <= 0)
		{
			return null;
		}
		return button.onClick.GetPersistentMethodName(0);
	}

	internal static Transform Clone(Transform template, Transform parent, string name)
	{
		Transform copy = UnityEngine.Object.Instantiate(template.gameObject, parent, worldPositionStays: false).transform;
		copy.name = name;
		foreach (setText s in copy.GetComponentsInChildren<setText>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(s);
		}
		foreach (lockDestroy l in copy.GetComponentsInChildren<lockDestroy>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(l);
		}
		foreach (tooltip t in copy.GetComponentsInChildren<tooltip>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(t);
		}
		foreach (Text text in copy.GetComponentsInChildren<Text>(includeInactive: true))
		{
			if (text.transform.parent == copy && text.name != "Text")
			{
				UnityEngine.Object.DestroyImmediate(text.gameObject);
			}
		}
		copy.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
		return copy;
	}

	// "Develop copy protection" in an idle development room's menu, under Develop engine (and Holy's anti-cheat).
	private static void EnsureEntry(roomButtonScript rb)
	{
		if (rb.uiOptions.Length <= 1 || !rb.uiOptions[1])
		{
			return;
		}
		Transform panel = rb.uiOptions[1].transform;
		Transform entry = panel.Find(EntryName);
		bool show = CopyProtectDev.On && CopyProtectDev.MarketOpen;
		Transform engines = panel.Cast<Transform>().FirstOrDefault((Transform c) => Method(c) == "BUTTON_Dev_Engines");
		if (!entry)
		{
			if (!show || !engines)
			{
				return;
			}
			entry = Clone(engines, panel, EntryName);
			roomButtonScript owner = rb;
			UIKit.OnClick(entry.GetComponent<Button>(), () => Click(owner));
		}
		entry.gameObject.SetActive(show);
		if (!show)
		{
			return;
		}
		Transform after = panel.Find("Holy_anticheat") ?? engines;
		if ((bool)after)
		{
			int target = after.GetSiblingIndex();
			entry.SetSiblingIndex((entry.GetSiblingIndex() < target) ? target : (target + 1));
		}
		Transform label = entry.Find("Text");
		if ((bool)label)
		{
			label.GetComponent<Text>().text = "Develop Copy Protection";
		}
		string locked = CopyProtectDev.LockReason();
		Transform lockIcon = entry.Find("Lock");
		if ((bool)lockIcon)
		{
			lockIcon.gameObject.SetActive(locked != null);
		}
		UIKit.SetTooltip(entry.gameObject, locked ?? "Develop your own copy protection, or update one you made.");
	}

	private static void Click(roomButtonScript rb)
	{
		UIKit.Sound();
		roomScript room = rb.rS_;
		if ((bool)G.Gui)
		{
			G.Gui.CloseAllRoomButtons();
		}
		if ((bool)G.MS)
		{
			G.MS.PauseGame(p: false);
		}
		string locked = CopyProtectDev.LockReason();
		if (locked != null)
		{
			CopyProtectDev.Report(locked);
			return;
		}
		CopyProtectPage.PreferredRoom = (room ? room.myID : (-1));
		EditorWindow.CopyProtect.Open();
		if (CopyProtectDev.Project == null && (bool)room)
		{
			EditorWindow.CopyProtect.Push(new CopyProtectNewPage());
		}
	}

	// A room working on our project: open its busy menu (cancel, staff...) with a "Copy protection" entry.
	private static void BusyPanel(roomButtonScript rb)
	{
		if (rb.uiOptions.Length <= 3 || !rb.uiOptions[3])
		{
			return;
		}
		GameObject panel = rb.uiOptions[3];
		bool ours = Ours(rb.rS_);
		Transform details = panel.transform.Find(DetailsName);
		if (!details && ours)
		{
			Transform template = panel.transform.Find("Option1");
			if ((bool)template)
			{
				details = Clone(template, panel.transform, DetailsName);
				details.SetSiblingIndex(template.GetSiblingIndex());
				UIKit.OnClick(details.GetComponent<Button>(), () =>
				{
					UIKit.Sound();
					if ((bool)G.Gui)
					{
						G.Gui.CloseAllRoomButtons();
					}
					if ((bool)G.MS)
					{
						G.MS.PauseGame(p: false);
					}
					EditorWindow.CopyProtect.Open();
				});
			}
		}
		if ((bool)details)
		{
			details.gameObject.SetActive(ours);
			Transform label = details.Find("Text");
			if (ours && (bool)label)
			{
				label.GetComponent<Text>().text = "Copy Protection";
			}
		}
		if (!ours)
		{
			return;
		}
		bool open = panel.activeSelf;
		G.Gui.CloseAllRoomButtons();
		panel.SetActive(!open);
		rb.uiObjects[2].SetActive(!open);
		rb.uiObjects[2].transform.SetParent(panel.transform);
		rb.transform.SetAsLastSibling();
		G.MS.PauseGame(!open);
	}

	// ---- The game's "cancel task" dialog ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_W_Aufgabe_Abbrechen), "Init")]
	private static void CancelDialog(Menu_W_Aufgabe_Abbrechen __instance, roomScript script_)
	{
		if (!Ours(script_))
		{
			return;
		}
		long refund = CopyProtectDev.Refund(CopyProtectDev.Project);
		__instance.uiObjects[0].GetComponent<Text>().text = ((refund > 0) ? G.Text(569, "You get <NUM> back.").Replace("<NUM>", G.Money(refund)) : "");
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_W_Aufgabe_Abbrechen), "BUTTON_Yes")]
	private static void CancelConfirmed(Menu_W_Aufgabe_Abbrechen __instance)
	{
		if (Ours(CancelRoom(__instance)))
		{
			CopyProtectDev.Cancel();
		}
	}
}
