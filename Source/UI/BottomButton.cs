using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HolyAddon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// Our bottom bar buttons (Editor, Contracts, Industry, Rooms Overview, MMO Management, Your Digital Store): copies of
// the Statistics button, placed after the last button on the bar, so they land to the right of The Holy Expansion's
// Social Media button, or of Statistics without it. A button that isn't available yet is darkened, like Holy's: faded
// icon, no click, and the reason in red in its tooltip.
// An arrow button before them shows or hides them (remembered). Where they'd cover the game's speed buttons (narrow
// screens, 1366 wide), they sit in a row just above the bar instead.
internal static class BottomButton
{
	private static GameObject toggle;

	private static Image toggleArrow;

	private static GameObject tray;

	private static RectTransform pause;

	private static bool Expanded => Plugin.BottomBarExpanded.Value;
	private class Entry
	{
		public string Id;

		public Func<string> Tooltip;

		public Func<Sprite> Icon;

		public EditorWindow Window;

		public Func<bool> Wanted;

		public Func<string> LockReason;

		public string ShownLock = "";

		public Sprite ShownIcon;

		public GameObject Button;
	}

	private static readonly List<Entry> entries = new List<Entry>
	{
		new Entry
		{
			Id = "Editor",
			Tooltip = () => "<b>Holy Unofficial Addon</b>\nEdit your employees, change game settings and more.",
			Icon = () => UIKit.Sprite(Plugin.ButtonIcon.Value) ?? UIKit.EditorIcon() ?? UIKit.Sprite("iconOptions"),
			Window = EditorWindow.Main,
			Wanted = () => true
		},
		new Entry
		{
			Id = "Contracts",
			Tooltip = () => "<b>Contracts</b>\nHire outside studios to research for you or work on your games in development.",
			Icon = () => UIKit.LoadSprite(Path.Combine(Plugin.PluginDir, "contracts.png")) ?? UIKit.Sprite("iconAuftragsarbeit"),
			Window = EditorWindow.Contracts,
			Wanted = () => Plugin.ContractsEnabled.Value
		},
		new Entry
		{
			Id = "Industry",
			Tooltip = () => "<b>Industry</b>\nEvery studio and where you stand with it, the latest industry news, and your deals.",
			Icon = () => Pages.IndustryUi.Icon,
			Window = EditorWindow.Industry,
			Wanted = () => Plugin.IndustryEvents.Value
		},
		new Entry
		{
			Id = "Rooms",
			Tooltip = () => "<b>Rooms Overview</b>\nEvery room's job, what it does by itself, who supports whom, and what needs a look. Click a room to go to it.",
			Icon = () => Pages.RoomsOverviewPage.RoomIcon,
			Window = EditorWindow.Rooms,
			Wanted = () => true
		},
		new Entry
		{
			Id = "Mmo",
			Tooltip = () => "<b>MMO Management</b>\nModernise your MMOs, grow them, release expansion packs and keep their players. Also the MMO subscription charts.",
			Icon = Core.Updates.UpdateIcons.Mmo,
			Window = EditorWindow.Mmo,
			Wanted = () => Plugin.MmoManagement.Value,
			LockReason = MmoLock
		},
		new Entry
		{
			Id = "Store",
			Tooltip = () => Core.Store.StoreOps.State.Built ? ("<b>" + Core.Store.StoreOps.State.Name + "</b>\nYour digital PC store: accounts, commission, catalogue, features and the rival stores.") : "<b>Your Digital Store</b>\nBuild your own digital PC store, take a cut of other studios' sales and compete with the rival stores.",
			Icon = () => Core.Store.StoreOps.State.Built ? Core.Updates.UpdateIcons.StoreLogo(Core.Store.StoreOps.State.Logo) : Core.Updates.UpdateIcons.Store(),
			Window = EditorWindow.Store,
			Wanted = () => Plugin.StoreEnabled.Value,
			LockReason = () => Core.Store.StoreOps.Open ? null : ("Digital stores arrive in " + Core.Store.StoreOps.OpenYear + ".")
		}
	};

