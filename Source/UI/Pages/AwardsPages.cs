using System;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.Awards;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// The MGH Games Awards ceremony: one page per category with its nominees, the winner revealed when you open the
// envelope, then the results. Black and gold on a stage backdrop (Assets/awards/awards-art.html), like a real
// awards night. Prizes are already handed out when the ceremony opens, so closing early loses nothing.
internal class AwardsCeremonyPage : Page
{
	private readonly int step;

	private bool revealed;

	public static readonly Color Gold = new Color(0.886f, 0.722f, 0.306f);

	public static readonly Color GoldLight = new Color(0.965f, 0.867f, 0.541f);

	public static readonly Color GoldDim = new Color(0.62f, 0.49f, 0.22f);

	public static readonly Color Ivory = new Color(0.96f, 0.94f, 0.89f);

	public static readonly Color Muted = new Color(0.73f, 0.67f, 0.55f);

	public static readonly Color Yours = new Color(0.5f, 0.77f, 1f);

	public static readonly Color Won = new Color(0.55f, 0.9f, 0.45f);

	public static readonly Color Lost = new Color(1f, 0.5f, 0.42f);

	public static readonly Color Rust = new Color(0.78f, 0.52f, 0.3f);

	private static readonly Color RowFill = new Color(0f, 0f, 0f, 0.5f);

	private static readonly Color WinnerFill = new Color(0.42f, 0.31f, 0.08f, 0.6f);

	private const float Left = 96f;

	private const float Right = 804f;

	public AwardsCeremonyPage(int step)
	{
		this.step = step;
	}

	private static AwardShowRecord Show => AwardsShow.Current;

	public override string Title => "MGH Games Awards" + ((Show != null) ? (" " + Show.Year) : "");

	public override Sprite IconSprite => UpdateIcons.Award("goty");

