using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// One of your copy protections: its numbers, updates, licensing and taking it off the market.
internal class CopyProtectProductPage : Page
{
	private readonly int id;

	private int room = CopyProtectPage.PreferredRoom;

	private string status = "";

	private bool statusGood;

	private int seen = -1;

	public CopyProtectProductPage(int id)
	{
		this.id = id;
	}

	private OwnCopyProtect Product => CopyProtectDev.Find(id);

	public override string Title => Product?.FullName ?? "Copy protection";

	public override Sprite IconSprite => (Product != null) ? CopyProtectDev.TechIcon(Product.Tech) : CopyProtectDev.Icon();

	public override float Width => 900f;

	public override float Height => 800f;

	public override void Build(EditorWindow w)
	{
		seen = CopyProtectDev.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		OwnCopyProtect p = Product;
		if (p == null)
		{
			w.Label("This copy protection no longer exists.", 24f, 60f, w.Width - 48f, 30f);
			return;
		}
		copyProtectScript s = CopyProtectDev.Script(p);
		float strength = CopyProtectDev.Strength(p);
		float y = 50f;
		w.Bar(14f, y, w.Width - 28f, 26f, strength / 100f, "Strength " + Mathf.RoundToInt(strength) + "%", UIKit.ValueColor(strength), null, "The higher it is, the more copies of the games using it sell (up to +20% at 100%). It wears down every week as crackers work on it; updates bring it back.");
		y += 36f;
		int weeksSince = CopyProtectDev.AbsWeek - p.UpdatedWeek;
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Technology", CopyProtectDev.TechName(p.Tech)),
			EditorWindow.KV("Released", Released(s)),
			EditorWindow.KV("Last update", (p.Minor == 0) ? "none yet" : (weeksSince + ((weeksSince == 1) ? " week ago" : " weeks ago"))),
			EditorWindow.KV("Strength lost per week", Wear(p)),
			EditorWindow.KV("Your games using it", CopyProtectDev.MyGamesUsing(p).Count.ToString()),
			EditorWindow.KV("Studios licensing it", p.Licensees.Count.ToString()),
			EditorWindow.KV("Rival games protected", p.RivalGames.ToString()),
			EditorWindow.KV("Licence income", G.Money(p.Revenue) + " (" + p.LicensesSold + ")")
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 14f;
		y = BuildUpdate(w, p, y);
		y = BuildLicensing(w, p, y);
		w.Toggle("Off the market", 24f, y, 300f, p.Retired, (bool v) =>
		{
			Say(CopyProtectDev.SetRetired(p, v), good: true);
			w.Rebuild();
		}, "No new games can pick it and no studio will license it. Games that already use it keep it.");
		Text line = w.Label(status, 18f, w.Height - 75f - 38f, w.Width - 36f, 30f);
		line.color = (statusGood ? UIKit.Green : UIKit.Red);
	}

	private static string Wear(OwnCopyProtect p)
	{
		int left = CopyProtectDev.UncrackedLeft(p);
		if (left > 0)
		{
			return "none, uncracked (" + left + ((left == 1) ? " more week)" : " more weeks)");
		}
		return "about " + CopyProtectDev.CrackedWear(p).ToString("0.00");
	}

	private static string Released(copyProtectScript s)
	{
		if (!s)
		{
			return "-";
		}
		try
		{
			return s.GetDateString();
		}
		catch (Exception)
		{
			return s.date_year.ToString();
		}
	}