	// MMO Management opens up with the game's own MMO unlock ("MMOs and server room unlocked").
	private static string MmoLock()
	{
		unlockScript u = (G.MainObj ? G.MainObj.GetComponent<unlockScript>() : null);
		if (!u || u.unlock == null || u.unlock.Length <= 21 || u.unlock[21])
		{
			return null;
		}
		gameplayFeatures gf = Core.Updates.GameUpdates.GF;
		string feature = ((gf != null) ? gf.GetName(23) : "Massively Multiplayer Online");
		return "Unlocks with MMOs: research the gameplay feature \"" + feature + "\".";
	}

	private static RectTransform stats;

	private static RectTransform publishing;

	private static float nextTry;

	private static float nextLayout;

	public static bool Available => G.Singleplayer;

	// The game's bottom bar is showing (it's hidden in some full-screen modes and menus).
	public static bool BarVisible
	{
		get
		{
			if ((bool)stats)
			{
				return stats.gameObject.activeInHierarchy;
			}
			return false;
		}
	}

	// The Editor hotkey.
	public static void Toggle()
	{
		Toggle(entries[0]);
	}

	private static void Toggle(Entry entry)
	{
		if (entry.LockReason?.Invoke() != null)
		{
			return;
		}
		EditorWindow window = entry.Window;
		if (window.IsOpen)
		{
			window.Close();
		}
		else if (Available && (bool)G.Gui && !G.Gui.menuOpen)
		{
			window.Open();
		}
	}

	public static void Tick()
	{
		if (!stats || !publishing || !toggle || !tray || entries.Any((Entry e) => !e.Button))
		{
			if (Time.unscaledTime < nextTry)
			{
				return;
			}
			nextTry = Time.unscaledTime + 1f;
			if (!Inject())
			{
				return;
			}
		}
		bool interactable = !G.Gui.menuOpen;
		bool slow = Time.unscaledTime >= nextLayout;
		bool anyWanted = Available && entries.Any((Entry e) => e.Wanted());
		if (toggle.activeSelf != anyWanted)
		{
			toggle.SetActive(anyWanted);
		}
		Button toggleButton = toggle.GetComponent<Button>();
		if (toggleButton.interactable != interactable)
		{
			toggleButton.interactable = interactable;
		}
		foreach (Entry entry in entries)
		{
			bool show = Available && entry.Wanted() && Expanded;
			if (entry.Button.activeSelf != show)
			{
				entry.Button.SetActive(show);
			}
			if (!show)
			{
				continue;
			}
			string locked = (slow ? entry.LockReason?.Invoke() : ((entry.ShownLock == "") ? null : entry.ShownLock));
			if (slow && locked != ((entry.ShownLock == "") ? null : entry.ShownLock))
			{
				entry.ShownLock = locked ?? "";
				Darken(entry, locked != null);
				UIKit.SetTooltip(entry.Button, entry.Tooltip() + ((locked != null) ? ("\n<color=red>" + locked + "</color>") : ""));
			}
			if (slow)
			{
				Sprite sprite = entry.Icon();
				if ((bool)sprite && sprite != entry.ShownIcon)
				{
					entry.ShownIcon = sprite;
					Image icon = IconOf(entry.Button);
					if ((bool)icon)
					{
						icon.sprite = sprite;
					}
					UIKit.SetTooltip(entry.Button, entry.Tooltip() + ((locked != null) ? ("\n<color=red>" + locked + "</color>") : ""));
				}
			}
			Button component = entry.Button.GetComponent<Button>();
			bool want = interactable && locked == null;
			if (component.interactable != want)
			{
				component.interactable = want;
			}
		}
		if (slow)
		{
			nextLayout = Time.unscaledTime + 0.5f;
			Layout();
		}
	}

	private static Image IconOf(GameObject button)
	{
		return button.GetComponentsInChildren<Image>(includeInactive: true).FirstOrDefault((Image im) => im.gameObject != button && im.name.StartsWith("Icon"));
	}

