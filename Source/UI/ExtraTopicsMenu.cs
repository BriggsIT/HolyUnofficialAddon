using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Topics;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// The game design screen's "More Topics" button: a copy of its Sub topic button, placed below it if there's room
// (else to its right, else as a small icon inside its right end). It opens a small chooser (ExtraTopicsPage) over the
// screen; each slot is picked in the game's own topic picker. The topics are attached to the game when development
// starts (Menu_DevGame.BUTTON_Start creates it through games.CreateNewGame).
[HarmonyPatch]
internal static class ExtraTopicsMenu
{
	private const int DevGameMenu = 56;

	private const int TopicPicker = 62;

	private const string ButtonName = "Addon_ExtraTopics";

	private static GameObject button;

	private static Text label;

	private static bool failed;

	private static string shown = "";

	// The topics picked for the game on the design screen: third and fourth (-1: none).
	public static int[] Pending = new int[2] { -1, -1 };

	// Copied from the game it's a port or remaster of: shown, not changed.
	public static bool Locked;

	// A contract game: its topics are set.
	public static bool Hidden;

	// Which slot the game's topic picker is choosing for (0: the game's own topics).
	private static int picking;

	private static int opening;

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

	private static Menu_DevGame_Theme Picker
	{
		get
		{
			GUI_Main gui = G.Gui;
			if (!gui || gui.uiObjects == null || gui.uiObjects.Length <= TopicPicker || !gui.uiObjects[TopicPicker])
			{
				return null;
			}
			return gui.uiObjects[TopicPicker].GetComponent<Menu_DevGame_Theme>();
		}
	}

	public static bool Unlocked(int slot)
	{
		return ExtraTopics.Researched(slot);
	}

	public static void Tick()
	{
		Menu_DevGame menu = Menu;
		if (!menu)
		{
			return;
		}
		if (!menu.gameObject.activeInHierarchy)
		{
			if (EditorWindow.ExtraTopics.IsOpen)
			{
				EditorWindow.ExtraTopics.Close();
			}
			return;
		}
		if (failed || !ExtraTopics.On)
		{
			return;
		}
		try
		{
			if (!ExtraTopics.EnsureSlots())
			{
				return;
			}
			// A topic the game's own buttons now use can't be an extra one too.
			for (int i = 0; i < 2; i++)
			{
				if (Pending[i] >= 0 && (Pending[i] == menu.g_GameMainTheme || Pending[i] == menu.g_GameSubTheme))
				{
					Pending[i] = -1;
				}
			}
			// Third topic and Fourth topic: a row on the design screen, always there (greyed out, with the research to
			// do as the tooltip, until it's done), so the screen keeps one size.
			bool want = true;
			if (ExtraTopicsRow.In && !ExtraTopicsRow.Alive)
			{
				// The screen was rebuilt (a save loaded): start again.
				ExtraTopicsRow.Remove();
				rowShown = "";
			}
			if (!want)
			{
				if (ExtraTopicsRow.In)
				{
					ExtraTopicsRow.Remove();
					rowShown = "";
				}
				if ((bool)button && button.activeSelf)
				{
					button.SetActive(value: false);
				}
				return;
			}
			if (!ExtraTopicsRow.Failed && (ExtraTopicsRow.In || ExtraTopicsRow.Insert(menu)))
			{
				RefreshRow();
				return;
			}
			// The row couldn't be made: the More Topics button and its window instead.
			if (!button && !Inject(menu))
			{
				return;
			}
			bool show = !Hidden && !menu.typ_contractGame;
			if (button.activeSelf != show)
			{
				button.SetActive(show);
			}
			if (!show)
			{
				return;
			}
			bool any = Unlocked(0) || Pending.Any((int t) => t >= 0);
			string text = ((ExtraTopics.Count(Pending) == 0) ? "More Topics" : ("+ " + ExtraTopics.List(Pending)));
			string key = text + "|" + any + "|" + Locked;
			if (key != shown)
			{
				shown = key;
				if ((bool)label)
				{
					label.text = text;
				}
				Button b = button.GetComponent<Button>();
				if ((bool)b)
				{
					b.interactable = any;
				}
				UIKit.SetTooltip(button, Tooltip());
			}
		}
		catch (Exception ex)
		{
			failed = true;
			Plugin.Log.LogError("More Topics button failed: " + ex);
		}
	}

