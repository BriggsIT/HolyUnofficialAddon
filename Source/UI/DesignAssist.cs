using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Design;
using HolyAddon.Core.Industry;
using HolyAddon.Core.Languages;
using HolyAddon.Core.Topics;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// The design assistant's Assist button on the game design screen's first page (Docs/Design-Assistant-Plan.md), with
// its options button the other side of the licence (UI/Pages/DesignAssistPage), and a Platforms button on the platform
// page; the game's own Auto Design Settings (design focus page) also sets the work priorities. They take the places
// of GameDev Assistant's buttons, which are hidden. One click fills in each part you've chosen, through the game's own
// setters so the screen, costs and work update as if you'd picked them:
// - main genre (by popularity and the trend, unless you keep yours), a sub-genre that goes with it (the first click
//   picks the best; each click after that picks again among the near-best, as do the topics);
// - main and sub topic, third and fourth topic (only ones that suit the genre and go with the others);
// - age group; design sliders (as the game's Auto Design Settings); work priorities (the genre's own ratio);
// - platforms (your dev kits that run the engine's tech level, by market share), engine features (the game's own
//   button), a licence (your best for the genre), languages (those worth their translation), a name.
// It only uses what your studio knows (DesignKnowledge) unless "Reveal every fit" is on; a part it can't fill yet is
// left as it is and said so.
[HarmonyPatch]
internal static class DesignAssist
{
	public static readonly string[] Parts = new string[12] { "Genre", "SubGenre", "Topics", "ExtraTopics", "AgeGroup", "Design", "Priorities", "Platforms", "Engine", "Licence", "Languages", "Name" };

	public static readonly string[] PartNames = new string[12] { "Main Genre", "Sub-Genre", "Main and Sub Topic", "Third and Fourth Topic", "Age Group", "Design Sliders", "Work Priorities", "Platforms", "Engine Features", "Licence", "Languages", "Name" };

	public static readonly string[] PartTips = new string[12]
	{
		"Picks the most promising genre you've researched and know a topic for (its popularity, the trend, your studio's experience), unless Keep My Main Genre is on and you've picked one yourself. Each click after the first picks again among the near-best, as do the sub-genre and topics.",
		"A sub-genre that goes with the main genre. Known once you've had the review report of a game of yours in that genre.",
		"The main and sub topic: ones known to suit the genre, favouring the trend and your experience.",
		"The third and fourth topic, once researched: only topics that suit the genre and go with the game's other topics (each adds about 10% to the cost and time). One that doesn't fit is taken off.",
		"An age group known to suit the genre.",
		"The game's Auto Design Settings: every design slider you know the right value for.",
		"Gameplay, graphics, sound and tech in the genre's own ratio (shown in the review report of a game in that genre).",
		"Your dev kits that run your engine's tech level, by market share, up to four (one for an exclusive). The timed exclusive's platform stays. The platform page has its own button for this.",
		"The game's Auto Engine Features: the best features your engine has that the platforms can run.",
		"Your best licence for the genre (never one that's bad for it). Off by default: a licence is used up.",
		"Each language whose sales are worth its translation, judged by your recent games' takings; your studio's own languages (free) always. Includes the new languages as they open.",
		"The licence's name, or a random one when there's no licence. Off by default."
	};

	// A const, so the settings can use it in Awake without starting this class (its delegate needs the game's code).
	public const string DefaultParts = "Genre,SubGenre,Topics,ExtraTopics,AgeGroup,Design,Priorities,Platforms,Engine,Languages";

	// The game's sales cut for each of its own languages a game lacks (gameScript.SellGame).
	private static readonly float[] LanguageShare = new float[11] { 0.05f, 0.03f, 0.03f, 0.02f, 0.02f, 0.02f, 0.01f, 0.02f, 0.02f, 0.03f, 0.04f };

	private static readonly Func<Menu_DevGame, int> DevCosts = AccessTools.MethodDelegate<Func<Menu_DevGame, int>>(AccessTools.Method(typeof(Menu_DevGame), "CalcDevCosts"));

	private const string ButtonName = "Addon_DesignAssist";

	private static GameObject button;

	private static GameObject options;

	private static bool failed;

	private static float next;

	private static float opened = -1f;

	private static string shown = "";

	public static string Last = "";

	public static bool PartOn(int i)
	{
		string[] on = (Plugin.DesignParts.Value ?? "").Split(',');
		return on.Any((string p) => p.Trim().Equals(Parts[i], StringComparison.OrdinalIgnoreCase));
	}

	public static void SetPart(int i, bool on)
	{
		List<string> list = Parts.Where((string p, int j) => (j == i) ? on : PartOn(j)).ToList();
		Plugin.DesignParts.Value = string.Join(",", list);
	}

	public static void ResetParts()
	{
		Plugin.DesignParts.Value = DefaultParts;
	}

	// ---- The buttons ----

	// What the last run couldn't fill in, for the Assist button's tooltip (the options window shows it all).
	public static string LastLeft = "";

	private static GameObject platforms;

	private static bool failedPlatforms;

