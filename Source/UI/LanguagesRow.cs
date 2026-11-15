using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Languages;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// The new languages on the game design screen: a second row of flags under the game's eleven (uiObjects[107..117]),
// copies of its own flag buttons with round flags of our own, white when picked and greyed out when not or before
// their year. The languages box grows by the row and what's under it (the cost, "Enable/disable all languages", the
// anti-cheat and copy protection boxes) moves down into the space the taller screen (ExtraTopicsRow) left on that
// page. Laid out while that page shows. Their cost goes into the screen's cost (ExtraTopicsMenu.Costs) and the
// languages box; they're attached to the game when development starts.
[HarmonyPatch]
internal static class LanguagesRow
{
	private class Change
	{
		public RectTransform R;

		public Vector2 Min;

		public Vector2 Max;
	}

	private const int FirstFlag = 107;

	private const float Tol = 3f;

	public static bool[] Pending = ExtraLanguages.New();

	private static bool In;

	private static bool Failed;

	private static RectTransform page;

	private static float rowH;

	private static Rect row;

	private static readonly List<Change> changes = new List<Change>();

	private static readonly List<GameObject> clones = new List<GameObject>();

	private static readonly Dictionary<RectTransform, Rect> content = new Dictionary<RectTransform, Rect>();

	private static Button[] buttons = new Button[0];

	private static Image[] images = new Image[0];

	private static readonly StringBuilder log = new StringBuilder();

	private static string shown = "";

	private static float next;

	private static bool starting;

	private static gameScript created;

	private static readonly Func<Menu_DevGame, int> DevCosts = AccessTools.MethodDelegate<Func<Menu_DevGame, int>>(AccessTools.Method(typeof(Menu_DevGame), "CalcDevCosts"));

	private static bool Contract
	{
		get
		{
			Menu_DevGame menu = ExtraTopicsMenu.Menu;
			if ((bool)menu)
			{
				return menu.typ_contractGame;
			}
			return false;
		}
	}

	public static void Tick()
	{
		Menu_DevGame menu = ExtraTopicsMenu.Menu;
		if (!menu || !menu.gameObject.activeInHierarchy || !ExtraLanguages.On)
		{
			return;
		}
		try
		{
			if (In && clones.Any((GameObject c) => !c))
			{
				Remove();
			}
			if (!ExtraTopicsRow.In)
			{
				if (In)
				{
					Remove();
				}
				return;
			}
			if (!In && !Failed && menu.uiObjects.Length > FirstFlag + 10 && (bool)menu.uiObjects[FirstFlag])
			{
				Transform window = menu.uiObjects[16].transform.parent.parent;
				Transform t = menu.uiObjects[FirstFlag].transform;
				while ((bool)t && t.parent != window)
				{
					t = t.parent;
				}
				if ((bool)t && t.gameObject.activeInHierarchy)
				{
					Insert(menu, (RectTransform)t);
				}
			}
			if (In && Time.unscaledTime >= next)
			{
				next = Time.unscaledTime + 0.4f;
				Refresh(menu, force: false);
			}
		}
		catch (Exception ex)
		{
			Failed = true;
			Plugin.Log.LogError("New languages row failed: " + ex);
		}
	}

	// ---- The row ----

	private static void Insert(Menu_DevGame menu, RectTransform p)
	{
		page = p;
		List<RectTransform> flags = new List<RectTransform>();
		for (int i = 0; i < 11; i++)
		{
			GameObject f = menu.uiObjects[FirstFlag + i];
			if (!f)
			{
				Fail("flag " + i + " is missing");
				return;
			}
			flags.Add((RectTransform)f.transform);
		}
		row = flags.Select(Local).Aggregate((Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax)));
		if (row.height < 10f || row.width < 100f)
		{
			Fail("the flags' row is " + row.size);
			return;
		}
		// A row of flags and a gap like the space between the game's rows.
		rowH = row.height + 16f;
		// Room: what's lowest on the page must stay clear of the bottom bar.
		Transform bar = page.parent.Find("Line") ?? page.parent.Find("PanelSeiten");
		float floor = ((bar is RectTransform brt) ? Local(brt).yMax : page.rect.yMin) + 6f;
		float lowest = float.MaxValue;
		foreach (Graphic g in page.GetComponentsInChildren<Graphic>(includeInactive: false))
		{
			Rect r = Local(g.rectTransform);
			if (g.enabled && r.width >= 2f && r.height >= 2f && r.height < page.rect.height * 0.5f && !g.transform.name.StartsWith("Addon_"))
			{
				lowest = Mathf.Min(lowest, r.yMin);
			}
		}
		if (lowest - rowH < floor)
		{
			Fail("no room on " + page.name + " (" + (lowest - floor).ToString("0") + " free, " + rowH.ToString("0") + " needed)");
			return;
		}
		changes.Clear();
		clones.Clear();
		content.Clear();
		log.Length = 0;
		log.Append("New languages row on ").Append(page.name).Append(": flags at ").Append(row).Append(", row ").Append(rowH.ToString("0.#")).AppendLine();
		Measure(page);
		Process(page, 0f);
		buttons = new Button[11];
		images = new Image[11];
		for (int i = 0; i < 11; i++)
		{
			RectTransform src = flags[i];
			GameObject copy = UnityEngine.Object.Instantiate(src.gameObject, src.parent, worldPositionStays: false);
			copy.name = "Addon_Language_" + ExtraLanguages.Defs[i].Code;
			RectTransform crt = (RectTransform)copy.transform;
			crt.offsetMin = src.offsetMin + new Vector2(0f, 0f - rowH);
			crt.offsetMax = src.offsetMax + new Vector2(0f, 0f - rowH);
			foreach (MonoBehaviour mb in copy.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
			{
				if (mb != null && (!(mb.GetType().Namespace ?? "").StartsWith("UnityEngine") || mb is UnityEngine.EventSystems.EventTrigger))
				{
					UnityEngine.Object.DestroyImmediate(mb);
				}
			}
			Button b = copy.GetComponent<Button>();
			Image img = copy.GetComponent<Image>();
			if (!b || !img)
			{
				Fail("the flag buttons aren't buttons with a picture");
				Remove();
				return;
			}
			img.sprite = ExtraLanguages.Icon(i);
			img.preserveAspect = true;
			int id = i;
			UIKit.OnClick(b, () => Toggle(id));
			buttons[i] = b;
			images[i] = img;
			clones.Add(copy);
		}
		In = true;
		shown = "";
		Plugin.Log.LogInfo(log.ToString().TrimEnd());
		Refresh(menu, force: true);
	}

