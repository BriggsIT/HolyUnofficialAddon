using System;
using System.Collections.Generic;
using System.Linq;
using HolyAddon.Core;
using HolyAddon.Core.CopyProtection;
using HolyAddon.Core.Store;
using HolyAddon.Core.Updates;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

internal static class StoreUi
{
	public static Image Picture(Transform parent, Sprite sprite, float x, float y, float size)
	{
		Image img = new GameObject("Logo", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
		img.transform.SetParent(parent, worldPositionStays: false);
		UIKit.Place(img.gameObject, x, y, size, size);
		img.sprite = sprite;
		img.preserveAspect = true;
		img.raycastTarget = false;
		return img;
	}

	public static string Pct(float f)
	{
		float p = f * 100f;
		return ((p < 10f) ? p.ToString("0.0") : Mathf.RoundToInt(p).ToString()) + "%";
	}

	public static readonly Color[] Colors = new Color[6]
	{
		new Color(0.42f, 0.3f, 0.78f),
		new Color(0.2f, 0.45f, 0.8f),
		new Color(0.15f, 0.62f, 0.6f),
		new Color(0.9f, 0.55f, 0.15f),
		new Color(0.75f, 0.3f, 0.45f),
		new Color(0.55f, 0.55f, 0.55f)
	};

	// ---- Your store's look ----

	private static readonly Dictionary<int, Color> logoColors = new Dictionary<int, Color>();

	public static readonly Color DefaultAccent = new Color(0.3f, 0.55f, 0.9f);

	// The window's skin: in your store's colour once it's built (UI/Skin).
	public static Skin SkinNow()
	{
		StoreState s = StoreOps.State;
		return Skin.Store(s.Built ? LogoColor(s.Logo) : DefaultAccent);
	}

	// A logo's main colour: the average of its strongly coloured pixels.
	public static Color LogoColor(int logo)
	{
		if (logoColors.TryGetValue(logo, out var c))
		{
			return c;
		}
		c = DefaultAccent;
		try
		{
			Sprite sprite = UpdateIcons.StoreLogo(logo);
			Texture2D tex = (sprite ? sprite.texture : null);
			if ((bool)tex && tex.isReadable)
			{
				Color[] px = tex.GetPixels();
				float r = 0f;
				float g = 0f;
				float b = 0f;
				float weight = 0f;
				for (int i = 0; i < px.Length; i += 3)
				{
					Color p = px[i];
					if (!(p.a < 0.6f))
					{
						Color.RGBToHSV(p, out float _, out float sat, out float val);
						if (!(sat < 0.35f) && !(val < 0.3f))
						{
							float k = sat * p.a;
							r += p.r * k;
							g += p.g * k;
							b += p.b * k;
							weight += k;
						}
					}
				}
				if (weight > 1f)
				{
					c = new Color(r / weight, g / weight, b / weight);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Store colour: " + ex.Message);
		}
		logoColors[logo] = c;
		return c;
	}

	// A game's cover tile: its picture filling the top, its name and a line below.
	public static RectTransform Tile(EditorWindow w, Transform parent, gameScript g, float x, float y, float width, float height, string line, bool highlight = false)
	{
		Skin skin = w.Skin;
		GameObject tile = new GameObject("Tile", typeof(RectTransform), typeof(Image));
		tile.transform.SetParent(parent, worldPositionStays: false);
		RectTransform rt = UIKit.Place(tile, x, y, width, height);
		Image back = tile.GetComponent<Image>();
		back.raycastTarget = false;
		Color fill = skin?.Panel ?? new Color(0.92f, 0.92f, 0.92f);
		Color edge = (highlight ? (skin?.Accent ?? UIKit.Blue) : (skin?.PanelLine ?? new Color(0f, 0f, 0f, 0.2f)));
		back.sprite = Skin.Rounded();
		back.type = Image.Type.Sliced;
		back.color = fill;
		float textH = 40f;
		float picH = height - textH;
		if ((bool)g)
		{
			GameObject frame = new GameObject("Cover", typeof(RectTransform), typeof(RectMask2D));
			frame.transform.SetParent(tile.transform, worldPositionStays: false);
			UIKit.Place(frame, 3f, 3f, width - 6f, picH - 3f);
			Image pic = new GameObject("Picture", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
			pic.transform.SetParent(frame.transform, worldPositionStays: false);
			pic.raycastTarget = false;
			Sprite shot = g.GetScreenshot();
			pic.sprite = shot;
			float aspect = (shot ? (shot.rect.width / Mathf.Max(1f, shot.rect.height)) : 1.78f);
			float pw = width - 6f;
			float ph = pw / aspect;
			if (ph < picH - 3f)
			{
				ph = picH - 3f;
				pw = ph * aspect;
			}
			RectTransform prt = pic.rectTransform;
			prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
			prt.sizeDelta = new Vector2(pw, ph);
			prt.anchoredPosition = Vector2.zero;
		}
		Image ring = Skin.Shape(tile.transform, "Edge", edge, outline: true);
		ring.transform.SetAsLastSibling();
		Text name = w.Label(g ? g.GetNameSimple() : "Empty spot", 8f, picH + 1f, width - 16f, 20f, TextAnchor.MiddleLeft, bold: true, tile.transform, 13);
		name.horizontalOverflow = HorizontalWrapMode.Overflow;
		Text sub = w.Label(line ?? "", 8f, picH + 19f, width - 16f, 18f, TextAnchor.MiddleLeft, bold: false, tile.transform, 11);
		if (skin != null)
		{
			sub.color = skin.InkDim;
		}
		if ((bool)g)
		{
			UIKit.SetTooltip(tile, g.GetTooltip());
			back.raycastTarget = true;
		}
		return rt;
	}
}

// Your Digital Store: building it, then its dashboard.
internal class StoreHomePage : Page
{
	private string name;

	private int logo = 1;

	private int roomId = -1;

	private string status = "";

	private bool statusGood;

	private int seen = -1;

	private float nextTick;

	public override string Title => "Your Digital Store";

	public override Sprite IconSprite => UpdateIcons.Store();

	public override float Width => 1080f;

	public override float Height => 890f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		string locked = StoreOps.LockReason();
		if (locked != null)
		{
			w.Info(locked, 14f, 50f, w.Width - 28f, 60f);
			return;
		}
		StoreState s = StoreOps.State;
		if (!s.Built)
		{
			UpdateProject running = StoreOps.Running();
			if (running != null)
			{
				Building(w, running);
			}
			else
			{
				BuildForm(w);
			}
			return;
		}
		Dashboard(w, s);
	}

	// ---- Before the store exists ----

	private void Building(EditorWindow w, UpdateProject p)
	{
		w.Label("<b>Your store is being built</b>", 24f, 52f, w.Width - 48f, 30f, TextAnchor.MiddleLeft, bold: false, null, 17);
		w.Bar(14f, 92f, w.Width - 28f, 26f, p.Fraction, Mathf.FloorToInt(p.Fraction * 100f) + "%", UIKit.Green);
		int task = p.TaskId;
		w.SmallButton("Open the Project", 14f, 130f, 220f, () => w.Push(new UpdateProjectPage(task)), enabled: true, null, null, 30f);
		w.SmallButton("Rival Stores", 248f, 130f, 220f, () => w.Push(new StoreChartsPage()), enabled: true, null, null, 30f);
	}

	private void BuildForm(EditorWindow w)
	{
		if (name == null)
		{
			string company = (G.MS ? G.MS.GetCompanyName() : "");
			name = (string.IsNullOrWhiteSpace(company) ? "My Store" : (company.Trim() + " Launcher"));
		}
		w.Label("Open your own digital PC store. Studios sell their games through it and pay you a cut; your own games get its front page. It needs a development room to build, then servers to run, and grows with its features, catalogue and reputation.", 24f, 46f, w.Width - 48f, 44f, TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 100f;
		w.Label("Name", 24f, y, 110f, 34f, TextAnchor.MiddleLeft, bold: true);
		w.Input(name, "Store name...", 140f, y, 420f, (string v) => name = v);
		w.SmallButton("Rival Stores", w.Width - 234f, y + 3f, 220f, () => w.Push(new StoreChartsPage()), enabled: true, "The stores you'll be up against.", null, 28f);
		y += 48f;
		w.Header("Logo (only your store can use these)", 14f, y, w.Width - 28f);
		y += 32f;
		float size = 92f;
		float gap = (w.Width - 28f - size * 6f) / 5f;
		for (int i = 1; i <= StoreOps.LogoCount; i++)
		{
			int n = i;
			float x = 14f + (float)((i - 1) % 6) * (size + gap);
			float ly = y + (float)((i - 1) / 6) * (size + 10f);
			if (n == logo)
			{
				MmoUi.Rect(w.Body, x - 4f, ly - 4f, size + 8f, size + 8f, UIKit.SelectedRow);
			}
			GameObject b = new GameObject("StoreLogo" + n, typeof(RectTransform), typeof(Image), typeof(Button));
			b.transform.SetParent(w.Body, worldPositionStays: false);
			UIKit.Place(b, x, ly, size, size);
			Image img = b.GetComponent<Image>();
			img.sprite = UpdateIcons.StoreLogo(n);
			img.preserveAspect = true;
			UIKit.OnClick(b.GetComponent<Button>(), () =>
			{
				UIKit.Sound();
				logo = n;
				w.Rebuild();
			});
		}
		y += 2f * (size + 10f) + 8f;
		UpdatePlan plan = StoreOps.PlanBuild();
		w.Label("Room", 24f, y, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
		roomId = MmoUi.RoomDropdown(w, 140f, y, w.Width - 154f, roomId, plan.Work, UpdateKind.StoreBuild, (int v) =>
		{
			roomId = v;
			w.Rebuild();
		});
		y += 44f;
		roomScript room = CopyProtectDev.FindRoom(roomId);
		RivalStore biggest = StoreOps.RivalStores.Where(StoreOps.IsOpen).OrderByDescending((RivalStore r) => StoreOps.RivalShare(r.Id)).FirstOrDefault();
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", room ? GameUpdates.Duration(plan.Work, room, UpdateKind.StoreBuild) : "-"),
			EditorWindow.KV("Digital PC buyers", MmoUi.Short(StoreOps.DigitalPcPlayers())),
			EditorWindow.KV("Biggest store now", (biggest != null) ? (biggest.Name + " (" + StoreUi.Pct(StoreOps.RivalShare(biggest.Id)) + ")") : "none yet")
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 2) + 10f;
		bool ok = (bool)room;
		w.BigButton("Build the Store", 14f, y, 260f, 44f, () =>
		{
			UpdateProject p = new UpdateProject
			{
				Kind = UpdateKind.StoreBuild,
				Title = "Store client 1.0: " + (name ?? "").Trim(),
				StoreName = name,
				StoreLogo = logo
			};
			string error = GameUpdates.Start(p, StoreOps.PlanBuild(), CopyProtectDev.FindRoom(roomId), null);
			status = error ?? "";
			statusGood = error == null;
			UIKit.Sound();
			w.Rebuild();
		}, ok, ok ? ("Costs " + G.Money(plan.Cost) + ", paid now.") : "Pick a free development room with staff.");
		Text line = w.Label(status, 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = (statusGood ? UIKit.Green : UIKit.Red);
	}

	// ---- The store ----

	private void Dashboard(EditorWindow w, StoreState s)
	{
		float W = w.Width;
		StoreBanner(w, s, 46f);
		Cards(w, s, 190f);
		UsersChart(w, s, 14f, 290f, 560f, 236f);
		TopSellers(w, 588f, 290f, W - 602f, 236f);
		float bw = (W - 28f - 4f * 10f) / 5f;
		List<KeyValuePair<string, KeyValuePair<Sprite, Action>>> row1 = new List<KeyValuePair<string, KeyValuePair<Sprite, Action>>>
		{
			Nav("Charts", UpdateIcons.Game("iconCharts", UpdateIcons.Store()), () => w.Push(new StoreTopSellersPage())),
			Nav("Games", UpdateIcons.Game("iconGames", UpdateIcons.Store()), () => w.Push(new StoreCatalogPage())),
			Nav((s.Requests.Count > 0) ? ("Studios (" + s.Requests.Count + " asking)") : "Studios", UpdateIcons.Game("iconPublishingOfferVertrag", UpdateIcons.Game("iconDeveloper")), () => w.Push(new StoreStudiosPage())),
			Nav("Features", UpdateIcons.Game("iconOptions", UpdateIcons.For(UpdateKind.StoreFeature)), () => w.Push(new StoreFeaturesPage())),
			Nav("Rival Stores", UpdateIcons.Game("iconMMOEnemy", UpdateIcons.Store()), () => w.Push(new StoreChartsPage()))
		};
		List<KeyValuePair<string, KeyValuePair<Sprite, Action>>> row2 = new List<KeyValuePair<string, KeyValuePair<Sprite, Action>>>
		{
			Nav("Front Page", UpdateIcons.Game("iconStar", UpdateIcons.Store()), () => w.Push(new StoreFrontPage())),
			Nav("Sales", UpdateIcons.Game("iconMarketingSpecial", UpdateIcons.Game("iconDollar")), () => w.Push(new SalesPage())),
			Nav("Wishlists", UpdateIcons.Game("iconHype", UpdateIcons.Store()), () => w.Push(new StoreWishlistPage())),
			Nav("MMO Launcher", UpdateIcons.Game("iconMMO", UpdateIcons.Mmo()), () => w.Push(new StoreLauncherPage())),
			Nav("Consoles", UpdateIcons.Game("iconP_Konsole", UpdateIcons.For(UpdateKind.StoreConsole)), () => w.Push(new StoreConsolesPage()))
		};
		for (int i = 0; i < 5; i++)
		{
			w.IconButton(row1[i].Key, row1[i].Value.Key, 14f + (float)i * (bw + 10f), 538f, bw, 52f, row1[i].Value.Value);
			w.IconButton(row2[i].Key, row2[i].Value.Key, 14f + (float)i * (bw + 10f), 596f, bw, 52f, row2[i].Value.Value);
		}
		Settings(w, s, 660f);
	}

	private static KeyValuePair<string, KeyValuePair<Sprite, Action>> Nav(string name, Sprite icon, Action open)
	{
		return new KeyValuePair<string, KeyValuePair<Sprite, Action>>(name, new KeyValuePair<Sprite, Action>(icon, open));
	}

	// The store's banner: its logo on a glow, its name and standing, and its share of digital PC buyers.
	public static void StoreBanner(EditorWindow w, StoreState s, float y)
	{
		float W = w.Width;
		RectTransform banner = w.Banner(UpdateIcons.Banner("store"), 14f, y, W - 28f, 132f, full: true);
		// The logo on a soft glow in its own colour (any shape of logo sits well on it), inside the banner.
		Color logoColor = StoreUi.LogoColor(s.Logo);
		Image glow = MmoUi.Rect(banner, 66f - 90f, 66f - 90f, 180f, 180f, new Color(logoColor.r, logoColor.g, logoColor.b, 0.7f));
		glow.sprite = SpriteTools.Glow();
		StoreUi.Picture(w.Body, UpdateIcons.StoreLogo(s.Logo), 38f, y + 24f, 84f);
		w.BannerText("<b>" + s.Name + "</b>", 146f, y + 16f, 520f, 30f, 19, Color.white);
		int games = StorePlus.GamesOnStore(out int yours);
		w.BannerText("Open since " + s.BuiltYear + "  ·  " + games + " games  ·  " + StorePlus.StudiosSigned() + " studios signed", 146f, y + 50f, 520f, 24f, 14, new Color(0.84f, 0.88f, 0.94f));
		int rank = StorePlus.Rank(out int stores);
		string sale = (StoreOps.SaleRunning() ? ("  ·  " + (StorePlus.Current(out int _)?.Name ?? "Sale") + " on now") : "");
		w.BannerText("#" + rank + " of " + stores + " digital stores" + sale, 146f, y + 78f, 520f, 24f, 14, new Color(1f, 0.8f, 0.3f), bold: true);
		w.BannerText("Share of digital PC players", W - 340f, y + 16f, 300f, 24f, 14, Color.white, bold: false, TextAnchor.MiddleRight);
		w.BannerText("<b>" + StoreUi.Pct(s.Share) + "</b>", W - 340f, y + 40f, 300f, 26f, 17, Color.white, bold: false, TextAnchor.MiddleRight);
		MmoUi.Rect(w.Body, W - 330f, y + 76f, 290f, 14f, new Color(0f, 0f, 0f, 0.55f));
		MmoUi.Rect(w.Body, W - 329f, y + 77f, Mathf.Max(2f, 288f * Mathf.Clamp01(s.Share)), 12f, new Color(0.3f, 0.78f, 0.36f));
	}

	private static void Cards(EditorWindow w, StoreState s, float y)
	{
		float cw = (w.Width - 28f - 30f) / 4f;
		long thisWeek = ((s.AccountHistory.Count >= 2) ? (s.AccountHistory[s.AccountHistory.Count - 1] - s.AccountHistory[s.AccountHistory.Count - 2]) : 0);
		bool young = s.BuiltWeek >= 0 && CopyProtectDev.AbsWeek - s.BuiltWeek < 26;
		w.StatCard(UpdateIcons.Game("iconTotalPlatformUser", UpdateIcons.Game("iconPlayer")), "Users", MmoUi.Short(s.Accounts), young ? "New store" : ((thisWeek >= 0) ? ("+" + MmoUi.Short(thisWeek) + " this week") : (MmoUi.Short(thisWeek) + " this week")), 14f, y, cw, 86f, young ? null : new Color?((thisWeek >= 0) ? UIKit.Green : UIKit.Red), MmoUi.Short(s.Active) + " active players. MMO players in your launcher: " + MmoUi.Short(s.LauncherAccounts) + ".");
		int games = StorePlus.GamesOnStore(out int yours);
		w.StatCard(UpdateIcons.Game("iconGames", UpdateIcons.Store()), "Games on the store", games.ToString(), yours + " of them yours", 14f + (cw + 10f), y, cw, 86f, null, "Your PC games with a digital version, and the share of other studios' PC games your store carries (" + StoreUi.Pct(s.Coverage) + ", from studio trust).");
		w.StatCard(UpdateIcons.Game("iconPublishingOfferVertrag", UpdateIcons.Game("iconDeveloper")), "Studios signed", StorePlus.StudiosSigned().ToString() + ((s.Requests.Count > 0) ? ("  (" + s.Requests.Count + " asking)") : ""), "Your cut: " + s.Commission + "%  ·  fee " + ((s.SigningFee > 0) ? G.Money(StorePlus.FeeNow(s.SigningFee)) : "free"), 14f + 2f * (cw + 10f), y, cw, 86f, null, "Studio trust " + Mathf.RoundToInt(s.Trust) + ". Studios ask to sell with you and pay your signing fee once: a lower fee, a fair cut and plenty of players bring more of them (Studios).");
		long income = s.CommissionMonth + s.PlacementMonth + s.ConsoleMonth;
		long costs = s.FeesMonth + s.ServerMonth;
		long net = income - costs;
		w.StatCard(UpdateIcons.Game("iconDollar", UpdateIcons.Store()), "This month", ((net >= 0) ? "+" : "") + G.Money(net), G.Money(income) + " in  ·  " + G.Money(costs) + " out", 14f + 3f * (cw + 10f), y, cw, 86f, new Color?(UIKit.Green), "In: commission " + G.Money(s.CommissionMonth) + ", front page spots " + G.Money(s.PlacementMonth) + ", console storefronts " + G.Money(s.ConsoleMonth) + ". Out: rival store fees " + G.Money(s.FeesMonth) + ", servers " + G.Money(s.ServerMonth) + ". Last month: commission " + G.Money(s.CommissionLast) + ".");
	}

	// Accounts week by week, with a scale and the latest week in green.
	private static void UsersChart(EditorWindow w, StoreState s, float x, float y, float width, float height)
	{
		w.Header("Users over the last two years", x, y, width);
		y += 28f;
		height -= 28f;
		RectTransform panel = w.Panel(x, y, width, height);
		int count = Mathf.Min(104, s.AccountHistory.Count);
		float left = 54f;
		float top = 12f;
		float bottom = 26f;
		float chartH = height - top - bottom;
		float chartW = width - left - 12f;
		long max = Math.Max(1L, (count > 0) ? s.AccountHistory.Skip(s.AccountHistory.Count - count).Max() : 1);
		for (int i = 0; i <= 2; i++)
		{
			float gy = top + chartH * (float)i / 2f;
			MmoUi.Rect(panel, left, gy, chartW, 1f, new Color(0f, 0f, 0f, 0.12f));
			long v = max * (2 - i) / 2;
			w.Label(MmoUi.Short(v), 4f, gy - 10f, left - 8f, 20f, TextAnchor.MiddleRight, bold: false, panel, 12);
		}
		if (count < 2)
		{
			w.Label("The chart fills in week by week.", left, top + chartH / 2f - 12f, chartW, 24f, TextAnchor.MiddleCenter, bold: false, panel, 13);
		}
		else
		{
			List<long> shown = s.AccountHistory.Skip(s.AccountHistory.Count - count).ToList();
			float step = chartW / 104f;
			float start = left + (float)(104 - count) * step;
			for (int i = 0; i < count; i++)
			{
				float bh = Mathf.Max(1f, chartH * (float)shown[i] / (float)max);
				Color c = ((i == count - 1) ? new Color(0.25f, 0.72f, 0.3f) : new Color(0.24f, 0.52f, 0.86f));
				MmoUi.Rect(panel, start + (float)i * step, top + chartH - bh, Mathf.Max(1f, step - 1f), bh, c);
			}
		}
		w.Label((count > 1) ? ((count - 1) + " weeks ago") : "", left, height - bottom + 2f, 200f, 22f, TextAnchor.MiddleLeft, bold: false, panel, 12);
		w.Label("This week", width - 212f, height - bottom + 2f, 200f, 22f, TextAnchor.MiddleRight, bold: false, panel, 12);
	}

	// The five best sellers on the store this week, with gold, silver and bronze for the top three.
	private static void TopSellers(EditorWindow w, float x, float y, float width, float height)
	{
		w.Header("Top sellers this week", x, y, width);
		y += 28f;
		List<KeyValuePair<gameScript, long>> top = StorePlus.TopSellers(5);
		genres gen = (G.MainObj ? G.MainObj.GetComponent<genres>() : null);
		float rowH = (height - 28f - 6f) / 5f;
		for (int i = 0; i < 5; i++)
		{
			RectTransform row = w.Panel(x, y + (float)i * (rowH + 1.5f), width, rowH);
			if (i >= top.Count)
			{
				continue;
			}
			gameScript g = top[i].Key;
			EditorWindow.RankBadge(row, i + 1, 6f, (rowH - 26f) / 2f, 26f);
			Sprite genre = (((bool)gen && g.maingenre >= 0) ? gen.GetPic(g.maingenre) : null);
			if ((bool)genre)
			{
				Image pic = MmoUi.Rect(row, 40f, (rowH - 26f) / 2f, 26f, 26f, Color.white);
				pic.sprite = genre;
				pic.preserveAspect = true;
			}
			Text name = w.Label(g.GetNameSimple(), 74f, 0f, width - 160f, rowH, TextAnchor.MiddleLeft, bold: true, row, 13);
			if (g.IsMyGame())
			{
				name.color = UIKit.Blue;
			}
			w.Label(MmoUi.Short(top[i].Value), width - 90f, 0f, 82f, rowH, TextAnchor.MiddleRight, bold: false, row, 13);
			UIKit.SetTooltip(row.gameObject, g.GetTooltip());
			Image hit = row.GetComponent<Image>();
			if ((bool)hit)
			{
				hit.raycastTarget = true;
			}
		}
		if (top.Count == 0)
		{
			w.Label("No sales yet this week.", x + 10f, y + 8f, width - 20f, 24f);
		}
	}

	private void Settings(EditorWindow w, StoreState s, float y)
	{
		float half = (w.Width - 28f - 14f) / 2f;
		w.Header("Settings", 14f, y, half);
		w.Header("Standing", 14f + half + 14f, y, half);
		y += 30f;
		w.Label("Your cut of other studios' sales", 24f, y, 240f, 30f, TextAnchor.MiddleLeft, bold: true, null, 13);
		int index = Mathf.Max(0, Array.IndexOf(StoreOps.Commissions, s.Commission));
		w.Dropdown(268f, y, half - 262f, StoreOps.Commissions.Select((int c) => c + "%").ToList(), index, (int v) =>
		{
			UIKit.Sound();
			StoreOps.State.Commission = StoreOps.Commissions[Mathf.Clamp(v, 0, StoreOps.Commissions.Length - 1)];
			status = "Studios now pay you " + StoreOps.State.Commission + "% of their sales. Studio trust moves over the next weeks.";
			statusGood = true;
			GameUpdates.Touch();
		});
		float rx = 14f + half + 14f;
		w.Label("Player reputation", rx + 10f, y, 150f, 26f, TextAnchor.MiddleLeft, bold: true, null, 13);
		w.Bar(rx + 160f, y + 3f, half - 166f, 20f, s.Reputation / 100f, Mathf.RoundToInt(s.Reputation).ToString(), UIKit.ValueColor(s.Reputation), null, "What players think of your store: features and sales help; outages, paid front page spots and exclusives hurt.");
		y += 34f;
		w.Toggle("Your PC games only on your store", 24f, y, half - 14f, s.AllExclusive, (bool v) =>
		{
			StoreOps.State.AllExclusive = v;
			GameUpdates.Touch();
		}, "Every PC game of yours with a digital version sells only on your store: no rival store fees and its buyers sign up with you, but buyers who shop elsewhere don't buy it, and players don't love exclusives. You can also pick games one by one under Games.");
		w.Label("Studio trust", rx + 10f, y, 150f, 26f, TextAnchor.MiddleLeft, bold: true, null, 13);
		w.Bar(rx + 160f, y + 3f, half - 166f, 20f, s.Trust / 100f, Mathf.RoundToInt(s.Trust).ToString(), UIKit.ValueColor(s.Trust), null, "What other studios think of selling with you: a lower cut and more players raise it. It decides how much of their catalogue your store carries (" + StoreUi.Pct(s.Coverage) + " now).");
		y += 38f;
		string info = (string.IsNullOrEmpty(status) ? "A lower cut makes studios sign more easily. Keeping your PC games on your store alone pulls players over, but buyers who shop elsewhere won't find them." : status);
		w.Info(info, 14f, y, w.Width - 28f, 44f);
		UpdateProject running = StoreOps.Running();
		if (running != null)
		{
			int task = running.TaskId;
			w.SmallButton("In Development: " + running.Title + "  " + Mathf.FloorToInt(running.Fraction * 100f) + "%", w.Width - 434f, 10f, 280f, () => w.Push(new UpdateProjectPage(task)), enabled: true, "Open the project.", null, 28f);
		}
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen || (StoreOps.Running() != null && Time.unscaledTime > nextTick))
		{
			nextTick = Time.unscaledTime + 2f;
			w.Rebuild();
		}
	}
}

// Your games on the store (exclusives) and other studios' games selling through it.
internal class StoreCatalogPage : Page
{
	private int seen = -1;

	public override string Title => "Store Catalogue";

	public override Sprite IconSprite => UpdateIcons.Store();

	public override float Width => 960f;

	public override float Height => 760f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		StoreState s = StoreOps.State;
		w.Label("Your PC games with a digital version sell on your store. Make one exclusive and it skips the rival stores: no fees to them and new accounts for you, but buyers who only shop elsewhere won't buy it.", 116f, 4f, w.Width - 130f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		gameScript[] all = ((G.MS && (bool)G.MS.games_) ? G.MS.games_.arrayGamesScripts : new gameScript[0]);
		List<gameScript> mine = all.Where((gameScript g) => (bool)g && g.isOnMarket && g.IsMyGame() && g.gameTyp == 0 && !g.arcade && !g.handy && StoreOps.PcFraction(g) > 0f).OrderByDescending((gameScript g) => (g.sellsPerWeek != null && g.sellsPerWeek.Length != 0) ? g.sellsPerWeek[0] : 0).ToList();
		w.Header("Your PC games (" + mine.Count + ")", 14f, 54f, w.Width - 28f);
		ListView list = w.List(13f, 84f, w.Width - 26f, 300f);
		Tiles(w, list, mine, 214f, (RowView row, gameScript g, float x, float tw) =>
		{
			bool exclusive = StoreOps.IsExclusive(g);
			string line = ((!g.digitalVersion) ? "no digital version" : (exclusive ? "exclusive to your store" : (StoreUi.Pct(StoreOps.FeeRate(g)) + " to rival stores")));
			StoreUi.Tile(w, row.T, g, x, 4f, tw, 170f, line, exclusive);
			int id = g.myID;
			bool can = StoreOps.CanBeExclusive(g);
			Button b = w.SmallButton(exclusive ? "Sell Everywhere" : "Make Exclusive", x, 178f, tw, () =>
			{
				StoreOps.SetExclusive(GameUpdates.FindGame(id), !exclusive);
				w.Rebuild();
			}, can, exclusive ? "Back on the rival stores too." : "Only on your store: no rival store fees, and its buyers sign up with you. Players don't love exclusives (reputation).", row.T, 28f);
		});
		if (mine.Count == 0)
		{
			w.Label("No PC games of yours on the market.", 24f, 90f, w.Width - 48f, 26f);
		}
		List<gameScript> others = StoreOps.OthersGames().OrderByDescending(StoreOps.CommissionOn).ToList();
		w.Header("Other studios: " + Mathf.RoundToInt((float)others.Count * s.Coverage) + " of " + others.Count + " PC games on the market sell on your store (best sellers)", 14f, 396f, w.Width - 28f);
		ListView top = w.List(13f, 426f, w.Width - 26f, w.Height - 75f - 436f);
		Tiles(w, top, others.Take(24).ToList(), 180f, (RowView row, gameScript g, float x, float tw) =>
		{
			StoreUi.Tile(w, row.T, g, x, 4f, tw, 170f, g.GetDeveloperName() + "  ·  " + G.Money(StoreOps.CommissionOn(g)) + " a week to you");
		});
	}

	// Games as rows of four cover tiles in a list.
	public static void Tiles(EditorWindow w, ListView list, List<gameScript> games, float rowHeight, Action<RowView, gameScript, float, float> draw)
	{
		const int perRow = 4;
		const float gap = 12f;
		for (int i = 0; i < games.Count; i += perRow)
		{
			RowView row = list.Row(rowHeight);
			Image back = row.T.GetComponent<Image>();
			if ((bool)back)
			{
				back.color = Color.clear;
			}
			Transform edge = row.T.Find("SkinEdge");
			if ((bool)edge)
			{
				edge.gameObject.SetActive(value: false);
			}
			float tw = (row.W - gap * (float)(perRow - 1)) / (float)perRow;
			for (int j = 0; j < perRow && i + j < games.Count; j++)
			{
				draw(row, games[i + j], (float)j * (tw + gap), tw);
			}
		}
	}

	private void MyRow(EditorWindow w, ListView list, gameScript g)
	{
		bool can = StoreOps.CanBeExclusive(g);
		bool exclusive = StoreOps.IsExclusive(g);
		RowView row = list.Row(48f, null, g.GetTooltip());
		if (exclusive)
		{
			row.Highlight();
		}
		row.Top(g.GetNameWithTag(), 10f, row.W - 360f);
		string info = ((!g.digitalVersion) ? "No digital version: sells in shops only." : (exclusive ? "Exclusive to your store: no fees, new accounts." : ("On your front page; rival stores take " + StoreUi.Pct(StoreOps.FeeRate(g)) + " of its digital PC money.")));
		row.Bottom(info, 10f, row.W - 200f).color = (can ? (w.Skin?.InkDim ?? Color.black) : UIKit.Grey);
		int id = g.myID;
		row.Button(exclusive ? "Sell Everywhere" : "Make Exclusive", row.W - 180f, 170f, () =>
		{
			StoreOps.SetExclusive(GameUpdates.FindGame(id), !exclusive);
			w.Rebuild();
		}, can, exclusive ? "Back on the rival stores too." : "Only on your store: no rival store fees, and its buyers sign up with you. Players don't love exclusives (reputation).");
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

// Store features: development room projects.
internal class StoreFeaturesPage : Page
{
	private int seen = -1;

	public override string Title => "Store Features";

	public override Sprite IconSprite => UpdateIcons.For(UpdateKind.StoreFeature);

	public override float Width => 900f;

	public override float Height => 640f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		w.Label("Each feature makes your store more attractive to players. Build them in a development room.", 116f, 10f, w.Width - 130f, 28f, TextAnchor.MiddleLeft, bold: false, null, 13);
		ListView list = w.List(13f, 50f, w.Width - 26f, w.Height - 75f - 60f);
		UpdateProject running = StoreOps.Running();
		foreach (StoreFeature f in StoreOps.Features)
		{
			bool done = StoreOps.HasFeature(f.Id);
			bool building = running != null && running.Kind == UpdateKind.StoreFeature && running.Usk == f.Id;
			string problem = StoreOps.FeatureProblem(f);
			int id = f.Id;
			RowView row = list.Row(64f, (done || building) ? ((Action)delegate
			{
				if (building)
				{
					w.Push(new UpdateProjectPage(running.TaskId));
				}
			}) : ((Action)delegate
			{
				w.Push(new StoreFeaturePage(id));
			}), f.About);
			if (done)
			{
				row.Highlight();
			}
			Image icon = row.Picture(UpdateIcons.StoreFeature(f.Id), 10f, 44f);
			Text top = row.Top(f.Name, 64f, row.W - 300f);
			row.Bottom(f.About, 64f, row.W - 300f);
			string state = (done ? "<color=#2e7d32>Done</color>" : (building ? ("In development " + Mathf.FloorToInt(running.Fraction * 100f) + "%") : ((problem != null && StoreOps.Year < f.Year) ? ("From " + f.Year) : G.Money(StoreOps.FeatureCost(f)))));
			row.Text(state, row.W - 220f, 210f, bold: true, TextAnchor.MiddleRight);
			if (!done && !building && StoreOps.Year < f.Year)
			{
				icon.color = new Color(1f, 1f, 1f, 0.4f);
				top.color = UIKit.Grey;
			}
		}
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}

internal class StoreFeaturePage : Page
{
	private readonly int featureId;

	private int roomId = -1;

	private string status = "";

	public StoreFeaturePage(int featureId)
	{
		this.featureId = featureId;
	}

	private StoreFeature Feature => StoreOps.Feature(featureId);

	public override string Title => Feature?.Name ?? "Store feature";

	public override Sprite IconSprite => UpdateIcons.StoreFeature(featureId);

	public override float Width => 760f;

	public override float Height => 470f;

	public override void Build(EditorWindow w)
	{
		BackButton(w);
		w.Label(G.Money(((bool)G.MS) ? G.MS.money : 0), w.Width - 274f, 10f, 260f, 28f, TextAnchor.MiddleRight, bold: true);
		StoreFeature f = Feature;
		if (f == null)
		{
			return;
		}
		StoreUi.Picture(w.Body, UpdateIcons.StoreFeature(f.Id), 14f, 48f, 64f);
		w.Label("<b>" + f.Name + "</b>", 92f, 46f, w.Width - 106f, 28f, TextAnchor.MiddleLeft, bold: false, null, 17);
		w.Label(f.About, 92f, 74f, w.Width - 106f, 40f, TextAnchor.UpperLeft, bold: false, null, 13);
		float y = 126f;
		UpdatePlan plan = StoreOps.PlanFeature(f);
		if (plan.Problem != null)
		{
			w.Info(plan.Problem, 14f, y, w.Width - 28f, 56f);
			return;
		}
		w.Label("Room", 24f, y, 110f, 30f, TextAnchor.MiddleLeft, bold: true);
		roomId = MmoUi.RoomDropdown(w, 140f, y, w.Width - 154f, roomId, plan.Work, UpdateKind.StoreFeature, (int v) =>
		{
			roomId = v;
			w.Rebuild();
		});
		y += 44f;
		roomScript room = CopyProtectDev.FindRoom(roomId);
		List<string> effects = new List<string>();
		effects.Add("store appeal +" + Mathf.RoundToInt(f.Appeal));
		if (f.Reputation > 0f)
		{
			effects.Add("reputation +" + Mathf.RoundToInt(f.Reputation));
		}
		if (f.Active > 0f)
		{
			effects.Add("active players +" + Mathf.RoundToInt(f.Active * 100f) + "%");
		}
		List<KeyValuePair<string, string>> facts = new List<KeyValuePair<string, string>>
		{
			EditorWindow.KV("Costs", G.Money(plan.Cost)),
			EditorWindow.KV("Time", room ? GameUpdates.Duration(plan.Work, room, UpdateKind.StoreFeature) : "-"),
			EditorWindow.KV("Effect", string.Join(", ", effects))
		};
		y += w.Facts(14f, y, w.Width - 28f, facts, 1) + 10f;
		bool ok = (bool)room;
		w.BigButton("Start Building It", 14f, y, 260f, 44f, () =>
		{
			UpdateProject p = new UpdateProject
			{
				Kind = UpdateKind.StoreFeature,
				Usk = f.Id,
				Title = f.Name + ": " + StoreOps.State.Name
			};
			string error = GameUpdates.Start(p, StoreOps.PlanFeature(f), CopyProtectDev.FindRoom(roomId), null);
			if (error != null)
			{
				status = error;
				w.Rebuild();
				return;
			}
			UIKit.Sound();
			w.Replace(new UpdateProjectPage(p.TaskId));
		}, ok, ok ? ("Costs " + G.Money(plan.Cost) + ", paid now.") : "Pick a free development room with staff.");
		Text line = w.Label(status, 290f, y, w.Width - 304f, 44f, TextAnchor.MiddleLeft, bold: false, null, 13);
		line.color = UIKit.Red;
	}
}

// Every store: yours, the rivals (open and closed) and buying straight from the studios.
internal class StoreChartsPage : Page
{
	private int seen = -1;

	public override string Title => "Rival Stores";

	public override Sprite IconSprite => UpdateIcons.Store();

	public override float Width => 920f;

	public override float Height => 720f;

	public override void Build(EditorWindow w)
	{
		seen = GameUpdates.Version;
		BackButton(w);
		StoreState s = StoreOps.State;
		w.Label("Where PC players buy their digital games. Stores open (and close) over the years.", 116f, 10f, w.Width - 130f, 28f, TextAnchor.MiddleLeft, bold: false, null, 13);
		List<KeyValuePair<float, Action<RowView>>> rows = new List<KeyValuePair<float, Action<RowView>>>();
		if (s.Built)
		{
			rows.Add(new KeyValuePair<float, Action<RowView>>(s.Share, (RowView row) =>
			{
				row.Highlight();
				Fill(row, UpdateIcons.StoreLogo(s.Logo), s.Name + "  <color=#666666>(yours)</color>", "Since " + s.BuiltYear + "  ·  " + MmoUi.Short(s.Accounts) + " accounts", s.Commission + "% commission", s.Share, closed: false);
			}));
		}
		foreach (RivalStore r in StoreOps.RivalStores)
		{
			bool open = StoreOps.IsOpen(r);
			bool closed = StoreOps.HasClosed(r);
			if (!open && !closed)
			{
				continue;
			}
			RivalStore rr = r;
			rows.Add(new KeyValuePair<float, Action<RowView>>(closed ? (-1f) : StoreOps.RivalShare(r.Id), (RowView row) =>
			{
				Fill(row, UpdateIcons.RivalLogo(rr.Id), rr.Name, (closed ? ("Closed in " + rr.CloseYear + ". ") : ("Since " + rr.OpenYear + ". ")) + rr.About, rr.OwnGamesOnly ? "own games only" : (Mathf.RoundToInt(StoreOps.RivalFee(rr, null) * 100f) + "% cut"), StoreOps.RivalShare(rr.Id), closed);
			}));
		}
		rows.Add(new KeyValuePair<float, Action<RowView>>(s.Direct, (RowView row) =>
		{
			Fill(row, null, "Straight from the studios", "Studios' own websites and boxed games with a key. Fades as stores grow.", "no store", s.Direct, closed: false);
		}));
		ListView list = w.List(13f, 50f, w.Width - 26f, w.Height - 75f - 60f);
		int rank = 0;
		foreach (KeyValuePair<float, Action<RowView>> r2 in rows.OrderByDescending((KeyValuePair<float, Action<RowView>> x) => x.Key))
		{
			RowView row = list.Row(56f);
			rank++;
			if (r2.Key >= 0f)
			{
				row.Text(rank + ".", 4f, 34f, bold: true, TextAnchor.MiddleRight);
			}
			r2.Value(row);
		}
		if (!StoreOps.Open)
		{
			w.Label("Digital stores arrive in " + StoreOps.OpenYear + ".", 24f, 60f, w.Width - 48f, 30f);
		}
	}

	private static void Fill(RowView row, Sprite logo, string name, string about, string fee, float share, bool closed)
	{
		Image img = ((bool)logo ? StoreUi.Picture(row.T, logo, 44f, 6f, 44f) : null);
		Text top = row.Top(name, 98f, row.W - 360f);
		Text bottom = row.Bottom(about, 98f, row.W - 360f);
		// One size for every store (the row's best fit shrank long descriptions to unreadable).
		bottom.resizeTextForBestFit = false;
		bottom.fontSize = 13;
		bottom.horizontalOverflow = HorizontalWrapMode.Wrap;
		bottom.verticalOverflow = VerticalWrapMode.Truncate;
		Text f = row.Text(fee, row.W - 250f, 140f, bold: false, TextAnchor.MiddleRight);
		Text s = row.Text(closed ? "closed" : StoreUi.Pct(share), row.W - 100f, 90f, bold: true, TextAnchor.MiddleRight);
		if (closed)
		{
			if ((bool)img)
			{
				img.color = new Color(1f, 1f, 1f, 0.4f);
			}
			top.color = UIKit.Grey;
			bottom.color = UIKit.Grey;
			f.color = UIKit.Grey;
			s.color = UIKit.Grey;
		}
	}

	public override void Tick(EditorWindow w)
	{
		if (GameUpdates.Version != seen)
		{
			w.Rebuild();
		}
	}
}
