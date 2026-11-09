using System;
using System.Collections.Generic;
using HolyAddon.Core;
using HolyAddon.UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// A game-style window showing a stack of pages (Home > Employees > one employee) with a Back button.
// The Editor and the Contracts window are both one of these.
internal class EditorWindow
{
	public static readonly EditorWindow Main = new EditorWindow("Editor_Window", () => new HomePage());

	public static readonly EditorWindow Contracts = new EditorWindow("Editor_ContractsWindow", () => new ContractsPage());

	public static readonly EditorWindow CopyProtect = new EditorWindow("Editor_CopyProtectWindow", () => new CopyProtectPage());

	public static readonly EditorWindow Updates = new EditorWindow("Editor_UpdatesWindow", () => new UpdatesHomePage());

	public static readonly EditorWindow Mmo = new EditorWindow("Editor_MmoWindow", () => new MmoListPage());

	public static readonly EditorWindow Store = new EditorWindow("Editor_StoreWindow", () => new StoreHomePage());

	// Opens on top of the game's new-game screen, which already holds the game in menu mode.
	public static readonly EditorWindow TimedExclusive = new EditorWindow("Editor_TimedExclusiveWindow", () => new TimedExclusivePage(), overlay: true);

	private readonly string objectName;

	private readonly Func<Page> rootPage;

	// A window over one of the game's own menus: it leaves menu mode (pause, room clicks off) to that menu.
	private readonly bool overlay;

	private GameObject root;

	private RectTransform frame;

	private Text titleText;

	private Image titleIcon;

	private Vector2 appliedSize;

	private readonly List<Page> stack = new List<Page>();

	private bool claimedInput;

	public RectTransform Body { get; private set; }

	public EditorWindow(string objectName, Func<Page> rootPage, bool overlay = false)
	{
		this.objectName = objectName;
		this.rootPage = rootPage;
		this.overlay = overlay;
	}

	public bool IsOpen
	{
		get
		{
			if ((bool)root)
			{
				return root.activeSelf;
			}
			return false;
		}
	}

	public Page Current
	{
		get
		{
			if (stack.Count <= 0)
			{
				return null;
			}
			return stack[stack.Count - 1];
		}
	}

	public float Width => Mathf.Min(Current?.Width ?? 614f, MaxSize.x);

	public float Height => Mathf.Min(Current?.Height ?? 640f, MaxSize.y);

	private Vector2 MaxSize
	{
		get
		{
			RectTransform rt = (UIKit.MenuParent as RectTransform);
			if (!rt || rt.rect.width < 100f)
			{
				return new Vector2(4000f, 4000f);
			}
			return new Vector2(rt.rect.width - 40f, rt.rect.height - 40f);
		}
	}

	private bool EnsureCreated()
	{
		if ((bool)root)
		{
			return true;
		}
		if (!UIKit.Init())
		{
			return false;
		}
		root = UIKit.Make("Window", UIKit.MenuParent);
		root.name = objectName;
		frame = (RectTransform)root.transform.Find("WindowMain");
		UIKit.Stretch(frame.Find("Fill_Window"));
		appliedSize = Vector2.zero;
		RectTransform titleBar = (RectTransform)frame.Find("Titelleiste");
		titleText = titleBar.Find("Title").GetComponent<Text>();
		UIKit.Stretch(titleText.transform, 70f, 0f, 48f);
		titleIcon = titleBar.Find("Icon").GetComponent<Image>();
		UIKit.OnClick(titleBar.Find("Button_Close").GetComponent<Button>(), Close);
		GameObject body = new GameObject("Body", typeof(RectTransform));
		body.transform.SetParent(frame, worldPositionStays: false);
		Body = (RectTransform)body.transform;
		UIKit.Stretch(Body, 0f, 75f);
		return true;
	}

	private void ApplySize()
	{
		Vector2 size = new Vector2(Width, Height);
		if (size == appliedSize)
		{
			return;
		}
		appliedSize = size;
		frame.anchoredPosition = Vector2.zero;
		frame.sizeDelta = size;
		RectTransform fill = (RectTransform)frame.Find("Fill_Titleleiste");
		fill.anchorMin = fill.anchorMax = new Vector2(0.5f, 0.5f);
		fill.sizeDelta = new Vector2(size.x, 75f);
		fill.anchoredPosition = new Vector2(0f, size.y / 2f - 38f);
		RectTransform bar = (RectTransform)frame.Find("Titelleiste");
		bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
		bar.sizeDelta = new Vector2(size.x - 21f, 53f);
		bar.anchoredPosition = new Vector2(0f, size.y / 2f - 37f);
	}