	private static string rowShown = "";

	// The row's buttons show the picked topics like the game's own topic buttons do.
	private static void RefreshRow()
	{
		string key = Pending[0] + "|" + Pending[1] + "|" + Unlocked(0) + "|" + Unlocked(1) + "|" + Locked + "|" + Hidden + "|" + (Menu ? (Menu.g_GameMainGenre + "/" + Menu.g_GameMainTheme + "/" + Menu.g_GameSubTheme) : "");
		if (key == rowShown)
		{
			return;
		}
		rowShown = key;
		GUI_Main gui = G.Gui;
		themes th = (G.MainObj ? G.MainObj.GetComponent<themes>() : null);
		for (int slot = 0; slot < 2; slot++)
		{
			int topic = Pending[slot];
			bool open = Unlocked(slot);
			if ((bool)ExtraTopicsRow.Names[slot])
			{
				ExtraTopicsRow.Names[slot].text = ((topic >= 0) ? ExtraTopics.TopicName(topic) : "---");
			}
			if ((bool)ExtraTopicsRow.Stars[slot] && (bool)gui)
			{
				int level = (((bool)th && topic >= 0 && th.themes_LEVEL != null && topic < th.themes_LEVEL.Length) ? th.themes_LEVEL[topic] : 0);
				try
				{
					gui.DrawStars(ExtraTopicsRow.Stars[slot], level);
				}
				catch
				{
				}
			}
			if ((bool)ExtraTopicsRow.Marks[slot] && (bool)gui && gui.uiSprites != null && gui.uiSprites.Length > 6)
			{
				ExtraTopicsRow.Marks[slot].sprite = gui.uiSprites[(topic >= 0) ? 6 : 3];
			}
			if ((bool)ExtraTopicsRow.Locks[slot])
			{
				ExtraTopicsRow.Locks[slot].SetActive(!open);
			}
			Button b = ExtraTopicsRow.Buttons[slot];
			if ((bool)b)
			{
				b.interactable = open && !Locked && !Hidden;
				UIKit.SetTooltip(b.gameObject, SlotTooltip(slot));
			}
		}
	}

	private static string SlotTooltip(int slot)
	{
		string title = "<b>" + ExtraTopics.SlotNames[slot] + "</b>\n";
		if (Hidden)
		{
			return title + "Contract games keep the topics they were ordered with.";
		}
		if (!Unlocked(slot))
		{
			return title + ExtraTopics.LockReason(slot);
		}
		Menu_DevGame menu = Menu;
		int topic = Pending[slot];
		string text = title + ((topic >= 0) ? UI.Pages.ExtraTopicsPage.Describe(topic, menu ? menu.g_GameMainGenre : (-1), Others(menu)) : "A topic that suits the genre and goes with the others lifts the review; one that doesn't, lowers it. Each extra topic adds about 10% to the development cost and time.");
		int count = ExtraTopics.Count(Pending);
		if (count > 0 && (bool)menu)
		{
			int change = ExtraTopics.ReviewChange(menu.g_GameMainGenre, menu.g_GameMainTheme, menu.g_GameSubTheme, Pending, out var _);
			text = text + "\n\nAll extra topics: review " + ((change > 0) ? ("+" + change + "%") : ((change < 0) ? (change + "%") : "unchanged")) + ", development cost and time +" + Mathf.RoundToInt(ExtraTopics.WorkPerTopic * 100f * (float)count) + "%.";
		}
		if (Locked)
		{
			text += "\n(Taken from the original game.)";
		}
		return text;
	}

