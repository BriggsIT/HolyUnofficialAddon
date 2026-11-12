using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HolyAddon.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HolyAddon.UI;

// Builds the Editor out of copies of the game's own UI pieces, so it looks like a native window.
// The menus and paths are the same ones The Holy Expansion clones, which are known to exist in this game version.
internal static class UIKit
{
	private static GameObject holder;

	private static readonly Dictionary<string, GameObject> T = new Dictionary<string, GameObject>();

	private static Dictionary<string, Sprite> sprites;

	private static readonly Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>();

	public static readonly Color BaseGreen = new Color(0.16f, 0.62f, 0.2f);

	public static readonly Color BaseRed = new Color(0.8f, 0.15f, 0.12f);

	public static readonly Color BaseOrange = new Color(0.95f, 0.55f, 0.1f);

	public static readonly Color BaseBlue = new Color(0.12f, 0.42f, 0.8f);

	public static readonly Color BaseGrey = new Color(0.45f, 0.45f, 0.45f);

	// The skin of the window whose page is being drawn (null for the game's own look): the colours below become
	// readable versions of themselves on its dark background.
	public static Skin ActiveSkin;

	public static Color Green => ActiveSkin?.Map(BaseGreen) ?? BaseGreen;

	public static Color Red => ActiveSkin?.Map(BaseRed) ?? BaseRed;

	public static Color Orange => ActiveSkin?.Map(BaseOrange) ?? BaseOrange;

	public static Color Blue => ActiveSkin?.Map(BaseBlue) ?? BaseBlue;

	public static Color Grey => ActiveSkin?.Map(BaseGrey) ?? BaseGrey;

	public static readonly Color SelectedRow = new Color(0.62f, 0.8f, 1f);

	public static Transform MenuParent { get; private set; }

	public static bool Init()
	{
		if ((bool)holder)
		{
			return true;
		}
		GUI_Main gui = G.Gui;
		if (!gui || gui.uiObjects == null || gui.uiObjects.Length < 410)
		{
			return false;
		}
		GameObject selectGame = gui.uiObjects[90];
		GameObject support = gui.uiObjects[141];
		GameObject staff = gui.uiObjects[29];
		GameObject rename = gui.uiObjects[37];
		if (!selectGame || !support || !staff || !rename)
		{
			return false;
		}
		MenuParent = selectGame.transform.parent;
		holder = new GameObject("EditorTemplates", typeof(RectTransform));
		holder.SetActive(value: false);
		holder.transform.SetParent(MenuParent, worldPositionStays: false);
		T.Clear();
		try
		{
			BuildWindowTemplate(gui, selectGame);
			T["Scroll"] = Piece(selectGame, "WindowMain/Scroll View ");
			Transform content = T["Scroll"].transform.Find("Viewport/Content");
			for (int i = content.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
			}
			T["Header"] = Piece(selectGame, "WindowMain/TxtTop");
			T["Dropdown"] = Piece(selectGame, "WindowMain/Dropdown_S_4");
			T["Empty"] = Piece(selectGame, "WindowMain/KeinEintrag");
			GameObject row = UnityEngine.Object.Instantiate(selectGame.GetComponent<Menu_Marketing_SelectGame>().uiPrefabs[0], holder.transform, worldPositionStays: false);
			row.name = "Row";
			Clean(row);
			T["RowBold"] = Detach(row.transform.Find("TextName"));
			T["RowText"] = Detach(row.transform.Find("TextInfo"));
			T["RowIcon"] = Detach(row.transform.Find("IconHype"));
			T["RowLine"] = Detach(row.transform.Find("Line"));
			for (int i = row.transform.childCount - 1; i >= 0; i--)
			{
				UnityEngine.Object.DestroyImmediate(row.transform.GetChild(i).gameObject);
			}
			T["Row"] = row;
			T["Paragraph"] = Piece(support, "Menue/Text");
			T["Panel"] = Piece(support, "Menue/PanelButtons");
			T["Bar"] = Piece(support, "Menue/FillBG");
			T["BoldText"] = Piece(support, "Menue/TextAuslastung");
			T["BigButton"] = Piece(support, "Menue/Button_Start");
			T["Toggle"] = Piece(support, "Menue/Toggle");
			T["Info"] = Piece(support, "Menue/Info");
			T["SmallButton"] = Piece(staff, "WindowMain/Button_Selektieren");
			T["Input"] = Piece(rename, "WindowMain/InputFieldName");
			foreach (EventTrigger trigger in T["Input"].GetComponentsInChildren<EventTrigger>(includeInactive: true))
			{
				UnityEngine.Object.DestroyImmediate(trigger);
			}
			T["Slider"] = SliderTemplate(gui);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Editor UI templates failed: " + ex);
			UnityEngine.Object.Destroy(holder);
			holder = null;
			return false;
		}
		Plugin.Log.LogInfo("Editor UI templates ready (" + T.Count + ")");
		return true;
	}

