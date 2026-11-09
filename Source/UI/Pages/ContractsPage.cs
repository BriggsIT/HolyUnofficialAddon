using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// Hire outside studios. Research: click items to put them in one contract (topics can be narrowed to a genre).
// Game work: quality passes, bug fixing, extra hands, or a batch of one studio's improvements (like the QA,
// graphics, sound and motion capture rooms offer). Studios' quotes are on the right; running jobs have their own tab.
internal class ContractsPage : Page
{
	public const int TabResearch = 0;

	public const int TabGames = 1;

	public const int TabActive = 2;

	// Set before opening the window to start on another tab (the on-screen tracker opens Active contracts).
	public static int StartTab;

	private int tab;

	private int category = 1;

	private string search = "";

	private int genreFilter = -1;

	// The research basket: every item picked, across categories, keyed by "type:slot".
	private readonly Dictionary<string, ResearchItem> basket = new Dictionary<string, ResearchItem>();

	private int gameIndex;

	private string service = "graphics";

	private int addonStudio = -1;

	private readonly List<int> addonSlots = new List<int>();

	private int sort;

	private string status = "";

	private ListView items;

	private ListView firms;

	private ListView active;

	private Text summary;

	private float refillAt;

	private int pendingCancel = -1;

	private float pendingUntil;

	public ContractsPage()
	{
		tab = StartTab;
		StartTab = TabResearch;
	}

	public override string Title => "Contracts";

	public override Sprite IconSprite => UIKit.LoadSprite(Path.Combine(Plugin.PluginDir, "contracts.png"));

	public override float Width => 1000f;

	public override float Height => 760f;

	private static float BodyHeight(EditorWindow w)
	{
		return w.Height - 75f;
	}

	private float ListTop => 128f;

	private float ListHeight(EditorWindow w)
	{
		return BodyHeight(w) - ListTop - 46f;
	}

	public override void Build(EditorWindow w)
	{
		TabButton(w, TabResearch, "Research", 14f);
		TabButton(w, TabGames, "Game work", 184f);
		TabButton(w, TabActive, "Active contracts (" + ContractBook.Active.Count() + ")", 354f);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		switch (tab)
		{
		case TabResearch:
			BuildResearch(w);
			break;
		case TabGames:
			BuildGames(w);
			break;
		default:
			BuildActive(w);
			break;
		}
		Text line = w.Label(status, 18f, BodyHeight(w) - 38f, w.Width - 36f, 30f);
		line.color = UIKit.Green;
	}

	private void TabButton(EditorWindow w, int index, string text, float x)
	{
		Button button = w.SmallButton(text, x, 10f, 160f, () =>
		{
			tab = index;
			status = "";
			w.Rebuild();
		}, enabled: true, null, null, 30f);
		if (tab == index)
		{
			button.GetComponent<Image>().color = UIKit.SelectedRow;
		}
	}

	private void QuotesHeader(EditorWindow w)
	{
		w.Label("Studios that will take the job", 476f, 50f, 260f, 30f, TextAnchor.MiddleLeft, bold: true);
		w.Dropdown(w.Width - 214f, 50f, 200f, new List<string> { "Best rated first", "Cheapest first", "Fastest first" }, sort, (int v) =>
		{
			sort = v;
			FillFirms(w);
		});
		summary = w.Label("", 476f, 88f, w.Width - 490f, 34f, TextAnchor.MiddleLeft, bold: false, null, 13);
	}

	// ---- Research ----