	public void Open()
	{
		if (!EnsureCreated())
		{
			Plugin.Log.LogWarning("Editor window is not ready yet");
			return;
		}
		UIKit.Sound();
		if (!overlay)
		{
			G.Gui.guiMainButtons_.CloseAllDropdowns();
		}
		stack.Clear();
		stack.Add(rootPage());
		root.transform.SetAsLastSibling();
		root.SetActive(value: true);
		if (!overlay)
		{
			G.Gui.OpenMenu(hideChars: false);
		}
		Rebuild();
	}

	public void Close()
	{
		if (!root)
		{
			return;
		}
		UIKit.Sound();
		root.SetActive(value: false);
		ReleaseInput();
		stack.Clear();
		if ((bool)G.Gui && !overlay)
		{
			G.Gui.CloseMenu();
		}
	}

	public void Push(Page page)
	{
		stack.Add(page);
		Rebuild();
	}

	// Swap the top page, e.g. stepping to the next employee without growing the Back history.
	public void Replace(Page page)
	{
		if (stack.Count > 0)
		{
			stack.RemoveAt(stack.Count - 1);
		}
		Push(page);
	}

	public void Back()
	{
		if (stack.Count <= 1)
		{
			Close();
			return;
		}
		stack.RemoveAt(stack.Count - 1);
		Rebuild();
	}

	public void Rebuild()
	{
		if (!root || Current == null)
		{
			return;
		}
		ReleaseInput();
		ApplySize();
		titleText.text = Current.Title;
		Sprite sprite = Current.IconSprite ?? UIKit.Sprite(Current.Icon) ?? UIKit.EditorIcon();
		if ((bool)sprite)
		{
			titleIcon.sprite = sprite;
		}
		for (int i = Body.childCount - 1; i >= 0; i--)
		{
			GameObject child = Body.GetChild(i).gameObject;
			child.SetActive(value: false);
			UnityEngine.Object.Destroy(child);
		}
		try
		{
			Current.Build(this);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError(Current.GetType().Name + " build failed: " + ex);
		}
	}

	public void Tick()
	{
		if (!IsOpen || Current == null)
		{
			return;
		}
		// While the search box has focus, stop the game's hotkeys and camera keys from reacting to typing.
		bool typing = false;
		foreach (InputField input in Body.GetComponentsInChildren<InputField>())
		{
			if (input.isFocused)
			{
				typing = true;
				break;
			}
		}
		if (typing)
		{
			claimedInput = true;
			G.Gui.selectInputField = true;
		}
		else
		{
			ReleaseInput();
		}
		Current.Tick(this);
	}

	private void ReleaseInput()
	{
		if (claimedInput)
		{
			claimedInput = false;
			if ((bool)G.Gui)
			{
				G.Gui.selectInputField = false;
			}
		}
	}

	public Text Label(string text, float x, float y, float w, float h = 24f, TextAnchor align = TextAnchor.MiddleLeft, bool bold = false, Transform parent = null, int size = 0)
	{
		GameObject go = UIKit.Make(bold ? "BoldText" : "Paragraph", parent ?? Body);
		UIKit.Place(go, x, y, w, h);
		Text component = go.GetComponent<Text>();
		component.text = text;
		component.alignment = align;
		if (size > 0)
		{
			component.fontSize = size;
			component.resizeTextForBestFit = false;
		}
		component.horizontalOverflow = HorizontalWrapMode.Wrap;
		component.verticalOverflow = VerticalWrapMode.Truncate;
		return component;
	}

	public Button BigButton(string text, float x, float y, float w, float h, Action onClick, bool enabled = true, string tooltip = null, Transform parent = null)
	{
		GameObject go = UIKit.Make("BigButton", parent ?? Body);
		UIKit.Place(go, x, y, w, h);
		foreach (Transform item in go.transform)
		{
			if (item.name == "Icon")
			{
				item.gameObject.SetActive(value: false);
			}
			else
			{
				UIKit.Stretch(item, (item.name == "Text") ? 6 : 0, 0f, (item.name == "Text") ? 6 : 0);
			}
		}
		go.transform.Find("Text").GetComponent<Text>().text = text;
		Button component = go.GetComponent<Button>();
		component.interactable = enabled;
		UIKit.OnClick(component, () =>
		{
			UIKit.Sound();
			onClick();
		});
		if (tooltip != null)
		{
			UIKit.SetTooltip(go, tooltip);
		}
		return component;
	}