	public static void Tick()
	{
		Menu_DevGame menu = ExtraTopicsMenu.Menu;
		if (!menu)
		{
			return;
		}
		if (!menu.gameObject.activeInHierarchy)
		{
			if (EditorWindow.DesignAssist.IsOpen)
			{
				EditorWindow.DesignAssist.Close();
			}
			opened = -1f;
			return;
		}
		if (opened < 0f)
		{
			// The screen opened for a new game: last time was another game's.
			opened = Time.unscaledTime;
			Last = "";
			LastLeft = "";
			shown = "";
			autoTried = false;
			clicks = 0;
			assistGenre = -1;
		}
		if (!DesignKnowledge.On)
		{
			foreach (GameObject go in new GameObject[3] { button, options, platforms })
			{
				if ((bool)go && go.activeSelf)
				{
					go.SetActive(value: false);
				}
			}
			GdaButtons(menu, show: true);
			AutoIcon(menu, ours: false);
			return;
		}
		// Wait (a second at most) for the taller screen (the topic row), so the free space is measured on the final
		// layout.
		if (ExtraTopics.On && !ExtraTopicsRow.In && !ExtraTopicsRow.Failed && Time.unscaledTime - opened < 1f)
		{
			return;
		}
		try
		{
			if (!failed && (!button || !options))
			{
				shown = "";
				failed = !Inject(menu);
			}
			if (!failedPlatforms && !platforms)
			{
				failedPlatforms = !InjectPlatforms(menu);
			}
			foreach (GameObject go in new GameObject[3] { button, options, platforms })
			{
				if ((bool)go && !go.activeSelf)
				{
					go.SetActive(value: true);
				}
			}
			// GameDev Assistant's own buttons stay hidden while ours are there (it may switch them back on); one of
			// ours that couldn't be placed leaves its counterpart showing.
			GdaButtons(menu, show: false);
			AutoIcon(menu, ours: true);
			if (Time.unscaledTime < next)
			{
				return;
			}
			next = Time.unscaledTime + 0.5f;
			string key = menu.typ_contractGame + "|" + LastLeft;
			if (key != shown && (bool)button)
			{
				shown = key;
				UIKit.SetTooltip(button, Tooltip(menu));
				UIKit.SetTooltip(options, "<b>Design Assistant</b>\nChoose what Assist fills in.");
				if ((bool)platforms)
				{
					UIKit.SetTooltip(platforms, "<b>Assist Platforms</b>\nYour dev kits that run your engine's tech level, by market share.");
				}
			}
		}
		catch (Exception ex)
		{
			failed = (failedPlatforms = true);
			GdaButtons(menu, show: true);
			Plugin.Log.LogError("Design assistant button failed: " + ex);
		}
	}

	private static string Tooltip(Menu_DevGame menu)
	{
		string text = "<b>Assist</b>\n" + (menu.typ_contractGame ? "Sets the design sliders, work priorities and engine features." : "Fills in the game; click again for another good mix. The cog chooses what.");
		if (!string.IsNullOrEmpty(LastLeft))
		{
			text = text + "\n" + DesignColours.Paint("Left as it was: ", -1) + LastLeft;
		}
		return text;
	}

	// ---- GameDev Assistant's buttons, hidden while ours are there ----

	// Its Assist and Licence buttons on the first page, and its Platforms button on the platform page (the round
	// button on the design focus page is the game's own Auto Design Settings, which stays, with our icon).
	private static readonly string[] GdaPaths = new string[3] { "WindowMain/Seite1/Button_GameDevAssistant", "WindowMain/Seite1/Button_GameDevAssistant_Licence", "WindowMain/Seite2/Button_GameDevAssistant_Platforms" };

	private static readonly GameObject[] gda = new GameObject[3];

	private static readonly bool[] gdaHidden = new bool[3];

	private static void GdaButtons(Menu_DevGame menu, bool show)
	{
		if (show && !gdaHidden.Any((bool h) => h))
		{
			return;
		}
		for (int i = 0; i < GdaPaths.Length; i++)
		{
			if (!gda[i])
			{
				Transform t = menu.transform.Find(GdaPaths[i]);
				gda[i] = (t ? t.gameObject : null);
				gdaHidden[i] = false;
			}
			if (!gda[i])
			{
				continue;
			}
			// Each is hidden only where ours took its place.
			bool ours = ((i < 2) ? ((bool)button && (bool)options) : ((bool)platforms));
			if (show || !ours)
			{
				if (gdaHidden[i])
				{
					gda[i].SetActive(value: true);
					gdaHidden[i] = false;
				}
			}
			else if (gda[i].activeSelf)
			{
				gda[i].SetActive(value: false);
				gdaHidden[i] = true;
			}
		}
	}

	private static RectTransform Page(Menu_DevGame menu)
	{
		GameObject[] ui = menu.uiObjects;
		if (ui == null || ui.Length <= 16 || !ui[16])
		{
			return null;
		}
		return ui[16].transform.parent as RectTransform;
	}

	// The design screen page (a child of its window) that holds this piece.
	private static RectTransform PageOf(Menu_DevGame menu, GameObject piece)
	{
		RectTransform first = Page(menu);
		if (!first || !piece)
		{
			return null;
		}
		Transform window = first.parent;
		Transform t = piece.transform;
		while ((bool)t && t.parent != window)
		{
			t = t.parent;
		}
		return t as RectTransform;
	}

	// ---- Our buttons on the first page: round, either side of the licence (where GameDev Assistant puts its own) ----