	private void BuildResearch(EditorWindow w)
	{
		List<string> names = ResearchCatalog.Order.Select(ResearchCatalog.CategoryName).ToList();
		w.Dropdown(14f, 50f, 220f, names, Array.IndexOf(ResearchCatalog.Order, category), (int v) =>
		{
			category = ResearchCatalog.Order[v];
			search = "";
			w.Rebuild();
		});
		w.Input(search, "Search...", 244f, 48f, 216f, (string v) =>
		{
			search = v;
			refillAt = Time.unscaledTime + 0.25f;
		});
		if (category == 1)
		{
			List<(int id, string name)> genres = ResearchCatalog.Genres();
			List<string> options = new List<string> { "Topics for any genre" };
			options.AddRange(genres.Select(((int id, string name) g) => "Suits " + g.name));
			int index = genres.FindIndex(((int id, string name) g) => g.id == genreFilter) + 1;
			w.Dropdown(14f, 88f, 220f, options, index, (int v) =>
			{
				genreFilter = ((v == 0) ? (-1) : genres[v - 1].id);
				FillResearch(w);
			});
		}
		else
		{
			w.Label("Click items to add them to one contract.", 18f, 88f, 216f, 30f, TextAnchor.MiddleLeft, bold: false, null, 13);
		}
		w.SmallButton("Select all shown", 244f, 90f, 120f, () =>
		{
			foreach (ResearchItem r in ResearchList())
			{
				basket[Key(r)] = r;
			}
			FillResearch(w, keepScroll: true);
			FillFirms(w);
		}, enabled: true, "Add every item in the list below to the contract.", null, 28f);
		w.SmallButton("Clear", 370f, 90f, 90f, () =>
		{
			basket.Clear();
			FillResearch(w, keepScroll: true);
			FillFirms(w);
		}, enabled: true, "Empty the contract.", null, 28f);
		QuotesHeader(w);
		items = w.List(13f, ListTop, 450f, ListHeight(w));
		firms = w.List(474f, ListTop, w.Width - 487f, ListHeight(w));
		// Drop anything from the basket that has since been researched or handed to a studio.
		foreach (string key in basket.Keys.ToList())
		{
			ResearchItem r = basket[key];
			if (ResearchCatalog.IsDone(r.Type, r.Slot) || ContractBook.Active.Any((Contract c) => c.IsResearch && c.Research.Any((ResearchJob j) => j.Type == r.Type && j.Slot == r.Slot && !j.Done)))
			{
				basket.Remove(key);
			}
		}
		FillResearch(w);
		FillFirms(w);
	}

	private static string Key(ResearchItem r)
	{
		return r.Type + ":" + r.Slot;
	}

	private List<ResearchItem> ResearchList()
	{
		string term = search.Trim();
		return ResearchCatalog.Items(category).Where((ResearchItem r) => (term.Length == 0 || r.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) && (category != 1 || genreFilter < 0 || ResearchCatalog.TopicFits(r.Slot, genreFilter))).ToList();
	}

	private void FillResearch(EditorWindow w, bool keepScroll = false)
	{
		items.Clear(keepScroll);
		List<ResearchItem> list = ResearchList();
		foreach (ResearchItem r in list)
		{
			ResearchItem item = r;
			bool picked = basket.ContainsKey(Key(item));
			RowView row = items.Row(42f, () =>
			{
				if (!basket.Remove(Key(item)))
				{
					basket[Key(item)] = item;
				}
				FillResearch(w, keepScroll: true);
				FillFirms(w);
			}, picked ? "Click to take it out of the contract." : "Click to add it to the contract.");
			if (picked)
			{
				row.Highlight();
			}
			row.Picture(item.Icon, 6f, 30f);
			row.Top(item.Name, 44f, row.W - 120f);
			row.Bottom("In-house " + G.Money(item.InHousePrice) + "  ·  " + Mathf.RoundToInt(item.PointsLeft) + " research points", 44f, row.W - 52f);
			if (picked)
			{
				row.Top("In contract", row.W - 110f, 102f, bold: true, TextAnchor.MiddleRight).color = UIKit.Blue;
			}
		}
		if (list.Count == 0)
		{
			RowView row = items.Row(42f);
			row.Text((search.Trim().Length > 0 || genreFilter >= 0) ? "Nothing matches your search." : "Nothing left to research here right now.", 8f, row.W - 16f, bold: false, TextAnchor.MiddleCenter);
		}
	}

	// ---- Game work ----

