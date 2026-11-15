using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HolyAddon.Core;
using HolyAddon.Core.Topics;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// "Third topic" and "Fourth topic" on the game design screen, a row like "Main topic" and "Subtopic" right under
// them, once Layered Settings is researched. The topic row is copied (headers, buttons, icons, stars, lock) and the
// window grows by one row: everything below the topic row on that page moves down, as does the window's bottom bar;
// backgrounds stretch; the other pages keep their content where it is; the window stays centred.
// Worked out from the screen itself (no layout groups: each element has its own anchors), in local units so the
// menu's opening animation doesn't matter. Undone when a save without the research is loaded.
internal static class ExtraTopicsRow
{
	private class Change
	{
		public RectTransform R;

		public Vector2 OffsetMin;

		public Vector2 OffsetMax;
	}

	private static readonly List<Change> changes = new List<Change>();

	private static readonly List<GameObject> clones = new List<GameObject>();

	private static readonly List<KeyValuePair<Transform, Transform>> pairs = new List<KeyValuePair<Transform, Transform>>();

	private static readonly Dictionary<RectTransform, Rect> content = new Dictionary<RectTransform, Rect>();

	// Each element's own rect, before any change (a scrolling list's content runs far past what it shows).
	private static readonly Dictionary<RectTransform, Rect> own = new Dictionary<RectTransform, Rect>();

	private static readonly List<RectTransform> toClone = new List<RectTransform>();

	private static readonly StringBuilder log = new StringBuilder();

	private static RectTransform page;

	// The gameplay features list (its items go in uiObjects[120]), the only list that grows.
	private static ScrollRect features;

	private static float pitch;

	private static float rowBottom;

	private static float rowTop;

	public static bool In { get; private set; }

	// The copies still exist (a load rebuilds the screen).
	public static bool Alive
	{
		get
		{
			if (clones.Count > 0)
			{
				return clones.All((GameObject c) => c);
			}
			return false;
		}
	}

	public static bool Failed { get; private set; }

	// How much taller the window is now.
	public static float Pitch => In ? pitch : 0f;

	// The copied pieces: buttons, topic names, stars, the red cross / tick, the fourth slot's lock.
	public static Button[] Buttons = new Button[2];

	public static Text[] Names = new Text[2];

	public static GameObject[] Stars = new GameObject[2];

	public static Image[] Marks = new Image[2];

	public static GameObject[] Locks = new GameObject[2];

	private const float Tol = 3f;

	public static bool Insert(Menu_DevGame menu)
	{
		if (In || Failed)
		{
			return In;
		}
		try
		{
			GameObject[] ui = menu.uiObjects;
			if (ui == null || ui.Length <= 27 || !ui[15] || !ui[16] || !ui[21] || !ui[23])
			{
				return Fail("the design screen's topic pieces weren't found");
			}
			RectTransform sub = ui[16].transform as RectTransform;
			page = (sub ? (sub.parent as RectTransform) : null);
			Button main = ButtonOf(ui[21].transform, page);
			if (!page || !main)
			{
				return Fail("the Main topic button wasn't found");
			}
			features = ((ui.Length > 120 && (bool)ui[120]) ? ui[120].GetComponentInParent<ScrollRect>(includeInactive: true) : null);
			Rect s = Local(sub);
			Rect m = Local((RectTransform)main.transform);
			Rect genre = Local((RectTransform)ui[15].transform);
			pitch = genre.yMin - s.yMin;
			rowBottom = Mathf.Min(s.yMin, m.yMin);
			rowTop = genre.yMin;
			if (pitch < 20f || pitch > 300f)
			{
				return Fail("odd row spacing " + pitch);
			}
			// The window: the highest of the page's parents that's about as wide as the page.
			RectTransform top = page;
			for (Transform t = page.parent; (bool)t; t = t.parent)
			{
				RectTransform rt = t as RectTransform;
				if (!rt || rt.GetComponent<Canvas>() != null)
				{
					break;
				}
				if (Local(rt).width <= page.rect.width * 1.3f)
				{
					top = rt;
				}
				if (t == menu.transform)
				{
					break;
				}
			}
			if (top == page)
			{
				return Fail("the window around the page wasn't found");
			}
			for (Transform t = page; (bool)t; t = t.parent)
			{
				if (t.GetComponent<LayoutGroup>() != null)
				{
					return Fail(t.name + " has a layout group");
				}
				if (t == top)
				{
					break;
				}
			}
			changes.Clear();
			clones.Clear();
			pairs.Clear();
			content.Clear();
			own.Clear();
			toClone.Clear();
			lists.Clear();
			log.Length = 0;
			Measure(top);
			log.Append("More Topics row: window ").Append(Path(top)).Append(", page ").Append(page.name).Append(", row spacing ").Append(pitch.ToString("0.#")).Append(", topic row ").Append(rowBottom.ToString("0.#")).Append("..").Append(rowTop.ToString("0.#")).AppendLine();
			// The window grows by a row and stays centred.
			Apply(top, 0f, pitch / 2f, (0f - pitch) / 2f, "window");
			Process(top, pitch, ancestor: true);
			foreach (RectTransform original in toClone)
			{
				GameObject copy = UnityEngine.Object.Instantiate(original.gameObject, original.parent, worldPositionStays: false);
				copy.name = original.name + "_Addon";
				copy.transform.SetSiblingIndex(original.GetSiblingIndex() + 1);
				RectTransform crt = (RectTransform)copy.transform;
				crt.offsetMin = original.offsetMin + new Vector2(0f, 0f - pitch);
				crt.offsetMax = original.offsetMax + new Vector2(0f, 0f - pitch);
				Strip(copy);
				clones.Add(copy);
				pairs.Add(new KeyValuePair<Transform, Transform>(original, copy.transform));
			}
			Wire(ui, main, sub);
			In = true;
			Plugin.Log.LogInfo(log.ToString().TrimEnd());
			return true;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("More Topics row failed: " + ex);
			Remove();
			Failed = true;
			return false;
		}
	}