	private static bool Inject(Menu_DevGame menu)
	{
		GameObject[] ui = menu.uiObjects;
		RectTransform page = Page(menu);
		if (!page || ui.Length <= 28 || !ui[28] || !UIKit.Init())
		{
			Plugin.Log.LogWarning("Design assistant: the design screen's first page wasn't found");
			return false;
		}
		foreach (string n in new string[2] { ButtonName, ButtonName + "_Options" })
		{
			Transform old = page.Find(n);
			if ((bool)old)
			{
				UnityEngine.Object.DestroyImmediate(old.gameObject);
			}
		}
		Button licence = ButtonOf(ui[28].transform, page);
		if (!licence)
		{
			Plugin.Log.LogWarning("Design assistant: the licence button wasn't found");
			return false;
		}
		Rect lic = Local((RectTransform)licence.transform, page);
		// GameDev Assistant's spots if it's there (Assist on the right, the options on the left), measured before its
		// buttons are hidden; else the same places worked out from the licence button.
		Transform gdaAssist = menu.transform.Find(GdaPaths[0]);
		Transform gdaLicence = menu.transform.Find(GdaPaths[1]);
		float size = ((bool)gdaAssist ? Local((RectTransform)gdaAssist, page).height : Mathf.Clamp(lic.height - 4f, 40f, 56f));
		Rect right = ((bool)gdaAssist ? Local((RectTransform)gdaAssist, page) : new Rect(lic.xMax + 8f, lic.center.y - size / 2f, size, size));
		Rect left = ((bool)gdaLicence ? Local((RectTransform)gdaLicence, page) : new Rect(lic.xMin - 8f - size, lic.center.y - size / 2f, size, size));
		HideGda(menu, 0);
		HideGda(menu, 1);
		string where = "either side of the licence";
		if (!Free(right, page) || !Free(left, page))
		{
			// Else the first free strip for both, from the licence row down.
			if (!Strip(page, lic.yMax, size * 2f + 8f, size, out var strip))
			{
				Plugin.Log.LogWarning("Design assistant: no free space on " + page.name + " for its buttons");
				return false;
			}
			left = new Rect(strip.x, strip.y, size, size);
			right = new Rect(strip.x + size + 8f, strip.y, size, size);
			where = "below the licence";
		}
		button = RoundButton(page, ButtonName, "design-assist.png", "Assist", right);
		UIKit.OnClick(button.GetComponent<Button>(), () =>
		{
			UIKit.Sound();
			Run(ExtraTopicsMenu.Menu);
		});
		options = RoundButton(page, ButtonName + "_Options", "design-options.png", "...", left);
		UIKit.OnClick(options.GetComponent<Button>(), () =>
		{
			UIKit.Sound();
			if (EditorWindow.DesignAssist.IsOpen)
			{
				EditorWindow.DesignAssist.Close();
			}
			else
			{
				EditorWindow.DesignAssist.Open();
			}
		});
		Plugin.Log.LogInfo("Design assistant: buttons added to " + page.name + ", " + where + " (" + right + ", " + left + ")" + ((bool)gdaAssist ? "; GameDev Assistant's are hidden" : ""));
		return true;
	}

	// ---- The platform page's button: Platforms only, at the right end of the market share row ----

	private static bool InjectPlatforms(Menu_DevGame menu)
	{
		GameObject[] ui = menu.uiObjects;
		GameObject share = ((ui.Length > 212) ? ui[212] : null);
		RectTransform page = PageOf(menu, share);
		if (!page)
		{
			Plugin.Log.LogWarning("Design assistant: the platform page wasn't found");
			return false;
		}
		Transform old = page.Find(ButtonName + "_Platforms");
		if ((bool)old)
		{
			UnityEngine.Object.DestroyImmediate(old.gameObject);
		}
		// Beside the engine (right, else left), in the free space at the top of the page; else right of the phones'
		// market share, its bottom level with it.
		HideGda(menu, 2);
		Rect phone = Local((RectTransform)share.transform, page);
		float size = 48f;
		List<KeyValuePair<string, Rect>> spots = new List<KeyValuePair<string, Rect>>();
		Button engine = page.GetComponentsInChildren<Button>(includeInactive: true).FirstOrDefault((Button b) => Calls(b, "BUTTON_Engine"));
		if ((bool)engine)
		{
			Rect e = Local((RectTransform)engine.transform, page);
			spots.Add(new KeyValuePair<string, Rect>("right of the engine", new Rect(e.xMax + 14f, e.center.y - size / 2f, size, size)));
			spots.Add(new KeyValuePair<string, Rect>("left of the engine", new Rect(e.xMin - 14f - size, e.center.y - size / 2f, size, size)));
		}
		spots.Add(new KeyValuePair<string, Rect>("right of the market share", new Rect(phone.xMax + 8f, phone.yMin, size, size)));
		KeyValuePair<string, Rect> free = spots.FirstOrDefault((KeyValuePair<string, Rect> s) => Free(s.Value, page));
		string where = free.Key;
		Rect spot = free.Value;
		if (where == null)
		{
			if (!Strip(page, phone.yMax, size, size, out spot))
			{
				Plugin.Log.LogWarning("Design assistant: no free space on " + page.name + " for the platforms button");
				return false;
			}
			where = "in free space";
		}
		platforms = RoundButton(page, ButtonName + "_Platforms", "design-platforms.png", "Assist", spot);
		UIKit.OnClick(platforms.GetComponent<Button>(), () =>
		{
			UIKit.Sound();
			Run(ExtraTopicsMenu.Menu, 7);
		});
		Plugin.Log.LogInfo("Design assistant: platforms button added to " + page.name + ", " + where + " (" + spot + ")");
		return true;
	}

	// Hidden before measuring the free space, and remembered so it's shown again if ours can't be placed.
	private static void HideGda(Menu_DevGame menu, int i)
	{
		if (!gda[i])
		{
			Transform t = menu.transform.Find(GdaPaths[i]);
			gda[i] = (t ? t.gameObject : null);
		}
		if ((bool)gda[i] && gda[i].activeSelf)
		{
			gda[i].SetActive(value: false);
			gdaHidden[i] = true;
		}
	}

	// The first free w x h space on the page, from the given height down, left to right.
	private static bool Strip(RectTransform page, float top, float w, float h, out Rect found)
	{
		Transform bar = page.parent.Find("Line") ?? page.parent.Find("PanelSeiten");
		float floor = (((bool)bar && bar is RectTransform brt) ? Local(brt, page).yMax : page.rect.yMin) + 6f;
		for (float y = top; y - h >= floor; y -= 6f)
		{
			for (float x = page.rect.xMin + 10f; x + w <= page.rect.xMax - 10f; x += 10f)
			{
				Rect r = new Rect(x, y - h, w, h);
				if (Free(r, page))
				{
					found = r;
					return true;
				}
			}
		}
		found = default(Rect);
		return false;
	}

	// ---- The game's own Auto Design Settings (design focus page) also sets the work priorities, as GameDev Assistant's
	// hook on it did ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_DevGame), "BUTTON_AutoDesignSettings")]
	private static void AutoDesign(Menu_DevGame __instance)
	{
		if (!DesignKnowledge.On)
		{
			return;
		}
		try
		{
			Priorities(__instance, new Report());
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Design assistant (Auto Design Settings): " + ex.Message);
		}
	}

	// That button wears our icon while the assistant is on, and its own again when it's off.
	private static Image autoImage;

	private static Sprite autoOwn;

	private static Sprite autoIcon;

	private static Image.Type autoType;