	private void BuildGames(EditorWindow w)
	{
		List<gameScript> games = GameServices.MyGamesInDevelopment();
		if (games.Count == 0)
		{
			w.Label("You have no games in development right now. Start one, then come back to hire help with it.", 18f, 60f, w.Width - 36f, 60f);
			return;
		}
		gameIndex = Mathf.Clamp(gameIndex, 0, games.Count - 1);
		w.Dropdown(14f, 50f, 446f, games.Select((gameScript g) => g.GetNameSimple() + "  (" + Mathf.RoundToInt(g.GetProzentGesamt()) + "% done)").ToList(), gameIndex, (int v) =>
		{
			gameIndex = v;
			addonSlots.Clear();
			addonStudio = -1;
			if (service == "addons")
			{
				service = "graphics";
			}
			w.Rebuild();
		});
		w.Label("Pick a job, or click improvements to bundle several from one studio.", 18f, 88f, 446f, 34f, TextAnchor.MiddleLeft, bold: false, null, 13);
		QuotesHeader(w);
		items = w.List(13f, ListTop, 450f, ListHeight(w));
		firms = w.List(474f, ListTop, w.Width - 487f, ListHeight(w));
		gameScript game = games[gameIndex];
		addonSlots.RemoveAll((int s) => !AddonAvailable(game, addonStudio, s));
		FillServices(w, game);
		FillFirms(w);
	}

	private gameScript SelectedGame()
	{
		List<gameScript> games = GameServices.MyGamesInDevelopment();
		if (games.Count == 0)
		{
			return null;
		}
		return games[Mathf.Clamp(gameIndex, 0, games.Count - 1)];
	}

	private static bool Running(gameScript g, string kind)
	{
		return ContractBook.Active.Any((Contract c) => c.GameId == g.myID && c.Kind == kind);
	}

	private static bool AddonAvailable(gameScript g, int studio, int slot)
	{
		if (studio < 0 || GameServices.AddonDone(g, studio, slot) || GameServices.AddonInRoom(g, studio, slot))
		{
			return false;
		}
		return !GameServices.AddonContracted(g, studio, slot);
	}

	private void FillServices(EditorWindow w, gameScript g, bool keepScroll = false)
	{
		items.Clear(keepScroll);
		Section("Quality work");
		foreach (string k in GameServices.Kinds)
		{
			string kind = k;
			RowView row = items.Row(46f, () =>
			{
				service = kind;
				addonSlots.Clear();
				addonStudio = -1;
				FillServices(w, g, keepScroll: true);
				FillFirms(w);
			}, GameServices.Blurb(kind));
			if (kind == service)
			{
				row.Highlight();
			}
			row.Top(GameServices.Name(kind), 8f, row.W - 170f);
			row.Bottom(Running(g, kind) ? "A studio is already on this" : GameServices.Blurb(kind), 8f, row.W - 16f);
			row.Top(GameServices.Status(g, kind), row.W - 168f, 160f, bold: false, TextAnchor.MiddleRight);
		}
		for (int s = 0; s < GameServices.StudioCount; s++)
		{
			int studio = s;
			List<int> open = (from i in Enumerable.Range(0, GameServices.AddonCount(studio))
				where AddonAvailable(g, studio, i)
				select i).ToList();
			RowView header = items.Row(34f, () =>
			{
				// Clicking a studio's heading picks all of its improvements that are still open.
				service = "addons";
				addonStudio = studio;
				addonSlots.Clear();
				addonSlots.AddRange(open);
				FillServices(w, g, keepScroll: true);
				FillFirms(w);
			}, (open.Count > 0) ? "Click to pick all of these that are still open." : null, open.Count > 0);
			header.Text(GameServices.StudioName(studio), 8f, header.W - 140f, bold: true);
			header.Text(open.Count + " open", header.W - 130f, 122f, bold: false, TextAnchor.MiddleRight);
			for (int i = 0; i < GameServices.AddonCount(studio); i++)
			{
				int slot = i;
				bool available = open.Contains(slot);
				bool picked = addonStudio == studio && addonSlots.Contains(slot);
				RowView row = items.Row(40f, () =>
				{
					if (addonStudio != studio)
					{
						addonSlots.Clear();
						addonStudio = studio;
					}
					service = "addons";
					if (!addonSlots.Remove(slot))
					{
						addonSlots.Add(slot);
					}
					FillServices(w, g, keepScroll: true);
					FillFirms(w);
				}, null, available);
				if (picked)
				{
					row.Highlight();
				}
				row.Picture(GameServices.AddonIcon(studio, slot), 6f, 28f).color = (available ? Color.white : new Color(1f, 1f, 1f, 0.4f));
				row.Top(GameServices.AddonName(studio, slot), 42f, row.W - 50f);
				string state = (GameServices.AddonDone(g, studio, slot) ? "Done" : (GameServices.AddonInRoom(g, studio, slot) ? "Being done in one of your rooms" : (GameServices.AddonContracted(g, studio, slot) ? "Contracted out" : ("In-house " + G.Money(GameServices.AddonInHouseCost(studio, slot, g))))));
				Text bottom = row.Bottom(state, 42f, row.W - 50f);
				if (!available)
				{
					bottom.color = UIKit.Grey;
				}
			}
		}
	}

