using HolyAddon.Core;
using UnityEngine;

namespace HolyAddon.UI.Pages;

// The addon's start page: one row per thing you can edit.
internal class HomePage : Page
{
	public override string Title => Plugin.Name;

	public override float Height => 618f;

	public override void Build(EditorWindow w)
	{
		w.Label("Pick what you want to edit. Changes apply straight away and are saved with your game as normal.", 18f, 10f, w.Width - 36f, 44f);
		ListView list = w.List(13f, 60f, w.Width - 26f, w.Height - 75f - 74f);
		int staff = Employees.All().Count;
		RowView row = list.Row(58f, () => w.Push(new EmployeesPage()), "Change skills and motivation of anyone on your staff.");
		row.Icon("iconPersonal", 10f, 40f);
		row.Top("Employees", 62f, row.W - 72f);
		row.Bottom(G.Text(184, "<NUM> Employees").Replace("<NUM>", staff.ToString()) + "  ·  skills, motivation, sickness", 62f, row.W - 72f);
		RowView settings = list.Row(58f, () => w.Push(new GameSettingsPage()), "Change the new-game settings mid-game, plus the addon's own options.");
		settings.Icon("iconOptions", 10f, 40f);
		settings.Top(G.Text(856, "Game settings"), 62f, settings.W - 72f);
		settings.Bottom("Difficulty, game speed, development time, random events  ·  furniture collisions, speed buttons", 62f, settings.W - 72f);
		int mmos = Core.Mmo.MmoOps.MyMmos().Count;
		RowView mmo = list.Row(58f, () => w.Push(new MmoListPage()), "Modernise your MMOs and keep their players: graphics overhauls, 64-bit clients, engine migrations, world expansions and content patches.");
		mmo.Picture(Core.Updates.UpdateIcons.Mmo(), 10f, 40f);
		mmo.Top("MMO Management", 62f, mmo.W - 72f);
		mmo.Bottom(((mmos == 1) ? "1 MMO" : (mmos + " MMOs")) + " on the market  ·  subscribers, age, how they hold up today", 62f, mmo.W - 72f);
		Core.Store.StoreState store = Core.Store.StoreOps.State;
		RowView shop = list.Row(58f, () => w.Push(new StoreHomePage()), "Run your own digital PC store: sell your games, take a cut of other studios' sales and compete with the rival stores.");
		shop.Picture(store.Built ? Core.Updates.UpdateIcons.StoreLogo(store.Logo) : Core.Updates.UpdateIcons.Store(), 10f, 40f);
		shop.Top(store.Built ? store.Name : "Your digital store", 62f, shop.W - 72f);
		shop.Bottom(store.Built ? (MmoUi.Short(store.Accounts) + " accounts  ·  " + Mathf.RoundToInt(store.Share * 100f) + "% of digital PC buyers") : ("Build your own PC store from " + Core.Store.StoreOps.OpenYear), 62f, shop.W - 72f);
		ComingSoon(list, "iconBank", "Company");
		ComingSoon(list, "iconGames", "Games");
	}

	private static void ComingSoon(ListView list, string icon, string name)
	{
		RowView row = list.Row(58f, null, null, enabled: false);
		row.Icon(icon, 10f, 40f).color = new Color(1f, 1f, 1f, 0.35f);
		row.Top(name, 62f, row.W - 72f).color = UIKit.Grey;
		row.Bottom("Coming soon", 62f, row.W - 72f).color = UIKit.Grey;
	}
}
