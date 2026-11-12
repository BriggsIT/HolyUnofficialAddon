using HolyAddon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// The addon's start page: one row per thing you can edit.
internal class HomePage : Page
{
	public override string Title => Plugin.Name;

	public override float Height => 742f;

	public override void Build(EditorWindow w)
	{
		w.Label("Pick what you want to edit. Changes apply straight away and are saved with your game as normal.", 18f, 10f, w.Width - 36f, 44f);
		ListView list = w.List(13f, 60f, w.Width - 26f, w.Height - 75f - 74f);
		// The employee editor is a cheat: only with "Enable cheats" on (Game settings).
		if (Plugin.EnableCheats.Value)
		{
			int staff = Employees.All().Count;
			RowView row = list.Row(58f, () => w.Push(new EmployeesPage()), "Change skills and motivation of anyone on your staff (cheat).");
			row.Icon("iconPersonal", 10f, 40f);
			row.Top("Employees  <color=#c62828>(cheat)</color>", 62f, row.W - 72f);
			row.Bottom(G.Text(184, "<NUM> Employees").Replace("<NUM>", staff.ToString()) + "  ·  skills, motivation, sickness", 62f, row.W - 72f);
		}
		RowView settings = list.Row(58f, () => w.Push(new GameSettingsPage()), "Change the new-game settings mid-game, plus the addon's own options.");
		settings.Icon("iconOptions", 10f, 40f);
		settings.Top(G.Text(856, "Game settings"), 62f, settings.W - 72f);
		settings.Bottom("Difficulty, game speed, development time, random events  ·  furniture collisions, speed buttons", 62f, settings.W - 72f);
		// Rows for things not available yet are greyed out like the game's locked options, with the reason as tooltip.
		int mmos = Core.Mmo.MmoOps.MyMmos().Count;
		string mmoLock = Core.Mmo.MmoOps.LockReason();
		Entry(list, mmoLock, () => w.Push(new MmoListPage()), "Modernise your MMOs and keep their players: graphics overhauls, 64-bit clients, engine migrations, world expansions and content patches.", Core.Updates.UpdateIcons.Mmo(), "MMO Management", ((mmos == 1) ? "1 MMO" : (mmos + " MMOs")) + " on the market  ·  subscribers, age, how they hold up today");
		Core.Store.StoreState store = Core.Store.StoreOps.State;
		Entry(list, store.Built ? null : Core.Store.StoreOps.LockReason(), () => w.Push(new StoreHomePage()), "Run your own digital PC store: sell your games, take a cut of other studios' sales and compete with the rival stores.", store.Built ? Core.Updates.UpdateIcons.StoreLogo(store.Logo) : Core.Updates.UpdateIcons.Store(), store.Built ? store.Name : "Your Digital Store", store.Built ? (MmoUi.Short(store.Accounts) + " accounts  ·  " + Mathf.RoundToInt(store.Share * 100f) + "% of digital PC buyers") : ("Build your own PC store from " + Core.Store.StoreOps.OpenYear));
		if (Plugin.MergedSales.Value)
		{
			Core.Store.SalesState sales = Core.Store.SalesEvents.State;
			Entry(list, Core.Store.SalesEvents.LockReason(), () => w.Push(new SalesPage()), "Plan the year's sales: your games' discount and your store's sale for each Summer, Winter, Spring and Autumn Sale.", Core.Updates.UpdateIcons.Game("iconMarketingSpecial", Core.Updates.UpdateIcons.Game("iconDollar")), "Sales Events", Core.Store.SalesEvents.Running ? (sales.Event + " on now: your games " + Core.Store.SalesEvents.Discounts[sales.Discount] + "% off") : "Summer and Winter Sales, plus your store's own  ·  discounts, pop-ups, how past sales went");
		}
		if (Plugin.Convention.Value)
		{
			Core.Events.ConPlan con = Core.Events.StudioCon.Plan;
			Entry(list, Core.Events.StudioCon.LockReason(), () => w.Push(new ConventionPage(pushed: true)), "Your yearly convention for all your games: venue, what goes on stage, extras, and which Support room prepares it.", ConUi.Icon, Core.Events.StudioCon.FullName(con.Year), con.Started ? ("Booked: " + Core.Events.StudioCon.VenueNames[con.Venue] + "  ·  doors open in August, week 4") : "Not booked yet  ·  doors open in August, week 4");
		}
		ComingSoon(list, "iconBank", "Company");
		ComingSoon(list, "iconGames", "Games");
	}

	// A row that opens a page, or, while locked, a greyed-out row that says why (tooltip and second line).
	private static void Entry(ListView list, string locked, System.Action open, string about, Sprite picture, string title, string line)
	{
		RowView row = list.Row(58f, (locked == null) ? open : null, locked ?? about, locked == null);
		Image pic = row.Picture(picture, 10f, 40f);
		Text top = row.Top(title, 62f, row.W - 72f);
		Text bottom = row.Bottom(locked ?? line, 62f, row.W - 72f);
		if (locked != null)
		{
			if ((bool)pic)
			{
				pic.color = new Color(1f, 1f, 1f, 0.35f);
			}
			top.color = UIKit.Grey;
			bottom.color = UIKit.Grey;
		}
	}

	private static void ComingSoon(ListView list, string icon, string name)
	{
		RowView row = list.Row(58f, null, null, enabled: false);
		row.Icon(icon, 10f, 40f).color = new Color(1f, 1f, 1f, 0.35f);
		row.Top(name, 62f, row.W - 72f).color = UIKit.Grey;
		row.Bottom("Coming soon", 62f, row.W - 72f).color = UIKit.Grey;
	}
}
