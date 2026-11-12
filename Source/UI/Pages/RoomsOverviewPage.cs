using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.Rooms;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// Every room at a glance: its job and progress, what it does by itself, who supports whom, and what needs a look.
// Click a room to go to it (the camera moves there and its menu opens).
internal class RoomsOverviewPage : Page
{
	// The room whose menu opened the overview: its type is filtered and its row highlighted (taken once per opening).
	public static roomScript FromRoom;

	private readonly roomScript from;

	private int filter = -1;

	public RoomsOverviewPage()
	{
		from = FromRoom;
		FromRoom = null;
	}

	// The week it was drawn for: it redraws (keeping your place in the list) only when a week has passed.
	private int week = -1;

	private ListView list;

	public override string Title => "Rooms Overview";

	public override Sprite IconSprite => RoomIcon;

	public static Sprite RoomIcon => UpdateIcons.Game("iconGebaeude", UpdateIcons.Game("iconBuildRoom", UpdateIcons.Game("iconOptions")));

	public override float Width => 1120f;

	public override float Height => 860f;

	public override void Build(EditorWindow w)
	{
		week = Core.CopyProtection.CopyProtectDev.AbsWeek;
		float W = w.Width;
		List<RoomInfo> rooms = RoomScan.Scan();
		if (filter < 0)
		{
			filter = (from ? Mathf.Max(0, RoomScan.GroupOf(from.typ)) : RoomScan.All);
		}
		int look = rooms.Count((RoomInfo r) => r.Warnings.Count > 0);
		float cw = (W - 28f - 30f) / 4f;
		w.StatCard(RoomIcon, "Rooms", rooms.Count.ToString(), "with jobs to do", 14f, 14f, cw, 80f);
		w.StatCard(UpdateIcons.Game("iconWorkPoints", RoomIcon), "Working", rooms.Count((RoomInfo r) => r.Working).ToString(), "right now", 14f + cw + 10f, 14f, cw, 80f);
		w.StatCard(UpdateIcons.Game("iconAchtung", RoomIcon), "Need a look", look.ToString(), (look > 0) ? "idle, staffing or at risk" : "all fine", 14f + 2f * (cw + 10f), 14f, cw, 80f, (look > 0) ? new Color?(new Color(0.8f, 0.45f, 0.05f)) : null);
		w.StatCard(UpdateIcons.Game("iconPersonal", RoomIcon), "Staff in rooms", rooms.Sum((RoomInfo r) => r.Staff).ToString(), rooms.Sum((RoomInfo r) => r.Desks) + " desks", 14f + 3f * (cw + 10f), 14f, cw, 80f);
		// Filters: one per room type you have, plus All and Needs a Look.
		List<int> groups = new List<int> { RoomScan.All };
		groups.AddRange(rooms.Select((RoomInfo r) => r.Group).Distinct().OrderBy((int g) => g));
		groups.Add(RoomScan.NeedsALook);
		float fx = 14f;
		float fw = Mathf.Min(120f, (W - 28f - (float)(groups.Count - 1) * 6f) / (float)groups.Count);
		foreach (int g2 in groups)
		{
			int count = ((g2 == RoomScan.All) ? rooms.Count : ((g2 == RoomScan.NeedsALook) ? look : rooms.Count((RoomInfo r) => r.Group == g2)));
			int gg = g2;
			if (filter == g2)
			{
				MmoUi.Rect(w.Body, fx, 140f, fw, 3f, UIKit.Blue);
			}
			w.SmallButton(RoomScan.GroupNames[g2] + " (" + count + ")", fx, 106f, fw, () =>
			{
				filter = gg;
				w.Rebuild();
			}, enabled: true, null, null, 30f);
			fx += fw + 6f;
		}
		List<RoomInfo> shown = rooms.Where((RoomInfo r) => filter == RoomScan.All || (filter == RoomScan.NeedsALook && r.Warnings.Count > 0) || r.Group == filter).ToList();
		float y = 152f;
		w.Label("Room", 26f, y, 230f, 22f, TextAnchor.MiddleLeft, bold: true, null, 12);
		w.Label("Job", 266f, y, 380f, 22f, TextAnchor.MiddleLeft, bold: true, null, 12);
		w.Label("Automation", 666f, y, 190f, 22f, TextAnchor.MiddleLeft, bold: true, null, 12);
		w.Label("Support", 866f, y, W - 880f, 22f, TextAnchor.MiddleLeft, bold: true, null, 12);
		y += 24f;
		list = w.List(13f, y, W - 26f, w.Height - 75f - y - 10f);
		foreach (RoomInfo info in shown)
		{
			Row(w, list, info, from);
		}
		if (shown.Count == 0)
		{
			RowView none = list.Row(40f);
			none.Text((filter == RoomScan.NeedsALook) ? "Nothing needs a look right now." : "No rooms here.", 12f, none.W - 24f);
		}
	}

