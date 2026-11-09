using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Mmo;
using HolyAddon.Core.Updates;
using HolyAddon.UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// The game's MMO addon screen (Menu_Dev_MMOAddon, GUI_Main.uiObjects[247]) opened from MMO Management. While it's
// showing it keeps the game in menu mode (paused, room clicks off), and its X only hides itself: normally the game's
// MMO list is still open underneath and ends menu mode when it closes. Opened from us there's no list, so X brings
// back the MMO's page in MMO Management instead (closing that ends menu mode as usual).
[HarmonyLib.HarmonyPatch]
internal static class MmoAddonScreen
{
	public static int FromGame = -1;

	private static bool ListOpen
	{
		get
		{
			GUI_Main gui = G.Gui;
			if ((bool)gui && gui.uiObjects != null && gui.uiObjects.Length > 246 && (bool)gui.uiObjects[246])
			{
				return gui.uiObjects[246].activeSelf;
			}
			return false;
		}
	}

	[HarmonyLib.HarmonyPostfix]
	[HarmonyLib.HarmonyPatch(typeof(Menu_Dev_MMOAddon), "BUTTON_Close")]
	private static void Closed()
	{
		int id = FromGame;
		FromGame = -1;
		if (id < 0 || ListOpen)
		{
			return;
		}
		BackToMmo(id);
	}

	// Reopens MMO Management at this MMO's page (after one of the game's own screens opened from there).
	public static void BackToMmo(int gameId)
	{
		try
		{
			EditorWindow.Mmo.Open();
			if (MmoOps.IsMyMmo(GameUpdates.FindGame(gameId)))
			{
				EditorWindow.Mmo.Push(new MmoDashboardPage(gameId));
			}
		}
		catch (System.Exception ex)
		{
			Plugin.Log.LogError("Back to MMO Management: " + ex);
			if ((bool)G.Gui)
			{
				G.Gui.CloseMenu();
			}
		}
	}

	// Starting the addon closes every menu and ends menu mode by itself.
	[HarmonyLib.HarmonyPostfix]
	[HarmonyLib.HarmonyPatch(typeof(Menu_Dev_MMOAddon), "BUTTON_Start")]
	private static void Started(Menu_Dev_MMOAddon __instance)
	{
		if (!__instance.gameObject.activeSelf)
		{
			FromGame = -1;
		}
	}

	// The game's free update screen (Menu_Dev_Update, GUI_Main.uiObjects[108]) works the same way: it holds menu
	// mode while open, and its X goes back to the game's update list. Opened from MMO Management, X comes back here.
	public static int UpdateFromGame = -1;

	[HarmonyLib.HarmonyPrefix]
	[HarmonyLib.HarmonyPatch(typeof(Menu_Dev_Update), "BUTTON_Close")]
	private static bool UpdateClosed(Menu_Dev_Update __instance)
	{
		int id = UpdateFromGame;
		UpdateFromGame = -1;
		if (id < 0)
		{
			return true;
		}
		if ((bool)G.Sfx)
		{
			G.Sfx.PlaySound(3, force: true);
		}
		__instance.gameObject.SetActive(value: false);
		BackToMmo(id);
		return false;
	}

	[HarmonyLib.HarmonyPostfix]
	[HarmonyLib.HarmonyPatch(typeof(Menu_Dev_Update), "BUTTON_Start")]
	private static void UpdateStarted(Menu_Dev_Update __instance)
	{
		if (!__instance.gameObject.activeSelf)
		{
			UpdateFromGame = -1;
		}
	}
}

// "MMO Management" in an idle development room's menu, below Develop Copy Protection.
internal static class MmoMenu
{
	private const string EntryName = "Editor_MmoManagement";

	private static bool logged;

	public static void EnsureEntry(roomButtonScript rb)
	{
		if (rb.uiOptions == null || rb.uiOptions.Length <= 1 || !rb.uiOptions[1])
		{
			return;
		}
		Transform panel = rb.uiOptions[1].transform;
		Transform entry = panel.Find(EntryName);
		bool show = MmoOps.On;
		Transform engines = panel.Cast<Transform>().FirstOrDefault((Transform c) => CopyProtectRoom.Method(c) == "BUTTON_Dev_Engines");
		if (!entry)
		{
			if (!show || !engines)
			{
				return;
			}
			entry = CopyProtectRoom.Clone(engines, panel, EntryName);
			roomButtonScript owner = rb;
			UIKit.OnClick(entry.GetComponent<Button>(), () => Click(owner));
			SetIcon(entry);
		}
		entry.gameObject.SetActive(show);
		if (!show)
		{
			return;
		}
		Transform after = Visible(panel, "Editor_CopyProtect") ?? Visible(panel, "Holy_anticheat") ?? engines;
		if ((bool)after)
		{
			int target = after.GetSiblingIndex();
			entry.SetSiblingIndex((entry.GetSiblingIndex() < target) ? target : (target + 1));
		}
		Transform label = entry.Find("Text");
		if ((bool)label)
		{
			label.GetComponent<Text>().text = "MMO Management";
		}
		int count = MmoOps.MyMmos().Count;
		Transform lockIcon = entry.Find("Lock");
		if ((bool)lockIcon)
		{
			lockIcon.gameObject.SetActive(value: false);
		}
		UIKit.SetTooltip(entry.gameObject, ((count == 0) ? "Once you have an MMO on the market: modernise it, grow it, release expansion packs and keep its players." : ("Modernise your MMOs, grow them, release expansion packs and keep their players (" + ((count == 1) ? "1 MMO" : (count + " MMOs")) + " on the market).")) + " Also the MMO subscription charts.");
	}

	private static Transform Visible(Transform panel, string name)
	{
		Transform t = panel.Find(name);
		if (!t || !t.gameObject.activeSelf)
		{
			return null;
		}
		return t;
	}

	// The cloned option shows the engine picture; swap in the MMO icon.
	private static void SetIcon(Transform entry)
	{
		Sprite sprite = UpdateIcons.Mmo();
		List<string> names = new List<string>();
		Image best = null;
		foreach (Image img in entry.GetComponentsInChildren<Image>(includeInactive: true))
		{
			names.Add(img.name);
			if (img.transform != entry && img.name != "Lock" && img.transform.parent.name != "Lock" && (!best || (img.name == "Icon" && best.name != "Icon")))
			{
				best = img;
			}
		}
		if (!logged)
		{
			logged = true;
			Plugin.Log.LogInfo("Room menu option images: " + string.Join(", ", names));
		}
		if ((bool)best && (bool)sprite)
		{
			best.sprite = sprite;
		}
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
		List<gameScript> mmos = MmoOps.MyMmos();
		MmoUi.PreferredRoom = (room ? room.myID : (-1));
		EditorWindow.Mmo.Open();
		if (mmos.Count == 1)
		{
			EditorWindow.Mmo.Push(new MmoDashboardPage(mmos[0].myID));
		}
	}
}