	private static bool autoTried;

	private static readonly List<Graphic> autoHidden = new List<Graphic>();

	private static void AutoIcon(Menu_DevGame menu, bool ours)
	{
		if (!autoImage)
		{
			// Looked for once each time the screen opens.
			if (!ours || autoTried)
			{
				return;
			}
			autoTried = true;
			Button b =menu.GetComponentsInChildren<Button>(includeInactive: true).FirstOrDefault((Button x) => Calls(x, "BUTTON_AutoDesignSettings"));
			autoImage = ((b != null) ? (b.targetGraphic as Image) : null);
			autoIcon = autoIcon ?? UIKit.LoadSprite(Path.Combine(Path.Combine(Plugin.PluginDir ?? "", "icons"), "design-focus.png"));
			if (!autoImage || !autoIcon)
			{
				autoImage = null;
				return;
			}
			autoOwn = autoImage.sprite;
			autoType = autoImage.type;
			autoHidden.Clear();
			Plugin.Log.LogInfo("Design assistant: the game's Auto Design Settings button (" + b.name + ") wears our icon");
		}
		if (ours == (autoImage.sprite == autoIcon))
		{
			return;
		}
		autoImage.sprite = (ours ? autoIcon : autoOwn);
		autoImage.type = (ours ? Image.Type.Simple : autoType);
		// Our icon is the whole round button, so the game's picture on it goes while ours is shown.
		if (ours)
		{
			autoHidden.Clear();
			autoHidden.AddRange(from g in autoImage.GetComponentsInChildren<Graphic>(includeInactive: true)
				where g != autoImage && g.enabled
				select g);
		}
		foreach (Graphic g in autoHidden)
		{
			if ((bool)g)
			{
				g.enabled = !ours;
			}
		}
	}

	// A round button of our own (icons\design-*.png), or the addon's small text button if the picture is missing.
	private static GameObject RoundButton(RectTransform page, string name, string file, string text, Rect at)
	{
		Sprite icon = UIKit.LoadSprite(Path.Combine(Path.Combine(Plugin.PluginDir ?? "", "icons"), file));
		GameObject go;
		if ((bool)icon)
		{
			go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
			go.transform.SetParent(page, worldPositionStays: false);
			Image img = go.GetComponent<Image>();
			img.sprite = icon;
			img.preserveAspect = true;
			go.GetComponent<Button>().targetGraphic = img;
		}
		else
		{
			go = UIKit.Make("SmallButton", page);
			go.name = name;
			foreach (Transform item in go.transform)
			{
				UIKit.Stretch(item);
			}
			Text label = go.transform.Find("Text")?.GetComponent<Text>();
			if ((bool)label)
			{
				label.text = text;
				label.resizeTextForBestFit = true;
				label.resizeTextMinSize = 9;
			}
		}
		go.SetActive(value: true);
		RectTransform rt = (RectTransform)go.transform;
		rt.anchorMin = (rt.anchorMax = new Vector2(0.5f, 0.5f));
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = at.size;
		rt.anchoredPosition = at.center - page.rect.center;
		go.transform.SetAsLastSibling();
		return go;
	}

