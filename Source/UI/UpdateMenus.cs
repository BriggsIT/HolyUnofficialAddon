using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using HolyAddon.Core;
using HolyAddon.Core.Updates;
using HolyAddon.UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// The game's own update menus, extended: four more rows in Updates & Addons, the game's update game list reused to
// pick a game for them, and the game's change-platform screen reused for ports of released games.
[HarmonyPatch]
internal static class UpdateMenus
{
	private static readonly UpdateKind[] Kinds = new UpdateKind[4]
	{
		UpdateKind.Port,
		UpdateKind.Engine,
		UpdateKind.Features,
		UpdateKind.Rating
	};

	private static readonly AccessTools.FieldRef<Menu_Dev_Addon, roomScript> AddonRoom = AccessTools.FieldRefAccess<Menu_Dev_Addon, roomScript>("rS_");

	private static readonly AccessTools.FieldRef<Menu_Dev_UpdateSelectGame, roomScript> SelectRoom = AccessTools.FieldRefAccess<Menu_Dev_UpdateSelectGame, roomScript>("rS_");

	// The update type the game list is picking a game for (null: the game's own free update).
	private static UpdateKind? mode;

	// The released game the change-platform screen is porting (null: the game's own platform change).
	private static gameScript portGame;

	private static roomScript portRoom;

	private static readonly Dictionary<Text, string> titles = new Dictionary<Text, string>();

	private static bool dumped;

	private static bool rowsFailed;

	public static Transform CloneButton(Transform template, Transform parent, string name)
	{
		Transform copy = UnityEngine.Object.Instantiate(template.gameObject, parent, worldPositionStays: false).transform;
		copy.name = name;
		StripGameScripts(copy);
		foreach (Text text in copy.GetComponentsInChildren<Text>(includeInactive: true))
		{
			if (text.transform.parent == copy && text.name != "Text")
			{
				UnityEngine.Object.DestroyImmediate(text.gameObject);
			}
		}
		Button button = copy.GetComponent<Button>();
		if ((bool)button)
		{
			button.onClick = new Button.ButtonClickedEvent();
		}
		return copy;
	}

	private static void StripGameScripts(Transform t)
	{
		foreach (setText s in t.GetComponentsInChildren<setText>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(s);
		}
		foreach (lockDestroy l in t.GetComponentsInChildren<lockDestroy>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(l);
		}
		foreach (tooltip tip in t.GetComponentsInChildren<tooltip>(includeInactive: true))
		{
			UnityEngine.Object.DestroyImmediate(tip);
		}
	}

	public static void Tick()
	{
		if (!G.Gui || G.Gui.uiObjects == null || G.Gui.uiObjects.Length <= 105)
		{
			return;
		}
		if (mode.HasValue && (bool)G.Gui.uiObjects[105] && !G.Gui.uiObjects[105].activeSelf)
		{
			ClearMode();
		}
		if ((bool)portGame && (bool)G.Gui.uiObjects[102] && !G.Gui.uiObjects[102].activeSelf && (!G.Gui.uiObjects[66] || !G.Gui.uiObjects[66].activeSelf))
		{
			EndPort();
		}
	}

	// ---- Updates & Addons: four more rows ----

	private static string Label(UpdateKind k)
	{
		return k switch
		{
			UpdateKind.Port => "Port to More Platforms",
			UpdateKind.Engine => "Update the Engine",
			UpdateKind.Features => "Add Gameplay Features",
			UpdateKind.Rating => "Change the Age Rating",
			_ => "",
		};
	}