	private static bool Fail(string why)
	{
		Failed = true;
		Plugin.Log.LogWarning("More Topics row: " + why + "; using a button instead");
		return false;
	}

	// Back to the game's own layout.
	public static void Remove()
	{
		for (int i = changes.Count - 1; i >= 0; i--)
		{
			Change c = changes[i];
			if ((bool)c.R)
			{
				c.R.offsetMin = c.OffsetMin;
				c.R.offsetMax = c.OffsetMax;
			}
		}
		foreach (GameObject go in clones)
		{
			if ((bool)go)
			{
				UnityEngine.Object.Destroy(go);
			}
		}
		changes.Clear();
		clones.Clear();
		pairs.Clear();
		Buttons = new Button[2];
		Names = new Text[2];
		Stars = new GameObject[2];
		Marks = new Image[2];
		Locks = new GameObject[2];
		In = false;
	}

	// ---- Working out what moves ----

	// Every element's extent on the page (its own rect if it shows something, and its children's), before any change.
	private static Rect Measure(RectTransform rt)
	{
		own[rt] = Local(rt);
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
		if (!any)
		{
			r = Rect.zero;
		}
		content[rt] = r;
		return r;
	}

	// g: how much the node grew (its bottom moved down, its top stayed).
	private static void Process(RectTransform node, float g, bool ancestor)
	{
		List<RectTransform> kids = new List<RectTransform>();
		for (int i = 0; i < node.childCount; i++)
		{
			if (node.GetChild(i) is RectTransform rt && !rt.name.EndsWith("_Addon") && rt.name != "Addon_ExtraTopics")
			{
				kids.Add(rt);
			}
		}
		foreach (RectTransform c in kids)
		{
			Rect r = (content.TryGetValue(c, out var value) ? value : Rect.zero);
			bool empty = r.width <= 0.5f && r.height <= 0.5f;
			bool below = !empty && r.yMax <= rowBottom + Tol;
			bool spans = !empty && r.yMin < rowBottom - Tol && r.yMax > rowBottom + Tol;
			if (ancestor)
			{
				if (c == page || page.IsChildOf(c))
				{
					Apply(c, g, 0f, 0f - pitch, (c == page) ? "page" : "holds the page");
					Process(c, pitch, c != page);
				}
				else if (PageSized(c))
				{
					// Another page of the screen: its content stays and its backgrounds stretch; on the gameplay
					// features page the list grows into the space too.
					Apply(c, g, 0f, 0f - pitch, "other page");
					if ((bool)features && features.transform.IsChildOf(c))
					{
						OtherPage(c, pitch);
					}
					else
					{
						KeepAll(c, pitch);
					}
				}
				else if (below)
				{
					Apply(c, g, 0f - pitch, 0f - pitch, "moves (bottom bar)");
				}
				else if (spans)
				{
					Apply(c, g, 0f, 0f - pitch, "stretches");
					KeepAll(c, pitch);
				}
				else
				{
					Apply(c, g, 0f, 0f, null);
				}
				continue;
			}
			bool inRow = !empty && r.yMin >= rowBottom - Tol && r.yMax <= rowTop + Tol;
			if (inRow)
			{
				Apply(c, g, 0f, 0f, "copied");
				toClone.Add(c);
			}
			else if (below)
			{
				Apply(c, g, 0f - pitch, 0f - pitch, "moves down");
			}
			else if (spans)
			{
				Apply(c, g, 0f, 0f - pitch, "stretches");
				if (c.childCount > 0)
				{
					Process(c, pitch, ancestor: false);
				}
			}
			else
			{
				Apply(c, g, 0f, 0f, null);
			}
		}
	}

