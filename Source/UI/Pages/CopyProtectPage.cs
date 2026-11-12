using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// Management > Your Copy Protection: the project in development and every copy protection you've made.
internal class CopyProtectPage : Page
{
	// The development room whose menu opened the window, used first when it's free.
	public static int PreferredRoom = -1;

	private int seen = -1;

	private string status = "";

	private bool statusGood;

	private bool confirmCancel;

	private GameObject progressBar;

	private float nextProgress;

	public override string Title => "Your Copy Protection";

	public override Sprite IconSprite => CopyProtectDev.Icon();

	public override float Width => 940f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		seen = CopyProtectDev.Version;
		progressBar = null;
		float bottom = w.Height - 75f;
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		float y = 10f;
		string locked = CopyProtectDev.LockReason();
		if (locked != null)
		{
			w.Info(locked + ResearchState(), 14f, 46f, w.Width - 28f, 64f);
			y = 120f;
		}
		else
		{
			w.Label("Make copy protection for your games instead of buying it: free to use, kept strong with updates, and something you can license to other studios.", 14f, y, w.Width - 300f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
			y = 62f;
		}
		y = BuildProject(w, y, locked == null);
		List<OwnCopyProtect> products = CopyProtectDev.Products.ToList();
		w.Header("Your Copy Protections (" + products.Count + ")", 14f, y, w.Width - 28f);
		y += 32f;
		float listHeight = bottom - y - 46f;
		if (products.Count == 0)
		{
			w.Label((locked == null) ? "None yet. Start one with New copy protection, or from a development room's menu." : "None yet.", 24f, y + 6f, w.Width - 48f, 30f);
		}
		else
		{
			ListView list = w.List(13f, y, w.Width - 26f, listHeight);
			foreach (OwnCopyProtect p in products.OrderBy((OwnCopyProtect x) => x.Retired).ThenByDescending((OwnCopyProtect x) => x.ReleasedWeek))
			{
				ProductRow(w, list, p);
			}
		}
		Text line = w.Label(status, 18f, bottom - 38f, w.Width - 36f, 30f);
		line.color = (statusGood ? UIKit.Green : UIKit.Red);
	}

	private static string ResearchState()
	{
		if (!CopyProtectDev.MarketOpen || CopyProtectDev.Researched)
		{
			return "";
		}
		if (CopyProtectDev.ResearchRunning)
		{
			return " Being researched: " + Mathf.FloorToInt(CopyProtectDev.ResearchPercent) + "%.";
		}
		return " It costs " + G.Money(CopyProtectDev.ResearchPrice) + ".";
	}