	private static string Tip(UpdateKind k)
	{
		return k switch
		{
			UpdateKind.Port => "<b>Port to more platforms</b>\nBring a released game to more platforms of the same kind (another console, PC...). The game gets a launch boost on them and keeps the extra reach. (Holy Unofficial Addon)",
			UpdateKind.Engine => "<b>Update the engine</b>\nMove a released game to newer engine features, from its own engine or another one you own or license. Better graphics, sound and tech, and a lasting sales bonus. (Holy Unofficial Addon)",
			UpdateKind.Features => "<b>Add gameplay features</b>\nAdd researched gameplay features to a released game without making an addon. Features that suit the genre help most. (Holy Unofficial Addon)",
			UpdateKind.Rating => "<b>Change the age rating</b>\nCut content so a released game gets a lower age rating and more players can buy it. (Holy Unofficial Addon)",
			_ => "",
		};
	}

	private static string LockReason(UpdateKind k)
	{
		if (k == UpdateKind.Port && !GameUpdates.SlotUnlocked(1))
		{
			return "Research making games for more than one platform first.";
		}
		return null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_Addon), "Init")]
	private static void AddonMenu(Menu_Dev_Addon __instance)
	{
		ClearMode();
		if (rowsFailed)
		{
			return;
		}
		try
		{
			List<Transform> rows = EnsureRows(__instance);
			if (rows == null)
			{
				return;
			}
			for (int i = 0; i < Kinds.Length; i++)
			{
				UpdateKind k = Kinds[i];
				Transform row = rows[i];
				string locked = LockReason(k);
				row.gameObject.SetActive(GameUpdates.On);
				Transform icon = __instance.transform.FindDeep("Addon_UpdateIcon_" + (int)k);
				if ((bool)icon)
				{
					icon.gameObject.SetActive(GameUpdates.On);
				}
				// The game's rows carry their icon as a child "Icon" of the button (copied from the F2P row).
				Image rowIcon = row.Find("Icon")?.GetComponent<Image>();
				Sprite sprite = UpdateIcons.For(k);
				if ((bool)rowIcon && (bool)sprite && rowIcon.sprite != sprite)
				{
					rowIcon.sprite = sprite;
					rowIcon.preserveAspect = true;
				}
				row.GetComponent<Button>().interactable = locked == null;
				Transform lockIcon = row.Find("Addon_Lock");
				if ((bool)lockIcon)
				{
					lockIcon.gameObject.SetActive(locked != null);
				}
				UIKit.SetTooltip(row.gameObject, ((locked != null) ? ("<b>" + Label(k) + "</b>\n" + locked) : Tip(k)) + (Core.Mmo.MmoOps.On ? "\nMMOs get this in MMO Management, with more effect on their players." : ""));
			}
			MoveMmoAddon(__instance, rows);
		}
		catch (Exception ex)
		{
			rowsFailed = true;
			Plugin.Log.LogError("Updates & Addons rows failed: " + ex);
		}
	}

	// While MMO Management is on, MMO addons are made from there (Expansion pack): the game's MMO addon row goes, the
	// rows below it move up and the window shrinks. Turning MMO Management off brings the row back.
	private static void MoveMmoAddon(Menu_Dev_Addon menu, List<Transform> rows)
	{
		RectTransform mmo = (menu.uiObjects[0] ? menu.uiObjects[0].GetComponent<RectTransform>() : null);
		if (!mmo || rows.Count < 2)
		{
			return;
		}
		bool hide = Core.Mmo.MmoOps.On;
		if (mmo.gameObject.activeSelf != hide)
		{
			return;
		}
		float spacing = rows[0].localPosition.y - rows[1].localPosition.y;
		if (spacing < 10f || spacing > 200f)
		{
			return;
		}
		RectTransform parent = mmo.parent as RectTransform;
		float y = mmo.localPosition.y;
		if (hide)
		{
			mmo.gameObject.SetActive(false);
			Grow(menu.transform, parent, y - spacing * 0.5f, 0f - spacing);
		}
		else
		{
			// The F2P row sits where the MMO row was; move it and everything below back down, but not the MMO row.
			Vector2 keep = mmo.anchoredPosition;
			Grow(menu.transform, parent, y + spacing * 0.5f, spacing);
			mmo.anchoredPosition = keep;
			mmo.gameObject.SetActive(true);
		}
	}

	private static Transform FindDeep(this Transform t, string name)
	{
		if (t.name == name)
		{
			return t;
		}
		foreach (Transform c in t)
		{
			Transform found = c.FindDeep(name);
			if ((bool)found)
			{
				return found;
			}
		}
		return null;
	}

	// The rows are clones of the F2P update row (button, icon, lock), stacked under it; the window grows to fit.
	private static List<Transform> EnsureRows(Menu_Dev_Addon menu)
	{
		if (menu.uiObjects == null || menu.uiObjects.Length < 4 || !menu.uiObjects[0] || !menu.uiObjects[2])
		{
			return null;
		}
		RectTransform mmo = menu.uiObjects[0].GetComponent<RectTransform>();
		RectTransform f2p = menu.uiObjects[2].GetComponent<RectTransform>();
		RectTransform parent = f2p.parent as RectTransform;
		List<Transform> existing = Kinds.Select((UpdateKind k) => parent.Find("Addon_Update_" + (int)k)).ToList();
		if (existing.All((Transform x) => (bool)x))
		{
			return existing;
		}
		if (!dumped)
		{
			dumped = true;
			Plugin.Log.LogInfo("Updates & Addons layout:\n" + Dump(menu.transform, 0));
		}
		if (mmo.parent != parent)
		{
			throw new Exception("the MMO and F2P buttons have different parents");
		}
		float spacing = mmo.localPosition.y - f2p.localPosition.y;
		if (spacing < 10f || spacing > 200f)
		{
			throw new Exception("unexpected row spacing " + spacing);
		}
		// The row's icon: an image left of the button, level with it, in the same parent.
		RectTransform icon = parent.Cast<Transform>().Select((Transform c) => c as RectTransform).FirstOrDefault((RectTransform c) => (bool)c && c != f2p && (bool)c.GetComponent<Image>() && !c.GetComponent<Button>() && Mathf.Abs(c.localPosition.y - f2p.localPosition.y) < spacing * 0.4f && c.localPosition.x < f2p.localPosition.x);
		// The lock: uiObjects[3], inside the button or next to it.
		Transform lockObject = (menu.uiObjects[3] ? menu.uiObjects[3].transform : null);
		bool lockInside = (bool)lockObject && lockObject.IsChildOf(f2p);
		string lockPath = (lockInside ? RelativePath(f2p, lockObject) : null);
		float grow = spacing * (float)Kinds.Length;
		Grow(menu.transform, parent, f2p.localPosition.y - spacing * 0.5f, grow);
		List<Transform> rows = new List<Transform>();
		for (int i = 0; i < Kinds.Length; i++)
		{
			UpdateKind k = Kinds[i];
			Vector3 offset = new Vector3(0f, (0f - spacing) * (float)(i + 1), 0f);
			Transform row = CloneButton(f2p, parent, "Addon_Update_" + (int)k);
			row.localPosition = f2p.localPosition + offset;
			if (lockPath != null)
			{
				Transform l = row.Find(lockPath);
				if ((bool)l)
				{
					l.name = "Addon_Lock";
				}
			}
			else if ((bool)lockObject && lockObject.parent == parent)
			{
				// A lock next to the button: copy it to the same place in the new row, then hang it on the row.
				Transform l2 = UnityEngine.Object.Instantiate(lockObject.gameObject, parent, worldPositionStays: false).transform;
				l2.name = "Addon_Lock";
				l2.localPosition = lockObject.localPosition + offset;
				l2.SetParent(row, worldPositionStays: true);
				StripGameScripts(l2);
			}
			Transform label = row.Find("Text");
			if ((bool)label)
			{
				label.GetComponent<Text>().text = Label(k);
			}
			UpdateKind kind = k;
			Menu_Dev_Addon owner = menu;
			UIKit.OnClick(row.GetComponent<Button>(), () => OpenList(owner, kind));
			if ((bool)icon)
			{
				Transform ic = UnityEngine.Object.Instantiate(icon.gameObject, parent, worldPositionStays: false).transform;
				ic.name = "Addon_UpdateIcon_" + (int)k;
				ic.localPosition = icon.localPosition + offset;
				StripGameScripts(ic);
				Image img = ic.GetComponent<Image>();
				Sprite sprite = UpdateIcons.For(k);
				if ((bool)img && (bool)sprite)
				{
					img.sprite = sprite;
					img.preserveAspect = true;
				}
			}
			rows.Add(row);
		}
		return rows;
	}

	private static string RelativePath(Transform root, Transform t)
	{
		List<string> parts = new List<string>();
		while ((bool)t && t != root)
		{
			parts.Insert(0, t.name);
			t = t.parent;
		}
		return string.Join("/", parts);
	}

	// Makes room for `grow` more height below `cut` (a y in `content`'s space): every rect from `content` up to the
	// menu window grows, things above the cut keep their distance to the top, things below it to the bottom.
	private static void Grow(Transform menuRoot, RectTransform content, float cut, float grow)
	{
		List<RectTransform> chain = new List<RectTransform>();
		RectTransform window = null;
		for (Transform t = content; (bool)t && t != menuRoot.parent; t = t.parent)
		{
			RectTransform rt = t as RectTransform;
			if (!rt)
			{
				break;
			}
			chain.Add(rt);
			if ((bool)t.GetComponent<Animation>() || t == menuRoot)
			{
				window = rt;
				break;
			}
		}
		if (!window || window.anchorMin.y != window.anchorMax.y)
		{
			throw new Exception("no fixed-size window found above the buttons");
		}
		Transform below = null;
		float cutHere = cut;
		for (int i = 0; i < chain.Count; i++)
		{
			RectTransform r = chain[i];
			if (i > 0)
			{
				// The cut in this rect's space: the bottom edge of the chain child holding the content.
				RectTransform child = chain[i - 1];
				cutHere = child.localPosition.y - child.rect.height * child.pivot.y;
				below = child;
			}
			foreach (Transform c in r)
			{
				RectTransform rc = c as RectTransform;
				if (!rc || c == below || rc.anchorMin.y != rc.anchorMax.y)
				{
					continue;
				}
				float a = rc.anchorMin.y;
				bool bottom = rc.localPosition.y < cutHere;
				float shift = (bottom ? ((0f - grow) * a) : (grow * (1f - a)));
				rc.anchoredPosition += new Vector2(0f, shift);
			}
			if (r.anchorMin.y == r.anchorMax.y)
			{
				r.sizeDelta += new Vector2(0f, grow);
				if (i < chain.Count - 1)
				{
					r.anchoredPosition += new Vector2(0f, grow * (r.pivot.y - r.anchorMin.y));
				}
			}
		}
	}

	private static string Dump(Transform t, int depth)
	{
		StringBuilder sb = new StringBuilder();
		RectTransform rt = t as RectTransform;
		sb.Append(new string(' ', depth * 2)).Append(t.name);
		if ((bool)rt)
		{
			sb.Append(" pos=").Append(rt.localPosition).Append(" size=").Append(rt.rect.size).Append(" anchors=").Append(rt.anchorMin)
				.Append("-")
				.Append(rt.anchorMax)
				.Append(" pivot=")
				.Append(rt.pivot);
		}
		if ((bool)t.GetComponent<Button>())
		{
			sb.Append(" [Button]");
		}
		if ((bool)t.GetComponent<Animation>())
		{
			sb.Append(" [Animation]");
		}
		sb.Append('\n');
		if (depth < 4)
		{
			foreach (Transform c in t)
			{
				sb.Append(Dump(c, depth + 1));
			}
		}
		return sb.ToString();
	}

	// ---- The game list, picking a game for one of our updates ----

	private static void OpenList(Menu_Dev_Addon menu, UpdateKind kind)
	{
		UIKit.Sound();
		string locked = LockReason(kind);
		if (locked != null)
		{
			Core.CopyProtection.CopyProtectDev.Report(locked);
			return;
		}
		roomScript room = AddonRoom(menu);
		menu.gameObject.SetActive(value: false);
		OpenListFor(kind, room);
	}

	// The game's update list, picking a game for one of our update types (also where our pages' Back leads).
	public static void OpenListFor(UpdateKind kind, roomScript room)
	{
		GameObject list = G.Gui.uiObjects[105];
		mode = kind;
		G.Gui.ActivateMenu(list);
		list.GetComponent<Menu_Dev_UpdateSelectGame>().Init(room, 0);
		SetTitle(list, Label(kind));
	}

	private static void ClearMode()
	{
		mode = null;
		RestoreTitles();
	}

	// The window title: the biggest text in the menu.
	private static void SetTitle(GameObject menu, string text)
	{
		Text title = menu.GetComponentsInChildren<Text>(includeInactive: true).Where((Text t) => !t.GetComponentInParent<ScrollRect>()).OrderByDescending((Text t) => t.fontSize).FirstOrDefault();
		if ((bool)title)
		{
			if (!titles.ContainsKey(title))
			{
				titles[title] = title.text;
			}
			title.text = text;
		}
	}

	private static void RestoreTitles()
	{
		foreach (KeyValuePair<Text, string> t in titles)
		{
			if ((bool)t.Key)
			{
				t.Key.text = t.Value;
			}
		}
		titles.Clear();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_UpdateSelectGame), "CheckGameData")]
	private static void OnlySuitable(gameScript script_, ref bool __result)
	{
		if (mode.HasValue && __result)
		{
			__result = GameUpdates.Problem(mode.Value, script_) == null;
		}
		// The game's own free update: MMOs get it in MMO Management.
		else if (!mode.HasValue && __result && (bool)script_ && script_.gameTyp == 1 && !script_.typ_mmoaddon && Core.Mmo.MmoOps.On)
		{
			__result = false;
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Item_DevGame_GameUpdate), "BUTTON_Click")]
	private static bool GamePicked(Item_DevGame_GameUpdate __instance)
	{
		if (!mode.HasValue)
		{
			return true;
		}
		UpdateKind kind = mode.Value;
		try
		{
			UIKit.Sound();
			GameObject list = G.Gui.uiObjects[105];
			roomScript room = __instance.rS_ ?? SelectRoom(list.GetComponent<Menu_Dev_UpdateSelectGame>());
			gameScript game = __instance.game_;
			ClearMode();
			list.SetActive(value: false);
			if (kind == UpdateKind.Port)
			{
				OpenPort(game, room);
				return false;
			}
			G.Gui.CloseMenu();
			EditorWindow.Updates.Open();
			Page page = kind switch
			{
				UpdateKind.Engine => new EngineUpdatePage(game.myID, room ? room.myID : (-1)),
				UpdateKind.Features => new FeatureUpdatePage(game.myID, room ? room.myID : (-1)),
				_ => new RatingUpdatePage(game.myID, room ? room.myID : (-1)),
			};
			EditorWindow.Updates.Push(page);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Opening the update failed: " + ex);
		}
		return false;
	}

	// ---- The change-platform screen as a port screen ----

	private static bool Porting(Menu_Dev_ChangePlatform m)
	{
		if ((bool)portGame && (bool)m)
		{
			return m.gS_ == portGame;
		}
		return false;
	}

	private static void OpenPort(gameScript game, roomScript room)
	{
		portGame = game;
		portRoom = room;
		GameObject menu = G.Gui.uiObjects[102];
		G.Gui.ActivateMenu(menu);
		menu.GetComponent<Menu_Dev_ChangePlatform>().Init(game);
		SetTitle(menu, Label(UpdateKind.Port));
	}

	private static void EndPort()
	{
		portGame = null;
		portRoom = null;
		RestoreTitles();
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_ChangePlatform), "Init")]
	private static void PortInit(Menu_Dev_ChangePlatform __instance)
	{
		if (Porting(__instance))
		{
			ShowPortPlan(__instance);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_ChangePlatform), "SetPlatform")]
	private static void PortPlatformSet(Menu_Dev_ChangePlatform __instance)
	{
		if (Porting(__instance))
		{
			ShowPortPlan(__instance);
		}
	}

	private static void ShowPortPlan(Menu_Dev_ChangePlatform m)
	{
		try
		{
			UpdatePlan plan = GameUpdates.PlanPort(portGame, m.g_GamePlatform);
			if (m.uiObjects.Length > 25 && (bool)m.uiObjects[25])
			{
				m.uiObjects[25].GetComponent<Text>().text = G.Money(plan.Cost);
			}
			if (m.uiObjects.Length > 28 && (bool)m.uiObjects[28])
			{
				string text = ((plan.Problem != null) ? "Add platforms of the same kind to the free slots. Platforms it's already on stay." : (plan.Notes + " Work: " + GameUpdates.Duration(plan.Work, portRoom, UpdateKind.Port) + " in " + Core.CopyProtection.CopyProtectDev.RoomName(portRoom) + "."));
				m.uiObjects[28].GetComponent<Text>().text = text;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Port plan: " + ex.Message);
		}
	}

	// ---- Which platform slots can be changed ----

	// Replaces the screen's own Update, which only sets which slots can be clicked. It used to be followed by our own
	// fixes, so a slot flipped off and on every frame and its colour flickered; now each slot is set once, to:
	// - porting a released game: free slots and ones whose platform left the market (platforms it's on stay);
	// - during development (the game's rule): slots whose platform left the market, plus ones whose platform isn't out
	//   yet (a console delayed by The Holy Expansion, or your own console still in development). Retro, exclusive
	//   and arcade games keep the game's lock on the other slots.
	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_Dev_ChangePlatform), "Update")]
	private static bool SlotButtons(Menu_Dev_ChangePlatform __instance)
	{
		gameScript g = __instance.gS_;
		if (!g)
		{
			return false;
		}
		try
		{
			bool porting = Porting(__instance);
			bool locked = !porting && (g.retro || g.exklusiv || g.arcade);
			for (int i = 0; i < 4 && i < g.gamePlatform.Length; i++)
			{
				GameObject slot = __instance.uiObjects[21 + i];
				Button b = (slot ? slot.GetComponent<Button>() : null);
				if (!b)
				{
					continue;
				}
				int id = g.gamePlatform[i];
				if (id > 0 && !g.gamePlatformScript[i])
				{
					g.FindMyPlatforms();
				}
				platformScript p = ((id > 0) ? g.gamePlatformScript[i] : null);
				bool want;
				if (porting)
				{
					bool taken = id != -1 && (!p || !p.vomMarktGenommen);
					want = !taken && GameUpdates.SlotUnlocked(i);
				}
				else if ((bool)p)
				{
					want = p.vomMarktGenommen || !p.isUnlocked;
				}
				else
				{
					// An empty slot: the screen's Init decides (researched slots), unless the game is locked.
					if (!locked)
					{
						continue;
					}
					want = false;
				}
				if (b.interactable != want)
				{
					b.interactable = want;
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Platform slots: " + ex.Message);
		}
		return false;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_ChangePlatform), "Init")]
	private static void UnreleasedHint(Menu_Dev_ChangePlatform __instance)
	{
		gameScript g = __instance.gS_;
		if (!g || Porting(__instance) || g.retro || g.exklusiv || __instance.uiObjects.Length <= 28 || !__instance.uiObjects[28])
		{
			return;
		}
		__instance.uiObjects[28].GetComponent<Text>().text = "You can replace platforms that have been taken off the market, or that aren't out yet (such as a delayed console).";
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_Dev_ChangePlatform), "BUTTON_Ok")]
	private static bool PortConfirmed(Menu_Dev_ChangePlatform __instance)
	{
		if (!Porting(__instance))
		{
			return true;
		}
		try
		{
			string problem = PortCheck(__instance);
			if (problem != null)
			{
				G.Gui.MessageBox(problem, closeMenu: false);
				return false;
			}
			gameScript g = portGame;
			UpdatePlan plan = GameUpdates.PlanPort(g, __instance.g_GamePlatform);
			UpdateProject project = new UpdateProject
			{
				Kind = UpdateKind.Port,
				Platforms = (int[])__instance.g_GamePlatform.Clone()
			};
			string error = GameUpdates.Start(project, plan, portRoom, g);
			if (error != null)
			{
				G.Gui.MessageBox(error, closeMenu: false);
				return false;
			}
			UIKit.Sound();
			__instance.gameObject.SetActive(value: false);
			G.Gui.CloseMenu();
			EndPort();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Starting the port failed: " + ex);
		}
		return false;
	}

	// The game's own checks from BUTTON_Ok: online features need online platforms, and platforms' required features.
	private static string PortCheck(Menu_Dev_ChangePlatform m)
	{
		gameScript g = portGame;
		gameplayFeatures gf = GameUpdates.GF;
		List<platformScript> chosen = m.g_GamePlatform.Where((int id) => id != -1).Select(GameUpdates.Platform).Where((platformScript p) => (bool)p).ToList();
		if (chosen.Any((platformScript p) => !p.internet))
		{
			string online = "";
			for (int j = 0; j < g.gameGameplayFeatures.Length; j++)
			{
				if (g.gameGameplayFeatures[j] && gf.gameplayFeatures_INTERNET[j])
				{
					online = online + "\n<color=blue>" + gf.GetName(j) + "</color>";
				}
			}
			if (online.Length > 0)
			{
				return G.Text(2198, "These features need platforms with internet:") + online;
			}
			if (g.gameTyp == 1 || g.gameTyp == 2)
			{
				return G.Text(1262, "Online games need platforms with internet.");
			}
		}
		string missing = "";
		foreach (platformScript p in chosen)
		{
			if (p.needFeatures == null)
			{
				continue;
			}
			foreach (int need in p.needFeatures)
			{
				if (need != -1 && !g.gameGameplayFeatures[need])
				{
					missing = missing + p.GetName() + ": " + gf.GetName(need) + "\n";
				}
			}
		}
		if (missing.Length > 0)
		{
			return G.Text(1020, "These platforms need features the game doesn't have:") + "\n<color=blue>" + missing + "</color>";
		}
		return null;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Dev_ChangePlatform), "BUTTON_Close")]
	private static void PortClosed()
	{
		EndPort();
	}

	// A manufacturer-exclusive game only lists your own platforms; a port may go anywhere (it ends the exclusivity).
	[HarmonyPrefix]
	[HarmonyPatch(typeof(Menu_DevGame_Platform), "SetData")]
	private static void PickerBefore(out bool __state)
	{
		__state = false;
		if ((bool)portGame && portGame.herstellerExklusiv && (bool)G.Gui.uiObjects[102] && G.Gui.uiObjects[102].activeSelf)
		{
			portGame.herstellerExklusiv = false;
			__state = true;
		}
	}

	[HarmonyFinalizer]
	[HarmonyPatch(typeof(Menu_DevGame_Platform), "SetData")]
	private static Exception PickerAfter(bool __state, Exception __exception)
	{
		if (__state && (bool)portGame)
		{
			portGame.herstellerExklusiv = true;
		}
		return __exception;
	}
}