	private static readonly List<Rect> lists = new List<Rect>();

	// Another page: a scrolling list that runs past the topic row grows into the new space (the gameplay features),
	// and what sits under it moves down with it; tall backgrounds stretch; everything else stays.
	private static void OtherPage(RectTransform node, float g)
	{
		List<RectTransform> later = new List<RectTransform>();
		for (int i = 0; i < node.childCount; i++)
		{
			if (!(node.GetChild(i) is RectTransform c))
			{
				continue;
			}
			Rect r = (content.TryGetValue(c, out var value) ? value : Rect.zero);
			bool spans = r.height > 0.5f && r.yMin < rowBottom - Tol && r.yMax > rowBottom + Tol;
			if (c == features.transform)
			{
				Apply(c, g, 0f, 0f - pitch, "gameplay features list stretches");
				lists.Add(own.TryGetValue(c, out var frame) ? frame : r);
			}
			else if (spans && c.childCount == 0 && c.GetComponent<Graphic>() != null && r.height > pitch * 3f)
			{
				Apply(c, g, 0f, 0f - pitch, "background stretches");
			}
			else if (c.childCount > 0 && features.transform.IsChildOf(c))
			{
				Apply(c, g, 0f, 0f, null);
				OtherPage(c, 0f);
			}
			else
			{
				later.Add(c);
			}
		}
		foreach (RectTransform c2 in later)
		{
			Rect r2 = (content.TryGetValue(c2, out var value2) ? value2 : Rect.zero);
			bool under = r2.height > 0.5f && lists.Any((Rect l) => r2.center.y < l.yMin + Tol && r2.xMin < l.xMax && r2.xMax > l.xMin);
			Apply(c2, g, under ? (0f - pitch) : 0f, under ? (0f - pitch) : 0f, under ? "moves down under the list" : null);
			if (!under && r2.height > 0.5f && lists.Count > 0)
			{
				log.Append("  ").Append(Path(c2)).Append(": stays (").Append(r2.center.y.ToString("0")).Append(" vs list bottom ").Append(lists[0].yMin.ToString("0")).Append(")").AppendLine();
			}
		}
	}

	// Everything stays where it is; tall backgrounds that run past the topic row stretch.
	private static void KeepAll(RectTransform node, float g)
	{
		for (int i = 0; i < node.childCount; i++)
		{
			if (node.GetChild(i) is RectTransform c)
			{
				Rect r = (content.TryGetValue(c, out var value) ? value : Rect.zero);
				bool background = c.childCount == 0 && c.GetComponent<Graphic>() != null && r.yMin < rowBottom - Tol && r.yMax > rowTop && r.height > pitch * 3f;
				Apply(c, g, 0f, background ? (0f - pitch) : 0f, background ? "background stretches" : null);
			}
		}
	}

	private static bool PageSized(RectTransform c)
	{
		Rect r = Local(c);
		if (Mathf.Abs(r.width - page.rect.width) < 4f)
		{
			return Mathf.Abs(r.height - page.rect.height) < 4f;
		}
		return false;
	}

	// Keep the element where it was though its parent grew by g, then move its top and bottom edges.
	private static void Apply(RectTransform r, float g, float moveTop, float moveBottom, string what)
	{
		if (g == 0f && moveTop == 0f && moveBottom == 0f)
		{
			return;
		}
		changes.Add(new Change
		{
			R = r,
			OffsetMin = r.offsetMin,
			OffsetMax = r.offsetMax
		});
		Vector2 min = r.offsetMin;
		Vector2 max = r.offsetMax;
		min.y += g * (1f - r.anchorMin.y) + moveBottom;
		max.y += g * (1f - r.anchorMax.y) + moveTop;
		r.offsetMin = min;
		r.offsetMax = max;
		if (what != null)
		{
			log.Append("  ").Append(Path(r)).Append(": ").Append(what).AppendLine();
		}
	}

	// ---- The copies ----

	private static void Strip(GameObject go)
	{
		foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
		{
			if (mb == null)
			{
				continue;
			}
			string ns = mb.GetType().Namespace ?? "";
			if (!ns.StartsWith("UnityEngine") || mb is UnityEngine.EventSystems.EventTrigger)
			{
				UnityEngine.Object.DestroyImmediate(mb);
			}
		}
		foreach (Button b in go.GetComponentsInChildren<Button>(includeInactive: true))
		{
			b.onClick = new Button.ButtonClickedEvent();
		}
	}

