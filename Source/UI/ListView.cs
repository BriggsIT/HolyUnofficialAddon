using System;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI;

// A scrolling list of game-style rows. Clear() lets a page refill just the list (e.g. while typing in a search box).
// In a skinned window (UI/Skin) the rows are drawn in the skin's colours.
internal class ListView
{
	public readonly ScrollRect Scroll;

	private readonly RectTransform content;

	private readonly float rowWidth;

	private readonly Skin skin;

	public int Count { get; private set; }

	public ListView(ScrollRect scroll, float rowWidth, Skin skin = null)
	{
		Scroll = scroll;
		content = scroll.content;
		this.rowWidth = rowWidth;
		this.skin = skin;
	}

	// keepScroll: refilling after a click on a row, so the list shouldn't jump back to the top.
	public void Clear(bool keepScroll = false)
	{
		for (int i = content.childCount - 1; i >= 0; i--)
		{
			GameObject child = content.GetChild(i).gameObject;
			child.SetActive(value: false);
			UnityEngine.Object.Destroy(child);
		}
		if (!keepScroll)
		{
			content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0f);
		}
		Count = 0;
	}

	public RowView Row(float height = 32f, Action onClick = null, string tooltip = null, bool enabled = true)
	{
		GameObject go = UIKit.Make("Row", content);
		((RectTransform)go.transform).sizeDelta = new Vector2(rowWidth, height);
		LayoutElement layout = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
		layout.preferredHeight = height;
		layout.preferredWidth = rowWidth;
		Button component = go.GetComponent<Button>();
		if (onClick != null && enabled)
		{
			UIKit.OnClick(component, () =>
			{
				UIKit.Sound();
				onClick();
			});
		}
		else
		{
			component.transition = Selectable.Transition.None;
		}
		if (tooltip != null)
		{
			UIKit.SetTooltip(go, tooltip);
		}
		skin?.DressRow(go);
		Count++;
		return new RowView(go.transform, height, rowWidth, skin);
	}
}

internal class RowView
{
	public readonly Transform T;

	private readonly float h;

	public readonly float W;

	private readonly Skin skin;

	public RowView(Transform t, float height, float width, Skin skin = null)
	{
		T = t;
		h = height;
		W = width;
		this.skin = skin;
	}

	public Text Text(string text, float x, float w, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft, float y = -1f, float th = 30f)
	{
		GameObject go = UIKit.Make(bold ? "RowBold" : "RowText", T);
		UIKit.Place(go, x, (y < 0f) ? ((h - th) / 2f) : y, w, th);
		Text component = go.GetComponent<Text>();
		component.text = ((skin != null) ? skin.Fix(text) : text);
		component.alignment = align;
		if (skin != null)
		{
			component.color = skin.Ink;
		}
		return component;
	}

	public Text Top(string text, float x, float w, bool bold = true, TextAnchor align = TextAnchor.MiddleLeft)
	{
		return Text(text, x, w, bold, align, Mathf.Max(1f, h / 2f - 21f), 20f);
	}

	public Text Bottom(string text, float x, float w, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
	{
		Text t = Text(text, x, w, bold, align, Mathf.Min(h - 21f, h / 2f + 1f), 20f);
		if (skin != null)
		{
			t.color = skin.InkDim;
		}
		return t;
	}

	public Image Icon(string sprite, float x, float size = 28f, string tooltip = null)
	{
		GameObject go = UIKit.Make("RowIcon", T);
		UIKit.Place(go, x, (h - size) / 2f, size, size);
		Image component = go.GetComponent<Image>();
		Sprite s = UIKit.Sprite(sprite);
		if ((bool)s)
		{
			component.sprite = s;
		}
		if (tooltip != null)
		{
			UIKit.SetTooltip(go, tooltip);
		}
		return component;
	}

	public Image Picture(Sprite sprite, float x, float size = 28f)
	{
		GameObject go = UIKit.Make("RowIcon", T);
		UIKit.Place(go, x, (h - size) / 2f, size, size);
		Image component = go.GetComponent<Image>();
		if ((bool)sprite)
		{
			component.sprite = sprite;
			component.preserveAspect = true;
		}
		else
		{
			component.enabled = false;
		}
		return component;
	}

	public Button Button(string text, float x, float w, Action onClick, bool enabled = true, string tooltip = null, float bh = 28f)
	{
		GameObject go = UIKit.Make("SmallButton", T);
		UIKit.Place(go, x, (h - bh) / 2f, w, bh);
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
		skin?.DressButton(go, primary: false);
		return component;
	}

	public void Highlight()
	{
		if (skin != null)
		{
			skin.HighlightRow(T);
			return;
		}
		Image image = T.GetComponent<Image>();
		if ((bool)image)
		{
			image.color = UIKit.SelectedRow;
		}
	}

	public void Line(float x)
	{
		GameObject go = UIKit.Make("RowLine", T);
		UIKit.Place(go, x, 2f, 2f, h - 4f);
		if (skin != null && (bool)go.GetComponent<Image>())
		{
			go.GetComponent<Image>().color = new Color(skin.Accent.r, skin.Accent.g, skin.Accent.b, 0.35f);
		}
	}

	public void Bar(float x, float w, float bh, float value, Color color, string text = "", string tooltip = null, float y = -1f)
	{
		GameObject go = UIKit.Make("Bar", T);
		UIKit.Place(go, x, (y < 0f) ? ((h - bh) / 2f) : y, w, bh);
		EditorWindow.StyleBar(go, value, text, color, bh);
		if (tooltip != null)
		{
			UIKit.SetTooltip(go, tooltip);
		}
	}
}