	public Button SmallButton(string text, float x, float y, float w, Action onClick, bool enabled = true, string tooltip = null, Transform parent = null, float h = 26f)
	{
		GameObject go = UIKit.Make("SmallButton", parent ?? Body);
		UIKit.Place(go, x, y, w, h);
		foreach (Transform item in go.transform)
		{
			UIKit.Stretch(item);
		}
		go.transform.Find("Text").GetComponent<Text>().text = text;
		Button component = go.GetComponent<Button>();
		component.interactable = enabled;
		UIKit.OnClick(component, () =>
		{
			UIKit.Sound();
			onClick();
		});
		if (tooltip != null)
		{
			UIKit.SetTooltip(go, tooltip);
		}
		return component;
	}

	public void Header(string text, float x, float y, float w = 254f, Transform parent = null)
	{
		GameObject go = UIKit.Make("Header", parent ?? Body);
		UIKit.Place(go, x, y, w, 24f);
		Transform label = go.transform.Find("Text");
		UIKit.Stretch(label, 8f, 0f, 8f);
		label.GetComponent<Text>().text = text;
	}

	public RectTransform Panel(float x, float y, float w, float h, Transform parent = null)
	{
		return UIKit.Place(UIKit.Make("Panel", parent ?? Body), x, y, w, h);
	}

	public void Info(string text, float x, float y, float w, float h, Transform parent = null)
	{
		GameObject go = UIKit.Make("Info", parent ?? Body);
		UIKit.Place(go, x, y, w, h);
		RectTransform rt = (RectTransform)go.transform.Find("TextInfo");
		UIKit.Stretch(rt, 44f, 2f, 6f, 2f);
		rt.GetComponent<Text>().text = text;
	}

	public static float FactsHeight(int count, int columns = 1)
	{
		return Mathf.CeilToInt((float)count / (float)columns) * 26f + 12f;
	}

	public float Facts(float x, float y, float w, IList<KeyValuePair<string, string>> rows, int columns = 1, Transform parent = null)
	{
		int perColumn = Mathf.CeilToInt((float)rows.Count / (float)columns);
		float h = FactsHeight(rows.Count, columns);
		RectTransform panel = Panel(x, y, w, h, parent);
		float colWidth = (w - 12f) / (float)columns;
		for (int i = 0; i < rows.Count; i++)
		{
			float cx = 6f + (float)(i / perColumn) * colWidth;
			float cy = 6 + i % perColumn * 26;
			Label(rows[i].Key, cx + 6f, cy, colWidth * 0.45f - 6f, 26f, TextAnchor.MiddleLeft, bold: false, panel, 13);
			Label(rows[i].Value, cx + colWidth * 0.4f, cy, colWidth * 0.6f - 10f, 26f, TextAnchor.MiddleRight, bold: true, panel, 13);
		}
		return h;
	}

	public static KeyValuePair<string, string> KV(string key, string value)
	{
		return new KeyValuePair<string, string>(key, value);
	}

	public Dropdown Dropdown(float x, float y, float w, IList<string> options, int value, Action<int> onChange, Transform parent = null)
	{
		GameObject go = UIKit.Make("Dropdown", parent ?? Body);
		UIKit.Place(go, x, y, w, 30f);
		Dropdown component = go.GetComponent<Dropdown>();
		component.onValueChanged = new Dropdown.DropdownEvent();
		component.ClearOptions();
		component.AddOptions(new List<string>(options));
		component.value = Mathf.Clamp(value, 0, Math.Max(0, options.Count - 1));
		component.RefreshShownValue();
		component.onValueChanged.AddListener((int v) =>
		{
			try
			{
				onChange(v);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Editor dropdown failed: " + ex);
			}
		});
		return component;
	}

	public Toggle Toggle(string text, float x, float y, float w, bool isOn, Action<bool> onChange, string tooltip = null, Transform parent = null)
	{
		GameObject go = UIKit.Make("Toggle", parent ?? Body);
		UIKit.Place(go, x, y, w, 26f);
		RectTransform label = (RectTransform)go.transform.Find("Label");
		label.anchorMin = new Vector2(0f, 0f);
		label.anchorMax = new Vector2(1f, 1f);
		label.offsetMin = new Vector2(34f, 0f);
		label.offsetMax = Vector2.zero;
		label.GetComponent<Text>().text = text;
		RectTransform box = (RectTransform)go.transform.Find("Background");
		box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
		box.anchoredPosition = new Vector2(14f, 0f);
		Toggle component = go.GetComponent<Toggle>();
		component.onValueChanged = new Toggle.ToggleEvent();
		component.SetIsOnWithoutNotify(isOn);
		component.onValueChanged.AddListener((bool v) =>
		{
			UIKit.Sound();
			try
			{
				onChange(v);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Editor toggle failed: " + ex);
			}
		});
		if (tooltip != null)
		{
			UIKit.SetTooltip(go, tooltip);
		}
		return component;
	}