	// The copy of one of the game's pieces, by its place under the copied element.
	private static Transform Copy(Transform original)
	{
		if (!original)
		{
			return null;
		}
		foreach (KeyValuePair<Transform, Transform> p in pairs)
		{
			if (original == p.Key)
			{
				return p.Value;
			}
			if (original.IsChildOf(p.Key))
			{
				List<int> path = new List<int>();
				for (Transform t = original; t != p.Key; t = t.parent)
				{
					path.Insert(0, t.GetSiblingIndex());
				}
				Transform x = p.Value;
				foreach (int i in path)
				{
					if (!x || i >= x.childCount)
					{
						return null;
					}
					x = x.GetChild(i);
				}
				return x;
			}
		}
		return null;
	}

	private static void Wire(GameObject[] ui, Button main, RectTransform sub)
	{
		Buttons[0] = Copy(main.transform)?.GetComponent<Button>();
		Buttons[1] = Copy(sub)?.GetComponent<Button>();
		Names[0] = Copy(ui[21].transform)?.GetComponent<Text>();
		Names[1] = Copy(ui[23].transform)?.GetComponent<Text>();
		Stars[0] = Copy(ui[22] ? ui[22].transform : null)?.gameObject;
		Stars[1] = Copy(ui[24] ? ui[24].transform : null)?.gameObject;
		Marks[0] = Copy(ui[25] ? ui[25].transform : null)?.GetComponent<Image>();
		Marks[1] = Copy(ui[26] ? ui[26].transform : null)?.GetComponent<Image>();
		Locks[1] = Copy(ui[19] ? ui[19].transform : null)?.gameObject;
		// The Main topic has no lock to copy: the third slot gets a copy of the fourth's, in the same place on its
		// button.
		if ((bool)Locks[1] && (bool)Buttons[0] && (bool)Buttons[1])
		{
			GameObject lock0 = UnityEngine.Object.Instantiate(Locks[1], Locks[1].transform.parent, worldPositionStays: false);
			lock0.name = "Lock_Addon";
			lock0.transform.position = Locks[1].transform.position + (Buttons[0].transform.position - Buttons[1].transform.position);
			lock0.transform.SetAsLastSibling();
			clones.Add(lock0);
			Locks[0] = lock0;
		}
		// The Main topic's tick for a genre fit, and the "new topic combination" badge, don't apply.
		foreach (int i in new int[2] { 27, 221 })
		{
			Transform x = ((ui.Length > i && (bool)ui[i]) ? Copy(ui[i].transform) : null);
			if ((bool)x)
			{
				x.gameObject.SetActive(value: false);
			}
		}
		// The headers: the copied labels that aren't the topic names, above each button.
		float mid = (Local((RectTransform)main.transform).center.x + Local(sub).center.x) / 2f;
		string[] headers = new string[2] { "Third topic", "Fourth topic" };
		foreach (GameObject go in clones)
		{
			foreach (Text t in go.GetComponentsInChildren<Text>(includeInactive: true))
			{
				if (t == Names[0] || t == Names[1])
				{
					continue;
				}
				Rect tr = Local(t.rectTransform);
				int slot = ((tr.center.x < mid) ? 0 : 1);
				Button b = Buttons[slot];
				if ((bool)b && tr.center.y > Local((RectTransform)b.transform).yMax - Tol)
				{
					t.text = headers[slot];
				}
			}
		}
		for (int i = 0; i < 2; i++)
		{
			int slot = i;
			if ((bool)Buttons[i])
			{
				UIKit.OnClick(Buttons[i], () =>
				{
					UIKit.Sound();
					ExtraTopicsMenu.Choose(slot);
				});
			}
			else
			{
				log.Append("  slot ").Append(i).Append(": no button found in the copy").AppendLine();
			}
		}
	}

	// ---- Helpers ----

	private static Button ButtonOf(Transform t, Transform stop)
	{
		for (Transform x = t; (bool)x && x != stop; x = x.parent)
		{
			Button b = x.GetComponent<Button>();
			if ((bool)b)
			{
				return b;
			}
		}
		return null;
	}

	// A rect in the page's own space.
	private static Rect Local(RectTransform rt)
	{
		Vector3[] c = new Vector3[4];
		rt.GetWorldCorners(c);
		Vector3 a = page.InverseTransformPoint(c[0]);
		Vector3 b = page.InverseTransformPoint(c[2]);
		return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
	}

	private static string Path(Transform t)
	{
		string p = t.name;
		Transform x = t.parent;
		for (int i = 0; i < 3 && (bool)x; i++)
		{
			p = x.name + "/" + p;
			x = x.parent;
		}
		return p;
	}
}