	private float BuildUpdate(EditorWindow w, OwnCopyProtect p, float y)
	{
		w.Header("Update", 14f, y, w.Width - 28f);
		y += 32f;
		string problem = CopyProtectDev.UpdateProblem(p);
		float work = CopyProtectDev.WorkNeeded(p.Tech, update: true);
		if (problem != null)
		{
			w.Label(problem, 24f, y, w.Width - 48f, 30f);
			return y + 44f;
		}
		room = CopyProtectPage.RoomDropdown(w, 24f, y, 420f, room, work, (int v) =>
		{
			room = v;
			w.Rebuild();
		});
		roomScript r = CopyProtectDev.FindRoom(room);
		float after = Mathf.Max(CopyProtectDev.Strength(p), CopyProtectDev.ReleaseStrength(p.Tech, r));
		string version = p.Name + " " + p.Major + "." + (p.Minor + 1);
		w.BigButton("Develop " + version, 460f, y - 4f, w.Width - 474f, 38f, () =>
		{
			string error = CopyProtectDev.StartUpdate(p, CopyProtectDev.FindRoom(room));
			if (error == null)
			{
				CopyProtectPage.PreferredRoom = -1;
				Say("Your team started on " + version + ".", good: true);
			}
			else
			{
				Say(error, good: false);
			}
			w.Rebuild();
		}, r, "Every game using it, yours and other studios', gets the update.");
		y += 40f;
		string reason = CopyProtectDev.FreshReason(p.Tech, r);
		w.Label("Patches the cracks: strength back to about " + Mathf.RoundToInt(after) + "% and uncracked again for " + CopyProtectDev.UncrackedWeeks(p.Licensing) + " weeks, for " + G.Money(CopyProtectDev.Cost(p.Tech, update: true)) + "." + ((reason != null) ? (" Not 100% because " + reason + ".") : "") + ((p.Tech == 4) ? " Always-online updates cost 1 reputation." : ""), 24f, y, w.Width - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13);
		return y + 52f;
	}

	private float BuildLicensing(EditorWindow w, OwnCopyProtect p, float y)
	{
		w.Header("Licensing", 14f, y, w.Width - 28f);
		y += 32f;
		w.Dropdown(24f, y, 300f, CopyProtectDev.LicensingNames, p.Licensing, (int v) =>
		{
			UIKit.Sound();
			Say(CopyProtectDev.SetLicensing(p, v), good: true);
			w.Rebuild();
		});
		long market = CopyProtectDev.MarketPrice();
		if (p.Licensing == 1)
		{
			List<long> fees = CopyProtectDev.FeeSteps.Select((float f) => (long)((float)market * f / 100f) * 100).ToList();
			int index = fees.FindIndex((long f) => f == p.Fee);
			List<string> options = fees.Select((long f, int i) => G.Money(f) + "  (" + Mathf.RoundToInt(CopyProtectDev.FeeSteps[i] * 100f) + "% of the shop price)").ToList();
			if (index < 0)
			{
				options.Insert(0, G.Money(p.Fee) + "  (current)");
				fees.Insert(0, p.Fee);
				index = 0;
			}
			w.Label("Per licence", 340f, y, 110f, 30f);
			w.Dropdown(450f, y, w.Width - 464f, options, index, (int v) =>
			{
				UIKit.Sound();
				CopyProtectDev.SetFee(p, fees[Mathf.Clamp(v, 0, fees.Count - 1)]);
				Say("Licences for " + p.FullName + " now cost " + G.Money(p.Fee) + ".", good: true);
				w.Rebuild();
			});
		}
		y += 40f;
		string text;
		switch (p.Licensing)
		{
		case 1:
			text = "Studios weigh it against the copy protections in the shops (newest costs " + G.Money(market) + "): stronger and cheaper means more sign-ups. Each one pays once and then protects its games with it. Below " + Mathf.RoundToInt(CopyProtectDev.WeakStrength) + "% they stop signing up, and some drop it.";
			break;
		case 2:
			text = "Any studio can use it for free if it's about as strong as what's in the shops. Each studio that picks it up brings you fans, and every fifth one some reputation. More games using it means crackers work on it harder.";
			break;
		default:
			text = "Only your own games use it, so fewer people can pick it apart: it stays uncracked for " + CopyProtectDev.UncrackedOwn + " weeks after each release (" + CopyProtectDev.UncrackedShared + " when licensed) and wears more slowly. Studios that already bought a licence keep it.";
			break;
		}
		w.Label(text, 24f, y, w.Width - 48f, 48f, TextAnchor.UpperLeft, bold: false, null, 13);
		y += 52f;
		if (p.Licensees.Count > 0)
		{
			List<string> names = (from i in p.Licensees
				select CopyProtectDev.FindStudio(i) into s
				where (bool)s
				select s.GetName()).ToList();
			w.Label("Licensed by: " + string.Join(", ", names.Take(8)) + ((names.Count > 8) ? (" and " + (names.Count - 8) + " more") : ""), 24f, y, w.Width - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13);
			y += 48f;
		}
		return y;
	}

	private void Say(string text, bool good)
	{
		status = text ?? "";
		statusGood = good;
	}

	public override void Tick(EditorWindow w)
	{
		if (CopyProtectDev.Version != seen)
		{
			w.Rebuild();
		}
	}
}