	private void Section(string text)
	{
		RowView row = items.Row(30f);
		row.Text(text, 8f, row.W - 16f, bold: true);
	}

	// ---- Quotes ----

	private void FillFirms(EditorWindow w)
	{
		if (firms == null || !firms.Scroll)
		{
			return;
		}
		firms.Clear();
		List<ResearchItem> research = null;
		gameScript game = null;
		List<int> addons = null;
		string kind;
		if (tab == TabResearch)
		{
			research = basket.Values.ToList();
			kind = "research";
			SetSummary((research.Count == 0) ? "" : (research.Count + ((research.Count == 1) ? " item" : " items") + " in the contract  ·  in-house " + G.Money(research.Sum((ResearchItem r) => r.InHousePrice)) + "  ·  " + Mathf.RoundToInt(research.Sum((ResearchItem r) => r.PointsLeft)) + " research points"));
			if (research.Count == 0)
			{
				Message(firms, "Click topics or other research on the left to put them in a contract, and studios will send you their quotes.");
				return;
			}
		}
		else
		{
			game = SelectedGame();
			kind = service;
			if (!game)
			{
				return;
			}
			if (kind == "addons")
			{
				addons = addonSlots.ToList();
				SetSummary((addons.Count == 0) ? "" : (GameServices.StudioName(addonStudio) + ": " + ContractBook.Names(addons.Select((int s) => GameServices.AddonName(addonStudio, s)).ToList())));
				if (addons.Count == 0)
				{
					Message(firms, "Click improvements on the left to bundle them into one contract.");
					return;
				}
			}
			else
			{
				SetSummary(GameServices.Name(kind) + " for " + game.GetNameSimple());
				if (Running(game, kind))
				{
					Message(firms, "A studio is already doing this for " + game.GetNameSimple() + ". See Active contracts.");
					return;
				}
			}
		}
		List<Quote> quotes = (from f in Firms.All()
			select (research != null) ? Hiring.Research(research, f) : ((addons != null) ? GameServices.QuoteAddons(game, addonStudio, addons, f) : GameServices.Quote(game, kind, f))).ToList();
		IEnumerable<Quote> sorted = sort switch
		{
			1 => quotes.OrderBy((Quote q) => q.Price),
			2 => quotes.OrderBy((Quote q) => q.Weeks).ThenBy((Quote q) => q.Price),
			_ => quotes.OrderByDescending((Quote q) => q.Firm.stars).ThenBy((Quote q) => q.Price),
		};
		foreach (Quote q in sorted.Take(40))
		{
			Quote quote = q;
			RowView row = firms.Row(48f);
			float infoX = row.W - 290f;
			row.Top(quote.Firm.GetName(), 8f, infoX - 12f);
			row.Bottom(Firms.Describe(quote.Firm), 8f, infoX - 12f);
			bool busy = ContractBook.FirmBusy(quote.Firm.myID);
			if (quote.Unavailable != null)
			{
				row.Top(quote.Unavailable, infoX, 190f, bold: false, TextAnchor.MiddleRight);
				continue;
			}
			row.Top(G.Money(quote.Price), infoX, 190f, bold: true, TextAnchor.MiddleRight);
			row.Bottom(((research == null) ? (quote.Effect + "  ·  ") : "") + quote.Weeks + ((quote.Weeks == 1) ? " week" : " weeks"), infoX - 40f, 230f, bold: false, TextAnchor.MiddleRight);
			row.Button(busy ? "Busy" : "Hire", row.W - 90f, 82f, () =>
			{
				Contract c = Hiring.Hire(quote, kind, research, game, addonStudio, addons);
				if (c != null)
				{
					UIKit.Sound();
					status = "Hired " + c.FirmName + " for " + G.Money(c.Price) + ": " + ContractBook.Title(c) + ", done in " + c.WeeksTotal + ((c.WeeksTotal == 1) ? " week." : " weeks.");
					basket.Clear();
					addonSlots.Clear();
					w.Rebuild();
				}
			}, !busy, busy ? "This studio is already working on a contract for you." : (quote.Effect + ". Paid up front."));
		}
		if (quotes.Count == 0)
		{
			Message(firms, "No independent studios are around to take the job yet.");
		}
	}