	// A locked button: faded icon (as The Holy Expansion does it).
	private static void Darken(Entry entry, bool locked)
	{
		foreach (Image image in entry.Button.GetComponentsInChildren<Image>(includeInactive: true))
		{
			if (image.name.StartsWith("Icon"))
			{
				image.color = (locked ? new Color(1f, 1f, 1f, 0.3f) : Color.white);
			}
		}
	}

	private static Button GameButton(string method)
	{
		return UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault((Button b) => b.onClick.GetPersistentEventCount() > 0 && b.onClick.GetPersistentMethodName(0) == method && b.onClick.GetPersistentTarget(0) is GUI_MainButtons);
	}

	private static bool Inject()
	{
		Button statsButton = GameButton("BUTTON_Statistics");
		Button publishingButton = GameButton("BUTTON_Publishing");
		if (!statsButton || !publishingButton)
		{
			return false;
		}
		stats = (RectTransform)statsButton.transform;
		publishing = (RectTransform)publishingButton.transform;
		Button pauseButton = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault((Button b) => b.name == "ButtonSpeed0");
		pause = (pauseButton ? ((RectTransform)pauseButton.transform) : null);
		if (!toggle)
		{
			toggle = CloneToggle(statsButton);
		}
		if (!tray)
		{
			tray = MakeTray();
		}
		foreach (Entry entry in entries)
		{
			if (!entry.Button)
			{
				entry.Button = Clone(entry, statsButton);
			}
		}
		Layout();
		return true;
	}

	// The arrow button: the game's own play arrow, pointing right to show the addon's buttons, left to hide them.
	private static GameObject CloneToggle(Button statsButton)
	{
		GameObject go = UnityEngine.Object.Instantiate(statsButton.gameObject, stats.parent, worldPositionStays: false);
		go.name = "Editor_Button_Toggle";
		Button component = go.GetComponent<Button>();
		component.onClick = new Button.ButtonClickedEvent();
		foreach (Transform child in go.transform.Cast<Transform>().ToList())
		{
			if ((bool)child.GetComponent<Button>() || child.name.StartsWith("Dropdown"))
			{
				UnityEngine.Object.DestroyImmediate(child.gameObject);
			}
		}
		foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
		{
			if (mb.GetType().Namespace == null && !(mb is tooltip))
			{
				UnityEngine.Object.DestroyImmediate(mb);
			}
		}
		toggleArrow = IconOf(go);
		Button play = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault((Button b) => b.name == "ButtonSpeed1");
		Image playIcon = (play ? SpeedButtons.IconOf((RectTransform)play.transform, G.Gui) : null);
		if ((bool)toggleArrow && (bool)playIcon && (bool)playIcon.sprite)
		{
			toggleArrow.sprite = playIcon.sprite;
			toggleArrow.preserveAspect = true;
			toggleArrow.color = Color.white;
		}
		tooltip tip = go.GetComponent<tooltip>();
		if ((bool)tip)
		{
			tip.shortcut = KeyCode.None;
		}
		component.onClick.AddListener(() =>
		{
			Plugin.BottomBarExpanded.Value = !Plugin.BottomBarExpanded.Value;
			UIKit.Sound();
			nextLayout = 0f;
		});
		go.transform.SetAsLastSibling();
		RefreshToggle(go);
		return go;
	}

	private static void RefreshToggle(GameObject go)
	{
		if ((bool)toggleArrow)
		{
			Vector3 s = toggleArrow.rectTransform.localScale;
			float x = (Expanded ? (0f - Mathf.Abs(s.x)) : Mathf.Abs(s.x));
			if (Mathf.Abs(s.x - x) > 0.001f)
			{
				toggleArrow.rectTransform.localScale = new Vector3(x, s.y, s.z);
			}
		}
		UIKit.SetTooltip(go, Expanded ? "<b>Hide the addon's buttons</b>" : "<b>Show the addon's buttons</b>\nThe addon's window, Contracts, Industry, Rooms Overview, MMO Management and Your Digital Store.");
	}

