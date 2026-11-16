using HolyAddon.Core.Design;

namespace HolyAddon.UI.Pages;

// The design assistant's options, behind the small button beside Assist on the game design screen: which parts the
// Assist button fills in, keeping your main genre, how much it knows, the colours; what it did last time.
internal class DesignAssistPage : Page
{
	public override string Title => "Design Assistant";

	public override string Icon => "iconOptions";

	public override float Width => 600f;

	public override float Height => 620f;

	public override void Build(EditorWindow w)
	{
		w.Label("The Assist button fills in the parts ticked here, using what your studio has learned from its games' review reports (or everything, with Reveal Every Fit). A part it can't fill yet is left as it is.", 18f, 8f, w.Width - 36f, 50f, UnityEngine.TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 64f;
		w.Header("Parts", 14f, y, w.Width - 28f);
		y += 32f;
		float col = (w.Width - 48f) / 2f;
		int rows = (DesignAssist.Parts.Length + 1) / 2;
		for (int i = 0; i < DesignAssist.Parts.Length; i++)
		{
			int part = i;
			float x = 24f + (float)(i / rows) * col;
			float py = y + (float)(i % rows) * 30f;
			w.Toggle(DesignAssist.PartNames[i], x, py, col - 10f, DesignAssist.PartOn(i), (bool v) => DesignAssist.SetPart(part, v), DesignAssist.PartTips[i]);
		}
		y += (float)rows * 30f + 10f;
		w.Header("How It Works", 14f, y, w.Width - 28f);
		y += 32f;
		w.Toggle("Keep My Main Genre", 24f, y, col - 10f, Plugin.DesignKeepMainGenre.Value, (bool v) => Plugin.DesignKeepMainGenre.Value = v, "When you've already picked a main genre, Assist builds the game around it instead of picking the most promising one.");
		w.Toggle("Reveal Every Fit", 24f + col, y, col - 10f, Plugin.DesignRevealAll.Value, (bool v) =>
		{
			Plugin.DesignRevealAll.Value = v;
			RefreshScreen();
		}, "Know every fit and the right design settings, as the game's sandbox options do: topics, age groups, sub-genres, sliders and work priorities. Off: only what your studio has learned from its games' review reports, as the game does.");
		y += 30f;
		w.Toggle("Colour-Blind Colours", 24f, y, col - 10f, Plugin.DesignColourBlind.Value, (bool v) =>
		{
			Plugin.DesignColourBlind.Value = v;
			RefreshScreen();
		}, "Blue for a good fit and orange for a bad one, instead of the game's green and red.");
		w.SmallButton("Default Parts", 24f + col + 34f, y, 150f, () =>
		{
			DesignAssist.ResetParts();
			w.Rebuild();
		}, enabled: true, "Every part except Licence and Name.");
		y += 40f;
		w.Header("Last Time", 14f, y, w.Width - 28f);
		y += 30f;
		// (The window's body is its height less the title bar.)
		float by = w.Height - 75f - 50f;
		w.Label(string.IsNullOrEmpty(DesignAssist.Last) ? "Not used yet." : DesignAssist.Last, 18f, y, w.Width - 36f, by - y - 6f, UnityEngine.TextAnchor.UpperLeft, bold: false, null, 12);
		w.BigButton("Assist Now", w.Width - 428f, by, 200f, 40f, () => DesignAssist.Run(ExtraTopicsMenu.Menu), DesignKnowledge.On);
		w.BigButton("Done", w.Width - 214f, by, 200f, 40f, w.Close);
	}

	// The screen's sliders show the change at once.
	private static void RefreshScreen()
	{
		Menu_DevGame menu = ExtraTopicsMenu.Menu;
		if ((bool)menu && menu.gameObject.activeInHierarchy)
		{
			menu.UpdateDesignSettings();
		}
	}
}
