using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// A "Rooms Overview" button in every room's menu: the game reuses one row of room tools (uiObjects[2]: rename,
// support another room and so on) in every room's menu, idle or busy, so a copy of one of its buttons there shows in
// all of them. It opens the overview on that room's type, with the room highlighted.
[HarmonyPatch]
internal static class RoomsMenu
{
	private const string ButtonName = "Editor_RoomsOverview";

	private static bool logged;

	[HarmonyPostfix]
	[HarmonyPatch(typeof(roomButtonScript), "BUTTON_Main")]
	private static void Opened(roomButtonScript __instance)
	{
		try
		{
			Ensure(__instance);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Rooms Overview room button: " + ex.Message);
		}
	}

	private static void Ensure(roomButtonScript rb)
	{
		if (!G.Singleplayer || rb.uiObjects == null || rb.uiObjects.Length <= 2 || !rb.uiObjects[2])
		{
			return;
		}
		Transform tools = rb.uiObjects[2].transform;
		if (!tools.gameObject.activeInHierarchy || (bool)tools.Find(ButtonName))
		{
			return;
		}
		List<RectTransform> buttons = (from Transform c in tools
			where c.name != ButtonName && c.gameObject.activeSelf && (bool)c.GetComponent<Button>() && c is RectTransform
			select (RectTransform)c).ToList();
		if (buttons.Count == 0)
		{
			return;
		}
		LayoutGroup layout = tools.GetComponent<LayoutGroup>();
		if (!logged)
		{
			logged = true;
			Plugin.Log.LogInfo("Room tools row: " + tools.name + " (" + ((layout != null) ? layout.GetType().Name : "no layout group") + "), buttons: " + string.Join(", ", buttons.Select((RectTransform b) => b.name + "@" + b.anchoredPosition)));
		}
		RectTransform last = buttons[buttons.Count - 1];
		GameObject go = UnityEngine.Object.Instantiate(last.gameObject, tools, worldPositionStays: false);
		go.name = ButtonName;
		foreach (setText s in go.GetComponentsInChildren<setText>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(s);
		}
		foreach (lockDestroy l in go.GetComponentsInChildren<lockDestroy>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(l);
		}
		Button button = go.GetComponent<Button>();
		button.onClick = new Button.ButtonClickedEvent();
		button.interactable = true;
		roomButtonScript owner = rb;
		button.onClick.AddListener(() => Open(owner));
		// The copied button's picture becomes the overview's; any label says what it is.
		Image picture = go.GetComponentsInChildren<Image>(includeInactive: true).FirstOrDefault((Image i) => i.gameObject != go);
		Sprite icon = RoomsOverviewPage.RoomIcon;
		if ((bool)picture && (bool)icon)
		{
			picture.sprite = icon;
			picture.preserveAspect = true;
			picture.color = Color.white;
		}
		foreach (Text t in go.GetComponentsInChildren<Text>(includeInactive: true))
		{
			t.text = "Rooms Overview";
		}
		UIKit.SetTooltip(go, "<b>Rooms Overview</b>\nEvery room's job, what it does by itself, who supports whom, and what needs a look.");
		// In a layout group it simply goes last; otherwise one step on from the last button, the way they're spaced.
		RectTransform mine = (RectTransform)go.transform;
		mine.SetAsLastSibling();
		if (layout == null)
		{
			Vector2 step = ((buttons.Count >= 2) ? (last.anchoredPosition - buttons[buttons.Count - 2].anchoredPosition) : new Vector2(last.rect.width + 4f, 0f));
			mine.anchoredPosition = last.anchoredPosition + step;
		}
	}

	private static void Open(roomButtonScript rb)
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
		RoomsOverviewPage.FromRoom = room;
		EditorWindow.Rooms.Open();
	}
}