	private static void Fail(string why)
	{
		Failed = true;
		Plugin.Log.LogWarning("New languages row: " + why);
	}

	private static void Remove()
	{
		for (int i = changes.Count - 1; i >= 0; i--)
		{
			if ((bool)changes[i].R)
			{
				changes[i].R.offsetMin = changes[i].Min;
				changes[i].R.offsetMax = changes[i].Max;
			}
		}
		foreach (GameObject c in clones)
		{
			if ((bool)c)
			{
				UnityEngine.Object.Destroy(c);
			}
		}
		changes.Clear();
		clones.Clear();
		buttons = new Button[0];
		images = new Image[0];
		In = false;
	}

	private static Rect Measure(RectTransform rt)
	{
		Rect r = Rect.zero;
		bool any = false;
		if (rt.GetComponent<Graphic>() != null || rt.childCount == 0)
		{
			r = Local(rt);
			any = r.width > 0.5f && r.height > 0.5f;
		}
		for (int i = 0; i < rt.childCount; i++)
		{
			if (rt.GetChild(i) is RectTransform child)
			{
				Rect cr = Measure(child);
				if (cr.width > 0.5f || cr.height > 0.5f)
				{
					r = (any ? Rect.MinMaxRect(Mathf.Min(r.xMin, cr.xMin), Mathf.Min(r.yMin, cr.yMin), Mathf.Max(r.xMax, cr.xMax), Mathf.Max(r.yMax, cr.yMax)) : cr);
					any = true;
				}
			}
		}
		content[rt] = (any ? r : Rect.zero);
		return content[rt];
	}

	// Below the flags: moves down; around them (the languages box): grows; the rest stays.
	private static void Process(RectTransform node, float g)
	{
		for (int i = 0; i < node.childCount; i++)
		{
			if (!(node.GetChild(i) is RectTransform c))
			{
				continue;
			}
			Rect r = (content.TryGetValue(c, out var value) ? value : Rect.zero);
			bool empty = r.width <= 0.5f && r.height <= 0.5f;
			bool below = !empty && r.yMax <= row.yMin + Tol;
			bool spans = !empty && r.yMin < row.yMin - Tol && r.yMax > row.yMin + Tol;
			if (below)
			{
				Apply(c, g, 0f - rowH, 0f - rowH, "moves down");
			}
			else if (spans && !IsFlag(c))
			{
				Apply(c, g, 0f, 0f - rowH, "grows");
				if (c.childCount > 0)
				{
					Process(c, rowH);
				}
			}
			else
			{
				Apply(c, g, 0f, 0f, null);
			}
		}
	}

	private static bool IsFlag(RectTransform c)
	{
		Menu_DevGame menu = ExtraTopicsMenu.Menu;
		for (int i = 0; i < 11; i++)
		{
			if ((bool)menu && menu.uiObjects[FirstFlag + i] == c.gameObject)
			{
				return true;
			}
		}
		return false;
	}

	private static void Apply(RectTransform r, float g, float moveTop, float moveBottom, string what)
	{
		if (g == 0f && moveTop == 0f && moveBottom == 0f)
		{
			return;
		}
		changes.Add(new Change
		{
			R = r,
			Min = r.offsetMin,
			Max = r.offsetMax
		});
		Vector2 min = r.offsetMin;
		Vector2 max = r.offsetMax;
		min.y += g * (1f - r.anchorMin.y) + moveBottom;
		max.y += g * (1f - r.anchorMax.y) + moveTop;
		r.offsetMin = min;
		r.offsetMax = max;
		if (what != null)
		{
			log.Append("  ").Append(r.parent ? (r.parent.name + "/") : "").Append(r.name).Append(": ").Append(what).AppendLine();
		}
	}

