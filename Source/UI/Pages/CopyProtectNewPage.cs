using System.Collections.Generic;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// Start a new copy protection: name, technology and the development room that builds it.
internal class CopyProtectNewPage : Page
{
	private string name = CopyProtectDev.RandomName();

	private int tech = CopyProtectDev.BestTech();

	private int room = CopyProtectPage.PreferredRoom;

	private string status = "";

	public override string Title => "New copy protection";

	public override Sprite IconSprite => CopyProtectDev.Icon();

	public override float Width => 860f;

	public override float Height => 836f;

	public override void Build(EditorWindow w)
	{
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		w.Label("Name", 24f, 52f, 110f, 34f, TextAnchor.MiddleLeft, bold: true);
		w.Input(name, "Name...", 140f, 52f, 360f, (string v) => name = v);
		w.SmallButton("Suggest a name", 512f, 56f, 160f, () =>
		{
			name = CopyProtectDev.RandomName();
			w.Rebuild();
		});
		w.Header("Technology", 14f, 100f, w.Width - 28f);
		int count = CopyProtectDev.TechNames.Length;
		ListView list = w.List(13f, 130f, w.Width - 26f, (float)count * 68f + 10f);
		for (int i = 0; i < count; i++)
		{
			TechRow(w, list, i);
		}
		float y = 130f + (float)count * 68f + 20f;
		w.Label("Room", 24f, y, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
		float work = CopyProtectDev.WorkNeeded(tech, update: false);
		room = CopyProtectPage.RoomDropdown(w, 140f, y, w.Width - 154f, room, work, (int v) =>
		{
			room = v;
			w.Rebuild();
		});
		y += 42f;
		roomScript r = CopyProtectDev.FindRoom(room);
		float strength = CopyProtectDev.ReleaseStrength(tech, r);
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Development costs", G.Money(CopyProtectDev.Cost(tech, update: false))),
			EditorWindow.KV("Time", r ? CopyProtectDev.Duration(work, r) : "-"),
			EditorWindow.KV("Cost per game", "Free"),
			EditorWindow.KV("Best in the shops now", Mathf.RoundToInt(CopyProtectDev.MarketBest()) + "%"),
			EditorWindow.KV("Strength at release", Mathf.RoundToInt(strength) + "%"),
			EditorWindow.KV("Uncracked for", CopyProtectDev.UncrackedOwn + " weeks (" + CopyProtectDev.UncrackedShared + " if licensed)"),
			EditorWindow.KV("Then loses per week", "about " + CopyProtectDev.BaseWear(tech).ToString("0.00"))
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 6f;
		string reason = CopyProtectDev.FreshReason(tech, r);
		w.Label((reason == null) ? "Starts at full strength: nobody has cracked it yet. Weekly loss is for your own games only; each game in the shops adds a little, licensing adds more." : ("Below 100% because " + reason + "."), 24f, y, w.Width - 48f, 36f, TextAnchor.UpperLeft, bold: false, null, 13);
		y += 42f;
		w.BigButton("Start development", 14f, y, 260f, 44f, () =>
		{
			string error = CopyProtectDev.StartNew(name, tech, CopyProtectDev.FindRoom(room));
			if (error == null)
			{
				CopyProtectPage.PreferredRoom = -1;
				w.Back();
				if (w.Current is CopyProtectPage page)
				{
					page.Say("Your team started on " + CopyProtectDev.Project.Name + ".", good: true);
					w.Rebuild();
				}
			}
			else
			{
				status = error;
				w.Rebuild();
			}
		}, r);
		Text line = w.Label(status, 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
	}

	private void TechRow(EditorWindow w, ListView list, int i)
	{
		bool ok = CopyProtectDev.TechUnlocked(i);
		RowView row = list.Row(58f, ok ? (() =>
		{
			tech = i;
			status = "";
			w.Rebuild();
		}) : null, CopyProtectDev.TechInfo[i], ok);
		if (i == tech)
		{
			row.Highlight();
		}
		Image picture = row.Picture(CopyProtectDev.TechIcon(i), 10f, 44f);
		Text top = row.Top(CopyProtectDev.TechNames[i], 64f, 300f);
		Text bottom = row.Bottom(ok ? CopyProtectDev.TechInfo[i] : ("Possible from " + CopyProtectDev.TechYear[i] + "."), 64f, row.W - 350f);
		bottom.horizontalOverflow = HorizontalWrapMode.Wrap;
		if (!ok)
		{
			picture.color = new Color(1f, 1f, 1f, 0.4f);
			top.color = UIKit.Grey;
			bottom.color = UIKit.Grey;
			return;
		}
		float strength = CopyProtectDev.Fresh(i);
		row.Bar(row.W - 280f, 130f, 22f, strength / 100f, UIKit.ValueColor(strength), Mathf.RoundToInt(strength) + "%", "Strength at release (a weak team takes a little off). Once cracked it loses about " + CopyProtectDev.BaseWear(i).ToString("0.00") + " a week when only your own games use it.");
		row.Text(G.Money(CopyProtectDev.Cost(i, update: false)), row.W - 140f, 128f, bold: true, TextAnchor.MiddleRight);
	}
}