	private float BuildProject(EditorWindow w, float y, bool canStart)
	{
		CopyProtectProject p = CopyProtectDev.Project;
		if (p == null)
		{
			w.BigButton("New Copy Protection", 14f, y, 260f, 44f, () => w.Push(new CopyProtectNewPage()), canStart, canStart ? "Pick a name, a technology and a development room." : CopyProtectDev.LockReason());
			if (canStart && CopyProtectDev.FreeRooms().Count == 0)
			{
				w.Label("All development rooms are busy right now.", 290f, y, w.Width - 310f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
			}
			return y + 60f;
		}
		roomScript room = CopyProtectDev.Rooms().FirstOrDefault((roomScript r) => r.taskID == p.TaskId);
		w.Header("In development", 14f, y, w.Width - 28f);
		y += 32f;
		string what = (p.IsUpdate ? "Update" : "New") + " · " + CopyProtectDev.TechName(p.Tech) + " · " + (room ? CopyProtectDev.RoomName(room) : "no room") + ((room != null) ? (" · " + CopyProtectDev.Duration(p.Needed - p.Progress, room) + " left") : "");
		w.Label("<b>" + p.Name + "</b>   " + what, 24f, y, w.Width - 300f, 30f);
		long refund = CopyProtectDev.Refund(p);
		w.SmallButton(confirmCancel ? "Really Cancel?" : "Cancel", w.Width - 214f, y + 2f, 200f, () =>
		{
			if (!confirmCancel)
			{
				confirmCancel = true;
				w.Rebuild();
				return;
			}
			confirmCancel = false;
			string name = p.Name;
			long back = CopyProtectDev.Cancel();
			Say("Work on " + name + " stopped." + ((back > 0) ? (" " + G.Money(back) + " of the development costs came back.") : ""), good: true);
			w.Rebuild();
		}, enabled: true, (refund > 0) ? ("Stop the project. " + G.Money(refund) + " of the development costs come back.") : "Stop the project.", null, 28f);
		y += 36f;
		progressBar = UIKit.Make("Bar", w.Body);
		UIKit.Place(progressBar, 24f, y, w.Width - 48f, 24f);
		ShowProgress();
		return y + 40f;
	}

	private void ShowProgress()
	{
		CopyProtectProject p = CopyProtectDev.Project;
		if ((bool)progressBar && p != null)
		{
			EditorWindow.StyleBar(progressBar, p.Fraction, Mathf.FloorToInt(p.Fraction * 100f) + "%", UIKit.Blue, 24f);
		}
	}

	private void ProductRow(EditorWindow w, ListView list, OwnCopyProtect p)
	{
		float strength = CopyProtectDev.Strength(p);
		int games = CopyProtectDev.MyGamesUsing(p).Count;
		RowView row = list.Row(60f, () => w.Push(new CopyProtectProductPage(p.Id)), "Open " + p.FullName + ": updates, licensing and more.");
		Image picture = row.Picture(CopyProtectDev.TechIcon(p.Tech), 10f, 40f);
		if (p.Retired)
		{
			picture.color = new Color(1f, 1f, 1f, 0.4f);
		}
		Text top = row.Top(p.FullName + (p.Retired ? "  (off the market)" : ""), 62f, 300f);
		if (p.Retired)
		{
			top.color = UIKit.Grey;
		}
		row.Bottom(CopyProtectDev.TechName(p.Tech) + " · " + CopyProtectDev.LicensingNames[p.Licensing], 62f, 300f);
		row.Bar(372f, 190f, 22f, strength / 100f, UIKit.ValueColor(strength), Mathf.RoundToInt(strength) + "%", "Strength: the higher it is, the more copies of your games sell. It wears down every week as crackers work on it.");
		row.Text(games + ((games == 1) ? " game" : " games") + ((p.Licensees.Count > 0) ? (" · " + p.Licensees.Count + ((p.Licensees.Count == 1) ? " studio" : " studios")) : ""), 576f, 170f, bold: false, TextAnchor.MiddleLeft);
		string problem = CopyProtectDev.UpdateProblem(p);
		roomScript room = PickRoom(PreferredRoom);
		string tip = problem ?? ((!room) ? "No free development room with staff." : ("Develop " + p.Name + " " + p.Major + "." + (p.Minor + 1) + " in " + CopyProtectDev.RoomName(room) + ": " + G.Money(CopyProtectDev.Cost(p.Tech, update: true)) + ", " + CopyProtectDev.Duration(CopyProtectDev.WorkNeeded(p.Tech, update: true), room) + ". Strength back to about " + Mathf.RoundToInt(CopyProtectDev.ReleaseStrength(p.Tech, room)) + "%."));
		row.Button("Update", row.W - 112f, 100f, () =>
		{
			string error = CopyProtectDev.StartUpdate(p, PickRoom(PreferredRoom));
			if (error != null)
			{
				Say(error, good: false);
			}
			else
			{
				PreferredRoom = -1;
				Say("Your team started on " + CopyProtectDev.Project.Name + ".", good: true);
			}
			w.Rebuild();
		}, problem == null && (bool)room, tip);
	}

	public void Say(string text, bool good)
	{
		status = text ?? "";
		statusGood = good;
	}

	// A free development room with staff: the one asked for if it's free, otherwise the fastest.
	public static roomScript PickRoom(int wanted)
	{
		List<roomScript> free = (from r in CopyProtectDev.FreeRooms()
			where CopyProtectDev.Staff(r).Count > 0
			select r).ToList();
		return free.FirstOrDefault((roomScript r) => r.myID == wanted) ?? free.FirstOrDefault();
	}

	// A dropdown of free development rooms; returns the chosen room's ID (or -1 when there's none).
	public static int RoomDropdown(EditorWindow w, float x, float y, float width, int selected, float work, Action<int> onChange)
	{
		List<roomScript> rooms = CopyProtectDev.FreeRooms();
		if (rooms.Count == 0)
		{
			w.Label("No free development room right now.", x, y, width, 30f).color = UIKit.Red;
			return -1;
		}
		int index = rooms.FindIndex((roomScript r) => r.myID == selected);
		if (index < 0)
		{
			roomScript best = PickRoom(selected);
			index = (best ? rooms.IndexOf(best) : 0);
		}
		List<string> options = rooms.Select((roomScript r) =>
		{
			int staff = CopyProtectDev.Staff(r).Count;
			return CopyProtectDev.RoomName(r) + ((staff == 0) ? "  (no staff)" : ("  (" + staff + " staff, " + CopyProtectDev.Duration(work, r) + ")"));
		}).ToList();
		w.Dropdown(x, y, width, options, index, (int v) =>
		{
			UIKit.Sound();
			onChange(rooms[Mathf.Clamp(v, 0, rooms.Count - 1)].myID);
		});
		return rooms[index].myID;
	}

	public override void Tick(EditorWindow w)
	{
		if (CopyProtectDev.Version != seen)
		{
			w.Rebuild();
			return;
		}
		if (Time.unscaledTime >= nextProgress)
		{
			nextProgress = Time.unscaledTime + 0.5f;
			ShowProgress();
		}
	}
}
