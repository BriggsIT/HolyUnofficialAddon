using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.Contracts;
using HolyAddon.UI.Pages;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HolyAddon.UI;

// Running contracts as on-screen cards built from the game's own game card (GUI_Main.uiPrefabs[11], gameTab):
// its frame, title, minimise button, the white info box with its icon slots and two rows (BGData), the icon strip
// (BGInfos) and the bar chart (BGCharts), with the game's scripts stripped and our contract shown in them.
// The cards live in their own stack, not the game's column, because the game sorts and filters every card there.
// Drag any card to move the stack; click one to open Contracts on the Active tab.
internal static class ContractTracker
{
	private const float CardWidth = 220f;

	private const float BlockX = 7.5f;

	private const float BlockWidth = 205f;

	private const float FoldedHeight = 34f;

	private const float FullHeight = 222f;

	private const float Gap = 6f;

	private const int MaxCards = 6;

	// Pieces copied from the game's card, kept inactive so nothing runs on them.
	private class Template
	{
		public Sprite Frame;

		public Image.Type FrameType = Image.Type.Sliced;

		public Color FrameColor = Color.white;

		public Material FrameMaterial;

		public GameObject Title;

		public GameObject Fold;

		public Image FoldIcon;

		public Sprite FoldOpen;

		public Sprite FoldClosed;

		public GameObject Data;

		public GameObject Infos;

		public GameObject Charts;

		public Font Font;

		public Color BarColor = new Color(0.2f, 0.27f, 0.36f);

		public bool Complete
		{
			get
			{
				if ((bool)Title && (bool)Data && (bool)Infos)
				{
					return Charts;
				}
				return false;
			}
		}
	}

	private static RectTransform stack;

	private static Template template;

	private static int drawn = -1;

	private static bool logged;

	public static void Tick()
	{
		bool wanted = Plugin.ContractTracker.Value && G.Singleplayer && BottomButton.BarVisible && ContractBook.Active.Any();
		if (!wanted)
		{
			if ((bool)stack && stack.gameObject.activeSelf)
			{
				stack.gameObject.SetActive(value: false);
			}
			return;
		}
		if (!stack)
		{
			if (!Create())
			{
				return;
			}
			drawn = -1;
		}
		if (!stack.gameObject.activeSelf)
		{
			stack.gameObject.SetActive(value: true);
		}
		if (drawn != ContractBook.Version)
		{
			Redraw();
		}
	}

	private static bool Create()
	{
		GUI_Main gui = G.Gui;
		if (!gui || !UIKit.Init())
		{
			return false;
		}
		Transform canvas = gui.transform;
		GameObject go = new GameObject("Editor_ContractTracker", typeof(RectTransform));
		stack = (RectTransform)go.transform;
		stack.SetParent(canvas, worldPositionStays: false);
		// Just above the game's left-hand news so every menu still opens on top of it.
		Transform news = canvas.Find("GUI_LeftNews");
		stack.SetSiblingIndex(((bool)news) ? (news.GetSiblingIndex() + 1) : 0);
		stack.anchorMin = stack.anchorMax = new Vector2(0f, 1f);
		stack.pivot = new Vector2(0f, 1f);
		if (Plugin.TrackerLeft.Value < 0f)
		{
			// 0.5.0 kept the panel's bottom-left corner; the cards hang down from their top-left corner instead.
			float height = ((canvas is RectTransform rt) ? rt.rect.height : 1080f);
			Plugin.TrackerLeft.Value = Plugin.TrackerX.Value;
			Plugin.TrackerTop.Value = Mathf.Max(0f, height - Plugin.TrackerY.Value - 70f);
		}
		stack.anchoredPosition = new Vector2(Plugin.TrackerLeft.Value, 0f - Plugin.TrackerTop.Value);
		template = LoadTemplate(gui);
		return true;
	}