	public override float Width => 900f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		w.Theme(Gold, new Color(0.04f, 0.03f, 0.02f), new Color(0.13f, 0.1f, 0.05f), GoldLight);
		Backdrop(w);
		AwardShowRecord show = Show;
		if (show == null || show.Results.Count == 0)
		{
			Text(w, "No awards ceremony has been held yet.", Left, 80f, Right - Left, 30f, 16, Ivory, TextAnchor.MiddleCenter);
			return;
		}
		if (step >= show.Results.Count)
		{
			BuildResults(w, show);
		}
		else
		{
			BuildCategory(w, show, show.Results[step]);
		}
	}

	// ---- One award ----

	private void BuildCategory(EditorWindow w, AwardShowRecord show, AwardResult r)
	{
		Text(w, "AWARD " + (step + 1) + " OF " + show.Results.Count, 0f, 30f, w.Width, 20f, 12, GoldDim, TextAnchor.MiddleCenter, bold: true);
		GoldButton(w, "SKIP TO THE RESULTS", Right - 210f, 26f, 210f, 30f, () => w.Replace(new AwardsCeremonyPage(show.Results.Count)), 12);
		Image trophy = MmoUi.Rect(w.Body, Left, 64f, 104f, 104f, Color.white);
		trophy.sprite = UpdateIcons.Award(r.Id);
		trophy.preserveAspect = true;
		UIKit.SetTooltip(trophy.gameObject, r.About);
		trophy.raycastTarget = true;
		Text(w, r.Dishonour ? "AND THE DISHONOUR" : "NOMINEES FOR", Left + 122f, 70f, 500f, 20f, 13, Gold, TextAnchor.MiddleLeft, bold: true);
		Text(w, r.Name.ToUpperInvariant(), Left + 122f, 90f, Right - Left - 122f, 44f, 30, r.Dishonour ? Rust : Ivory, TextAnchor.MiddleLeft, bold: true);
		Text(w, r.About, Left + 122f, 136f, Right - Left - 122f, 24f, 13, Muted, TextAnchor.MiddleLeft);
		Rule(w, 176f);
		float y = 188f;
		if (r.Nominees.Count == 0)
		{
			Text(w, "No game qualified this year.", Left, y + 20f, Right - Left, 30f, 15, Muted, TextAnchor.MiddleCenter);
		}
		for (int i = 0; i < r.Nominees.Count && i < 5; i++)
		{
			Nominee(w, r.Nominees[i], y, revealed && i == 0, r.Dishonour);
			y += 64f;
		}
		const float bottom = 512f;
		if (r.Nominees.Count == 0 || revealed)
		{
			if (r.Nominees.Count > 0)
			{
				AwardEntry win = r.Nominees[0];
				string line = (r.Dishonour ? "The dishonour goes to " : "And the winner is ") + "<b>" + win.Name + "</b>" + ((win.Studio.Length > 0) ? ("  (" + win.Studio + ")") : "");
				Text(w, line, Left, bottom, Right - Left, 26f, 17, win.Mine ? (r.Dishonour ? Lost : Yours) : Ivory, TextAnchor.MiddleLeft);
				if (win.Mine && r.Prize.Length > 0)
				{
					Text(w, (r.Dishonour ? "It costs you: " : "Your prize: ") + r.Prize + ".", Left, bottom + 28f, Right - Left - 240f, 40f, 13, r.Dishonour ? Lost : Won, TextAnchor.UpperLeft);
				}
			}
			bool last = step + 1 >= Show.Results.Count;
			GoldButton(w, last ? "SEE ALL THE WINNERS" : "NEXT AWARD", Right - 220f, bottom + 50f, 220f, 42f, () => w.Replace(new AwardsCeremonyPage(step + 1)), 15);
		}
		else
		{
			GoldButton(w, "OPEN THE ENVELOPE", (w.Width - 300f) / 2f, bottom + 22f, 300f, 48f, () =>
			{
				revealed = true;
				AwardEntry win2 = r.Nominees[0];
				AwardsShow.Sound((win2.Mine != r.Dishonour) ? 44 : 45);
				w.Rebuild();
			}, 17);
		}
	}

	private static void Nominee(EditorWindow w, AwardEntry e, float y, bool winner, bool dishonour)
	{
		float width = Right - Left;
		Image row = MmoUi.Rect(w.Body, Left, y, width, 58f, winner ? WinnerFill : RowFill);
		if (winner)
		{
			Border(w.Body, Left, y, width, 58f, dishonour ? Rust : Gold, 2f);
		}
		gameScript g = ((e.GameId >= 0) ? GameUpdates.FindGame(e.GameId) : null);
		if ((bool)g)
		{
			MmoUi.Shot(w, g, Left + 8f, y + 5f, 86f, 48f);
			Border(w.Body, Left + 8f, y + 5f, 86f, 48f, GoldDim, 1f);
		}
		else if (e.CompanyId >= 0)
		{
			publisherScript p = Company(e.CompanyId);
			Image logo = MmoUi.Rect(w.Body, Left + 27f, y + 5f, 48f, 48f, Color.white);
			logo.sprite = (p ? p.GetLogo() : null);
			logo.preserveAspect = true;
			if (!logo.sprite)
			{
				logo.enabled = false;
			}
		}
		string name = e.Name + (e.Mine ? "  <color=#80c4ff>(yours)</color>" : "");
		Text(w, name, Left + 108f, y + 7f, width - 260f, 24f, winner ? 18 : 16, winner ? GoldLight : Ivory, TextAnchor.MiddleLeft, bold: true);
		Text(w, ((e.Studio.Length > 0) ? (e.Studio + "   ") : "") + e.Stat, Left + 108f, y + 31f, width - 260f, 22f, 13, Muted, TextAnchor.MiddleLeft);
		if (winner)
		{
			Text(w, dishonour ? "WORST" : "WINNER", Right - 150f, y, 136f, 58f, 18, dishonour ? Rust : Gold, TextAnchor.MiddleRight, bold: true);
		}
	}

	// ---- All the winners ----

	private void BuildResults(EditorWindow w, AwardShowRecord show)
	{
		Text(w, "THE WINNERS OF " + show.Year, 0f, 28f, w.Width, 34f, 24, GoldLight, TextAnchor.MiddleCenter, bold: true);
		Rule(w, 68f);
		float y = 78f;
		foreach (AwardResult r in show.Results)
		{
			AwardEntry win = r.Nominees.FirstOrDefault();
			bool mine = win != null && win.Mine;
			Image row = MmoUi.Rect(w.Body, Left, y, Right - Left, 31f, (mine && !r.Dishonour) ? WinnerFill : RowFill);
			UIKit.SetTooltip(row.gameObject, r.About);
			row.raycastTarget = true;
			Image icon = MmoUi.Rect(w.Body, Left + 6f, y + 2f, 27f, 27f, Color.white);
			icon.sprite = UpdateIcons.Award(r.Id);
			icon.preserveAspect = true;
			Text(w, r.Name, Left + 42f, y, 220f, 31f, 14, r.Dishonour ? Rust : Gold, TextAnchor.MiddleLeft, bold: true);
			string who = ((win == null) ? "<color=#8a7f6a>nobody qualified</color>" : ("<b>" + win.Name + "</b>" + ((win.Studio.Length > 0) ? ("  <color=#b9aa8a>" + win.Studio + "</color>") : "")));
			Text(w, who, Left + 266f, y, Right - Left - 266f - 130f, 31f, 14, mine ? (r.Dishonour ? Lost : Yours) : Ivory, TextAnchor.MiddleLeft);
			int nominations = r.Nominees.Count((AwardEntry e) => e.Mine);
			if (mine)
			{
				Text(w, r.Dishonour ? "YOURS" : "YOU WON", Right - 124f, y, 116f, 31f, 13, r.Dishonour ? Lost : Won, TextAnchor.MiddleRight, bold: true);
			}
			else if (nominations > 0)
			{
				Text(w, nominations + ((nominations == 1) ? " nomination" : " nominations"), Right - 124f, y, 116f, 31f, 12, Muted, TextAnchor.MiddleRight);
			}
			y += 34f;
		}
		Rule(w, y + 4f);
		Text(w, show.Summary, Left, y + 12f, Right - Left - 240f, 48f, 14, show.Summary.StartsWith("You won") ? Won : Muted, TextAnchor.UpperLeft);
		GoldButton(w, "CLOSE", Right - 220f, y + 14f, 220f, 42f, w.Close, 15);
	}

	// ---- Drawing ----

	private static void Backdrop(EditorWindow w)
	{
		Sprite bg = UpdateIcons.AwardsBackdrop();
		Image image = MmoUi.Rect(w.Body, 0f, 0f, w.Width, w.Height - 75f, bg ? Color.white : new Color(0.06f, 0.05f, 0.03f));
		image.sprite = bg;
	}

	private static Text Text(EditorWindow w, string text, float x, float y, float width, float height, int size, Color color, TextAnchor align, bool bold = false)
	{
		Text t = w.Label(text, x, y, width, height, align, bold, null, size);
		t.color = color;
		t.horizontalOverflow = HorizontalWrapMode.Wrap;
		return t;
	}

	// A gold rule across the page, brighter in the middle.
	private static void Rule(EditorWindow w, float y)
	{
		MmoUi.Rect(w.Body, Left, y, Right - Left, 1f, GoldDim);
		MmoUi.Rect(w.Body, Left + (Right - Left) * 0.25f, y - 0.5f, (Right - Left) * 0.5f, 2f, Gold);
	}

	private static void Border(Transform parent, float x, float y, float width, float height, Color c, float t)
	{
		MmoUi.Rect(parent, x, y, width, t, c);
		MmoUi.Rect(parent, x, y + height - t, width, t, c);
		MmoUi.Rect(parent, x, y, t, height, c);
		MmoUi.Rect(parent, x + width - t, y, t, height, c);
	}

	// Black with a gold edge and gold capitals; lights up when you point at it.
	private static void GoldButton(EditorWindow w, string text, float x, float y, float width, float height, Action onClick, int size)
	{
		Image fill = MmoUi.Rect(w.Body, x, y, width, height, Color.white);
		fill.raycastTarget = true;
		Button button = fill.gameObject.AddComponent<Button>();
		button.targetGraphic = fill;
		ColorBlock colors = button.colors;
		colors.normalColor = new Color(0.1f, 0.075f, 0.03f, 0.95f);
		colors.highlightedColor = new Color(0.3f, 0.22f, 0.07f, 1f);
		colors.pressedColor = new Color(0.45f, 0.33f, 0.1f, 1f);
		colors.selectedColor = colors.normalColor;
		colors.colorMultiplier = 1f;
		button.colors = colors;
		UIKit.OnClick(button, () =>
		{
			UIKit.Sound();
			onClick();
		});
		Border(w.Body, x, y, width, height, Gold, 2f);
		Border(w.Body, x + 4f, y + 4f, width - 8f, height - 8f, new Color(Gold.r, Gold.g, Gold.b, 0.35f), 1f);
		Text label = Text(w, text, x, y, width, height, size, GoldLight, TextAnchor.MiddleCenter, bold: true);
		label.raycastTarget = false;
	}

	private static publisherScript Company(int id)
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return null;
		}
		return all.FirstOrDefault((publisherScript p) => (bool)p && p.myID == id);
	}
}
