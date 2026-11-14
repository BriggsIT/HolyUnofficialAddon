using System;
using System.Collections.Generic;
using HolyAddon.Core;
using HolyAddon.Core.Store;
using HolyAddon.UI.Pages;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// A game-style window showing a stack of pages (Home > Employees > one employee) with a Back button.
// The Editor and the Contracts window are both one of these.
internal class EditorWindow
{
	public static readonly EditorWindow Main = new EditorWindow("Editor_Window", () => new HomePage());

	public static readonly EditorWindow Contracts = new EditorWindow("Editor_ContractsWindow", () => new ContractsPage())
	{
		titleOf = () => new KeyValuePair<Color, Color>(new Color(0.07f, 0.42f, 0.45f), Color.white)
	};

	public static readonly EditorWindow CopyProtect = new EditorWindow("Editor_CopyProtectWindow", () => new CopyProtectPage());

	public static readonly EditorWindow Updates = new EditorWindow("Editor_UpdatesWindow", () => new UpdatesHomePage());

	// MMO Management and the store keep the game's look, with accents (banners, stat cards, icon buttons, a title
	// bar in their own colour). A window can also take a full skin (UI/Skin); none uses one at the moment.
	public static readonly Color MmoTitleBar = new Color(0.13f, 0.22f, 0.4f);

	public static readonly Color MmoTitleText = new Color(0.98f, 0.85f, 0.52f);

	public static readonly EditorWindow Mmo = new EditorWindow("Editor_MmoWindow", () => new MmoListPage())
	{
		titleOf = () => new KeyValuePair<Color, Color>(MmoTitleBar, MmoTitleText)
	};

	// The store's title bar takes its logo's colour.
	public static readonly EditorWindow Store = new EditorWindow("Editor_StoreWindow", () => new StoreHomePage())
	{
		titleOf = () => StoreOps.State.Built ? new KeyValuePair<Color, Color>?(new KeyValuePair<Color, Color>(StoreUi.LogoColor(StoreOps.State.Logo), Color.white)) : null
	};

	// Current charts (Statistics menu): what sells right now.
	public static readonly EditorWindow Charts = new EditorWindow("Editor_ChartsWindow", () => new CurrentChartsPage());

	// Opens on top of the game's new-game screen, which already holds the game in menu mode.
	public static readonly EditorWindow TimedExclusive = new EditorWindow("Editor_TimedExclusiveWindow", () => new TimedExclusivePage(), overlay: true);

	// The MGH Games Awards ceremony (Core/Awards/AwardsShow), opened by the game's December awards.
	public static readonly EditorWindow Awards = new EditorWindow("Editor_AwardsWindow", () => new AwardsCeremonyPage(0));

	// The yearly MMO fan festival (Core/Mmo/MmoFanFest), opened in October.
	public static readonly EditorWindow FanFest = new EditorWindow("Editor_FanFestWindow", () => new MmoFanFestPage())
	{
		titleOf = () => new KeyValuePair<Color, Color>(MmoTitleBar, MmoTitleText)
	};

	// A studio asking to sell on your store (Core/Store/StorePlus).
	public static readonly EditorWindow StudioRequest = new EditorWindow("Editor_StudioRequestWindow", () => new StudioRequestPage())
	{
		titleOf = () => StoreOps.State.Built ? new KeyValuePair<Color, Color>?(new KeyValuePair<Color, Color>(StoreUi.LogoColor(StoreOps.State.Logo), Color.white)) : null
	};

	// Sales Events (Core/Store/SalesEvents): the planner, and the pop-up as each sale starts. In your store's colours
	// once you own one, sale red before that.
	public static readonly Color SaleTitleBar = new Color(0.72f, 0.17f, 0.15f);

	public static KeyValuePair<Color, Color>? SaleTitle()
	{
		if (StoreOps.State.Built)
		{
			return new KeyValuePair<Color, Color>(StoreUi.LogoColor(StoreOps.State.Logo), Color.white);
		}
		return new KeyValuePair<Color, Color>(SaleTitleBar, Color.white);
	}