	private static Rect Local(RectTransform rt)
	{
		Vector3[] c = new Vector3[4];
		rt.GetWorldCorners(c);
		Vector3 a = page.InverseTransformPoint(c[0]);
		Vector3 b = page.InverseTransformPoint(c[2]);
		return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
	}

	// ---- Picking ----

	private static void Toggle(int i)
	{
		Menu_DevGame menu = ExtraTopicsMenu.Menu;
		if (!menu || Contract || !ExtraLanguages.Unlocked(i))
		{
			return;
		}
		UIKit.Sound();
		Pending[i] = !Pending[i];
		Refresh(menu, force: true);
		DevCosts(menu);
	}

	private static void Refresh(Menu_DevGame menu, bool force)
	{
		int year = (G.MS ? G.MS.year : 0);
		int points = menu.GetGesamtDevPoints();
		string key = string.Join("", Pending.Select((bool b) => b ? "1" : "0")) + "|" + year + "|" + points + "|" + Contract + "|" + (G.MS ? G.MS.GetCountryID() : (-1));
		if (!force && key == shown)
		{
			return;
		}
		shown = key;
		for (int i = 0; i < buttons.Length; i++)
		{
			if (!buttons[i])
			{
				continue;
			}
			LanguageDef d = ExtraLanguages.Defs[i];
			bool open = ExtraLanguages.Unlocked(i) && !Contract;
			bool on = open && Pending[i];
			images[i].color = (on ? Color.white : menu.languageColor);
			buttons[i].interactable = open;
			string tip;
			if (Contract)
			{
				tip = "<b>" + d.Name + "</b>\nContract games are translated as the client ordered.";
			}
			else if (!ExtraLanguages.Unlocked(i))
			{
				tip = "<b>" + d.Name + "</b>\nOpens in " + d.Year + ".";
			}
			else
			{
				tip = "<b>" + d.Name + "</b>\nSales +" + Mathf.RoundToInt(d.Share * 100f) + "% while the game is on the market. Translation: " + (ExtraLanguages.Native(i) ? "free (your studio speaks it)" : G.Money((long)points * 5L)) + ".";
			}
			UIKit.SetTooltip(buttons[i].gameObject, tip);
		}
	}

	// The cost of the picked new languages on the design screen (added to the screen's cost).
	public static long DesignCost(Menu_DevGame menu)
	{
		if (!ExtraLanguages.On || !menu || menu.typ_contractGame || ExtraLanguages.Picked(Pending) == 0)
		{
			return 0L;
		}
		bool[] open = Pending.Select((bool b, int i) => b && ExtraLanguages.Unlocked(i)).ToArray();
		return ExtraLanguages.Cost(open, menu.GetGesamtDevPoints());
	}

	// "Enable/disable all languages" takes the new ones along.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "BUTTON_AlleSprachen")]
	private static void AllLanguages(Menu_DevGame __instance)
	{
		if (!ExtraLanguages.On || __instance.typ_contractGame)
		{
			return;
		}
		bool state = __instance.g_GameLanguage != null && __instance.g_GameLanguage.Length > 0 && __instance.g_GameLanguage[0];
		for (int i = 0; i < Pending.Length; i++)
		{
			Pending[i] = state && ExtraLanguages.Unlocked(i);
		}
		if (In)
		{
			Refresh(__instance, force: true);
		}
		DevCosts(__instance);
	}

	// ---- Which game: new, sequel, spin-off, port, remaster, contract ----

	private static void From(int gameId)
	{
		bool[] langs = ExtraLanguages.Of(GameUpdates.FindGame(gameId));
		Pending = ((langs != null) ? langs.Take(ExtraLanguages.Count).Concat(Enumerable.Repeat(element: false, Mathf.Max(0, ExtraLanguages.Count - langs.Length))).ToArray() : ExtraLanguages.New());
		shown = "";
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitNewGame")]
	private static void NewGame()
	{
		Pending = ExtraLanguages.New();
		shown = "";
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitNachfolger")]
	private static void Sequel(int orgID_)
	{
		From(orgID_);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitSpinoff")]
	private static void Spinoff(int orgID_)
	{
		From(orgID_);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitPort")]
	private static void Port(int orgIP_)
	{
		From(orgIP_);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitRemaster")]
	private static void Remaster(int orgID_)
	{
		From(orgID_);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "InitContractGame")]
	private static void ContractGame()
	{
		Pending = ExtraLanguages.New();
		shown = "";
	}

	// ---- Starting development: attach them to the new game ----

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_DevGame), "BUTTON_Start")]
	private static void StartBefore(Menu_DevGame __instance, out bool __state)
	{
		__state = ExtraLanguages.On && !__instance.typ_contractGame && ExtraLanguages.Picked(Pending) > 0;
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
				bool[] open = Pending.Select((bool b, int i) => b && ExtraLanguages.Unlocked(i)).ToArray();
				ExtraLanguages.Attach(created, open);
				Pending = ExtraLanguages.New();
				shown = "";
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("New languages start: " + ex);
			}
		}
		created = null;
		return __exception;
	}
}
