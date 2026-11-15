using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core.Topics;
using UnityEngine;

namespace HolyAddon.UI.Pages;

// The chooser behind the design screen's "More Topics" button: the third and fourth topics, what each does for the
// review, and the extra work. Opens over the design screen (EditorWindow.ExtraTopics, an overlay window); each slot
// is picked in the game's own topic picker.
internal class ExtraTopicsPage : Page
{
	public override string Title => "More Topics";

	public override Sprite IconSprite => ExtraTopics.Icon(0);

	public override float Width => 600f;

	public override float Height => 470f;

	public override void Build(EditorWindow w)
	{
		Menu_DevGame menu = ExtraTopicsMenu.Menu;
		int genre = (menu ? menu.g_GameMainGenre : (-1));
		List<int> all = ExtraTopicsMenu.Others(menu);
		w.Label("Give the game more topics. One that suits the genre and goes with the game's other topics lifts the review; one that doesn't, lowers it. Each extra topic adds about 10% to the development cost and time.", 18f, 8f, w.Width - 36f, 58f, TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 74f;
		for (int slot = 0; slot < 2; slot++)
		{
			int s = slot;
			int topic = ExtraTopicsMenu.Pending[slot];
			bool open = ExtraTopicsMenu.Unlocked(slot);
			string reason = ExtraTopics.LockReason(slot);
			w.Header(ExtraTopics.SlotNames[slot], 14f, y, w.Width - 28f);
			y += 32f;
			w.Label((topic >= 0) ? ExtraTopics.TopicName(topic) : (open ? "None" : "<color=#8a8a8a>Locked</color>"), 18f, y, 220f, 26f, TextAnchor.MiddleLeft, bold: true, null, 15);
			string choose = (ExtraTopicsMenu.Locked ? "Taken from the original game." : (open ? null : reason));
			w.SmallButton((topic >= 0) ? "Change..." : "Choose...", w.Width - 262f, y, 120f, () => ExtraTopicsMenu.Choose(s), open && !ExtraTopicsMenu.Locked, choose ?? "Pick it in the game's topic list.");
			w.SmallButton("Remove", w.Width - 134f, y, 120f, () =>
			{
				ExtraTopicsMenu.Pending[s] = -1;
				w.Rebuild();
			}, topic >= 0 && !ExtraTopicsMenu.Locked, ExtraTopicsMenu.Locked ? "Taken from the original game." : null);
			y += 30f;
			string about = (open ? ((topic >= 0) ? Describe(topic, genre, all) : "") : reason);
			w.Label(about, 18f, y, w.Width - 36f, 40f, TextAnchor.UpperLeft, bold: false, null, 12);
			y += 48f;
		}
		w.Header("Altogether", 14f, y, w.Width - 28f);
		y += 32f;
		int[] pending = ExtraTopicsMenu.Pending;
		int count = ExtraTopics.Count(pending);
		string review = "No extra topics.";
		if (count > 0 && (bool)menu)
		{
			int change = ExtraTopics.ReviewChange(genre, menu.g_GameMainTheme, menu.g_GameSubTheme, pending, out var _);
			review = "Review " + ((change > 0) ? ("<color=#3f9a22>+" + change + "%</color>") : ((change < 0) ? ("<color=#c4161c>" + change + "%</color>") : "unchanged")) + ", development cost and time +" + Mathf.RoundToInt(ExtraTopics.WorkPerTopic * 100f * (float)count) + "%.";
		}
		w.Label(review, 18f, y, w.Width - 36f, 26f, TextAnchor.MiddleLeft, bold: true, null, 14);
		y += 40f;
		w.BigButton("Done", w.Width - 214f, y, 200f, 40f, w.Close);
	}

	internal static string Describe(int topic, int genre, List<int> all)
	{
		ExtraTopics.Verdict v = ExtraTopics.Judge(topic, genre, all);
		string genreName = ((genre >= 0) ? Core.Industry.IndustryCore.GenrePlural(genre) : "the genre");
		string text = (v.Suits ? ("Suits " + genreName + ".") : ("<color=#c4161c>Doesn't suit " + genreName + ".</color>"));
		if (v.ClashesWith.Count > 0)
		{
			text = text + " <color=#c4161c>Clashes with " + ExtraTopics.List(v.ClashesWith) + ".</color>";
		}
		else if (v.GoesWith.Count > 0)
		{
			text = text + " Goes with " + ExtraTopics.List(v.GoesWith) + ".";
		}
		else
		{
			text += " Nothing in common with the other topics.";
		}
		return text;
	}
}