	private static Template LoadTemplate(GUI_Main gui)
	{
		Template t = new Template();
		GameObject probe = UIKit.Make("Paragraph", stack);
		t.Font = probe.GetComponent<Text>().font;
		Object.DestroyImmediate(probe);
		if (gui.uiPrefabs == null || gui.uiPrefabs.Length <= 11 || !gui.uiPrefabs[11])
		{
			return t;
		}
		GameObject holder = new GameObject("Editor_CardTemplate", typeof(RectTransform));
		holder.SetActive(value: false);
		holder.transform.SetParent(gui.transform, worldPositionStays: false);
		GameObject card = Object.Instantiate(gui.uiPrefabs[11], holder.transform, worldPositionStays: false);
		gameTab tab = card.GetComponent<gameTab>();
		Image frame = card.GetComponent<Image>();
		if ((bool)frame)
		{
			t.Frame = frame.sprite;
			t.FrameType = frame.type;
			t.FrameColor = frame.color;
			t.FrameMaterial = frame.material;
		}
		if (!logged)
		{
			logged = true;
			LogPrefab(card, tab);
		}
		if ((bool)tab && tab.uiObjects != null && tab.uiObjects.Length > 12)
		{
			t.Title = tab.uiObjects[0];
			Transform full = (((bool)tab.uiObjects[7]) ? tab.uiObjects[7].transform : null);
			t.Data = (((bool)full) ? full.Find("BGData")?.gameObject : null);
			t.Infos = (((bool)full) ? full.Find("BGInfos")?.gameObject : null);
			t.Charts = tab.uiObjects[12];
			Text title = (((bool)t.Title) ? t.Title.GetComponent<Text>() : null);
			if ((bool)title)
			{
				t.Font = title.font;
			}
			Image bar = ((tab.uiBalken != null && tab.uiBalken.Length > 0 && (bool)tab.uiBalken[0]) ? tab.uiBalken[0].GetComponent<Image>() : null);
			if ((bool)bar)
			{
				t.BarColor = bar.color;
			}
			if (tab.uiSprites != null && tab.uiSprites.Length > 1)
			{
				t.FoldOpen = tab.uiSprites[0];
				t.FoldClosed = tab.uiSprites[1];
			}
			Button fold = card.GetComponentsInChildren<Button>(includeInactive: true).FirstOrDefault((Button b) => b.onClick.GetPersistentEventCount() > 0 && b.onClick.GetPersistentMethodName(0) == "BUTTON_Minimize");
			if ((bool)fold)
			{
				t.Fold = fold.gameObject;
				t.FoldIcon = (((bool)tab.uiObjects[8] && tab.uiObjects[8].transform.IsChildOf(fold.transform)) ? tab.uiObjects[8].GetComponent<Image>() : fold.GetComponentsInChildren<Image>(includeInactive: true).LastOrDefault());
			}
		}
		Strip(card);
		return t;
	}

	// Remove the game's logic from the copy: its scripts, its button actions and its text-ID labels and tooltips.
	private static void Strip(GameObject card)
	{
		foreach (MonoBehaviour mb in card.GetComponentsInChildren<MonoBehaviour>(includeInactive: true))
		{
			if ((bool)mb && (mb is setText || (mb.GetType().Namespace == null && !(mb is tooltip))))
			{
				Object.DestroyImmediate(mb);
			}
		}
		foreach (Button b in card.GetComponentsInChildren<Button>(includeInactive: true))
		{
			b.onClick = new Button.ButtonClickedEvent();
		}
		foreach (tooltip tip in card.GetComponentsInChildren<tooltip>(includeInactive: true))
		{
			tip.textID = -1;
			tip.textArray = "";
			tip.c = "";
		}
	}