	public static readonly EditorWindow Sales = new EditorWindow("Editor_SalesWindow", () => new SalesPage())
	{
		titleOf = SaleTitle
	};

	public static readonly EditorWindow SalePopup = new EditorWindow("Editor_SalePopupWindow", () => new SalePopupPage())
	{
		titleOf = SaleTitle
	};

	// The yearly convention (Core/Events/StudioCon): planner, August pop-up and results, in your company logo's colour.
	public static readonly Color ConventionFallback = new Color(0.36f, 0.2f, 0.55f);

	public static KeyValuePair<Color, Color>? ConventionTitle()
	{
		Sprite logo = (((bool)G.Gui && (bool)G.MS) ? G.Gui.GetCompanyLogo(G.MS.GetCompanyLogoID()) : null);
		return new KeyValuePair<Color, Color>(SpriteTools.MainColor(logo, ConventionFallback), Color.white);
	}

	public static readonly EditorWindow Convention = new EditorWindow("Editor_ConventionWindow", ConventionPage.Root)
	{
		titleOf = ConventionTitle
	};

	// Industry events (Core/Industry): the decision pop-up, in the colour of the studio it's about, and the Industry
	// window with every studio and where you stand.
	public static KeyValuePair<Color, Color>? IndustryTitle()
	{
		Core.Industry.IndustryPending p = Core.Industry.IndustryCore.State.Pending;
		publisherScript studio = ((p != null) ? Core.Industry.IndustryCore.Studio(p.C.StudioId) : null);
		return new KeyValuePair<Color, Color>(IndustryUi.StudioColor(studio), Color.white);
	}

	public static readonly EditorWindow IndustryEvent = new EditorWindow("Editor_IndustryEventWindow", () => new IndustryEventPage())
	{
		titleOf = IndustryTitle
	};

	public static readonly EditorWindow Industry = new EditorWindow("Editor_IndustryWindow", () => new IndustryPage())
	{
		titleOf = () => new KeyValuePair<Color, Color>(IndustryUi.Navy, Color.white)
	};

	// Every room at a glance (bottom bar, and a button in every room's menu).
	public static readonly EditorWindow Rooms = new EditorWindow("Editor_RoomsWindow", () => new RoomsOverviewPage());

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

	// The window's look, asked again at every rebuild (the store's follows its logo); null = the game's own.
	private readonly Func<Skin> skinOf;

	// The title bar's colour and its text's (null = the game's blue), asked again at every rebuild.
	private Func<KeyValuePair<Color, Color>?> titleOf;

	private readonly Dictionary<Image, Sprite> titleSprites = new Dictionary<Image, Sprite>();

	public RectTransform Body { get; private set; }

	public Skin Skin { get; private set; }

	public EditorWindow(string objectName, Func<Page> rootPage, bool overlay = false, Func<Skin> skin = null)
	{
		this.objectName = objectName;
		this.rootPage = rootPage;
		this.overlay = overlay;
		skinOf = skin;
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

	// The page's own size: pages lay out at it, and a window too big for the screen is scaled down to fit (ApplySize),
	// so on small screens (1366x768...) nothing is cut off at the bottom.
	public float Width => Current?.Width ?? 614f;

	public float Height => Current?.Height ?? 640f;

	private float appliedFit = 1f;

	private float FitScale(Vector2 size)
	{
		Vector2 max = MaxSize;
		return Mathf.Clamp(Mathf.Min(max.x / Mathf.Max(1f, size.x), max.y / Mathf.Max(1f, size.y)), 0.4f, 1f);
	}

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
		float fit = FitScale(size);
		if (size == appliedSize && Mathf.Abs(fit - appliedFit) < 0.001f)
		{
			return;
		}
		appliedSize = size;
		appliedFit = fit;
		frame.anchoredPosition = Vector2.zero;
		frame.sizeDelta = size;
		frame.localScale = new Vector3(fit, fit, 1f);
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
		Skin skin = null;
		try
		{
			skin = skinOf?.Invoke();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Window skin: " + ex.Message);
		}
		Skin = skin;
		if (skin != null)
		{
			Theme(skin.Border, skin.Background, skin.Bar, skin.Title);
			Backdrop(skin);
		}
		else if (titleOf != null)
		{
			try
			{
				KeyValuePair<Color, Color>? title = titleOf();
				if (title.HasValue)
				{
					TitleAccent(title.Value.Key, title.Value.Value);
				}
			}
			catch (Exception ex3)
			{
				Plugin.Log.LogWarning("Window title colour: " + ex3.Message);
			}
		}
		UIKit.ActiveSkin = skin;
		try
		{
			Current.Build(this);
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogError(Current.GetType().Name + " build failed: " + ex2);
		}
		finally
		{
			UIKit.ActiveSkin = null;
		}
	}