	public InputField Input(string text, string placeholder, float x, float y, float w, Action<string> onChange, Transform parent = null)
	{
		GameObject go = UIKit.Make("Input", parent ?? Body);
		UIKit.Place(go, x, y, w, 34f);
		foreach (Transform item in go.transform)
		{
			if (item.name == "Square")
			{
				UIKit.Stretch(item);
			}
			else
			{
				UIKit.Stretch(item, 10f, 0f, 10f);
			}
		}
		InputField component = go.GetComponent<InputField>();
		component.onValueChanged = new InputField.OnChangeEvent();
		component.onEndEdit = new InputField.EndEditEvent();
		component.onSubmit = new InputField.SubmitEvent();
		component.characterLimit = 40;
		component.text = text ?? "";
		Transform hint = go.transform.Find("Placeholder");
		if ((bool)hint)
		{
			hint.GetComponent<Text>().text = placeholder;
		}
		component.onValueChanged.AddListener((string v) =>
		{
			try
			{
				onChange(v);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Editor input failed: " + ex);
			}
		});
		return component;
	}

	// A 0-100 style slider with a live value label on its right. onChange runs while dragging, without rebuilding the page.
	public Slider Slider(float x, float y, float w, float min, float max, float value, Action<float> onChange, Func<float, string> format = null, bool colorByValue = false, Transform parent = null)
	{
		const float labelWidth = 58f;
		GameObject go = UIKit.Make("Slider", parent ?? Body);
		UIKit.Place(go, x, y, w - labelWidth - 6f, 30f);
		Slider slider = go.GetComponent<Slider>();
		slider.onValueChanged = new Slider.SliderEvent();
		slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
		slider.wholeNumbers = false;
		slider.minValue = min;
		slider.maxValue = max;
		slider.interactable = true;
		slider.navigation = new Navigation { mode = Navigation.Mode.None };
		slider.SetValueWithoutNotify(Mathf.Clamp(value, min, max));
		Text label = Label("", x + w - labelWidth, y, labelWidth, 30f, TextAnchor.MiddleRight, bold: true, parent);
		Image fill = ((bool)slider.fillRect) ? slider.fillRect.GetComponent<Image>() : null;
		Action<float> show = (float v) =>
		{
			label.text = (format != null) ? format(v) : v.ToString("0.0");
			if (colorByValue && (bool)fill)
			{
				fill.color = UIKit.ValueColor(v);
			}
		};
		show(slider.value);
		slider.onValueChanged.AddListener((float v) =>
		{
			show(v);
			try
			{
				onChange(v);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Editor slider failed: " + ex);
			}
		});
		return slider;
	}

	public void Bar(float x, float y, float w, float h, float value, string text, Color color, Transform parent = null, string tooltip = null)
	{
		GameObject go = UIKit.Make("Bar", parent ?? Body);
		UIKit.Place(go, x, y, w, h);
		StyleBar(go, value, text, color, h);
		if (tooltip != null)
		{
			UIKit.SetTooltip(go, tooltip);
		}
	}

	public static void StyleBar(GameObject go, float value, string text, Color color, float h)
	{
		RectTransform fill = (RectTransform)go.transform.Find("Fill");
		fill.anchorMin = new Vector2(0f, 0f);
		fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
		fill.pivot = new Vector2(0f, 0.5f);
		fill.offsetMin = new Vector2(1f, 1f);
		fill.offsetMax = new Vector2((value > 0.001f) ? (-1) : 0, -1f);
		UIKit.FillStyle(fill.GetComponent<Image>(), color);
		Transform textT = go.transform.Find("FillText");
		UIKit.Stretch(textT);
		Text component = textT.GetComponent<Text>();
		component.text = text;
		component.alignment = TextAnchor.MiddleCenter;
		component.fontSize = Mathf.Min(component.fontSize, (int)h - 2);
		UIKit.BarTextStyle(component);
	}

	public ListView List(float x, float y, float w, float h)
	{
		GameObject go = UIKit.Make("Scroll", Body);
		UIKit.Place(go, x, y, w, h);
		return new ListView(go.GetComponent<ScrollRect>(), w - 38f);
	}

	public void Empty(string text, float y)
	{
		GameObject go = UIKit.Make("Empty", Body);
		RectTransform rt = (RectTransform)go.transform;
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.anchoredPosition = new Vector2(0f, 0f - y);
		go.transform.Find("TextTitle").GetComponent<Text>().text = text;
		Animation anim = go.GetComponent<Animation>();
		if ((bool)anim)
		{
			anim.enabled = false;
		}
	}
}