	private static void BuildWindowTemplate(GUI_Main gui, GameObject src)
	{
		GameObject window = UnityEngine.Object.Instantiate(src, holder.transform, worldPositionStays: false);
		window.name = "Window";
		foreach (MonoBehaviour mb in window.GetComponents<MonoBehaviour>())
		{
			if (!(mb is Graphic))
			{
				UnityEngine.Object.DestroyImmediate(mb);
			}
		}
		Transform main = window.transform.Find("WindowMain");
		for (int i = window.transform.childCount - 1; i >= 0; i--)
		{
			if (window.transform.GetChild(i) != main)
			{
				UnityEngine.Object.DestroyImmediate(window.transform.GetChild(i).gameObject);
			}
		}
		for (int i = main.childCount - 1; i >= 0; i--)
		{
			string name = main.GetChild(i).name;
			if (name != "Fill_Window" && name != "Fill_Titleleiste" && name != "Titelleiste")
			{
				UnityEngine.Object.DestroyImmediate(main.GetChild(i).gameObject);
			}
		}
		Clean(window);
		Image image = window.GetComponent<Image>();
		if ((bool)image && (bool)gui.matFill_Window)
		{
			image.material = gui.matFill_Window;
		}
		window.SetActive(value: false);
		T["Window"] = window;
	}

	// Borrow a slider from the salary negotiation menu (or any menu that has one); build a plain one if none is found.
	private static GameObject SliderTemplate(GUI_Main gui)
	{
		Slider source = null;
		if ((bool)gui.uiObjects[409])
		{
			source = gui.uiObjects[409].GetComponentsInChildren<Slider>(includeInactive: true).FirstOrDefault();
		}
		if (!source)
		{
			foreach (GameObject menu in gui.uiObjects)
			{
				if ((bool)menu)
				{
					source = menu.GetComponentsInChildren<Slider>(includeInactive: true).FirstOrDefault();
					if ((bool)source)
					{
						break;
					}
				}
			}
		}
		GameObject slider;
		if ((bool)source)
		{
			slider = UnityEngine.Object.Instantiate(source.gameObject, holder.transform, worldPositionStays: false);
			Clean(slider);
			foreach (EventTrigger trigger in slider.GetComponentsInChildren<EventTrigger>(includeInactive: true))
			{
				UnityEngine.Object.DestroyImmediate(trigger);
			}
			foreach (tooltip tip in slider.GetComponentsInChildren<tooltip>(includeInactive: true))
			{
				UnityEngine.Object.DestroyImmediate(tip);
			}
			// Value labels or buttons that came along with the slider belong to the old menu.
			foreach (Text text in slider.GetComponentsInChildren<Text>(includeInactive: true))
			{
				UnityEngine.Object.DestroyImmediate(text.gameObject);
			}
		}
		else
		{
			Plugin.Log.LogWarning("No game slider found to copy, using a plain one");
			slider = PlainSlider();
		}
		slider.name = "Slider";
		slider.SetActive(value: true);
		return slider;
	}

	private static GameObject PlainSlider()
	{
		GameObject go = new GameObject("Slider", typeof(RectTransform));
		go.transform.SetParent(holder.transform, worldPositionStays: false);
		Image bg = NewImage("Background", go.transform, new Color(0.25f, 0.25f, 0.25f));
		Stretch(bg.transform, 0f, 7f, 0f, 7f);
		GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
		fillArea.transform.SetParent(go.transform, worldPositionStays: false);
		Stretch(fillArea.transform, 2f, 9f, 2f, 9f);
		Image fill = NewImage("Fill", fillArea.transform, Green);
		((RectTransform)fill.transform).sizeDelta = Vector2.zero;
		GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
		handleArea.transform.SetParent(go.transform, worldPositionStays: false);
		Stretch(handleArea.transform, 8f, 0f, 8f, 0f);
		Image handle = NewImage("Handle", handleArea.transform, Color.white);
		((RectTransform)handle.transform).sizeDelta = new Vector2(16f, 0f);
		Slider slider = go.AddComponent<Slider>();
		slider.fillRect = (RectTransform)fill.transform;
		slider.handleRect = (RectTransform)handle.transform;
		slider.targetGraphic = handle;
		slider.direction = Slider.Direction.LeftToRight;
		return go;
	}

	private static Image NewImage(string name, Transform parent, Color color)
	{
		GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
		go.transform.SetParent(parent, worldPositionStays: false);
		Image image = go.GetComponent<Image>();
		image.color = color;
		return image;
	}

	private static GameObject Piece(GameObject root, string path)
	{
		Transform transform = root.transform.Find(path);
		if (!transform)
		{
			throw new Exception("UI template missing: " + root.name + "/" + path);
		}
		GameObject go = UnityEngine.Object.Instantiate(transform.gameObject, holder.transform, worldPositionStays: false);
		go.name = transform.name;
		go.SetActive(value: true);
		Clean(go);
		return go;
	}

	private static GameObject Detach(Transform t)
	{
		t.SetParent(holder.transform, worldPositionStays: false);
		t.gameObject.SetActive(value: true);
		return t.gameObject;
	}

