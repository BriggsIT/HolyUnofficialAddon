using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// The new-game screen's "Timed exclusive" button: a copy of its "Buy DevKits" button, in the free space to its left
// on the platform page (there's no room for more rows between the platform type dropdown and the platform cards).
// Shown for multiplatform games with two or more platforms picked; it opens a small chooser (TimedExclusivePage)
// over the screen. The choice is attached to the game when development starts (Menu_DevGame.BUTTON_Start creates
// it through games.CreateNewGame).
[HarmonyPatch]
internal static class TimedExclusiveMenu
{
	private const int DevGameMenu = 56;

	private const string ButtonName = "Addon_TimedExclusive";

	private static GameObject button;

	private static Text label;

	private static bool wasOpen;

	private static bool failed;

	private static string shownLabel = "";

	public static int PickedFirst = -1;

	public static int PickedMonths = 12;

	// BUTTON_Start: the game it creates, and the choice to attach.
	private static bool starting;

	private static gameScript created;

	public static Menu_DevGame Menu
	{
		get
		{
			GUI_Main gui = G.Gui;
			if (!gui || gui.uiObjects == null || gui.uiObjects.Length <= DevGameMenu || !gui.uiObjects[DevGameMenu])
			{
				return null;
			}
			return gui.uiObjects[DevGameMenu].GetComponent<Menu_DevGame>();
		}
	}

	public static void Tick()
	{
		Menu_DevGame menu = Menu;
		if (!menu)
		{
			return;
		}
		bool open = menu.gameObject.activeInHierarchy;
		if (open && !wasOpen)
		{
			// A new game: starts without a timed exclusive.
			PickedFirst = -1;
		}
		wasOpen = open;
		if (!open)
		{
			if (EditorWindow.TimedExclusive.IsOpen)
			{
				EditorWindow.TimedExclusive.Close();
			}
			return;
		}
		if (failed)
		{
			return;
		}
		try
		{
			if (!button && !Inject(menu))
			{
				return;
			}
			bool show = Wanted(menu);
			if (button.activeSelf != show)
			{
				button.SetActive(show);
			}
			if (!show)
			{
				if (EditorWindow.TimedExclusive.IsOpen)
				{
					EditorWindow.TimedExclusive.Close();
				}
				return;
			}
			if (PickedFirst >= 0 && !Platforms(menu).Contains(PickedFirst))
			{
				PickedFirst = -1;
			}
			string text = ((PickedFirst < 0) ? "Timed exclusive" : ("Timed: " + PickedMonths + " months"));
			if (text != shownLabel)
			{
				shownLabel = text;
				label.text = text;
				UIKit.SetTooltip(button, Tooltip());
			}
		}
		catch (Exception ex)
		{
			failed = true;
			Plugin.Log.LogError("Timed exclusive button failed: " + ex);
		}
	}

	public static List<int> Platforms(Menu_DevGame menu)
	{
		if (!menu || menu.g_GamePlatform == null)
		{
			return new List<int>();
		}
		return menu.g_GamePlatform.Where((int id) => id != -1).Distinct().ToList();
	}

	public static bool Wanted(Menu_DevGame menu)
	{
		if (!TimedExclusives.On || !menu || menu.typ_contractGame || menu.uiObjects.Length <= 146 || !menu.uiObjects[146])
		{
			return false;
		}
		Dropdown type = menu.uiObjects[146].GetComponent<Dropdown>();
		if (!type || type.value != 0)
		{
			return false;
		}
		return Platforms(menu).Count >= 2;
	}

	public static string Name(int id)
	{
		platformScript p = GameUpdates.Platform(id);
		if (!p)
		{
			return "?";
		}
		return p.GetName();
	}

	public static string Tooltip()
	{
		if (PickedFirst < 0)
		{
			return "<b>Timed exclusive</b>\nRelease the game on one of its platforms first, as an exclusive: its maker pays you for the deal, and the game comes to the other platforms after the time you pick. You develop it for all of them now.";
		}
		return "<b>Timed exclusive on " + Name(PickedFirst) + " for " + PickedMonths + " months</b>\n" + DealText(PickedFirst, PickedMonths) + "\nClick to change.";
	}

