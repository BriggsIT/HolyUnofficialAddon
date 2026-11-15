using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// The new-game screen's "Timed Exclusive" button: a copy of its "Buy DevKits" button, in the free space to its left
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
			// With the taller design screen (ExtraTopicsRow), the choice sits on the platform page itself.
			if (ExtraTopicsRow.In && TimedExclusives.On)
			{
				if (button.activeSelf)
				{
					button.SetActive(value: false);
				}
				if (EditorWindow.TimedExclusive.IsOpen)
				{
					EditorWindow.TimedExclusive.Close();
				}
				if (PickedFirst >= 0 && !Platforms(menu).Contains(PickedFirst))
				{
					PickedFirst = -1;
				}
				Strip(menu);
				return;
			}
			if ((bool)strip)
			{
				UnityEngine.Object.Destroy(strip);
				strip = null;
				stripKey = "";
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
			string text = ((PickedFirst < 0) ? "Timed Exclusive" : ("Timed: " + PickedMonths + " months"));
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

	// ---- On the platform page: the choice itself, under "Buy DevKits" ----

	private static GameObject strip;

	private static string stripKey = "";

	// Two rows of buttons in the game's style (copies of "Buy DevKits"): which platform first, and for how long,
	// with what the deal pays underneath. Greyed out, with the reason, when the game can't have one.
	private static void Strip(Menu_DevGame menu)
	{
		Transform page = menu.uiObjects[146].transform.parent;
		RectTransform buy = page.Find("Button_PlatformKaufen") as RectTransform;
		// Laid out while the platform page is showing, so its rows are where they'll be.
		if (!buy || !page.gameObject.activeInHierarchy)
		{
			return;
		}
		int[] slots = ((menu.g_GamePlatform != null) ? menu.g_GamePlatform.Take(4).ToArray() : new int[0]);
		bool wanted = Wanted(menu);
		string key = string.Join(",", slots) + "|" + PickedFirst + "|" + PickedMonths + "|" + wanted;
		if ((bool)strip && key == stripKey)
		{
			return;
		}
		stripKey = key;
		if ((bool)strip)
		{
			UnityEngine.Object.Destroy(strip);
		}
		RectTransform pageRt = (RectTransform)page;
		Rect b = Local(buy, pageRt);
		// As wide as the row "Buy DevKits" is in: the help icon on the left to the platforms' total on the right
		// (tall things such as the platforms' background don't count).
		float left = b.xMin;
		float right = b.xMax;
		foreach (Graphic gr in page.GetComponentsInChildren<Graphic>(includeInactive: false))
		{
			if (((bool)strip && gr.transform.IsChildOf(strip.transform)) || ((bool)button && gr.transform.IsChildOf(button.transform)))
			{
				continue;
			}
			Rect r = Local(gr.rectTransform, pageRt);
			if (r.width >= 2f && r.height >= 2f && r.height <= b.height * 2.5f && r.yMax >= b.yMin - 4f && r.yMin <= b.yMax + 4f)
			{
				left = Mathf.Min(left, r.xMin);
				right = Mathf.Max(right, r.xMax);
			}
		}
		float space = ExtraTopicsRow.Pitch - 18f;
		float topY = b.yMin - 16f;
		strip = new GameObject("Addon_TimedExclusiveStrip", typeof(RectTransform));
		strip.transform.SetParent(page, worldPositionStays: false);
		RectTransform srt = (RectTransform)strip.transform;
		srt.anchorMin = (srt.anchorMax = new Vector2(0.5f, 0.5f));
		srt.pivot = new Vector2(0.5f, 0.5f);
		srt.sizeDelta = new Vector2(right - left, space);
		srt.anchoredPosition = new Vector2((left + right) / 2f, topY - space / 2f) - pageRt.rect.center;
		float w = right - left;
		float rowH = Mathf.Clamp((space - 26f) / 2f, 22f, b.height);
		// The timed exclusive's icon on the left, as tall as the panel.
		float icon = Mathf.Min(space - 6f, 66f);
		Image pic = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		pic.transform.SetParent(strip.transform, worldPositionStays: false);
		Place((RectTransform)pic.transform, 0f, (space - icon) / 2f, icon, icon);
		pic.sprite = UpdateIcons.TimedExclusive();
		pic.preserveAspect = true;
		pic.raycastTarget = false;
		float lx = icon + 10f;
		float labelW = 118f;
		string why = (wanted ? null : (menu.typ_contractGame ? "Contract games don't have timed exclusives." : "Pick two or more platforms for a multiplatform game first."));
		// First on: none, or one of the game's four platform slots (an empty slot stays greyed out).
		Label("Timed exclusive", lx, 0f, labelW, rowH, bold: true, buy);
		List<KeyValuePair<int, string>> choices = new List<KeyValuePair<int, string>>
		{
			new KeyValuePair<int, string>(-1, "None")
		};
		for (int k = 0; k < 4; k++)
		{
			int id0 = ((k < slots.Length) ? slots[k] : (-1));
			choices.Add(new KeyValuePair<int, string>(id0, (id0 >= 0) ? Name(id0) : ((k == 0) ? "Main Platform" : ("Platform " + (k + 1)))));
		}
		float bx = lx + labelW + 6f;
		float bw = (w - bx - 6f * (float)(choices.Count - 1)) / (float)choices.Count;
		for (int i = 0; i < choices.Count; i++)
		{
			int id = choices[i].Key;
			bool empty = i > 0 && id < 0;
			bool on = !empty && PickedFirst == id;
			string tip = why ?? (empty ? "No platform in this slot." : ((id < 0) ? "Release on all its platforms at once." : ("<b>" + choices[i].Value + " first</b>\n" + DealText(id, PickedMonths))));
			SmallButton(on ? ("[" + choices[i].Value + "]") : choices[i].Value, bx + (float)i * (bw + 6f), 0f, bw, rowH, wanted && !empty, tip, buy, () =>
			{
				PickedFirst = id;
			});
		}
		// For how long
		float y2 = rowH + 6f;
		Label("Exclusive for", lx, y2, labelW, rowH, bold: true, buy);
		int n = TimedExclusives.Windows.Length;
		float mw = (w - bx - 6f * (float)(n - 1)) / (float)n;
		for (int j = 0; j < n; j++)
		{
			int m = TimedExclusives.Windows[j];
			bool on2 = PickedMonths == m;
			string tip2 = why ?? ((PickedFirst < 0) ? "Pick the platform that gets it first." : "Longer deals pay more, but the other platforms wait longer.");
			SmallButton(on2 ? ("[" + m + " months]") : (m + " months"), bx + (float)j * (mw + 6f), y2, mw, rowH, wanted && PickedFirst >= 0, tip2, buy, () =>
			{
				PickedMonths = m;
			});
		}
		// What it pays
		string text = why ?? ((PickedFirst < 0) ? "Release the game on one platform first; its maker pays you for the deal, and the others get it later." : DealText(PickedFirst, PickedMonths));
		Label(text, lx, y2 + rowH + 4f, w - lx, Mathf.Max(18f, space - (y2 + rowH + 4f)), bold: false, buy);
	}

	// A copy of "Buy DevKits" at (x, y) from the strip's top left.
	private static void SmallButton(string text, float x, float y, float w, float h, bool enabled, string tip, RectTransform template, Action onClick)
	{
		GameObject go = UnityEngine.Object.Instantiate(template.gameObject, strip.transform, worldPositionStays: false);
		go.name = "Choice";
		foreach (setText s in go.GetComponentsInChildren<setText>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(s);
		}
		foreach (tooltip t in go.GetComponentsInChildren<tooltip>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(t);
		}
		Place((RectTransform)go.transform, x, y, w, h);
		Text label2 = go.GetComponentInChildren<Text>(includeInactive: true);
		if ((bool)label2)
		{
			label2.text = text;
			label2.resizeTextForBestFit = true;
			label2.resizeTextMinSize = 8;
			label2.resizeTextMaxSize = Mathf.Max(9, label2.fontSize);
		}
		Button b = go.GetComponent<Button>();
		b.onClick = new Button.ButtonClickedEvent();
		b.interactable = enabled;
		UIKit.OnClick(b, () =>
		{
			UIKit.Sound();
			onClick();
		});
		if (!string.IsNullOrEmpty(tip))
		{
			UIKit.SetTooltip(go, tip);
		}
	}

	// A label in the font of "Buy DevKits".
	private static void Label(string text, float x, float y, float w, float h, bool bold, RectTransform template)
	{
		Text source = template.GetComponentInChildren<Text>(includeInactive: true);
		if (!source)
		{
			return;
		}
		GameObject go = UnityEngine.Object.Instantiate(source.gameObject, strip.transform, worldPositionStays: false);
		go.name = "Label";
		foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
		{
			if (!(mb is Graphic))
			{
				UnityEngine.Object.DestroyImmediate(mb);
			}
		}
		Place((RectTransform)go.transform, x, y, w, h);
		Text t = go.GetComponent<Text>();
		t.text = (bold ? ("<b>" + text + "</b>") : text);
		t.supportRichText = true;
		t.alignment = (bold ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft);
		t.horizontalOverflow = HorizontalWrapMode.Wrap;
		t.verticalOverflow = VerticalWrapMode.Truncate;
		t.resizeTextForBestFit = true;
		t.resizeTextMinSize = 8;
		t.resizeTextMaxSize = Mathf.Max(9, source.fontSize);
		t.raycastTarget = false;
	}

	// Top-left placement inside the strip.
	private static void Place(RectTransform rt, float x, float y, float w, float h)
	{
		rt.anchorMin = (rt.anchorMax = new Vector2(0f, 1f));
		rt.pivot = new Vector2(0f, 1f);
		rt.sizeDelta = new Vector2(w, h);
		rt.anchoredPosition = new Vector2(x, 0f - y);
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