	private void SetSummary(string text)
	{
		if ((bool)summary)
		{
			summary.text = text;
		}
	}

	private static void Message(ListView list, string text)
	{
		RowView row = list.Row(56f);
		row.Text(text, 8f, row.W - 16f, bold: false, TextAnchor.MiddleCenter);
	}

	// ---- Active contracts ----

	private void BuildActive(EditorWindow w)
	{
		active = w.List(13f, 50f, w.Width - 26f, BodyHeight(w) - 50f - 46f);
		FillActive(w);
	}

	private void FillActive(EditorWindow w, bool keepScroll = false)
	{
		active.Clear(keepScroll);
		List<Contract> running = ContractBook.Active.ToList();
		if (running.Count == 0)
		{
			Message(active, "No contracts running. Hire a studio from the Research or Game work tab.");
		}
		foreach (Contract c in running)
		{
			Contract contract = c;
			RowView row = active.Row(52f, null, Details(c));
			row.Top(ContractBook.Title(c), 8f, row.W - 360f);
			row.Bottom(c.FirmName + "  ·  hired " + c.Started + "  ·  paid " + G.Money(c.Price), 8f, row.W - 360f);
			string progress = "Week " + Mathf.Min(c.WeeksDone + 1, c.WeeksTotal) + " of " + c.WeeksTotal + (c.Late ? ", running late" : "");
			row.Bar(row.W - 345f, 200f, 22f, c.Progress, c.Late ? UIKit.Orange : UIKit.Green, progress);
			bool confirming = pendingCancel == c.Id && Time.unscaledTime < pendingUntil;
			row.Button(confirming ? "Sure?" : "Cancel", row.W - 130f, 120f, () =>
			{
				if (pendingCancel == contract.Id && Time.unscaledTime < pendingUntil)
				{
					pendingCancel = -1;
					ContractBook.Cancel(contract);
					status = "Cancelled: " + ContractBook.Title(contract) + ". " + contract.Result.Replace("Cancelled", "").TrimStart(',', ' ');
					w.Rebuild();
				}
				else
				{
					pendingCancel = contract.Id;
					pendingUntil = Time.unscaledTime + 4f;
					FillActive(w, keepScroll: true);
				}
			}, enabled: true, "Call the contract off. Half of the money for work not yet done comes back.");
		}
		List<Contract> done = ContractBook.History.ToList();
		if (done.Count == 0)
		{
			return;
		}
		RowView header = active.Row(34f);
		header.Text("Recently finished", 8f, header.W - 16f, bold: true);
		foreach (Contract c in done)
		{
			RowView row = active.Row(44f, null, c.Result);
			row.Top(ContractBook.Title(c) + "  ·  " + c.FirmName, 8f, row.W - 16f);
			row.Bottom(c.Result ?? "", 8f, row.W - 16f);
		}
	}

	// Tooltip listing what a contract covers and what's been delivered so far.
	public static string Details(Contract c)
	{
		string text = "<b>" + ContractBook.Title(c) + "</b>\n" + c.FirmName;
		if (c.IsResearch && c.Research.Count > 1)
		{
			foreach (ResearchJob j in c.Research)
			{
				text = text + "\n" + (j.Done ? "<color=green>done</color>  " : "") + j.Name;
			}
		}
		if (c.IsAddons)
		{
			foreach (int slot in c.Addons)
			{
				text = text + "\n" + (c.AddonsDone.Contains(slot) ? "<color=green>done</color>  " : "") + GameServices.AddonName(c.Studio, slot);
			}
		}
		return text;
	}

	public override void Tick(EditorWindow w)
	{
		if (refillAt > 0f && Time.unscaledTime >= refillAt)
		{
			refillAt = 0f;
			if (tab == TabResearch && items != null)
			{
				FillResearch(w);
			}
		}
		if (pendingCancel >= 0 && Time.unscaledTime >= pendingUntil && tab == TabActive && active != null)
		{
			pendingCancel = -1;
			FillActive(w);
		}
	}
}
