using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HolyAddon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// Our bottom bar buttons (Editor, Contracts, MMO Management, Your digital store): copies of the Statistics button,
// placed after the last button on the bar, so they land to the right of The Holy Expansion's Social Media button, or
// of Statistics without it. A button that isn't available yet is darkened, like Holy's: faded icon, no click, and the
// reason in red in its tooltip.
internal static class BottomButton
{
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
			Tooltip = () => Core.Store.StoreOps.State.Built ? ("<b>" + Core.Store.StoreOps.State.Name + "</b>\nYour digital PC store: accounts, commission, catalogue, features and the rival stores.") : "<b>Your digital store</b>\nBuild your own digital PC store, take a cut of other studios' sales and compete with the rival stores.",
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
		if (!stats || !publishing || entries.Any((Entry e) => !e.Button))
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
		foreach (Entry entry in entries)
		{
			bool show = Available && entry.Wanted();
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
		foreach (Entry entry in entries)
		{
			if (!entry.Button || !entry.Button.activeSelf)
			{
				continue;
			}
			RectTransform mine = (RectTransform)entry.Button.transform;
			if ((mine.anchoredPosition - next).sqrMagnitude > 0.01f)
			{
				mine.anchoredPosition = next;
			}
			next += step;
		}
	}
}