	private static void Row(EditorWindow w, ListView list, RoomInfo info, roomScript from)
	{
		roomScript room = info.Room;
		string tip = info.Name + "\n" + info.Job + ((info.Progress >= 0f) ? (" (" + Mathf.RoundToInt(info.Progress * 100f) + "%)") : "") + ((info.Auto.Count > 0) ? ("\nDoes by itself: " + string.Join(", ", info.Auto)) : "") + ((info.Support.Count > 0) ? ("\n" + string.Join("\n", info.Support)) : "") + ((info.Warnings.Count > 0) ? ("\n<color=#c62828>" + string.Join("\n", info.Warnings) + "</color>") : "") + "\n\nClick to go to this room.";
		RowView row = list.Row(60f, () =>
		{
			RoomScan.Jump(room);
			w.Close();
		}, tip);
		if (room == from)
		{
			row.Highlight();
		}
		row.Top(info.Name, 12f, 240f);
		Text staff = row.Bottom(info.Staff + ((info.Staff == 1) ? " staff" : " staff") + ((info.Desks > 0) ? ("  ·  " + info.Desks + " desks") : ""), 12f, 240f);
		Text job = row.Top(info.Job, 252f, 390f, bold: info.Working);
		if (!info.Working)
		{
			job.color = UIKit.Grey;
		}
		if (info.Warnings.Count > 0)
		{
			Text warn = row.Bottom(string.Join("  ·  ", info.Warnings), 252f, 390f);
			warn.color = (info.Warnings.Any((string x) => x.StartsWith("At risk") || x == "No staff") ? new Color(0.78f, 0.16f, 0.16f) : new Color(0.8f, 0.45f, 0.05f));
		}
		else if (info.Progress >= 0f)
		{
			row.Bar(252f, 380f, 14f, info.Progress, UIKit.Blue, Mathf.RoundToInt(info.Progress * 100f) + "%", null, 36f);
		}
		if (info.Auto.Count > 0)
		{
			Fixed(row.Text(string.Join("\n", info.Auto.Take(3)), 652f, 190f, bold: false, TextAnchor.MiddleLeft, 4f, 52f)).color = new Color(0.35f, 0.25f, 0.6f);
		}
		if (info.Support.Count > 0)
		{
			// At most three lines: two links and "+N more" when there are over three.
			int shown = ((info.Support.Count > 3) ? 2 : 3);
			Fixed(row.Text(string.Join("\n", info.Support.Take(shown)) + ((info.Support.Count > shown) ? ("\n+" + (info.Support.Count - shown) + " more") : ""), 852f, row.W - 862f, bold: false, TextAnchor.MiddleLeft, 4f, 52f)).color = UIKit.Grey;
		}
	}

	// The same size on every row, whatever the line count (best fit made one-line cells bigger than three-line ones).
	private static Text Fixed(Text t)
	{
		t.resizeTextForBestFit = false;
		t.fontSize = 12;
		t.lineSpacing = 1f;
		t.horizontalOverflow = HorizontalWrapMode.Overflow;
		t.verticalOverflow = VerticalWrapMode.Truncate;
		return t;
	}

	public override void Tick(EditorWindow w)
	{
		if (Core.CopyProtection.CopyProtectDev.AbsWeek == week)
		{
			return;
		}
		Vector2 scroll = ((list != null && (bool)list.Scroll) ? list.Scroll.content.anchoredPosition : Vector2.zero);
		w.Rebuild();
		if (list != null && (bool)list.Scroll)
		{
			list.Scroll.content.anchoredPosition = scroll;
		}
	}
}