	public static string DealText(int platform, int months)
	{
		platformScript p = GameUpdates.Platform(platform);
		float rate = TimedExclusives.DealRate(p, months);
		if (rate <= 0f)
		{
			return "Your own platform: no deal money, but it sells more of your consoles while it's exclusive.";
		}
		return p.GetManufacturer() + " pays " + Mathf.RoundToInt(rate * 100f) + "% of the development costs when it comes out (a full exclusive gets " + Mathf.RoundToInt(p.GetExklusivBonus()) + "% off).";
	}

	private static bool Inject(Menu_DevGame menu)
	{
		if (menu.uiObjects.Length <= 146 || !menu.uiObjects[146])
		{
			return false;
		}
		Transform page = menu.uiObjects[146].transform.parent;
		RectTransform buy = page.Find("Button_PlatformKaufen") as RectTransform;
		if (!buy)
		{
			failed = true;
			Plugin.Log.LogWarning("Timed exclusive: the Buy DevKits button wasn't found");
			return false;
		}
		GameObject go = UnityEngine.Object.Instantiate(buy.gameObject, page, worldPositionStays: false);
		go.name = ButtonName;
		foreach (setText s in go.GetComponentsInChildren<setText>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(s);
		}
		foreach (tooltip t in go.GetComponentsInChildren<tooltip>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(t);
		}
		Button b = go.GetComponent<Button>();
		b.onClick = new Button.ButtonClickedEvent();
		UIKit.OnClick(b, () =>
		{
			UIKit.Sound();
			if (EditorWindow.TimedExclusive.IsOpen)
			{
				EditorWindow.TimedExclusive.Close();
			}
			else
			{
				EditorWindow.TimedExclusive.Open();
			}
		});
		label = go.GetComponentInChildren<Text>(includeInactive: true);
		if ((bool)label)
		{
			label.resizeTextForBestFit = true;
			label.resizeTextMinSize = 9;
			label.resizeTextMaxSize = Mathf.Max(10, label.fontSize);
		}
		Place((RectTransform)go.transform, buy, page.Find("InfoPlatformTyp") as RectTransform, (RectTransform)page);
		button = go;
		shownLabel = "";
		go.SetActive(value: false);
		Plugin.Log.LogInfo("Timed exclusive button added to the new-game screen");
		return true;
	}

	// In the gap left of "Buy DevKits", up to the help icon, same height.
	private static void Place(RectTransform mine, RectTransform buy, RectTransform info, RectTransform page)
	{
		float buyLeft = Local(buy, page).xMin;
		float left = (((bool)info && info.gameObject.activeSelf) ? (Local(info, page).xMax + 6f) : (buyLeft - 136f));
		float right = buyLeft - 8f;
		float width = Mathf.Clamp(right - left, 90f, 150f);
		mine.sizeDelta = new Vector2(width, buy.sizeDelta.y);
		Rect buyRect = Local(buy, page);
		float targetCenter = right - width / 2f;
		mine.anchoredPosition = buy.anchoredPosition + new Vector2(targetCenter - buyRect.center.x, 0f);
	}

	// A child's rect in the page's own space.
	private static Rect Local(RectTransform rt, RectTransform page)
	{
		Vector3[] c = new Vector3[4];
		rt.GetWorldCorners(c);
		Vector3 a = page.InverseTransformPoint(c[0]);
		Vector3 b = page.InverseTransformPoint(c[2]);
		return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
	}

	// ---- Starting development: attach the choice to the new game ----

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_DevGame), "BUTTON_Start")]
	private static void StartBefore(Menu_DevGame __instance, out bool __state)
	{
		__state = PickedFirst >= 0 && Wanted(__instance);
		starting = __state;
		created = null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(games), "CreateNewGame")]
	private static void GameCreated(gameScript __result)
	{
		if (starting)
		{
			created = __result;
		}
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(Menu_DevGame), "BUTTON_Start")]
	private static Exception StartAfter(bool __state, Exception __exception)
	{
		starting = false;
		if (__state && (bool)created && __exception == null)
		{
			try
			{
				TimedExclusives.Register(created, PickedFirst, PickedMonths);
				PickedFirst = -1;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Timed exclusive start: " + ex);
			}
		}
		created = null;
		if (EditorWindow.TimedExclusive.IsOpen)
		{
			EditorWindow.TimedExclusive.Close();
		}
		return __exception;
	}
}