	// Nothing else on the page is there (big backgrounds aside), and it's inside the page.
	private static bool Free(Rect r, RectTransform page)
	{
		Rect pr = page.rect;
		if (r.xMin < pr.xMin || r.xMax > pr.xMax || r.yMin < pr.yMin || r.yMax > pr.yMax)
		{
			return false;
		}
		Rect inner = new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f);
		foreach (Graphic g in page.GetComponentsInChildren<Graphic>(includeInactive: true))
		{
			if (!g.enabled || g.transform == page || !OnWithin(g.transform, page))
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

	// The button runs this method of the game's (set in the game's own screen, so it's found whatever it's called).
	private static bool Calls(Button b, string method)
	{
		for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
		{
			if (b.onClick.GetPersistentMethodName(i) == method)
			{
				return true;
			}
		}
		return false;
	}

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

	private static Rect Local(RectTransform rt, RectTransform page)
	{
		Vector3[] c = new Vector3[4];
		rt.GetWorldCorners(c);
		Vector3 a = page.InverseTransformPoint(c[0]);
		Vector3 b = page.InverseTransformPoint(c[2]);
		return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
	}

	// ---- Assisting ----

	private class Report
	{
		public readonly List<string> Done = new List<string>();

		public readonly List<string> Left = new List<string>();
	}

	// The game lets you change it (its button, slider or field is live).
	private static bool Can(Menu_DevGame menu, int ui)
	{
		GameObject go = ((ui < menu.uiObjects.Length) ? menu.uiObjects[ui] : null);
		if (!go)
		{
			return false;
		}
		Button b = ButtonOf(go.transform, menu.transform);
		if ((bool)b)
		{
			return b.interactable;
		}
		Selectable s = go.GetComponent<Selectable>();
		if (!s)
		{
			return true;
		}
		return s.interactable;
	}

	// The parts ticked in the options, or just the ones given (a page's own button: the platform page's runs Platforms).
	public static void Run(Menu_DevGame menu, params int[] only)
	{
		if (!menu || !menu.gameObject.activeInHierarchy || !DesignKnowledge.On)
		{
			return;
		}
		if (only.Length == 0)
		{
			clicks++;
		}
		Report r = new Report();
		bool contract = menu.typ_contractGame;
		bool copy = menu.g_portIP != -1 || menu.typ_remaster;
		// Each part on its own, so one that fails (another mod's hook on a game method, say) doesn't stop the rest.
		void Step(int part, bool allowed, Action<Menu_DevGame, Report> set)
		{
			if (!allowed || !((only.Length == 0) ? PartOn(part) : only.Contains(part)))
			{
				return;
			}
			try
			{
				set(menu, r);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Design assistant (" + Parts[part] + ") failed: " + ex);
				r.Left.Add(PartNames[part].ToLowerInvariant() + " (it went wrong; the log has the details)");
			}
		}
		Step(0, !contract && !copy, MainGenre);
		if (only.Length == 0 && menu.g_GameMainGenre < 0 && !contract && !copy)
		{
			r.Left.Add("everything that depends on the genre (pick a main genre first)");
		}
		Step(1, !contract && !copy, SubGenre);
		Step(2, !contract && !copy, Topics);
		Step(3, !contract && !copy, MoreTopics);
		Step(4, !contract && !copy, AgeGroup);
		Step(5, allowed: true, Design);
		Step(6, allowed: true, Priorities);
		Step(7, !contract, Platforms);
		Step(8, allowed: true, Engine);
		Step(9, !contract && !copy, Licence);
		Step(10, !contract, Languages);
		// (A sequel keeps the name the game gave it.)
		Step(11, !contract && !copy && !menu.typ_nachfolger, Name);
		try
		{
			DevCosts(menu);
			menu.GetGesamtDevPoints();
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogWarning("Design assistant (costs): " + ex2.Message);
		}
		List<string> lines = new List<string>();
		if (r.Done.Count > 0)
		{
			lines.Add(DesignColours.Paint("Set: ", 1) + string.Join("; ", r.Done) + ".");
		}
		if (r.Left.Count > 0)
		{
			lines.Add(DesignColours.Paint("Left as it was: ", -1) + string.Join("; ", r.Left) + ".");
		}
		if (lines.Count == 0)
		{
			lines.Add("Nothing to change.");
		}
		Last = string.Join("\n", lines);
		LastLeft = ((r.Left.Count > 0) ? string.Join("; ", r.Left) : "");
		shown = "";
		if (EditorWindow.DesignAssist.IsOpen)
		{
			EditorWindow.DesignAssist.Rebuild();
		}
	}

	private static genres Gen => IndustryCore.Genres;

	private static themes Th
	{
		get
		{
			if (!G.MainObj)
			{
				return null;
			}
			return G.MainObj.GetComponent<themes>();
		}
	}

	private static float At(float[] a, int i)
	{
		if (a == null || i < 0 || i >= a.Length)
		{
			return 0f;
		}
		return a[i];
	}

	private static int At(int[] a, int i)
	{
		if (a == null || i < 0 || i >= a.Length)
		{
			return 0;
		}
		return a[i];
	}

	private static List<int> GenresOpen()
	{
		genres gen = Gen;
		List<int> list = new List<int>();
		if (!gen || gen.genres_UNLOCK == null)
		{
			return list;
		}
		for (int i = 0; i < gen.genres_UNLOCK.Length; i++)
		{
			if (gen.genres_UNLOCK[i] && gen.IsErforscht(i))
			{
				list.Add(i);
			}
		}
		return list;
	}

	private static float GenreScore(int g)
	{
		genres gen = Gen;
		mainScript ms = G.MS;
		float s = At(gen.genres_BELIEBTHEIT, g) + (float)(At(gen.genres_LEVEL, g) * 4) - (float)(At(gen.genres_MARKT, g) * 2);
		if (ms.trendGenre == g)
		{
			s += 25f;
		}
		if (ms.trendAntiGenre == g)
		{
			s -= 40f;
		}
		return s;
	}

	private static float TopicScore(int t)
	{
		themes th = Th;
		mainScript ms = G.MS;
		float s = At(th.themes_LEVEL, t) * 4 - At(th.themes_MARKT, t) * 2;
		if (ms.trendTheme == t)
		{
			s += 30f;
		}
		if (ms.trendAntiTheme == t)
		{
			s -= 50f;
		}
		return s;
	}

	private static List<int> TopicsOpen()
	{
		themes th = Th;
		List<int> list = new List<int>();
		if (!th || th.themes_LEVEL == null)
		{
			return list;
		}
		for (int i = 0; i < th.themes_LEVEL.Length; i++)
		{
			if (th.IsErforscht(i))
			{
				list.Add(i);
			}
		}
		return list;
	}

	// ---- Variety: the first Assist on a game picks the best; each click after that picks again among the near-best ----

	// How far below the best an option may score to be picked on a repeat click: about one popularity step's worth for
	// a genre, five stars of experience for a topic.
	private const float Margin = 20f;

	// Assist clicks on this game (reset when the screen opens), and the main genre Assist itself last picked.
	private static int clicks;

	private static int assistGenre = -1;

	private static readonly Func<int, int> Roll = (int n) => UnityEngine.Random.Range(0, n);

	private static int Pick(IEnumerable<int> options, Func<int, float> score, int current)
	{
		return Pick(options, score, current, clicks > 1, Roll);
	}

	// The best option; on a repeat click (again), a random one of those within Margin of the best, other than the one
	// in place, so each click changes it while there's another good one. -1 if there are none.
	internal static int Pick(IEnumerable<int> options, Func<int, float> score, int current, bool again, Func<int, int> random)
	{
		List<KeyValuePair<int, float>> list = (from o in options
			select new KeyValuePair<int, float>(o, score(o)) into p
			orderby p.Value descending
			select p).ToList();
		if (list.Count == 0)
		{
			return -1;
		}
		if (!again)
		{
			return list[0].Key;
		}
		float top = list[0].Value;
		List<int> near = (from p in list
			where top - p.Value <= Margin && p.Key != current
			select p.Key).ToList();
		return (near.Count == 0) ? list[0].Key : near[random(near.Count)];
	}

	private static void MainGenre(Menu_DevGame menu, Report r)
	{
		if (!Can(menu, 5))
		{
			return;
		}
		// Yours is kept; one Assist picked can be picked again.
		if (Plugin.DesignKeepMainGenre.Value && menu.g_GameMainGenre >= 0 && menu.g_GameMainGenre != assistGenre)
		{
			return;
		}
		// Genres your studio knows a topic for, so the topics can be filled in too (any genre while it knows none).
		List<int> open = GenresOpen();
		List<int> topics = TopicsOpen();
		List<int> known = open.Where((int g) => topics.Any((int t) => DesignKnowledge.TopicFit(t, g) > 0)).ToList();
		int best = Pick((known.Count > 0) ? known : open, GenreScore, menu.g_GameMainGenre);
		if (best >= 0 && best != menu.g_GameMainGenre)
		{
			if (best == menu.g_GameSubGenre)
			{
				menu.SetSubGenre(-1);
			}
			menu.SetMainGenre(best);
			r.Done.Add("main genre " + IndustryCore.GenreName(best));
		}
		if (best >= 0)
		{
			assistGenre = best;
		}
	}

	private static void SubGenre(Menu_DevGame menu, Report r)
	{
		int main = menu.g_GameMainGenre;
		if (main < 0 || !Can(menu, 6))
		{
			return;
		}
		if (!DesignKnowledge.GenreKnown(main))
		{
			r.Left.Add("sub-genre (the review report of a " + IndustryCore.GenreName(main) + " game of yours lists the good ones)");
			return;
		}
		int best = Pick(GenresOpen().Where((int g) => g != main && DesignKnowledge.ComboFit(main, g) > 0), GenreScore, menu.g_GameSubGenre);
		if (best < 0)
		{
			r.Left.Add("sub-genre (none you've researched goes with " + IndustryCore.GenreName(main) + ")");
		}
		else if (best != menu.g_GameSubGenre)
		{
			menu.SetSubGenre(best);
			r.Done.Add("sub-genre " + IndustryCore.GenreName(best));
		}
	}

	private static void Topics(Menu_DevGame menu, Report r)
	{
		int main = menu.g_GameMainGenre;
		if (main < 0)
		{
			return;
		}
		HashSet<int> extra = new HashSet<int>(ExtraTopicsMenu.Pending.Where((int t) => t >= 0));
		bool keepExtra = !PartOn(3);
		List<int> fits = (from t in TopicsOpen()
			where DesignKnowledge.TopicFit(t, main) > 0 && (!keepExtra || !extra.Contains(t))
			select t).ToList();
		string genre = IndustryCore.GenreName(main);
		if (Can(menu, 21))
		{
			int first = Pick(fits, TopicScore, menu.g_GameMainTheme);
			if (first < 0)
			{
				if (DesignKnowledge.TopicFit(menu.g_GameMainTheme, main) <= 0)
				{
					r.Left.Add("main topic (no topic is known to suit " + genre + " yet)");
				}
			}
			else if (first != menu.g_GameMainTheme)
			{
				if (first == menu.g_GameSubTheme)
				{
					menu.SetSubTheme(-1);
				}
				menu.SetMainTheme(first);
				r.Done.Add("main topic " + ExtraTopics.TopicName(first));
			}
		}
		if (Can(menu, 23))
		{
			int second = Pick(fits.Where((int t) => t != menu.g_GameMainTheme), TopicScore, menu.g_GameSubTheme);
			if (second < 0)
			{
				if (DesignKnowledge.TopicFit(menu.g_GameSubTheme, main) <= 0)
				{
					r.Left.Add("sub topic (no other topic is known to suit " + genre + " yet)");
				}
			}
			else if (second != menu.g_GameSubTheme)
			{
				menu.SetSubTheme(second);
				r.Done.Add("sub topic " + ExtraTopics.TopicName(second));
			}
		}
	}

	private static void MoreTopics(Menu_DevGame menu, Report r)
	{
		int main = menu.g_GameMainGenre;
		if (main < 0 || !ExtraTopics.On || ExtraTopicsMenu.Locked || ExtraTopicsMenu.Hidden)
		{
			return;
		}
		List<int> open = TopicsOpen();
		for (int slot = 0; slot < 2; slot++)
		{
			if (!ExtraTopicsMenu.Unlocked(slot))
			{
				continue;
			}
			int[] pending = ExtraTopicsMenu.Pending;
			List<int> others = new List<int> { menu.g_GameMainTheme, menu.g_GameSubTheme, pending[1 - slot] };
			Dictionary<int, float> fits = new Dictionary<int, float>();
			foreach (int t in open)
			{
				if (others.Contains(t) || DesignKnowledge.TopicFit(t, main) <= 0)
				{
					continue;
				}
				ExtraTopics.Verdict v = ExtraTopics.Judge(t, main, others);
				if (v.ClashesWith.Count == 0 && v.GoesWith.Count > 0)
				{
					fits[t] = v.Score * 100f + TopicScore(t);
				}
			}
			int best = Pick(fits.Keys, (int t) => fits[t], pending[slot]);
			string name = ExtraTopics.SlotNames[slot].ToLowerInvariant();
			if (best >= 0)
			{
				if (best != pending[slot])
				{
					pending[slot] = best;
					r.Done.Add(name + " " + ExtraTopics.TopicName(best));
				}
			}
			else if (pending[slot] >= 0 && (DesignKnowledge.TopicFit(pending[slot], main) < 0 || ExtraTopics.Judge(pending[slot], main, others).ClashesWith.Count > 0))
			{
				r.Done.Add(name + " taken off (" + ExtraTopics.TopicName(pending[slot]) + " didn't fit)");
				pending[slot] = -1;
			}
		}
	}

	private static void AgeGroup(Menu_DevGame menu, Report r)
	{
		int main = menu.g_GameMainGenre;
		if (main < 0 || !Can(menu, 4))
		{
			return;
		}
		if (DesignKnowledge.AgeFit(main, menu.g_GameZielgruppe) > 0)
		{
			return;
		}
		foreach (int g in new int[5] { 4, 2, 1, 0, 3 })
		{
			if (DesignKnowledge.AgeFit(main, g) > 0)
			{
				menu.SetZielgruppe(g);
				r.Done.Add("age group " + (G.TS ? G.TS.GetGameZielgruppe(g) : g.ToString()));
				return;
			}
		}
		r.Left.Add("age group (none is known to suit " + IndustryCore.GenreName(main) + " yet)");
	}

	private static Slider SliderOf(GameObject row)
	{
		if (!row || row.transform.childCount <= 1)
		{
			return null;
		}
		return row.transform.GetChild(1).GetComponent<Slider>();
	}

	private static void Design(Menu_DevGame menu, Report r)
	{
		genres gen = Gen;
		int main = menu.g_GameMainGenre;
		if (main < 0 || !gen || menu.uiDesignschwerpunkte == null || menu.uiDesignschwerpunkte.Length == 0)
		{
			return;
		}
		Slider first = SliderOf(menu.uiDesignschwerpunkte[0]);
		if ((bool)first && !first.interactable)
		{
			return;
		}
		int known = 0;
		int total = menu.g_Designschwerpunkt.Length + menu.g_Designausrichtung.Length;
		for (int i = 0; i < menu.g_Designschwerpunkt.Length; i++)
		{
			if (gen.GetFocusKnown(i, main, menu.g_GameSubGenre))
			{
				known++;
			}
		}
		for (int j = 0; j < menu.g_Designausrichtung.Length; j++)
		{
			if (gen.GetAlignKnown(j, main, menu.g_GameSubGenre))
			{
				known++;
			}
		}
		if (known == 0)
		{
			r.Left.Add("design sliders (the right values for " + Pair(menu) + " aren't known yet)");
			return;
		}
		// What the game's Auto Design Settings does (BUTTON_AutoDesignSettings), without calling it: GameDev Assistant
		// hooks that button and crashes on The Holy Expansion's genres.
		int sub = menu.g_GameSubGenre;
		for (int i = 0; i < menu.g_Designschwerpunkt.Length; i++)
		{
			if (gen.GetFocusKnown(i, main, sub))
			{
				menu.g_Designschwerpunkt[i] = gen.GetFocus(i, main, sub);
			}
		}
		for (int j = 0; j < menu.g_Designausrichtung.Length; j++)
		{
			if (gen.GetAlignKnown(j, main, sub))
			{
				menu.g_Designausrichtung[j] = gen.GetAlign(j, main, sub);
			}
		}
		menu.UpdateDesignSettings();
		menu.UpdateDesignSlider();
		r.Done.Add("design sliders" + ((known < total) ? (" (" + known + " of " + total + " known)") : ""));
	}

	private static string Pair(Menu_DevGame menu)
	{
		return IndustryCore.GenreName(menu.g_GameMainGenre) + ((menu.g_GameSubGenre >= 0) ? (" / " + IndustryCore.GenreName(menu.g_GameSubGenre)) : "");
	}

	private static void Priorities(Menu_DevGame menu, Report r)
	{
		genres gen = Gen;
		int main = menu.g_GameMainGenre;
		if (main < 0 || !gen || !Can(menu, 97))
		{
			return;
		}
		if (!DesignKnowledge.GenreKnown(main))
		{
			r.Left.Add("work priorities (the review report of a " + IndustryCore.GenreName(main) + " game of yours shows them)");
			return;
		}
		float[] pct = new float[4]
		{
			At(gen.genres_GAMEPLAY, main),
			At(gen.genres_GRAPHIC, main),
			At(gen.genres_SOUND, main),
			At(gen.genres_CONTROL, main)
		};
		float sum = pct.Sum();
		if (sum <= 0f)
		{
			return;
		}
		// Twenty steps of 5% in the genre's ratio, the largest remainders rounded up.
		int[] steps = new int[4];
		float[] rest = new float[4];
		for (int i = 0; i < 4; i++)
		{
			float exact = pct[i] / sum * 20f;
			steps[i] = Mathf.FloorToInt(exact);
			rest[i] = exact - (float)steps[i];
		}
		foreach (int k in (from i in Enumerable.Range(0, 4)
			orderby rest[i] descending
			select i).Take(20 - steps.Sum()))
		{
			steps[k]++;
		}
		for (int i = 0; i < 4; i++)
		{
			Slider s = menu.uiObjects[97 + i] ? menu.uiObjects[97 + i].GetComponent<Slider>() : null;
			if ((bool)s)
			{
				s.value = Mathf.Clamp(steps[i], s.minValue, s.maxValue);
			}
		}
		menu.SetAP_Gameplay();
		menu.SetAP_Grafik();
		menu.SetAP_Sound();
		menu.SetAP_Technik();
		r.Done.Add("work priorities " + string.Join("/", steps.Select((int x) => x * 5 + "%")));
	}

	// The platforms you have a dev kit for that run your engine's tech level (the platform list's "matching tech level"),
	// by market share into slots 1 to 4 (one for an exclusive), within the platform type picked. The timed
	// exclusive's platform stays if it still qualifies.
	private static void Platforms(Menu_DevGame menu, Report r)
	{
		mainScript ms = G.MS;
		GameObject[] ui = menu.uiObjects;
		if (!ms || !ui[146] || !ui[140] || !ui[140].GetComponent<Button>() || !ui[140].GetComponent<Button>().interactable)
		{
			return;
		}
		int kind = ui[146].GetComponent<Dropdown>().value;
		List<int> slots = new List<int> { 0 };
		if (kind != 1 && kind != 4)
		{
			for (int s = 1; s <= 3; s++)
			{
				Button b = (ui[32 + s] ? ui[32 + s].GetComponent<Button>() : null);
				if ((bool)b && b.interactable)
				{
					slots.Add(s);
				}
			}
		}
		int tech = menu.GetEngineTechLevel();
		List<platformScript> kits = new List<platformScript>();
		foreach (GameObject go in GameObject.FindGameObjectsWithTag("Platform"))
		{
			platformScript p = (go ? go.GetComponent<platformScript>() : null);
			if ((bool)p && p.inBesitz && (p.isUnlocked || p.ownerID == ms.myID) && !p.IsProConsoleInDev() && (kind != 2 || p.ownerID == ms.myID) && (kind == 5) == (p.typ == 3) && (kind == 4) == (p.typ == 4) && (kind == 3) == p.vomMarktGenommen)
			{
				kits.Add(p);
			}
		}
		if (kits.Count == 0)
		{
			r.Left.Add("platforms (you have no dev kit for this kind of game)");
			return;
		}
		int timed = TimedExclusiveMenu.PickedFirst;
		List<platformScript> chosen = (from p in kits
			where p.tech >= tech
			orderby p.myID == timed descending, p.GetMarktanteil() descending
			select p).Take(slots.Count).ToList();
		if (chosen.Count == 0)
		{
			r.Left.Add("platforms (none you have a dev kit for runs your engine's tech level " + tech + ")");
			return;
		}
		// Best market share in the first slot.
		chosen = chosen.OrderByDescending((platformScript p) => p.GetMarktanteil()).ToList();
		List<int> before = slots.Select((int s) => menu.g_GamePlatform[s]).ToList();
		if (before.SequenceEqual(slots.Select((int s, int i) => (i < chosen.Count) ? chosen[i].myID : (-1))))
		{
			return;
		}
		foreach (int s in slots)
		{
			menu.SetPlatform(s, -1);
		}
		for (int i = 0; i < chosen.Count && i < slots.Count; i++)
		{
			menu.SetPlatform(slots[i], chosen[i].myID);
		}
		r.Done.Add("platforms " + string.Join(", ", chosen.Select((platformScript p) => p.GetName())));
	}

	private static void Engine(Menu_DevGame menu, Report r)
	{
		if (!menu.g_GameEngineScript_ || !Can(menu, 139))
		{
			return;
		}
		int[] before = (int[])menu.g_GameEngineFeature.Clone();
		menu.BUTTON_AutoEngineFeature();
		if (!before.SequenceEqual(menu.g_GameEngineFeature))
		{
			r.Done.Add("engine features");
		}
	}

	private static void Licence(Menu_DevGame menu, Report r)
	{
		licences lic = (G.MainObj ? G.MainObj.GetComponent<licences>() : null);
		int main = menu.g_GameMainGenre;
		if (!lic || lic.licence_GEKAUFT == null || main < 0 || !Can(menu, 28) || menu.g_GameLicence >= 0)
		{
			return;
		}
		int best = -1;
		float bestScore = float.MinValue;
		for (int i = 0; i < lic.licence_GEKAUFT.Length; i++)
		{
			if (lic.licence_GEKAUFT[i] <= 0 || At(lic.licence_GENREBAD, i) == main)
			{
				continue;
			}
			float s = At(lic.licence_QUALITY, i) + ((At(lic.licence_GENREGOOD, i) == main) ? 50f : 0f);
			if (s > bestScore)
			{
				best = i;
				bestScore = s;
			}
		}
		if (best >= 0)
		{
			menu.SetLicence(best);
			r.Done.Add("licence " + lic.GetName(best));
		}
	}

	// What a game of yours takes: the middle of your last five finished games, or twice this one's cost for a first.
	private static long Takings(Menu_DevGame menu)
	{
		mainScript ms = G.MS;
		List<long> takings = (from g in IndustryCore.AllGames()
			where g.IsMyGame() && !g.inDevelopment && !g.typ_contractGame && !g.pubOffer && (g.typ_standard || g.typ_nachfolger || g.typ_spinoff || g.typ_remaster) && g.umsatzTotal > 0
			orderby g.isOnMarket, g.myID descending
			select g.umsatzTotal).Take(5).ToList();
		if (takings.Count == 0)
		{
			return 2L * DevCosts(menu);
		}
		takings.Sort();
		return takings[takings.Count / 2];
	}

	private static void Languages(Menu_DevGame menu, Report r)
	{
		mainScript ms = G.MS;
		if (!ms || menu.g_GameLanguage == null)
		{
			return;
		}
		long takings = Takings(menu);
		float factor = ((ExtraTopics.On && !ExtraTopicsMenu.Hidden) ? ExtraTopics.WorkFactor(ExtraTopicsMenu.Pending) : 1f);
		float cost = (float)menu.GetGesamtDevPoints() * 5f * factor;
		int added = 0;
		int dropped = 0;
		for (int i = 0; i < menu.g_GameLanguage.Length && i < LanguageShare.Length; i++)
		{
			GameObject flag = ((107 + i < menu.uiObjects.Length) ? menu.uiObjects[107 + i] : null);
			Button b = (flag ? flag.GetComponent<Button>() : null);
			if (!flag || ((bool)b && !b.interactable))
			{
				continue;
			}
			bool want = ms.Muttersprache(i) || LanguageShare[i] * (float)takings >= cost;
			if (menu.g_GameLanguage[i] != want)
			{
				// As the game's SetLanguage does, without a click sound for each.
				menu.g_GameLanguage[i] = want;
				Image img = flag.GetComponent<Image>();
				if ((bool)img)
				{
					img.color = (want ? Color.white : menu.languageColor);
				}
				if (want)
				{
					added++;
				}
				else
				{
					dropped++;
				}
			}
		}
		if (ExtraLanguages.On)
		{
			for (int j = 0; j < ExtraLanguages.Count && j < LanguagesRow.Pending.Length; j++)
			{
				bool want2 = ExtraLanguages.Unlocked(j) && (ExtraLanguages.Native(j) || ExtraLanguages.Defs[j].Share * (float)takings >= cost);
				if (LanguagesRow.Pending[j] != want2)
				{
					LanguagesRow.Pending[j] = want2;
					if (want2)
					{
						added++;
					}
					else
					{
						dropped++;
					}
				}
			}
		}
		if (added + dropped > 0)
		{
			int count = menu.AnzahlLanguages() + (ExtraLanguages.On ? ExtraLanguages.Picked(LanguagesRow.Pending) : 0);
			r.Done.Add(count + ((count == 1) ? " language" : " languages") + " (each worth its " + G.Money((long)cost) + " translation at " + G.Money(takings) + " takings)");
		}
	}

	private static void Name(Menu_DevGame menu, Report r)
	{
		InputField field = (menu.uiObjects[0] ? menu.uiObjects[0].GetComponent<InputField>() : null);
		if (!field || !field.interactable)
		{
			return;
		}
		if (menu.g_GameLicence >= 0)
		{
			menu.SetLicenceName();
		}
		else
		{
			menu.BUTTON_RandomGameName();
		}
		r.Done.Add("name " + field.text);
	}
}
