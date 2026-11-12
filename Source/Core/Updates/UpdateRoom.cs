using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using HolyAddon.Core.CopyProtection;
using HolyAddon.UI;
using HolyAddon.UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.Core.Updates;

// Marks a development room's task object ("Task_<id>") as one of the addon's updates.
internal class UpdateTask : MonoBehaviour
{
	public int TaskId;
}

// The development room side of the addon's updates: staff working on them, the room's progress window, its busy
// menu and the game's cancel-task dialog. Same pattern as the copy protection projects.
[HarmonyPatch]
internal static class UpdateRoom
{
	private const string DetailsName = "Addon_UpdateDetails";

	private static readonly MethodInfo WorkSpeed = AccessTools.Method(typeof(characterScript), "GetWorkSpeed");

	private static readonly MethodInfo GetWorkResult = AccessTools.Method(typeof(characterScript), "GetWorkResult");

	private static readonly MethodInfo CreatePop = AccessTools.Method(typeof(characterScript), "CreatePopInSeconds");

	private static readonly MethodInfo Learn = AccessTools.Method(typeof(characterScript), "Learn");

	private static readonly AccessTools.FieldRef<characterScript, bool> UiVisible = AccessTools.FieldRefAccess<characterScript, bool>("uiVisible");

	private static readonly AccessTools.FieldRef<characterScript, int> PopTechnik = AccessTools.FieldRefAccess<characterScript, int>("ePop_Technik");

	private static readonly AccessTools.FieldRef<characterScript, int> PopGameplay = AccessTools.FieldRefAccess<characterScript, int>("ePop_Gameplay");

	private static readonly AccessTools.FieldRef<characterScript, int> PopGrafik = AccessTools.FieldRefAccess<characterScript, int>("ePop_Grafik");

	private static readonly AccessTools.FieldRef<roomScript, roomButtonScript> RoomButtons = AccessTools.FieldRefAccess<roomScript, roomButtonScript>("rbS_");

	private static readonly MethodInfo LerpBalken = AccessTools.Method(typeof(roomWindow), "LerpBalken");

	private static readonly AccessTools.FieldRef<Menu_W_Aufgabe_Abbrechen, roomScript> CancelRoom = AccessTools.FieldRefAccess<Menu_W_Aufgabe_Abbrechen, roomScript>("rS_");

	public static float Speed(characterScript c)
	{
		try
		{
			return Convert.ToSingle(WorkSpeed.Invoke(c, null));
		}
		catch
		{
			return 1f;
		}
	}

	public static void Attach(UpdateProject p, roomScript room)
	{
		p.RoomId = room.myID;
		GameObject go = new GameObject("Task_" + p.TaskId);
		go.AddComponent<UpdateTask>().TaskId = p.TaskId;
		room.taskID = p.TaskId;
		room.taskGameObject = go;
	}