	private static void LogPrefab(GameObject card, gameTab tab)
	{
		List<string> lines = new List<string>();
		foreach (RectTransform rt in card.GetComponentsInChildren<RectTransform>(includeInactive: true))
		{
			if (rt.name.Length <= 2 || rt.parent.name == "PanelChartsOnline")
			{
				continue;
			}
			int index = (((bool)tab && tab.uiObjects != null) ? System.Array.IndexOf(tab.uiObjects, rt.gameObject) : (-1));
			string parts = string.Join(",", from c in rt.GetComponents<Component>()
				where !(c is RectTransform) && !(c is CanvasRenderer)
				select c.GetType().Name);
			lines.Add("  " + Path(card.transform, rt) + ((index >= 0) ? (" = uiObjects[" + index + "]") : "") + " " + Mathf.RoundToInt(rt.rect.width) + "x" + Mathf.RoundToInt(rt.rect.height) + " at " + Mathf.RoundToInt(rt.anchoredPosition.x) + "," + Mathf.RoundToInt(rt.anchoredPosition.y) + " [" + parts + "]");
		}
		Plugin.Log.LogInfo("Game card prefab:\n" + string.Join("\n", lines.Take(160)));
	}

	private static string Path(Transform root, Transform t)
	{
		List<string> parts = new List<string>();
		while ((bool)t && t != root)
		{
			parts.Insert(0, t.name);
			t = t.parent;
		}
		return (parts.Count == 0) ? root.name : string.Join("/", parts);
	}

	private static void Redraw()
	{
		drawn = ContractBook.Version;
		if (!stack)
		{
			return;
		}
		for (int i = stack.childCount - 1; i >= 0; i--)
		{
			GameObject child = stack.GetChild(i).gameObject;
			child.SetActive(value: false);
			Object.Destroy(child);
		}
		List<Contract> running = ContractBook.Active.ToList();
		float y = 0f;
		foreach (Contract c in running.Take(MaxCards))
		{
			y += Card(c, y) + Gap;
		}
		if (running.Count > MaxCards)
		{
			Text more = Label(stack, "and " + (running.Count - MaxCards) + " more contracts...", 4f, y, CardWidth - 8f, 20f, 12, FontStyle.Italic, Color.white);
			Outline outline = more.gameObject.AddComponent<Outline>();
			outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
			y += 22f;
		}
		stack.sizeDelta = new Vector2(CardWidth, y);
		Clamp();
	}

	// One contract's card; returns its height.
	private static float Card(Contract c, float y)
	{
		bool folded = c.Folded;
		float height = (folded ? FoldedHeight : FullHeight);
		GameObject card = new GameObject("Contract_" + c.Id, typeof(RectTransform), typeof(Image), typeof(Button));
		RectTransform rt = (RectTransform)card.transform;
		rt.SetParent(stack, worldPositionStays: false);
		UIKit.Place(card, 0f, y, CardWidth, height);
		Image frame = card.GetComponent<Image>();
		if ((bool)template.Frame)
		{
			frame.sprite = template.Frame;
			frame.type = template.FrameType;
			frame.material = template.FrameMaterial;
		}
		frame.color = template.FrameColor;
		Button open = card.GetComponent<Button>();
		open.transition = Selectable.Transition.None;
		UIKit.OnClick(open, OpenWindow);
		card.AddComponent<TrackerDrag>();
		UIKit.SetTooltip(card, ContractsPage.Details(c) + "\n<color=grey>Click to open Contracts, drag to move the cards.</color>");
		Title(rt, c);
		Fold(rt, c);
		if (folded)
		{
			return height;
		}
		publisherScript firm = Firms.Find(c.FirmId);
		if (template.Complete)
		{
			Data(rt, c, firm);
			Infos(rt, c, firm);
			Chart(rt, c);
		}
		else
		{
			Plain(rt, c, firm);
		}
		string weeks = (c.Late ? ("Running late · week " + Mathf.Min(c.WeeksDone + 1, c.WeeksTotal) + "/" + c.WeeksTotal) : ("Week " + Mathf.Min(c.WeeksDone + 1, c.WeeksTotal) + " of " + c.WeeksTotal));
		Label(rt, weeks, 9f, 197f, 130f, 22f, 13, FontStyle.Bold, c.Late ? UIKit.Orange : UIKit.Green);
		Text price = Label(rt, G.Money(c.Price), 100f, 197f, CardWidth - 109f, 22f, 13, FontStyle.Bold, new Color(0.15f, 0.15f, 0.15f));
		price.alignment = TextAnchor.MiddleRight;
		return height;
	}