	public static string Tooltip()
	{
		if (!Unlocked(0) && ExtraTopics.Count(Pending) == 0)
		{
			return "<b>More Topics</b>\n" + ExtraTopics.LockReason(0);
		}
		string text = "<b>More Topics</b>\nGive the game a third" + (Unlocked(1) ? " and a fourth" : "") + " topic. Topics that suit the genre and go with the others lift the review; each one adds about 10% to the development cost and time.";
		if (ExtraTopics.Count(Pending) > 0)
		{
			text = text + "\n\nNow: " + ExtraTopics.List(Pending);
		}
		if (Locked)
		{
			text += "\n(Taken from the original game.)";
		}
		return text;
	}

	// ---- The button ----

	private static bool Inject(Menu_DevGame menu)
	{
		if (menu.uiObjects == null || menu.uiObjects.Length <= 16 || !menu.uiObjects[16])
		{
			return false;
		}
		RectTransform sub = menu.uiObjects[16].transform as RectTransform;
		RectTransform page = (sub ? (sub.parent as RectTransform) : null);
		if (!sub || !page)
		{
			failed = true;
			Plugin.Log.LogWarning("More Topics: the Sub topic button wasn't found");
			return false;
		}
		Rect s = Local(sub, page);
		const float gap = 6f;
		Rect below = new Rect(s.x, s.y - gap - s.height, s.width, s.height);
		Rect right = new Rect(s.xMax + gap, s.y, Mathf.Min(s.width, 170f), s.height);
		string where;
		GameObject go;
		if (Free(below, page, sub))
		{
			go = Copy(sub, page, below);
			where = "below the Sub topic button";
		}
		else if (Free(right, page, sub))
		{
			go = Copy(sub, page, right);
			where = "right of the Sub topic button";
		}
		else
		{
			float size = Mathf.Max(20f, s.height - 8f);
			go = IconButton(page, new Rect(s.xMax - size - 4f, s.y + (s.height - size) / 2f, size, size));
			where = "inside the Sub topic button";
		}
		go.name = ButtonName;
		Button b = go.GetComponent<Button>();
		b.onClick = new Button.ButtonClickedEvent();
		UIKit.OnClick(b, () =>
		{
			UIKit.Sound();
			if (EditorWindow.ExtraTopics.IsOpen)
			{
				EditorWindow.ExtraTopics.Close();
			}
			else
			{
				EditorWindow.ExtraTopics.Open();
			}
		});
		go.transform.SetAsLastSibling();
		button = go;
		shown = "";
		go.SetActive(value: false);
		Plugin.Log.LogInfo("More Topics button added to the game design screen, " + where + " (Sub topic at " + s + " in " + page.name + ", " + page.rect.size + ")");
		return true;
	}