	public static void Detach(UpdateProject p)
	{
		foreach (UpdateTask task in UnityEngine.Object.FindObjectsOfType<UpdateTask>())
		{
			if (task.TaskId == p.TaskId)
			{
				task.gameObject.name = "Task_done";
				UnityEngine.Object.Destroy(task.gameObject);
			}
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
	public static void Restore(UpdateProject p)
	{
		roomScript room = CopyProtectDev.FindRoom(p.RoomId);
		if ((bool)room && (room.taskID == -1 || room.taskID == p.TaskId) && !GameObject.Find("Task_" + p.TaskId))
		{
			Attach(p, room);
		}
	}

	public static void ClearTasks()
	{
		foreach (UpdateTask task in UnityEngine.Object.FindObjectsOfType<UpdateTask>())
		{
			task.gameObject.name = "Task_done";
			UnityEngine.Object.Destroy(task.gameObject);
		}
	}

	public static UpdateProject Ours(roomScript r)
	{
		if (!r || !r.taskGameObject || !r.taskGameObject.GetComponent<UpdateTask>())
		{
			return null;
		}
		return GameUpdates.ProjectInRoom(r);
	}

	// ---- Work ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(characterScript), "WORK_Engine")]
	private static void DevWork(characterScript __instance, roomScript rS_, ref bool __result)
	{
		if (__result || !rS_ || rS_.typ != 1)
		{
			return;
		}
		UpdateProject p = Ours(rS_);
		if (p == null)
		{
			return;
		}
		__result = true;
		try
		{
			characterScript c = __instance;
			float desk = (c.objectUsingS_ ? (((float)c.objectUsingS_.qualitaet - 1f) / 10f + 1f) : 1f);
			float points = UnityEngine.Random.Range(0.1f, GameUpdates.Skill(c, p.Kind) * 0.1f) * desk;
			points = (float)GetWorkResult.Invoke(c, new object[1] { points });
			UpdateKind k = p.Kind;
			bool graphics = k == UpdateKind.MmoGraphics || k == UpdateKind.MmoExpansion;
			bool design = k == UpdateKind.Features || k == UpdateKind.Rating || k == UpdateKind.MmoFeatures || k == UpdateKind.MmoRating || k == UpdateKind.MmoExpansion || k == UpdateKind.MmoPatch;
			bool programming = k != UpdateKind.Rating && k != UpdateKind.MmoRating && k != UpdateKind.MmoGraphics;
			if (!c.settings_.disableWorkIcons && UiVisible(c))
			{
				c.StartCoroutine((IEnumerator)CreatePop.Invoke(c, new object[4]
				{
					(k == UpdateKind.MmoGraphics) ? PopGrafik(c) : (design ? PopGameplay(c) : PopTechnik(c)),
					points,
					0f,
					13
				}));
			}
			Learn.Invoke(c, new object[8]
			{
				design,
				programming,
				graphics,
				false,
				false,
				false,
				false,
				false
			});
			GameUpdates.Work(points, rS_);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Update work failed: " + ex);
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
		if (!r || r.typ != 1)
		{
			return false;
		}
		UpdateProject p = Ours(r);
		if (p == null)
		{
			return false;
		}
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
		rw.uiObjects[0].GetComponent<Text>().text = p.Title;
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
		Sprite sprite = UpdateIcons.For(p.Kind);
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

	// ---- The busy room's menu: an "Update" entry that opens the project ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(roomButtonScript), "BUTTON_Main")]
	private static void RoomClicked(roomButtonScript __instance)
	{
		if (!__instance.rS_ || __instance.rS_.typ != 1 || __instance.uiOptions == null || __instance.uiOptions.Length <= 3 || !__instance.uiOptions[3])
		{
			return;
		}
		try
		{
			BusyPanel(__instance);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Update busy menu: " + ex);
		}
	}

	private static void BusyPanel(roomButtonScript rb)
	{
		GameObject panel = rb.uiOptions[3];
		UpdateProject p = Ours(rb.rS_);
		Transform details = panel.transform.Find(DetailsName);
		if (!details && p != null)
		{
			Transform template = panel.transform.Find("Option1");
			if ((bool)template)
			{
				details = UpdateMenus.CloneButton(template, panel.transform, DetailsName);
				details.SetSiblingIndex(template.GetSiblingIndex());
				roomButtonScript owner = rb;
				UIKit.OnClick(details.GetComponent<Button>(), () =>
				{
					UIKit.Sound();
					UpdateProject now = Ours(owner.rS_);
					if ((bool)G.Gui)
					{
						G.Gui.CloseAllRoomButtons();
					}
					if ((bool)G.MS)
					{
						G.MS.PauseGame(p: false);
					}
					EditorWindow window = ((now == null) ? EditorWindow.Updates : ((GameUpdates.IsMmo(now.Kind) || GameUpdates.IsAntiBot(now.Kind)) ? EditorWindow.Mmo : (GameUpdates.IsStore(now.Kind) ? EditorWindow.Store : EditorWindow.Updates)));
					window.Open();
					if (now != null)
					{
						window.Push(new UpdateProjectPage(now.TaskId));
					}
				});
			}
		}
		if ((bool)details)
		{
			details.gameObject.SetActive(p != null);
			Transform label = details.Find("Text");
			if (p != null && (bool)label)
			{
				label.GetComponent<Text>().text = BusyLabel(p.Kind);
			}
		}
		if (p == null)
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

	// Room menu entries are in title case, like the game's own.
	private static string BusyLabel(UpdateKind k)
	{
		return k switch
		{
			UpdateKind.Port => "Port Details",
			UpdateKind.Engine => "Engine Update",
			UpdateKind.Features => "New Features",
			UpdateKind.Rating => "Age Rating Edit",
			UpdateKind.MmoGraphics => "Graphics Overhaul",
			UpdateKind.MmoClient64 => "64-Bit Client",
			UpdateKind.MmoMigration => "Engine Migration",
			UpdateKind.MmoExpansion => "World Expansion",
			UpdateKind.MmoPatch => "Content Patch",
			UpdateKind.MmoPort => "New Platforms",
			UpdateKind.MmoFeatures => "New Game Systems",
			UpdateKind.MmoRating => "Age Rating Change",
			UpdateKind.MmoBotBust => "Bot Ban Wave",
			UpdateKind.MmoWelcomeBack => "Welcome-Back Campaign",
			UpdateKind.MmoClassic => "Classic Servers",
			UpdateKind.MmoFreeTrial => "Free Trial",
			UpdateKind.MmoStability => "Stability Patch",
			UpdateKind.MmoClassicPhase => "Classic Phase",
			UpdateKind.StoreBuild => "Store Client",
			UpdateKind.StoreFeature => "Store Feature",
			UpdateKind.StoreConsole => "Console Storefront",
			UpdateKind.AntiBotBuild => "Anti-Bot System",
			UpdateKind.AntiBotUpdate => "Anti-Bot Update",
			_ => "Update",
		};
	}

	// ---- The game's "cancel task" dialog ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_W_Aufgabe_Abbrechen), "Init")]
	private static void CancelDialog(Menu_W_Aufgabe_Abbrechen __instance, roomScript script_)
	{
		UpdateProject p = Ours(script_);
		if (p != null)
		{
			long refund = GameUpdates.Refund(p);
			__instance.uiObjects[0].GetComponent<Text>().text = ((refund > 0) ? G.Text(569, "You get <NUM> back.").Replace("<NUM>", G.Money(refund)) : "");
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_W_Aufgabe_Abbrechen), "BUTTON_Yes")]
	private static void CancelConfirmed(Menu_W_Aufgabe_Abbrechen __instance)
	{
		UpdateProject p = Ours(CancelRoom(__instance));
		if (p != null)
		{
			GameUpdates.Cancel(p);
		}
	}
}