	private static void Title(RectTransform card, Contract c)
	{
		Text text;
		if ((bool)template.Title)
		{
			GameObject go = Object.Instantiate(template.Title, card, worldPositionStays: false);
			go.SetActive(value: true);
			UIKit.Place(go, BlockX, 0f, BlockWidth, FoldedHeight);
			text = go.GetComponent<Text>();
			int size = text.fontSize;
			text.resizeTextForBestFit = true;
			text.resizeTextMaxSize = size;
			text.resizeTextMinSize = Mathf.Min(size, 10);
		}
		else
		{
			text = Label(card, "", BlockX, 0f, BlockWidth - 14f, FoldedHeight, 14, FontStyle.Bold, new Color(0.2f, 0.2f, 0.2f));
			text.alignment = TextAnchor.MiddleCenter;
		}
		text.text = CardTitle(c);
		text.raycastTarget = false;
	}

	// The game's own round minimise button, where the game puts it.
	private static void Fold(RectTransform card, Contract c)
	{
		GameObject go;
		if ((bool)template.Fold)
		{
			go = Object.Instantiate(template.Fold, card, worldPositionStays: false);
			go.SetActive(value: true);
			RectTransform src = (RectTransform)template.Fold.transform;
			RectTransform rt = (RectTransform)go.transform;
			// Keep its spot relative to the card's top-right corner, like on the game's cards.
			rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
			rt.pivot = src.pivot;
			rt.sizeDelta = src.rect.size;
			rt.anchoredPosition = OffsetFromTopRight(src);
			Image icon = (((bool)template.FoldIcon) ? go.GetComponentsInChildren<Image>(includeInactive: true).FirstOrDefault((Image im) => im.name == template.FoldIcon.name) : null);
			Sprite sprite = (c.Folded ? template.FoldClosed : template.FoldOpen);
			if ((bool)icon && (bool)sprite)
			{
				icon.sprite = sprite;
			}
		}
		else
		{
			go = UIKit.Make("SmallButton", card);
			foreach (Transform t in go.transform)
			{
				UIKit.Stretch(t);
			}
			go.transform.Find("Text").GetComponent<Text>().text = (c.Folded ? "+" : "-");
			UIKit.Place(go, CardWidth - 28f, 6f, 22f, 22f);
		}
		UIKit.OnClick(go.GetComponent<Button>(), () =>
		{
			UIKit.Sound();
			c.Folded = !c.Folded;
			ContractBook.Touch();
		});
		UIKit.SetTooltip(go, c.Folded ? "Show the full card." : "Minimise the card.");
	}

	// Where an element sits relative to the top-right corner of a 220 x 217 card, whatever its own anchors.
	private static Vector2 OffsetFromTopRight(RectTransform src)
	{
		RectTransform parent = src.parent as RectTransform;
		Vector2 size = (((bool)parent) ? parent.rect.size : new Vector2(CardWidth, 217f));
		Vector2 anchor = new Vector2(Mathf.Lerp(0f, size.x, (src.anchorMin.x + src.anchorMax.x) / 2f), Mathf.Lerp(0f, size.y, (src.anchorMin.y + src.anchorMax.y) / 2f));
		Vector2 pivotPoint = anchor + src.anchoredPosition;
		return pivotPoint - size;
	}