	// Nothing else on the page is there (backgrounds aside), and it's inside the page.
	private static bool Free(Rect r, RectTransform page, RectTransform sub)
	{
		Rect pr = page.rect;
		if (r.xMin < pr.xMin - 1f || r.xMax > pr.xMax + 1f || r.yMin < pr.yMin - 1f || r.yMax > pr.yMax + 1f)
		{
			return false;
		}
		Rect inner = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f);
		// The page may be hidden right now (the design screen shows one side at a time): look at what's switched on
		// within it, not in the whole scene.
		foreach (Graphic g in page.GetComponentsInChildren<Graphic>(includeInactive: true))
		{
			if (!g.enabled || g.transform == page || g.transform.IsChildOf(sub) || !OnWithin(g.transform, page))
			{
				continue;
			}
			Rect gr = Local(g.rectTransform, page);
			if (gr.width < 2f || gr.height < 2f || (gr.width >= pr.width * 0.8f && gr.height >= pr.height * 0.4f))
			{
				continue;
			}
			if (gr.Overlaps(inner))
			{
				return false;
			}
		}
		return true;
	}

	private static bool OnWithin(Transform t, Transform page)
	{
		for (Transform x = t; (bool)x && x != page; x = x.parent)
		{
			if (!x.gameObject.activeSelf)
			{
				return false;
			}
		}
		return true;
	}

	// A copy of the Sub topic button: its background and one label, with the research icon on the left.
	private static GameObject Copy(RectTransform sub, RectTransform page, Rect at)
	{
		GameObject go = UnityEngine.Object.Instantiate(sub.gameObject, page, worldPositionStays: false);
		Strip(go);
		Text first = go.GetComponentsInChildren<Text>(includeInactive: true).FirstOrDefault();
		for (int i = go.transform.childCount - 1; i >= 0; i--)
		{
			Transform c = go.transform.GetChild(i);
			if (!first || (c != first.transform && !first.transform.IsChildOf(c)))
			{
				UnityEngine.Object.DestroyImmediate(c.gameObject);
			}
		}
		RectTransform rt = (RectTransform)go.transform;
		rt.anchorMin = (rt.anchorMax = new Vector2(0.5f, 0.5f));
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = at.size;
		rt.anchoredPosition = at.center - page.rect.center;
		float icon = at.height - 10f;
		Image img = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		img.transform.SetParent(go.transform, worldPositionStays: false);
		img.sprite = ExtraTopics.Icon(0);
		img.preserveAspect = true;
		img.raycastTarget = false;
		RectTransform irt = (RectTransform)img.transform;
		irt.anchorMin = (irt.anchorMax = new Vector2(0f, 0.5f));
		irt.pivot = new Vector2(0f, 0.5f);
		irt.sizeDelta = new Vector2(icon, icon);
		irt.anchoredPosition = new Vector2(6f, 0f);
		if ((bool)first)
		{
			label = first;
			RectTransform trt = (RectTransform)first.transform;
			trt.SetParent(go.transform, worldPositionStays: false);
			trt.anchorMin = Vector2.zero;
			trt.anchorMax = Vector2.one;
			trt.offsetMin = new Vector2(icon + 12f, 2f);
			trt.offsetMax = new Vector2(-6f, -2f);
			first.alignment = TextAnchor.MiddleLeft;
			first.resizeTextForBestFit = true;
			first.resizeTextMinSize = 9;
			first.resizeTextMaxSize = Mathf.Max(10, first.fontSize);
			first.horizontalOverflow = HorizontalWrapMode.Wrap;
			first.verticalOverflow = VerticalWrapMode.Truncate;
		}
		return go;
	}

	private static GameObject IconButton(RectTransform page, Rect at)
	{
		GameObject go = new GameObject(ButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
		go.transform.SetParent(page, worldPositionStays: false);
		RectTransform rt = (RectTransform)go.transform;
		rt.anchorMin = (rt.anchorMax = new Vector2(0.5f, 0.5f));
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = at.size;
		rt.anchoredPosition = at.center - page.rect.center;
		Image img = go.GetComponent<Image>();
		img.sprite = ExtraTopics.Icon(0);
		img.preserveAspect = true;
		go.GetComponent<Button>().targetGraphic = img;
		label = null;
		return go;
	}

	private static void Strip(GameObject go)
	{
		foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
		{
			if (mb == null || mb is Graphic || mb is Selectable || mb is Mask || mb is RectMask2D)
			{
				continue;
			}
			UnityEngine.Object.DestroyImmediate(mb);
		}
	}

	private static Rect Local(RectTransform rt, RectTransform page)
	{
		Vector3[] c = new Vector3[4];
		rt.GetWorldCorners(c);
		Vector3 a = page.InverseTransformPoint(c[0]);
		Vector3 b = page.InverseTransformPoint(c[2]);
		return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
	}

	// ---- Which game: new, sequel, spin-off, port, remaster, contract ----

	private static void From(int gameId, bool locked)
	{
		Pending = new int[2] { -1, -1 };
		Locked = locked;
		Hidden = false;
		int[] extra = ExtraTopics.Of(GameUpdates.FindGame(gameId));
		if (extra != null)
		{
			for (int i = 0; i < Mathf.Min(2, extra.Length); i++)
			{
				Pending[i] = extra[i];
			}
		}
		shown = "";
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitNewGame")]
	private static void NewGame()
	{
		Pending = new int[2] { -1, -1 };
		Locked = false;
		Hidden = false;
		shown = "";
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitNachfolger")]
	private static void Sequel(int orgID_)
	{
		From(orgID_, locked: false);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitSpinoff")]
	private static void Spinoff(int orgID_)
	{
		From(orgID_, locked: false);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitPort")]
	private static void Port(int orgIP_)
	{
		From(orgIP_, locked: true);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitRemaster")]
	private static void Remaster(int orgID_)
	{
		From(orgID_, locked: true);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitContractGame")]
	private static void Contract()
	{
		Pending = new int[2] { -1, -1 };
		Locked = false;
		Hidden = true;
		shown = "";
	}

	// ---- The game's topic picker, choosing an extra topic ----

	public static void Choose(int slot)
	{
		Menu_DevGame menu = Menu;
		if (!menu || Locked || !Unlocked(slot))
		{
			return;
		}
		if (EditorWindow.ExtraTopics.IsOpen)
		{
			EditorWindow.ExtraTopics.Close();
		}
		opening = slot + 1;
		menu.BUTTON_Thema(1);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_DevGame_Theme), "Init")]
	private static void PickerOpens()
	{
		picking = opening;
		opening = 0;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame_Theme), "SetData")]
	private static void PickerList(Menu_DevGame_Theme __instance)
	{
		if (picking <= 0)
		{
			return;
		}
		try
		{
			int slot = picking - 1;
			Text title = __instance.uiObjects[1].GetComponent<Text>();
			if ((bool)title)
			{
				title.text = ExtraTopics.SlotNames[slot];
			}
			Button remove = __instance.uiObjects[6].GetComponent<Button>();
			if ((bool)remove)
			{
				remove.interactable = Pending[slot] >= 0;
			}
			int other = Pending[1 - slot];
			foreach (Item_DevGame_Theme item in __instance.uiObjects[0].GetComponentsInChildren<Item_DevGame_Theme>(includeInactive: true))
			{
				if (item.myID == other)
				{
					item.GetComponent<Button>().interactable = false;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("More Topics picker: " + ex.Message);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Item_DevGame_Theme), "BUTTON_Click")]
	private static bool Picked(Item_DevGame_Theme __instance)
	{
		if (picking <= 0)
		{
			return true;
		}
		int slot = picking - 1;
		picking = 0;
		UIKit.Sound();
		Pending[slot] = __instance.myID;
		shown = "";
		Menu_DevGame_Theme picker = Picker;
		if ((bool)picker)
		{
			picker.gameObject.SetActive(value: false);
		}
		Reopen();
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_DevGame_Theme), "BUTTON_ThemaEntfernen")]
	private static bool Removed(Menu_DevGame_Theme __instance)
	{
		if (picking <= 0)
		{
			return true;
		}
		int slot = picking - 1;
		picking = 0;
		UIKit.Sound();
		Pending[slot] = -1;
		shown = "";
		__instance.gameObject.SetActive(value: false);
		Reopen();
		return false;
	}

	// Back to the More Topics window, when that's how the topics are picked (the row isn't there).
	private static void Reopen()
	{
		rowShown = "";
		if (!ExtraTopicsRow.In)
		{
			EditorWindow.ExtraTopics.Open();
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame_Theme), "BUTTON_Close")]
	private static void PickerClosed()
	{
		if (picking > 0)
		{
			picking = 0;
			Reopen();
		}
	}

	// ---- Starting development: attach the topics to the new game ----

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_DevGame), "BUTTON_Start")]
	private static void StartBefore(Menu_DevGame __instance, out bool __state)
	{
		__state = ExtraTopics.On && !Hidden && !__instance.typ_contractGame && ExtraTopics.Count(Pending) > 0;
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
				ExtraTopics.Started(created, Pending);
				Pending = new int[2] { -1, -1 };
				shown = "";
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("More Topics start: " + ex);
			}
		}
		created = null;
		if (EditorWindow.ExtraTopics.IsOpen)
		{
			EditorWindow.ExtraTopics.Close();
		}
		return __exception;
	}

	// ---- The design screen's cost shows the extra work ----

	private static readonly Func<Menu_DevGame, int> ContentCosts = AccessTools.MethodDelegate<Func<Menu_DevGame, int>>(AccessTools.Method(typeof(Menu_DevGame), "CalcDevCosts_Kontent"));

	private static readonly Func<Menu_DevGame, int> OtherCosts = AccessTools.MethodDelegate<Func<Menu_DevGame, int>>(AccessTools.Method(typeof(Menu_DevGame), "CalcDevCosts_Sonstiges"));

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "CalcDevCosts")]
	private static void Costs(Menu_DevGame __instance, ref int __result)
	{
		if (!ExtraTopics.On || __instance.typ_contractGame)
		{
			return;
		}
		bool topics = !Hidden && ExtraTopics.Count(Pending) > 0;
		// The new languages (LanguagesRow): the game's price, added like its own languages, before the topics' extra.
		int languages = (int)LanguagesRow.DesignCost(__instance);
		if (!topics && languages == 0)
		{
			return;
		}
		try
		{
			int before = __result + languages;
			__result = (topics ? Mathf.RoundToInt((float)before * ExtraTopics.WorkFactor(Pending)) : before);
			int extra = __result - before;
			mainScript ms = G.MS;
			Text total = __instance.uiObjects[2].GetComponent<Text>();
			if ((bool)total && (bool)ms)
			{
				string old = total.text ?? "";
				int cut = old.IndexOf(" (", StringComparison.Ordinal);
				total.text = ms.GetMoney(__result, showDollar: true) + ((cut >= 0) ? old.Substring(cut) : "");
			}
			if (topics)
			{
				Text content = __instance.uiObjects[154].GetComponent<Text>();
				if ((bool)content && (bool)ms)
				{
					content.text = ms.GetMoney(ContentCosts(__instance) + extra, showDollar: true);
				}
			}
			if (languages > 0 && (bool)ms)
			{
				// The languages box: the game's own languages plus the new ones.
				int points = __instance.GetGesamtDevPoints();
				int own = 0;
				for (int j = 0; j < __instance.g_GameLanguage.Length; j++)
				{
					if (__instance.g_GameLanguage[j] && !ms.Muttersprache(j))
					{
						own += points * 5;
					}
				}
				// (The game's own "other costs" call writes the languages box too, so it goes first.)
				Text other = __instance.uiObjects[155].GetComponent<Text>();
				if ((bool)other)
				{
					other.text = ms.GetMoney(OtherCosts(__instance) + languages, showDollar: true);
				}
				Text box = __instance.uiObjects[184].GetComponent<Text>();
				if ((bool)box)
				{
					box.text = ms.GetMoney(own + languages, showDollar: true);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("More Topics cost: " + ex.Message);
		}
	}

	// The work shown in the bottom bar includes the extra topics (only the number shown: the game's own count also
	// prices translations, and the cost already has the extra).
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "GetGesamtDevPoints")]
	private static void Work(Menu_DevGame __instance, int __result)
	{
		if (!ExtraTopics.On || Hidden || __instance.typ_contractGame || ExtraTopics.Count(Pending) == 0)
		{
			return;
		}
		try
		{
			Text work = __instance.uiObjects[148].GetComponent<Text>();
			mainScript ms = G.MS;
			if ((bool)work && (bool)ms)
			{
				work.text = ms.GetMoney(WorkWithTopics(__result), showDollar: false);
			}
		}
		catch
		{
		}
	}

	public static int WorkWithTopics(int points)
	{
		if (Hidden || ExtraTopics.Count(Pending) == 0)
		{
			return points;
		}
		return Mathf.RoundToInt((float)points * ExtraTopics.WorkFactor(Pending));
	}

	public static List<int> Others(Menu_DevGame menu)
	{
		List<int> list = new List<int>();
		if ((bool)menu)
		{
			list.Add(menu.g_GameMainTheme);
			list.Add(menu.g_GameSubTheme);
		}
		list.AddRange(Pending);
		return list;
	}
}