	// Behind a skinned page: the skin's colour with a soft glow along the top.
	private void Backdrop(Skin skin)
	{
		Image back = NewImage("SkinBackdrop", Body, skin.Body);
		UIKit.Stretch(back.transform, 4f, 0f, 4f, 4f);
		Image glow = NewImage("SkinGlow", Body, skin.Glow);
		glow.sprite = Skin.Fade(up: true);
		RectTransform rt = (RectTransform)glow.transform;
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.offsetMin = new Vector2(4f, -160f);
		rt.offsetMax = new Vector2(-4f, 0f);
		rt.localScale = new Vector3(1f, -1f, 1f);
	}

	private static Image NewImage(string name, Transform parent, Color color)
	{
		Image img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		img.transform.SetParent(parent, worldPositionStays: false);
		img.color = color;
		img.raycastTarget = false;
		return img;
	}

	// A wide picture across the page (cropped to fill it), fading into the page on the left and at the bottom: the
	// top of a launcher. Returns the area to put text and buttons in.
	public static readonly Color BannerColor = new Color(0.055f, 0.085f, 0.14f);

	// White text on a banner, with a soft shadow.
	public Text BannerText(string text, float x, float y, float w, float h, int size, Color color, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
	{
		Text t = Label(text, x, y, w, h, align, bold, null, size);
		t.color = color;
		Shadow shadow = t.GetComponent<Shadow>() ?? t.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
		shadow.effectDistance = new Vector2(1f, -1f);
		return t;
	}

	// A wide picture across the page, cropped to fill it: art (full) covers the whole banner, a game's screenshot
	// sits on the right and fades into the banner's colour on the left. The top of a launcher.
	public RectTransform Banner(Sprite picture, float x, float y, float w, float h, bool full = false)
	{
		GameObject area = new GameObject("Banner", typeof(RectTransform), typeof(RectMask2D));
		area.transform.SetParent(Body, worldPositionStays: false);
		RectTransform rt = UIKit.Place(area, x, y, w, h);
		Color under = Skin?.Body ?? BannerColor;
		NewImage("Under", area.transform, under).rectTransform.sizeDelta = Vector2.zero;
		UIKit.Stretch(area.transform.Find("Under"));
		if ((bool)picture && full)
		{
			Image art = NewImage("Art", area.transform, Color.white);
			art.sprite = picture;
			float aspect = picture.rect.width / Mathf.Max(1f, picture.rect.height);
			float aw = w;
			float ah = aw / Mathf.Max(0.1f, aspect);
			if (ah < h)
			{
				ah = h;
				aw = ah * aspect;
			}
			RectTransform art2 = art.rectTransform;
			art2.anchorMin = art2.anchorMax = new Vector2(0.5f, 0.5f);
			art2.sizeDelta = new Vector2(aw, ah);
			art2.anchoredPosition = Vector2.zero;
		}
		else if ((bool)picture)
		{
			Image pic = NewImage("Picture", area.transform, Color.white);
			pic.sprite = picture;
			float aspect = picture.rect.width / Mathf.Max(1f, picture.rect.height);
			float pw = w * 0.72f;
			float ph = pw / Mathf.Max(0.1f, aspect);
			if (ph < h)
			{
				ph = h;
				pw = ph * aspect;
			}
			RectTransform prt = pic.rectTransform;
			prt.anchorMin = prt.anchorMax = new Vector2(1f, 0.5f);
			prt.pivot = new Vector2(1f, 0.5f);
			prt.sizeDelta = new Vector2(pw, ph);
			prt.anchoredPosition = Vector2.zero;
			// Fade the picture into the page from the left.
			Image fade = NewImage("FadeLeft", area.transform, under);
			fade.sprite = Skin.Fade(up: false);
			RectTransform frt = fade.rectTransform;
			frt.anchorMin = new Vector2(1f, 0f);
			frt.anchorMax = new Vector2(1f, 1f);
			frt.pivot = new Vector2(1f, 0.5f);
			frt.sizeDelta = new Vector2(pw * 0.75f, 0f);
			frt.anchoredPosition = new Vector2(0f - pw * 0.25f, 0f);
			Image fill = NewImage("FillLeft", area.transform, under);
			RectTransform lrt = fill.rectTransform;
			lrt.anchorMin = new Vector2(0f, 0f);
			lrt.anchorMax = new Vector2(1f, 1f);
			lrt.offsetMin = Vector2.zero;
			lrt.offsetMax = new Vector2(0f - pw + 2f, 0f);
			Image shade = NewImage("Shade", area.transform, new Color(under.r, under.g, under.b, 0.25f));
			UIKit.Stretch(shade.transform);
		}
		if (Skin != null)
		{
			Image line = NewImage("Line", area.transform, new Color(Skin.Accent.r, Skin.Accent.g, Skin.Accent.b, 0.6f));
			RectTransform l = line.rectTransform;
			l.anchorMin = new Vector2(0f, 0f);
			l.anchorMax = new Vector2(1f, 0f);
			l.pivot = new Vector2(0.5f, 0f);
			l.sizeDelta = new Vector2(0f, 1.5f);
			l.anchoredPosition = Vector2.zero;
		}
		return rt;
	}

	// One of the game's big buttons with a picture on its left (like its own menus' main buttons).
	public Button IconButton(string text, Sprite icon, float x, float y, float w, float h, Action onClick, bool enabled = true, string tooltip = null, Transform parent = null)
	{
		Button button = BigButton(text, x, y, w, h, onClick, enabled, tooltip, parent);
		Transform pic = button.transform.Find("Icon");
		Transform label = button.transform.Find("Text");
		float size = Mathf.Min(h - 12f, 44f);
		if ((bool)pic && (bool)icon)
		{
			pic.gameObject.SetActive(value: true);
			Image img = pic.GetComponent<Image>();
			img.sprite = icon;
			img.preserveAspect = true;
			img.color = Color.white;
			RectTransform prt = (RectTransform)pic;
			prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
			prt.pivot = new Vector2(0f, 0.5f);
			prt.sizeDelta = new Vector2(size, size);
			prt.anchoredPosition = new Vector2(8f, 0f);
		}
		if ((bool)label)
		{
			UIKit.Stretch(label, (bool)icon ? (size + 14f) : 6f, 0f, 6f);
			Text t = label.GetComponent<Text>();
			t.alignment = (bool)icon ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
		}
		return button;
	}

	// A small card: a picture, a title, a big number and a line under it (like the game's statistics tiles).
	public RectTransform StatCard(Sprite icon, string title, string value, string line, float x, float y, float w, float h, Color? lineColor = null, string tooltip = null)
	{
		RectTransform card = Panel(x, y, w, h);
		float size = Mathf.Min(h - 16f, 56f);
		if ((bool)icon)
		{
			Image img = NewImage("Icon", card, Color.white);
			img.sprite = icon;
			img.preserveAspect = true;
			UIKit.Place(img.gameObject, 8f, (h - size) / 2f, size, size);
		}
		float tx = ((bool)icon ? (size + 16f) : 10f);
		Text t1 = Label(title, tx, 6f, w - tx - 6f, 20f, TextAnchor.MiddleLeft, bold: false, card, 13);
		t1.color = UIKit.Grey;
		Label(value, tx, 24f, w - tx - 6f, 26f, TextAnchor.MiddleLeft, bold: true, card, 17);
		if (!string.IsNullOrEmpty(line))
		{
			Text t3 = Label(line, tx, h - 26f, w - tx - 6f, 20f, TextAnchor.MiddleLeft, bold: false, card, 12);
			t3.color = lineColor ?? UIKit.Grey;
		}
		if (tooltip != null)
		{
			Image hit = card.GetComponent<Image>();
			if ((bool)hit)
			{
				hit.raycastTarget = true;
			}
			UIKit.SetTooltip(card.gameObject, tooltip);
		}
		return card;
	}

	public static readonly Color RankGold = new Color(0.9f, 0.68f, 0.12f);

	public static readonly Color RankSilver = new Color(0.62f, 0.66f, 0.72f);

	public static readonly Color RankBronze = new Color(0.72f, 0.45f, 0.24f);

	public static readonly Color RankOther = new Color(0.82f, 0.84f, 0.87f);

	// A chart place: gold, silver and bronze for the top three.
	public static void RankBadge(Transform parent, int rank, float x, float y, float size)
	{
		Color c = rank switch
		{
			1 => RankGold,
			2 => RankSilver,
			3 => RankBronze,
			_ => RankOther,
		};
		Image badge = NewImage("Rank", parent, c);
		UIKit.Place(badge.gameObject, x, y, size, size);
		GameObject go = UIKit.Make("BoldText", badge.transform);
		UIKit.Stretch(go.transform);
		Text t = go.GetComponent<Text>();
		t.text = rank.ToString();
		t.alignment = TextAnchor.MiddleCenter;
		t.fontSize = (int)(size * 0.55f);
		t.resizeTextForBestFit = false;
		t.color = (rank <= 3) ? Color.white : new Color(0.25f, 0.27f, 0.3f);
		t.raycastTarget = false;
	}

	// Recolours this window's own frame: its border, background, title bar and title text (the awards ceremony's
	// black and gold). Only this window's copy of the frame changes.
	public void Theme(Color border, Color background, Color bar, Color title)
	{
		if (!EnsureCreated())
		{
			return;
		}
		Image frameImage = frame.GetComponent<Image>();
		if ((bool)frameImage)
		{
			frameImage.color = border;
		}
		Tint(frame.Find("Fill_Window"), background);
		Tint(frame.Find("Fill_Titleleiste"), bar);
		Tint(frame.Find("Titelleiste"), bar);
		titleText.color = title;
	}

	// The title bar in another colour, keeping the game's gloss and edges: its picture in grey, tinted.
	public void TitleAccent(Color bar, Color text)
	{
		if (!EnsureCreated())
		{
			return;
		}
		foreach (string name in new string[2] { "Fill_Titleleiste", "Titelleiste" })
		{
			Transform t = frame.Find(name);
			Image img = (t ? t.GetComponent<Image>() : null);
			if (!img)
			{
				continue;
			}
			if (!titleSprites.TryGetValue(img, out var original))
			{
				original = img.sprite;
				titleSprites[img] = original;
			}
			Sprite grey = SpriteTools.GreyCopy(original);
			if ((bool)grey)
			{
				img.sprite = grey;
			}
			img.color = bar;
		}
		titleText.color = text;
	}

	private static void Tint(Transform t, Color c)
	{
		Image image = (t ? t.GetComponent<Image>() : null);
		if ((bool)image)
		{
			image.color = c;
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
		component.text = ((Skin != null) ? Skin.Fix(text) : text);
		component.alignment = align;
		if (size > 0)
		{
			component.fontSize = size;
			component.resizeTextForBestFit = false;
		}
		component.horizontalOverflow = HorizontalWrapMode.Wrap;
		component.verticalOverflow = VerticalWrapMode.Truncate;
		if (Skin != null)
		{
			component.color = Skin.Ink;
		}
		return component;
	}

	// A page's short explanation at the top, between the Back button and the money: a long one shrinks a little
	// rather than being cut off.
	public Text AboutLine(string text)
	{
		Text line = Label(text, 116f, 2f, Width - 116f - 196f, 48f, TextAnchor.MiddleLeft, bold: false, null, 12);
		line.resizeTextForBestFit = true;
		line.resizeTextMinSize = 9;
		line.resizeTextMaxSize = 12;
		return line;
	}

	// The money you have, top right (next to the about line).
	public Text MoneyLine()
	{
		return Label(G.Money(((bool)G.MS) ? G.MS.money : 0), Width - 184f, 10f, 170f, 28f, TextAnchor.MiddleRight, bold: true);
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
		Skin?.DressButton(go, primary: true);
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
		Skin?.DressButton(go, primary: false);
		return component;
	}

	public void Header(string text, float x, float y, float w = 254f, Transform parent = null)
	{
		if (Skin != null)
		{
			// Capitals in the accent colour over a thin rule, brighter at its start.
			Transform p = parent ?? Body;
			Text t = Label(Skin.CapsHeaders ? text.ToUpperInvariant() : text, x + 2f, y, w - 4f, 22f, TextAnchor.MiddleLeft, bold: true, p, 14);
			t.color = Skin.AccentLight;
			Image rule = NewImage("Rule", p, new Color(Skin.Accent.r, Skin.Accent.g, Skin.Accent.b, 0.35f));
			UIKit.Place(rule.gameObject, x, y + 23f, w, 1f);
			Image bright = NewImage("RuleBright", p, Skin.Accent);
			UIKit.Place(bright.gameObject, x, y + 22.5f, Mathf.Min(120f, w * 0.3f), 2f);
			return;
		}
		GameObject go = UIKit.Make("Header", parent ?? Body);
		UIKit.Place(go, x, y, w, 24f);
		Transform label = go.transform.Find("Text");
		UIKit.Stretch(label, 8f, 0f, 8f);
		label.GetComponent<Text>().text = text;
	}

	public RectTransform Panel(float x, float y, float w, float h, Transform parent = null)
	{
		GameObject go = UIKit.Make("Panel", parent ?? Body);
		Skin?.Box(go, Skin.Panel, Skin.PanelLine);
		return UIKit.Place(go, x, y, w, h);
	}

	public void Info(string text, float x, float y, float w, float h, Transform parent = null)
	{
		if (Skin != null)
		{
			RectTransform box = Panel(x, y, w, h, parent);
			Image stripe = NewImage("Stripe", box, Skin.Accent);
			UIKit.Place(stripe.gameObject, 0f, 4f, 3f, h - 8f);
			Label(text, 14f, 2f, w - 22f, h - 4f, TextAnchor.MiddleLeft, bold: false, box, 13);
			return;
		}
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
			Text key = Label(rows[i].Key, cx + 6f, cy, colWidth * 0.45f - 6f, 26f, TextAnchor.MiddleLeft, bold: false, panel, 13);
			if (Skin != null)
			{
				key.color = Skin.InkDim;
			}
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
		Skin?.DressDropdown(go);
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
		if (Skin != null)
		{
			label.GetComponent<Text>().color = Skin.Ink;
		}
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
		Skin?.DressInput(go);
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
		Skin?.DressScroll(go);
		return new ListView(go.GetComponent<ScrollRect>(), w - 38f, Skin);
	}

	public void Empty(string text, float y)
	{
		GameObject go = UIKit.Make("Empty", Body);
		RectTransform rt = (RectTransform)go.transform;
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.anchoredPosition = new Vector2(0f, 0f - y);
		go.transform.Find("TextTitle").GetComponent<Text>().text = text;
		if (Skin != null)
		{
			go.transform.Find("TextTitle").GetComponent<Text>().color = Skin.InkDim;
		}
		Animation anim = go.GetComponent<Animation>();
		if ((bool)anim)
		{
			anim.enabled = false;
		}
	}
}