	// The white box: icons along the top (the items or improvements, and the studio's logo in the middle),
	// then two rows like the game's Total and Week.
	private static void Data(RectTransform card, Contract c, publisherScript firm)
	{
		GameObject data = Block(template.Data, card, 34f, 56f);
		Hide(data, "Schublade", "IconPanel/IconPubOffer", "IconPanel/IconCommercialFlop", "IconPanel/IconCommercialHit", "IconPanel/IconGamePass", "IconPanel/IconBugs");
		(string, string) first = ("Studio", ((bool)firm) ? firm.GetName() : c.FirmName);
		(string, string) second = Second(c);
		SetText(data, "TextGesamt", first.Item1);
		SetText(data, "NumGesamt", first.Item2);
		SetText(data, "TextWoche", second.Item1);
		SetText(data, "NumWoche", second.Item2);
		List<(Sprite sprite, bool done)> pictures = Pictures(c);
		string[] slots = new string[5] { "IconPanel/IconGameTyp", "IconPanel/IconPlatformTyp", "IconGenre", "IconGameSize", "IconLicence" };
		for (int i = 0; i < slots.Length; i++)
		{
			Transform slot = data.transform.Find(slots[i]);
			if (!slot)
			{
				continue;
			}
			Image image = slot.GetComponent<Image>();
			if (i < pictures.Count && (bool)image)
			{
				slot.gameObject.SetActive(value: true);
				image.sprite = pictures[i].sprite;
				image.preserveAspect = true;
				image.color = (pictures[i].done ? Color.white : new Color(1f, 1f, 1f, 0.55f));
			}
			else
			{
				slot.gameObject.SetActive(value: false);
			}
		}
		Sprite logo = (((bool)firm) ? firm.GetLogo() : null);
		if ((bool)logo)
		{
			GameObject go = new GameObject("StudioLogo", typeof(RectTransform), typeof(Image));
			go.transform.SetParent(data.transform, worldPositionStays: false);
			UIKit.Place(go, (BlockWidth - 70f) / 2f, 2f, 70f, 24f);
			Image image = go.GetComponent<Image>();
			image.sprite = logo;
			image.preserveAspect = true;
			image.raycastTarget = false;
		}
	}

	private static (string, string) Second(Contract c)
	{
		if (c.IsResearch)
		{
			return ("Done", c.Research.Count((ResearchJob j) => j.Done) + " of " + c.Research.Count);
		}
		if (c.IsAddons)
		{
			return ("Done", c.AddonsDone.Count + " of " + c.Addons.Count);
		}
		return ("Job", GameServices.Name(c.Kind));
	}

	// The icon strip: weeks worked, weeks to go, relations with the studio, the studio's rating.
	private static void Infos(RectTransform card, Contract c, publisherScript firm)
	{
		GameObject infos = Block(template.Infos, card, 93f, 25f);
		int left = Mathf.Max(0, c.WeeksTotal - c.WeeksDone);
		Info(infos, "IconWeekCharts", "IconWeekCharts/NumWeekCharts", c.WeeksDone.ToString(), null, "Weeks worked so far");
		Info(infos, "IconTime", "IconTime/NumTime", left.ToString(), null, "Weeks to go" + (c.Late ? " (running late)" : ""));
		Info(infos, "IconHype", "IconHype/NumHype", ((bool)firm) ? Mathf.RoundToInt(firm.GetRelation()).ToString() : "-", "iconHerz", "Your relations with the studio (out of 100)");
		Info(infos, "IconIP", "IconIP/NumIP", ((bool)firm) ? firm.GetStarsAmount().ToString() : "-", "iconStar", "The studio's rating (out of 5)");
	}

	private static void Info(GameObject block, string iconPath, string numberPath, string value, string sprite, string tip)
	{
		Transform icon = block.transform.Find(iconPath);
		if (!icon)
		{
			return;
		}
		Sprite s = ((sprite != null) ? UIKit.Sprite(sprite) : null);
		if ((bool)s)
		{
			icon.GetComponent<Image>().sprite = s;
		}
		SetText(block, numberPath, value);
		UIKit.SetTooltip(icon.gameObject, tip);
	}

