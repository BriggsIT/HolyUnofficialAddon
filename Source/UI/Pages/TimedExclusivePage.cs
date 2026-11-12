using System.Collections.Generic;
using HolyAddon.Core;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// The chooser behind the new-game screen's "Timed Exclusive" button: which platform gets the game first, and for
// how long. Opens over the new-game screen (EditorWindow.TimedExclusive, an overlay window).
internal class TimedExclusivePage : Page
{
	public override string Title => "Timed Exclusive";

	public override Sprite IconSprite => UpdateIcons.For(UpdateKind.Port);

	public override float Width => 620f;

	public override float Height => 600f;

	public override void Build(EditorWindow w)
	{
		Menu_DevGame menu = TimedExclusiveMenu.Menu;
		List<int> platforms = TimedExclusiveMenu.Platforms(menu);
		w.Label("Release the game on one of its platforms first, as an exclusive. Its maker pays you for the deal, and the game comes to the other platforms when it ends. You develop it for all of them now.", 18f, 8f, w.Width - 36f, 58f, TextAnchor.UpperLeft, bold: false, null, 13);
		w.Header("First on", 14f, 70f, w.Width - 28f);
		ListView list = w.List(13f, 100f, w.Width - 26f, 64f + 52f * 4f);
		RowView off = list.Row(48f, () => Pick(w, -1), "Release on all its platforms at once.");
		if (TimedExclusiveMenu.PickedFirst < 0)
		{
			off.Highlight();
		}
		off.Text("No timed exclusive", 14f, off.W - 28f, bold: true);
		foreach (int id in platforms)
		{
			platformScript p = GameUpdates.Platform(id);
			int pid = id;
			RowView row = list.Row(52f, () => Pick(w, pid), p ? p.GetTooltip() : null);
			if (TimedExclusiveMenu.PickedFirst == id)
			{
				row.Highlight();
			}
			if ((bool)p)
			{
				Image img = new GameObject("Platform", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
				img.transform.SetParent(row.T, worldPositionStays: false);
				UIKit.Place(img.gameObject, 8f, 4f, 44f, 44f);
				img.preserveAspect = true;
				img.raycastTarget = false;
				try
				{
					p.SetPic(img.gameObject);
				}
				catch
				{
					img.enabled = false;
				}
			}
			row.Top(TimedExclusiveMenu.Name(id), 62f, row.W - 72f);
			row.Bottom(TimedExclusiveMenu.DealText(id, TimedExclusiveMenu.PickedMonths), 62f, row.W - 72f);
		}
		float y = 100f + 64f + 52f * 4f + 12f;
		w.Header("For", 14f, y, w.Width - 28f);
		y += 32f;
		float bw = (w.Width - 28f - 4f * 8f) / 5f;
		for (int i = 0; i < TimedExclusives.Windows.Length; i++)
		{
			int m = TimedExclusives.Windows[i];
			bool on = TimedExclusiveMenu.PickedMonths == m;
			w.SmallButton(on ? ("[" + m + " months]") : (m + " months"), 14f + (float)i * (bw + 8f), y, bw, () =>
			{
				TimedExclusiveMenu.PickedMonths = m;
				w.Rebuild();
			}, TimedExclusiveMenu.PickedFirst >= 0 && !on, "Longer deals pay more, but the other platforms wait longer.", null, 30f);
		}
		y += 44f;
		w.BigButton("Done", w.Width - 214f, y, 200f, 40f, w.Close);
	}

	private static void Pick(EditorWindow w, int platform)
	{
		TimedExclusiveMenu.PickedFirst = platform;
		w.Rebuild();
	}
}
