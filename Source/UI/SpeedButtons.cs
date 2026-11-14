using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HolyAddon.UI;

// Turbo and Ultra speed buttons next to the game's pause / play / fast buttons (ButtonSpeed0-2, bottom right).
// The game's speed controls (and the date next to them) slide left to make room, so nothing on the right moves.
[HarmonyPatch]
internal static class SpeedButtons
{
	private class Extra
	{
		public string Name;

		public Func<float> Speed;

		public Func<KeyCode> Key;

		public int Arrows;

		public GameObject Button;

		public GameObject Indicator;
	}

	private static readonly List<Extra> extras = new List<Extra>
	{
		new Extra
		{
			Name = "Turbo",
			Speed = () => Mathf.Clamp(Plugin.TurboSpeed.Value, 4f, 30f),
			Key = () => Plugin.TurboKey.Value,
			Arrows = 3
		},
		new Extra
		{
			Name = "Ultra",
			Speed = () => Mathf.Clamp(Plugin.UltraSpeed.Value, 6f, 30f),
			Key = () => Plugin.UltraKey.Value,
			Arrows = 4
		}
	};

	private static RectTransform fast;

	private static float nextTry;

	private static bool unsupported;

	private static bool Wanted
	{
		get
		{
			if (Plugin.ExtraSpeedButtons.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static void Tick()
	{
		if (!fast || extras.Any((Extra e) => !e.Button))
		{
			if (unsupported || !Wanted || Time.unscaledTime < nextTry)
			{
				return;
			}
			nextTry = Time.unscaledTime + 2f;
			Inject();
			return;
		}
		bool wanted = Wanted;
		foreach (Extra extra in extras)
		{
			if (extra.Button.activeSelf != wanted)
			{
				extra.Button.SetActive(wanted);
			}
			tooltip tip = extra.Button.GetComponent<tooltip>();
			if ((bool)tip)
			{
				tip.shortcut = extra.Key();
				tip.c = "<b>" + extra.Name + " speed</b> (" + extra.Speed().ToString("0.#") + "x)";
			}
		}
	}

	private static Button Find(string name)
	{
		return UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault((Button b) => b.name == name);
	}

	private static void Inject()
	{
		Button pause = Find("ButtonSpeed0");
		Button play = Find("ButtonSpeed1");
		Button fastButton = Find("ButtonSpeed2");
		if (!pause || !play || !fastButton)
		{
			return;
		}
		RectTransform b0 = (RectTransform)pause.transform;
		RectTransform b1 = (RectTransform)play.transform;
		RectTransform b2 = (RectTransform)fastButton.transform;
		Transform parent = b2.parent;
		if (b0.parent != parent || b1.parent != parent)
		{
			Plugin.Log.LogWarning("Speed buttons are not side by side, extra speed buttons skipped");
			LogLayout(b2);
			unsupported = true;
			return;
		}
		LogLayout(b2);
		GUI_Main gui = G.Gui;
		GameObject fastIndicator = (((bool)gui && gui.uiObjects.Length > 14) ? gui.uiObjects[14] : null);
		Vector3 step = b2.position - b1.position;
		bool layoutGroup = (bool)parent.GetComponent<LayoutGroup>();
		if (!layoutGroup && !b2.GetComponent<SpeedShiftMarker>())
		{
			MakeRoom(b0, b1, b2, step, extras.Count, gui);
			b2.gameObject.AddComponent<SpeedShiftMarker>();
		}
		Image playIcon = IconOf(b1, gui);
		GameObject playIndicator = (((bool)gui && gui.uiObjects.Length > 13) ? gui.uiObjects[13] : null);
		Sprite playOn = SpriteOf(playIndicator);
		Sprite fastOn = SpriteOf(fastIndicator);
		// If the game's "on" highlights are coloured copies of the arrows (not a shared glow), ours need arrows too.
		bool indicatorIsIcon = (bool)playOn && (bool)fastOn && playOn != fastOn;
		for (int i = 0; i < extras.Count; i++)
		{
			Extra extra = extras[i];
			if ((bool)extra.Button)
			{
				UnityEngine.Object.Destroy(extra.Button);
			}
			if ((bool)extra.Indicator)
			{
				UnityEngine.Object.Destroy(extra.Indicator);
			}
			GameObject go = UnityEngine.Object.Instantiate(b2.gameObject, parent, worldPositionStays: false);
			go.name = "Editor_Speed" + extra.Name;
			go.transform.SetSiblingIndex(b2.GetSiblingIndex() + 1 + i);
			if (!layoutGroup)
			{
				go.transform.position = b2.position + step * (i + 1);
			}
			Button button = go.GetComponent<Button>();
			button.onClick = new Button.ButtonClickedEvent();
			foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
			{
				if (mb is SpeedShiftMarker || (mb.GetType().Namespace == null && !(mb is tooltip)))
				{
					UnityEngine.Object.DestroyImmediate(mb);
				}
			}
			extra.Indicator = IndicatorFor(go, b2, fastIndicator);
			Image icon = IconOf((RectTransform)go.transform, gui, extra.Indicator);
			List<Image> arrows = Arrows(icon, playIcon ? playIcon.sprite : null, extra.Arrows);
			if (arrows == null)
			{
				Badge(go, extra.Speed().ToString("0.#") + "x");
			}
			else if (icon.gameObject == go)
			{
				// The arrows replaced the button's own image, so give them the button's hover / press tint.
				ArrowTint tint = go.AddComponent<ArrowTint>();
				tint.Init(button, arrows);
				button.transition = Selectable.Transition.None;
			}
			if (indicatorIsIcon && (bool)extra.Indicator)
			{
				Arrows(extra.Indicator.GetComponent<Image>(), playOn, extra.Arrows);
			}
			UIKit.SetTooltip(go, "<b>" + extra.Name + " speed</b>");
			go.GetComponent<tooltip>().shortcut = extra.Key();
			Extra captured = extra;
			button.onClick.AddListener(() =>
			{
				try
				{
					if ((bool)G.Gui)
					{
						G.Gui.BUTTON_GameSpeed(captured.Speed());
					}
				}
				catch (Exception ex)
				{
					Plugin.Log.LogError("Speed button failed: " + ex);
				}
			});
			extra.Button = go;
		}
		fast = b2;
		Plugin.Log.LogInfo("Turbo and Ultra speed buttons added");
	}

	// Slide pause / play / fast, the game's speed indicators and anything right up against them on the left
	// (the date and week bar) left by the width of the new buttons.
	private static void MakeRoom(RectTransform b0, RectTransform b1, RectTransform b2, Vector3 step, int count, GUI_Main gui)
	{
		Rect fastRect = WorldRect(b2);
		float buttonWidth = Mathf.Max(1f, fastRect.width);
		List<Transform> members = new List<Transform> { b0, b1, b2 };
		List<Transform> candidates = new List<Transform>();
		foreach (Transform level in new Transform[2] { b2.parent, b2.parent.parent })
		{
			if (!level)
			{
				continue;
			}
			foreach (Transform child in level)
			{
				if (!members.Contains(child) && child != b2.parent && child is RectTransform)
				{
					candidates.Add(child);
				}
			}
		}
		float left = WorldRect(b0).xMin;
		bool added = true;
		while (added)
		{
			added = false;
			foreach (Transform candidate in candidates)
			{
				if (members.Contains(candidate))
				{
					continue;
				}
				Rect r = WorldRect((RectTransform)candidate);
				bool sameRow = r.yMax > fastRect.yMin && r.yMin < fastRect.yMax;
				bool small = r.width > 0f && r.width < buttonWidth * 8f;
				bool touching = r.xMax <= fastRect.xMax + 1f && r.xMax >= left - buttonWidth;
				if (sameRow && small && touching)
				{
					members.Add(candidate);
					left = Mathf.Min(left, r.xMin);
					added = true;
				}
			}
		}
		if ((bool)gui)
		{
			for (int i = 12; i <= 14 && i < gui.uiObjects.Length; i++)
			{
				Transform indicator = ((bool)gui.uiObjects[i]) ? gui.uiObjects[i].transform : null;
				if ((bool)indicator && !members.Any((Transform m) => indicator.IsChildOf(m)))
				{
					members.Add(indicator);
				}
			}
		}
		// The date panel (Canvas_Time) also holds the divider to the right of the speed buttons; that must stay put.
		List<Transform> stay = new List<Transform>();
		foreach (Transform member in members)
		{
			if (member == b0 || member == b1 || member == b2)
			{
				continue;
			}
			foreach (RectTransform part in member.GetComponentsInChildren<RectTransform>(includeInactive: true))
			{
				if (part != member && WorldRect(part).xMin >= fastRect.xMax - 2f && !stay.Any((Transform s) => part.IsChildOf(s)))
				{
					stay.Add(part);
				}
			}
		}
		foreach (Transform member in members)
		{
			if (!members.Any((Transform m) => m != member && member.IsChildOf(m)))
			{
				member.position -= step * count;
			}
		}
		foreach (Transform part in stay)
		{
			part.position += step * count;
		}
		Plugin.Log.LogInfo("Speed bar: moved " + string.Join(", ", members.Select((Transform m) => m.name)) + ((stay.Count > 0) ? ("; left in place " + string.Join(", ", stay.Select((Transform s) => s.name))) : ""));
	}

	// The game's "fast is on" highlight (GUI_Main.uiObjects[14]) copied for a new button.
	private static GameObject IndicatorFor(GameObject button, RectTransform fastButton, GameObject fastIndicator)
	{
		if (!fastIndicator)
		{
			return null;
		}
		if (fastIndicator.transform.IsChildOf(fastButton))
		{
			Transform copy = button.transform.Find(PathFrom(fastButton, fastIndicator.transform));
			return ((bool)copy) ? copy.gameObject : null;
		}
		GameObject go = UnityEngine.Object.Instantiate(fastIndicator, fastIndicator.transform.parent, worldPositionStays: true);
		go.name = button.name + "_On";
		go.transform.position = button.transform.position + (fastIndicator.transform.position - fastButton.position);
		go.SetActive(value: false);
		return go;
	}

	private static string PathFrom(Transform root, Transform t)
	{
		List<string> list = new List<string>();
		while ((bool)t && t != root)
		{
			list.Insert(0, t.name);
			t = t.parent;
		}
		return string.Join("/", list);
	}

	// The arrow image of a speed button: a child image that is not one of the game's on/off indicators,
	// or else the button's own image (the game's speed buttons are drawn that way).
	internal static Image IconOf(RectTransform button, GUI_Main gui, GameObject skip = null)
	{
		HashSet<GameObject> indicators = new HashSet<GameObject>();
		if ((bool)gui)
		{
			for (int i = 12; i <= 14 && i < gui.uiObjects.Length; i++)
			{
				if ((bool)gui.uiObjects[i])
				{
					indicators.Add(gui.uiObjects[i]);
				}
			}
		}
		if ((bool)skip)
		{
			indicators.Add(skip);
		}
		Image child = (from im in button.GetComponentsInChildren<Image>(includeInactive: true)
			where im.gameObject != button.gameObject && (bool)im.sprite && !indicators.Any((GameObject ind) => im.transform.IsChildOf(ind.transform))
			orderby im.rectTransform.rect.width * im.rectTransform.rect.height descending
			select im).FirstOrDefault();
		if ((bool)child)
		{
			return child;
		}
		Image own = button.GetComponent<Image>();
		if (!own || !own.sprite)
		{
			return null;
		}
		return own;
	}

	private static Sprite SpriteOf(GameObject go)
	{
		Image image = ((bool)go) ? go.GetComponent<Image>() : null;
		if (!image)
		{
			return null;
		}
		return image.sprite;
	}

	// Draw a row of the game's own ">" arrows over an image and hide the image itself (it stays clickable).
	private static List<Image> Arrows(Image icon, Sprite arrowSprite, int count)
	{
		if (!icon || !arrowSprite)
		{
			return null;
		}
		RectTransform rt = icon.rectTransform;
		GameObject strip = new GameObject("Arrows", typeof(RectTransform));
		RectTransform s = (RectTransform)strip.transform;
		s.SetParent(rt, worldPositionStays: false);
		s.SetSiblingIndex(0);
		UIKit.Stretch(s);
		Color color = icon.color;
		icon.color = new Color(color.r, color.g, color.b, 0f);
		List<Image> arrows = new List<Image>();
		Rect area = rt.rect;
		Rect spriteRect = arrowSprite.rect;
		float aspect = spriteRect.width / Mathf.Max(1f, spriteRect.height);
		float overlap = 0.62f;
		float w = area.width / (1f + overlap * (float)(count - 1));
		float h = w / aspect;
		if (h > area.height)
		{
			h = area.height;
			w = h * aspect;
		}
		float total = w + w * overlap * (float)(count - 1);
		for (int i = 0; i < count; i++)
		{
			GameObject arrow = new GameObject("Arrow" + i, typeof(RectTransform), typeof(Image));
			RectTransform a = (RectTransform)arrow.transform;
			a.SetParent(s, worldPositionStays: false);
			a.anchorMin = a.anchorMax = new Vector2(0.5f, 0.5f);
			a.pivot = new Vector2(0.5f, 0.5f);
			a.sizeDelta = new Vector2(w, h);
			a.anchoredPosition = new Vector2((0f - total) / 2f + w / 2f + w * overlap * (float)i, 0f);
			Image image = arrow.GetComponent<Image>();
			image.sprite = arrowSprite;
			image.color = color;
			image.material = icon.material;
			image.preserveAspect = true;
			image.raycastTarget = false;
			arrows.Add(image);
		}
		return arrows;
	}

	// Fallback when the button's arrow image can't be found: a small "6x" label in the corner.
	private static void Badge(GameObject button, string text)
	{
		Text font = ((bool)G.Gui) ? G.Gui.GetComponentInChildren<Text>(includeInactive: true) : null;
		if (!font)
		{
			return;
		}
		GameObject go = new GameObject("Badge", typeof(RectTransform), typeof(Text), typeof(Outline));
		RectTransform rt = (RectTransform)go.transform;
		rt.SetParent(button.transform, worldPositionStays: false);
		UIKit.Stretch(rt);
		Text label = go.GetComponent<Text>();
		label.font = font.font;
		label.fontSize = 11;
		label.fontStyle = FontStyle.Bold;
		label.alignment = TextAnchor.LowerRight;
		label.color = Color.white;
		label.raycastTarget = false;
		label.text = text;
		go.GetComponent<Outline>().effectColor = Color.black;
	}

	internal static Rect WorldRect(RectTransform rt)
	{
		Vector3[] corners = new Vector3[4];
		rt.GetWorldCorners(corners);
		return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
	}

	// One-off dump of the speed buttons' surroundings, so a layout problem can be fixed from the log.
	private static void LogLayout(RectTransform fastButton)
	{
		Transform parent = fastButton.parent;
		List<string> lines = new List<string>();
		foreach (Transform level in new Transform[2] { parent, parent.parent })
		{
			if (!level)
			{
				continue;
			}
			lines.Add("[" + level.name + "]" + ((bool)level.GetComponent<LayoutGroup>() ? " (layout group)" : ""));
			foreach (Transform child in level)
			{
				if (child is RectTransform rt)
				{
					Rect r = WorldRect(rt);
					lines.Add("  " + child.name + (child.gameObject.activeSelf ? "" : " (hidden)") + " x " + Mathf.RoundToInt(r.xMin) + ".." + Mathf.RoundToInt(r.xMax) + " y " + Mathf.RoundToInt(r.yMin) + ".." + Mathf.RoundToInt(r.yMax));
				}
			}
		}
		GUI_Main gui = G.Gui;
		foreach (Transform button in parent)
		{
			if (!button.name.StartsWith("ButtonSpeed"))
			{
				continue;
			}
			lines.Add("[" + button.name + "] transition " + button.GetComponent<Button>()?.transition);
			foreach (Image image in button.GetComponentsInChildren<Image>(includeInactive: true))
			{
				int index = ((bool)gui) ? Array.IndexOf(gui.uiObjects, image.gameObject) : (-1);
				lines.Add("  " + PathFrom(button.parent, image.transform) + ((index >= 0) ? (" = uiObjects[" + index + "]") : "") + (image.gameObject.activeSelf ? "" : " (hidden)") + " sprite " + ((bool)image.sprite ? image.sprite.name : "none") + " color " + image.color);
			}
		}
		Plugin.Log.LogInfo("Speed bar layout:\n" + string.Join("\n", lines));
	}

	// The game lights uiObjects[14] for any speed above 1; light our own button instead when its speed is on.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(GUI_Main), "UpdateData")]
	private static void Indicators(GUI_Main __instance)
	{
		if (!fast || !(bool)G.MS)
		{
			return;
		}
		float speed = G.MS.GetGameSpeed();
		bool ours = false;
		foreach (Extra extra in extras)
		{
			bool on = (bool)extra.Button && extra.Button.activeInHierarchy && Mathf.Abs(speed - extra.Speed()) < 0.01f;
			ours |= on;
			if ((bool)extra.Indicator && extra.Indicator.activeSelf != on)
			{
				extra.Indicator.SetActive(on);
			}
		}
		GameObject fastIndicator = ((__instance.uiObjects != null && __instance.uiObjects.Length > 14) ? __instance.uiObjects[14] : null);
		if (ours && (bool)fastIndicator && fastIndicator.activeSelf)
		{
			fastIndicator.SetActive(value: false);
		}
	}
}

// Marks the game's fast button once its neighbours have been moved, so they never get moved twice.
internal class SpeedShiftMarker : MonoBehaviour
{
}

// Hover and press tint for arrows drawn over a button's (now invisible) own image, using the button's colours.
internal class ArrowTint : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
	private Button button;

	private List<Image> arrows;

	private List<Color> baseColors;

	private ColorBlock colors;

	private bool tint;

	private bool over;

	private bool down;

	private bool lastInteractable = true;

	public void Init(Button b, List<Image> images)
	{
		button = b;
		arrows = images;
		baseColors = images.Select((Image i) => i.color).ToList();
		colors = b.colors;
		tint = b.transition == Selectable.Transition.ColorTint;
		Refresh();
	}

	public void OnPointerEnter(PointerEventData e)
	{
		over = true;
		Refresh();
	}

	public void OnPointerExit(PointerEventData e)
	{
		over = false;
		down = false;
		Refresh();
	}

	public void OnPointerDown(PointerEventData e)
	{
		down = true;
		Refresh();
	}

	public void OnPointerUp(PointerEventData e)
	{
		down = false;
		Refresh();
	}

	private void Update()
	{
		if ((bool)button && button.interactable != lastInteractable)
		{
			Refresh();
		}
	}

	private void Refresh()
	{
		if (!button || arrows == null || !tint)
		{
			return;
		}
		lastInteractable = button.interactable;
		Color c = ((!button.interactable) ? colors.disabledColor : (down ? colors.pressedColor : (over ? colors.highlightedColor : colors.normalColor)));
		for (int i = 0; i < arrows.Count; i++)
		{
			if ((bool)arrows[i])
			{
				arrows[i].color = baseColors[i] * c * colors.colorMultiplier;
			}
		}
	}
}