	// The bar chart: one bar per week (or per group of weeks), done weeks solid and climbing, the rest faint,
	// extra weeks from running late in orange.
	private static void Chart(RectTransform card, Contract c)
	{
		GameObject charts = Block(template.Charts, card, 121f, 74f);
		Hide(charts, "PanelChartsOnline");
		Transform panel = charts.transform.Find("PanelCharts");
		if (!panel)
		{
			return;
		}
		List<Image> bars = (from Transform t in panel
			select t.GetComponent<Image>() into i
			where (bool)i
			select i).ToList();
		int weeks = Mathf.Max(1, c.WeeksTotal);
		int used = Mathf.Min(bars.Count, weeks);
		int planned = (c.Late ? Mathf.Max(1, weeks - 2) : weeks);
		for (int i = 0; i < bars.Count; i++)
		{
			Image bar = bars[i];
			if (i >= used)
			{
				bar.fillAmount = 0f;
				continue;
			}
			float end = (float)(i + 1) / (float)used;
			bool done = (float)c.WeeksDone / (float)weeks >= end - 0.001f;
			bool late = c.Late && end * (float)weeks > (float)planned + 0.001f;
			Color color = (late ? UIKit.Orange : template.BarColor);
			bar.color = (done ? color : new Color(color.r, color.g, color.b, 0.25f));
			bar.fillAmount = 0.3f + 0.7f * end;
		}
	}

	private static GameObject Block(GameObject source, RectTransform card, float y, float height)
	{
		GameObject go = Object.Instantiate(source, card, worldPositionStays: false);
		go.SetActive(value: true);
		UIKit.Place(go, BlockX, y, BlockWidth, height);
		foreach (Graphic g in go.GetComponentsInChildren<Graphic>(includeInactive: true))
		{
			g.raycastTarget = g.GetComponent<tooltip>() != null;
		}
		return go;
	}

	private static void Hide(GameObject root, params string[] paths)
	{
		foreach (string path in paths)
		{
			Transform t = root.transform.Find(path);
			if ((bool)t)
			{
				t.gameObject.SetActive(value: false);
			}
		}
	}

	private static void SetText(GameObject root, string path, string value)
	{
		Transform t = root.transform.Find(path);
		Text text = (((bool)t) ? t.GetComponent<Text>() : null);
		if ((bool)text)
		{
			text.text = value;
		}
	}

	// Used only if the game's card ever changes shape: a simple card in the same spirit.
	private static void Plain(RectTransform card, Contract c, publisherScript firm)
	{
		RectTransform info = Box(card, BlockX, 34f, BlockWidth, 60f);
		Label(info, "Studio", 6f, 6f, 70f, 20f, 13, FontStyle.Normal, new Color(0.2f, 0.2f, 0.2f));
		Text a = Label(info, ((bool)firm) ? firm.GetName() : c.FirmName, 70f, 6f, BlockWidth - 76f, 20f, 13, FontStyle.Normal, UIKit.Blue);
		a.alignment = TextAnchor.MiddleRight;
		(string, string) second = Second(c);
		Label(info, second.Item1, 6f, 30f, 70f, 20f, 13, FontStyle.Normal, new Color(0.2f, 0.2f, 0.2f));
		Text b = Label(info, second.Item2, 70f, 30f, BlockWidth - 76f, 20f, 13, FontStyle.Normal, UIKit.Blue);
		b.alignment = TextAnchor.MiddleRight;
		RectTransform chart = Box(card, BlockX, 100f, BlockWidth, 90f);
		Bar(chart, c);
	}

	private static RectTransform Box(Transform parent, float x, float y, float w, float h)
	{
		GameObject go = new GameObject("Box", typeof(RectTransform), typeof(Image));
		go.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rt = UIKit.Place(go, x, y, w, h);
		Image image = go.GetComponent<Image>();
		image.color = Color.white;
		image.raycastTarget = false;
		Outline outline = go.AddComponent<Outline>();
		outline.effectColor = new Color(0.55f, 0.55f, 0.55f);
		return rt;
	}

	private static void Bar(RectTransform chart, Contract c)
	{
		GameObject bar = UIKit.Make("Bar", chart);
		UIKit.Place(bar, 8f, 30f, BlockWidth - 16f, 22f);
		EditorWindow.StyleBar(bar, c.Progress, Mathf.RoundToInt(c.Progress * 100f) + "%", c.Late ? UIKit.Orange : template.BarColor, 22f);
	}