	// Strip the game's menu logic from a copied piece so only the visuals are left.
	private static void Clean(GameObject go)
	{
		foreach (MonoBehaviour mb in go.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
		{
			if (mb == null)
			{
				continue;
			}
			string name = mb.GetType().Name;
			if (mb is setText || mb is lockDestroy || name.StartsWith("Menu_") || name.StartsWith("Item_") || name == "DisableInputSearch")
			{
				UnityEngine.Object.DestroyImmediate(mb);
			}
		}
		foreach (Button button in go.GetComponentsInChildren<Button>(includeInactive: true))
		{
			button.onClick = new Button.ButtonClickedEvent();
		}
		foreach (Toggle toggle in go.GetComponentsInChildren<Toggle>(includeInactive: true))
		{
			toggle.onValueChanged = new Toggle.ToggleEvent();
		}
		foreach (Dropdown dropdown in go.GetComponentsInChildren<Dropdown>(includeInactive: true))
		{
			dropdown.onValueChanged = new Dropdown.DropdownEvent();
		}
		foreach (Slider slider in go.GetComponentsInChildren<Slider>(includeInactive: true))
		{
			slider.onValueChanged = new Slider.SliderEvent();
		}
	}

	public static GameObject Make(string template, Transform parent)
	{
		if (!T.TryGetValue(template, out GameObject value))
		{
			throw new Exception("Unknown UI template " + template);
		}
		GameObject go = UnityEngine.Object.Instantiate(value, parent, worldPositionStays: false);
		go.name = template;
		return go;
	}

	public static Sprite Sprite(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		if (sprites == null || sprites.Count == 0)
		{
			sprites = new Dictionary<string, Sprite>();
			foreach (Sprite sprite in Resources.FindObjectsOfTypeAll<Sprite>())
			{
				if ((bool)sprite && !sprites.ContainsKey(sprite.name))
				{
					sprites[sprite.name] = sprite;
				}
			}
		}
		if (!sprites.TryGetValue(name, out Sprite value) || !value)
		{
			return null;
		}
		return value;
	}

	// The Editor's own icon, loaded once from icon.png next to Editor.dll.
	public static Sprite EditorIcon()
	{
		return LoadSprite(Path.Combine(Plugin.PluginDir, "icon.png"));
	}

	// A PNG from the plugin folder as a sprite, loaded once and kept for the whole session (null if missing).
	public static Sprite LoadSprite(string path)
	{
		if (loaded.TryGetValue(path, out Sprite cached) && (bool)cached)
		{
			return cached;
		}
		if (!File.Exists(path))
		{
			Plugin.Log.LogWarning("Image not found: " + path);
			loaded[path] = null;
			return null;
		}
		string name = "Editor_" + Path.GetFileNameWithoutExtension(path);
		Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true)
		{
			name = name,
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Trilinear,
			hideFlags = HideFlags.DontUnloadUnusedAsset
		};
		if (!texture.LoadImage(File.ReadAllBytes(path)))
		{
			Plugin.Log.LogWarning("Image could not be read: " + path);
			loaded[path] = null;
			return null;
		}
		Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
		sprite.name = name;
		sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
		loaded[path] = sprite;
		return sprite;
	}

	public static RectTransform Place(GameObject go, float x, float y, float w, float h)
	{
		RectTransform rt = (RectTransform)go.transform;
		rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
		rt.pivot = new Vector2(0f, 1f);
		rt.anchoredPosition = new Vector2(x, 0f - y);
		rt.sizeDelta = new Vector2(w, h);
		return rt;
	}

	public static void Stretch(Transform t, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
	{
		RectTransform rt = (RectTransform)t;
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.offsetMin = new Vector2(left, bottom);
		rt.offsetMax = new Vector2(0f - right, 0f - top);
	}

	public static void SetTooltip(GameObject go, string text)
	{
		tooltip tip = go.GetComponent<tooltip>() ?? go.AddComponent<tooltip>();
		tip.textID = -1;
		tip.textArray = "";
		tip.c = text;
	}

	public static void OnClick(Button b, Action a)
	{
		b.onClick = new Button.ButtonClickedEvent();
		b.onClick.AddListener(() =>
		{
			try
			{
				a();
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Editor action failed: " + ex);
			}
		});
	}

	public static void FillStyle(Image img, Color color)
	{
		img.type = Image.Type.Simple;
		img.fillAmount = 1f;
		img.color = color;
	}

	public static void BarTextStyle(Text t)
	{
		t.color = Color.white;
		Outline outline = t.GetComponent<Outline>() ?? t.gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
		outline.effectDistance = new Vector2(1f, -1f);
	}

	// Same red / yellow / green bands the game uses for employee skill bars.
	public static Color ValueColor(float v)
	{
		Color[] c = ((bool)G.Gui) ? G.Gui.colorsBalken : null;
		if (c == null || c.Length < 3)
		{
			return (v < 30f) ? Red : ((v < 70f) ? Orange : Green);
		}
		if (v < 30f)
		{
			return c[0];
		}
		return (v < 70f) ? c[1] : c[2];
	}

	public static void Sound(int id = 3)
	{
		if ((bool)G.Sfx)
		{
			G.Sfx.PlaySound(id, force: true);
		}
	}
}