	// The backing for the row above the bar, in the bar's own look where it can be found.
	private static GameObject MakeTray()
	{
		GameObject go = new GameObject("Editor_BarTray", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(stats.parent, worldPositionStays: false);
		RectTransform rt = (RectTransform)go.transform;
		rt.anchorMin = stats.anchorMin;
		rt.anchorMax = stats.anchorMax;
		rt.pivot = new Vector2(0.5f, 0.5f);
		Image image = go.GetComponent<Image>();
		Image bar = null;
		Transform level = stats.parent;
		for (int i = 0; i < 3 && (bool)level; i++)
		{
			Image candidate = level.GetComponent<Image>();
			if ((bool)candidate && (bool)candidate.sprite)
			{
				bar = candidate;
				break;
			}
			level = level.parent;
		}
		if ((bool)bar)
		{
			image.sprite = bar.sprite;
			image.type = bar.type;
			image.color = bar.color;
		}
		else
		{
			image.color = new Color(0.92f, 0.92f, 0.92f, 0.96f);
		}
		go.transform.SetAsFirstSibling();
		go.SetActive(value: false);
		return go;
	}

	private static GameObject Clone(Entry entry, Button statsButton)
	{
		GameObject go = UnityEngine.Object.Instantiate(statsButton.gameObject, stats.parent, worldPositionStays: false);
		go.name = "Editor_Button_" + entry.Id;
		// The new button must not answer to the Statistics hotkey or open the Statistics dropdown.
		Button component = go.GetComponent<Button>();
		component.onClick = new Button.ButtonClickedEvent();
		foreach (Transform child in go.transform.Cast<Transform>().ToList())
		{
			if ((bool)child.GetComponent<Button>() || child.name.StartsWith("Dropdown"))
			{
				UnityEngine.Object.DestroyImmediate(child.gameObject);
			}
		}
		foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
		{
			if (mb.GetType().Namespace == null && !(mb is tooltip))
			{
				UnityEngine.Object.DestroyImmediate(mb);
			}
		}
		Image icon = IconOf(go);
		Sprite sprite = entry.Icon();
		if ((bool)icon && (bool)sprite)
		{
			icon.sprite = sprite;
			icon.preserveAspect = true;
			entry.ShownIcon = sprite;
		}
		entry.ShownLock = "";
		UIKit.SetTooltip(go, entry.Tooltip());
		tooltip tip = go.GetComponent<tooltip>();
		if ((bool)tip)
		{
			tip.shortcut = KeyCode.None;
		}
		component.onClick.AddListener(() =>
		{
			try
			{
				Toggle(entry);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError(entry.Id + " button failed: " + ex);
			}
		});
		go.transform.SetAsLastSibling();
		Plugin.Log.LogInfo(entry.Id + " button added to the bottom bar");
		return go;
	}

	// Re-run every half second: other mods add or hide their own buttons whenever they like.
	private static void Layout()
	{
		Vector2 step = stats.anchoredPosition - publishing.anchoredPosition;
		HashSet<Transform> ours = new HashSet<Transform>(from e in entries
			where (bool)e.Button
			select e.Button.transform);
		if ((bool)toggle)
		{
			ours.Add(toggle.transform);
			RefreshToggle(toggle);
		}
		if ((bool)tray)
		{
			ours.Add(tray.transform);
		}
		RectTransform last = stats;
		foreach (Transform child in stats.parent)
		{
			if (ours.Contains(child) || !child.gameObject.activeSelf || !(child is RectTransform rt))
			{
				continue;
			}
			if (Vector2.Dot(rt.anchoredPosition - last.anchoredPosition, step) > 0.01f)
			{
				last = rt;
			}
		}
		Vector2 next = last.anchoredPosition + step;
		if ((bool)toggle && toggle.activeSelf)
		{
			Place((RectTransform)toggle.transform, next);
			next += step;
		}
		List<RectTransform> shown = (from e in entries
			where (bool)e.Button && e.Button.activeSelf
			select (RectTransform)e.Button.transform).ToList();
		// On the bar, as long as they stay clear of the speed buttons.
		for (int i = 0; i < shown.Count; i++)
		{
			Place(shown[i], next + step * (float)i);
			if (shown[i].localScale != Vector3.one)
			{
				shown[i].localScale = Vector3.one;
			}
		}
		bool fits = shown.Count == 0 || !pause || !pause.gameObject.activeInHierarchy || SpeedButtons.WorldRect(shown[shown.Count - 1]).xMax <= SpeedButtons.WorldRect(pause).xMin - 2f;
		if (fits)
		{
			if ((bool)tray && tray.activeSelf)
			{
				tray.SetActive(value: false);
			}
			return;
		}
		// If the bar clips what's drawn outside it, a row above it wouldn't show: squeeze them onto the bar instead.
		if (Clipped())
		{
			float room = SpeedButtons.WorldRect(pause).xMin - 4f - SpeedButtons.WorldRect(shown[0]).xMin;
			float need = SpeedButtons.WorldRect(shown[shown.Count - 1]).xMax - SpeedButtons.WorldRect(shown[0]).xMin;
			float k = Mathf.Clamp((need > 0f) ? (room / need) : 1f, 0.5f, 1f);
			for (int k2 = 0; k2 < shown.Count; k2++)
			{
				Place(shown[k2], next + step * k * (float)k2);
				shown[k2].localScale = new Vector3(k, k, 1f);
			}
			return;
		}
		// Otherwise in a row just above the bar, starting over the arrow button.
		Vector2 start = (((bool)toggle && toggle.activeSelf) ? ((RectTransform)toggle.transform).anchoredPosition : (next - step)) + new Vector2(0f, stats.rect.height + 10f);
		for (int j = 0; j < shown.Count; j++)
		{
			Place(shown[j], start + step * (float)j);
		}
		if ((bool)tray)
		{
			RectTransform t = (RectTransform)tray.transform;
			Vector2 first = Centre(shown[0]);
			Vector2 lastCentre = Centre(shown[shown.Count - 1]);
			float pad = 6f;
			t.anchorMin = stats.anchorMin;
			t.anchorMax = stats.anchorMax;
			t.anchoredPosition = (first + lastCentre) / 2f;
			t.sizeDelta = new Vector2(Mathf.Abs(lastCentre.x - first.x) + shown[0].rect.width + pad * 2f, shown[0].rect.height + pad * 2f);
			if (!tray.activeSelf)
			{
				tray.SetActive(value: true);
			}
		}
	}

	private static bool? clipped;

	// Whether the bar (or what holds it) masks its contents to its own area.
	private static bool Clipped()
	{
		if (!clipped.HasValue)
		{
			bool found = false;
			Transform level = stats.parent;
			for (int i = 0; i < 4 && (bool)level; i++)
			{
				if ((bool)level.GetComponent<RectMask2D>() || (bool)level.GetComponent<Mask>())
				{
					found = true;
					break;
				}
				level = level.parent;
			}
			clipped = found;
			Plugin.Log.LogInfo("Bottom bar: the addon's buttons " + (found ? "squeeze onto the bar" : "move to a row above the bar") + " when there's no room for them");
		}
		return clipped.Value;
	}

	private static void Place(RectTransform rt, Vector2 at)
	{
		if ((rt.anchoredPosition - at).sqrMagnitude > 0.01f)
		{
			rt.anchoredPosition = at;
		}
	}

	// A button's centre in its parent's anchored space (its pivot may not be the centre).
	private static Vector2 Centre(RectTransform rt)
	{
		return rt.anchoredPosition + new Vector2((0.5f - rt.pivot.x) * rt.rect.width, (0.5f - rt.pivot.y) * rt.rect.height);
	}
}