	private static string CardTitle(Contract c)
	{
		if (c.IsResearch)
		{
			if (c.Research.Count == 1)
			{
				return c.Research[0].Name;
			}
			bool topics = c.Research.All((ResearchJob j) => j.Type == 1);
			return c.Research.Count + (topics ? " topics" : " research items");
		}
		return c.Item ?? "Contract";
	}

	private static List<(Sprite, bool)> Pictures(Contract c)
	{
		List<(Sprite, bool)> list = new List<(Sprite, bool)>();
		if (c.IsResearch)
		{
			foreach (ResearchJob j in c.Research)
			{
				list.Add((ResearchCatalog.Icon(j.Type, j.Slot), j.Done));
			}
		}
		else if (c.IsAddons)
		{
			foreach (int slot in c.Addons)
			{
				list.Add((GameServices.AddonIcon(c.Studio, slot), c.AddonsDone.Contains(slot)));
			}
		}
		else
		{
			gameScript game = GameServices.Find(c.GameId);
			if ((bool)game)
			{
				list.Add((game.GetTypSprite(), true));
				list.Add((game.GetPlatformTypSprite(), true));
				list.Add((game.GetSizeSprite(), true));
			}
		}
		return list.Where(((Sprite, bool) p) => (bool)p.Item1).ToList();
	}

	private static Text Label(Transform parent, string text, float x, float y, float w, float h, int size, FontStyle fontStyle, Color color)
	{
		GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text));
		go.transform.SetParent(parent, worldPositionStays: false);
		UIKit.Place(go, x, y, w, h);
		Text t = go.GetComponent<Text>();
		t.font = template.Font;
		t.fontSize = size;
		t.fontStyle = fontStyle;
		t.color = color;
		t.text = text;
		t.alignment = TextAnchor.MiddleLeft;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		t.verticalOverflow = VerticalWrapMode.Truncate;
		t.raycastTarget = false;
		return t;
	}

	private static void OpenWindow()
	{
		if (!G.Gui || G.Gui.menuOpen || EditorWindow.Contracts.IsOpen)
		{
			return;
		}
		ContractsPage.StartTab = ContractsPage.TabActive;
		EditorWindow.Contracts.Open();
	}

	// Keep the cards on screen (after a drag, or when the window size changes).
	public static void Clamp()
	{
		if (!stack || !(stack.parent is RectTransform canvas))
		{
			return;
		}
		Rect area = canvas.rect;
		Vector2 p = stack.anchoredPosition;
		p.x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, area.width - CardWidth));
		p.y = Mathf.Clamp(p.y, 0f - Mathf.Max(0f, area.height - Mathf.Min(stack.sizeDelta.y, 60f)), 0f);
		stack.anchoredPosition = p;
	}

	public static void Moved()
	{
		Clamp();
		Plugin.TrackerLeft.Value = Mathf.Round(stack.anchoredPosition.x);
		Plugin.TrackerTop.Value = Mathf.Round(0f - stack.anchoredPosition.y);
	}

	public static void Drag(Vector2 delta)
	{
		if ((bool)stack)
		{
			stack.anchoredPosition += delta;
			Clamp();
		}
	}
}

// Drags the whole stack of contract cards by any card.
internal class TrackerDrag : MonoBehaviour, IBeginDragHandler, IEventSystemHandler, IDragHandler, IEndDragHandler
{
	private Canvas canvas;

	public void OnBeginDrag(PointerEventData eventData)
	{
		canvas = GetComponentInParent<Canvas>()?.rootCanvas;
	}

	public void OnDrag(PointerEventData eventData)
	{
		float scale = (((bool)canvas) ? canvas.scaleFactor : 1f);
		ContractTracker.Drag(eventData.delta / Mathf.Max(0.01f, scale));
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		ContractTracker.Moved();
	}
}
