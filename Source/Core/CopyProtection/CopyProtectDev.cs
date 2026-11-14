using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.Core.CopyProtection;

// A copy protection your studio made. The game itself holds it as a normal copyProtectScript (COPYPROTECT_<Id>),
// so it shows up wherever the game lists copy protections and is saved with it; this is the rest of its story.
internal class OwnCopyProtect
{
	public int Id;

	public string Name;

	public int Tech;

	public int Major = 1;

	public int Minor;

	// 0 = your own games only, 1 = licences sold to other studios, 2 = free for every studio.
	public int Licensing;

	public long Fee;

	public List<int> Licensees = new List<int>();

	public int LicensesSold;

	public long Revenue;

	// Rival games that shipped with it.
	public int RivalGames;

	public int ReleasedWeek;

	public int UpdatedWeek;

	public int TroubleWeek = -1000;

	public bool Retired;

	[JsonIgnore]
	public string FullName => Name + " " + Major + "." + Minor;
}

// The copy protection your team is working on right now (a new one or an update), done as a development room task.
internal class CopyProtectProject
{
	public int TaskId;

	public int RoomId = -1;

	// "SafeDisc 1.0"; BaseName is "SafeDisc".
	public string Name;

	public string BaseName;

	public int Tech;

	public int Major = 1;

	// The product being updated, or -1 for a new copy protection.
	public int ProductId = -1;

	public float Needed;

	public float Progress;

	public long Cost;

	[JsonIgnore]
	public bool IsUpdate => ProductId >= 0;

	[JsonIgnore]
	public float Fraction => Mathf.Clamp01(Progress / Mathf.Max(1f, Needed));
}

internal class CopyProtectState
{
	public List<OwnCopyProtect> Products = new List<OwnCopyProtect>();

	public CopyProtectProject Project;

	// This save has had the "Copy protection" research set up (the game never offers it on its own).
	public bool ResearchSetUp;

	// The "you can now develop copy protection" news was shown.
	public bool Announced;

	public long TotalRevenue;

	public int TotalLicenses;
}

// Develop your own copy protection: research it under Other research, build it in a development room, keep it
// strong with updates, and use it in your games for free or license it to other studios.
[HarmonyPatch]
internal static class CopyProtectDev
{
	public const int ResearchSlot = 37;

	private const string SaveKey = "mgt2.editor.copyprotect";

	public const int UpdateCooldown = 8;

	public const float WeakStrength = 35f;

	private const float UpdateShare = 0.35f;

	public static readonly string[] TechNames = new string[5] { "Disk check", "Code wheel and serial key", "Disc fingerprint", "Online activation", "Always-online DRM" };

	public static readonly string[] TechInfo = new string[5]
	{
		"The game looks for a hidden mark on its disk that copiers can't reproduce.",
		"Players enter a code from the manual, code wheel or box before they can play.",
		"Every disc carries a physical fingerprint that burners and copy tools can't copy.",
		"Each copy is activated once over the internet with its own key.",
		"The game checks in with your servers every time it starts. Very hard to crack, but players hate it: every release and update costs reputation."
	};

	public static readonly int[] TechYear = new int[5] { 1980, 1986, 1993, 2002, 2010 };

	// How big a job it is, as a share of building an engine with every engine feature you've researched.
	private static readonly float[] Share = new float[5] { 0.3f, 0.45f, 0.6f, 0.8f, 1f };

	// Strength lost per week once cracking starts (the bought ones lose 0.2): newer technology is harder to crack.
	// It's scaled by how many people can get their hands on it (see Exposure).
	private static readonly float[] BaseDecay = new float[5] { 0.4f, 0.34f, 0.28f, 0.22f, 0.15f };

	// A technology stays state of the art for this many years; after that crackers' tools for it are ready on
	// day one, so a new release or update of it starts weaker (3% per year, never below 40%).
	public const int FreshYears = 6;

	// Weeks after a release or update before anyone has cracked it: longer when only your own games carry it.
	public const int UncrackedOwn = 6;

	public const int UncrackedShared = 3;

	public static readonly string[] LicensingNames = new string[3] { "Own games only", "Sell licences to studios", "Free for every studio" };

	public static readonly float[] FeeSteps = new float[5] { 0.5f, 0.75f, 1f, 1.5f, 2f };

	private static readonly string[] NameA = new string[12] { "Safe", "Lock", "Iron", "Vault", "Seal", "Guard", "Key", "Fort", "Cipher", "Solid", "Prime", "Secure" };

	private static readonly string[] NameB = new string[10] { "Disc", "Guard", "Lock", "Shield", "Key", "Seal", "Protect", "Sentry", "Wall", "Code" };

	public static CopyProtectState S = new CopyProtectState();

	private static readonly AccessTools.FieldRef<mainScript, GameObject[]> CopyProtectCache = AccessTools.FieldRefAccess<mainScript, GameObject[]>("arrCopyProtect");

	private static Sprite icon;

	private static bool loggedResearch;

	// Bumped whenever something changes, so open pages can refresh.
	public static int Version { get; private set; }

	// New entry points (room menu, Management menu, research) show only with the setting on; running projects and
	// released products carry on either way.
	public static bool On
	{
		get
		{
			if (Plugin.CopyProtection.Value)
			{
				return G.Singleplayer;
			}
			return false;
		}
	}

	public static CopyProtectProject Project => S.Project;

	public static IEnumerable<OwnCopyProtect> Products => S.Products;

	public static int Year
	{
		get
		{
			if (!G.MS)
			{
				return 1976;
			}
			return G.MS.year;
		}
	}

	public static int AbsWeek
	{
		get
		{
			if (!G.MS)
			{
				return 0;
			}
			return (G.MS.year * 12 + G.MS.month - 1) * 4 + G.MS.week - 1;
		}
	}

	public static void Touch()
	{
		Version++;
	}

	// ---- When it becomes available ----

	private static forschungSonstiges Other
	{
		get
		{
			if (!G.MainObj)
			{
				return null;
			}
			return G.MainObj.GetComponent<forschungSonstiges>();
		}
	}

	private static bool SlotExists(forschungSonstiges fs)
	{
		if ((bool)fs && fs.RES_POINTS != null && fs.RES_POINTS_LEFT != null && fs.RES_PRICE != null && fs.RES_POINTS.Length > ResearchSlot && fs.RES_POINTS_LEFT.Length > ResearchSlot)
		{
			return fs.RES_PRICE.Length > ResearchSlot;
		}
		return false;
	}

	// The first copy protection reaches the shops in August 1980; the research is offered from then on.
	public static bool MarketOpen
	{
		get
		{
			unlockScript unlock = (G.MainObj ? G.MainObj.GetComponent<unlockScript>() : null);
			if ((bool)unlock)
			{
				return unlock.Get(31);
			}
			return false;
		}
	}

	public static bool Researched
	{
		get
		{
			forschungSonstiges other = Other;
			if (S.ResearchSetUp && SlotExists(other))
			{
				return other.IsErforscht(ResearchSlot);
			}
			return false;
		}
	}

	public static bool ResearchRunning
	{
		get
		{
			forschungSonstiges other = Other;
			if (SlotExists(other))
			{
				return other.BereitsInAnderenRaumAktiv(ResearchSlot);
			}
			return false;
		}
	}

	public static float ResearchPercent
	{
		get
		{
			forschungSonstiges other = Other;
			if (!SlotExists(other))
			{
				return 0f;
			}
			return other.GetProzent(ResearchSlot);
		}
	}

	public static long ResearchPrice
	{
		get
		{
			forschungSonstiges other = Other;
			if (!SlotExists(other))
			{
				return 0L;
			}
			return other.RES_PRICE[ResearchSlot];
		}
	}

	// Shown in the research room's Other research list right now.
	public static bool ResearchOffered
	{
		get
		{
			if (On && S.ResearchSetUp && MarketOpen && SlotExists(Other) && !Researched)
			{
				return !ResearchRunning;
			}
			return false;
		}
	}

	public static string ResearchName => G.Text(381, "Copy protection");

	public static string ResearchTooltip => "Develop your own copy protection in a development room: free for your own games, kept strong with updates, and something you can license to other studios. (Holy Unofficial Addon)";

	// What stops you starting a copy protection right now, or null if nothing does.
	public static string LockReason()
	{
		if (!MarketOpen)
		{
			return "Copy protection doesn't exist yet. The first one comes out in August 1980.";
		}
		if (!Researched)
		{
			return "Research \"" + ResearchName + "\" first, in a research room under Other research.";
		}
		return null;
	}

	// Give "Copy protection" (Other research slot 37, unused by the game) its points, price and picture.
	// RES_POINTS isn't saved, so this runs after every load; the points left are saved by the game.
	private static void SetUpResearch()
	{
		forschungSonstiges fs = Other;
		if (!SlotExists(fs))
		{
			return;
		}
		if (!loggedResearch)
		{
			loggedResearch = true;
			Plugin.Log.LogInfo("Other research slot 37 from the game: points " + fs.RES_POINTS[ResearchSlot] + ", left " + fs.RES_POINTS_LEFT[ResearchSlot] + ", price " + fs.RES_PRICE[ResearchSlot] + ", picture " + ((fs.RES_SPRITE != null && fs.RES_SPRITE.Length > ResearchSlot && (bool)fs.RES_SPRITE[ResearchSlot]) ? fs.RES_SPRITE[ResearchSlot].name : "none"));
		}
		float points = fs.RES_POINTS[ResearchSlot];
		if (points <= 0f)
		{
			points = (float)Math.Round(Reference(fs.RES_POINTS.Select((float p) => (double)p), 60.0));
			fs.RES_POINTS[ResearchSlot] = points;
		}
		if (fs.RES_PRICE[ResearchSlot] <= 0)
		{
			fs.RES_PRICE[ResearchSlot] = (int)Math.Round(Reference(fs.RES_PRICE.Select((int p) => (double)p), 25000.0) / 1000.0) * 1000;
		}
		if (fs.RES_SPRITE != null && fs.RES_SPRITE.Length > ResearchSlot && !fs.RES_SPRITE[ResearchSlot])
		{
			fs.RES_SPRITE[ResearchSlot] = Icon();
		}
		if (!S.ResearchSetUp)
		{
			// The game never offers this research, so nobody can have done it yet.
			fs.RES_POINTS_LEFT[ResearchSlot] = points;
			S.ResearchSetUp = true;
			Plugin.Log.LogInfo("Copy protection research set up: " + points + " points, " + fs.RES_PRICE[ResearchSlot]);
		}
		else if (fs.RES_POINTS_LEFT[ResearchSlot] > points)
		{
			fs.RES_POINTS_LEFT[ResearchSlot] = points;
		}
	}

	// Average of the room research (slots 28 to 34) as a yardstick for points and price.
	private static double Reference(IEnumerable<double> values, double fallback)
	{
		List<double> list = values.Skip(28).Take(7).Where((double v) => v > 0.0).ToList();
		if (list.Count != 0)
		{
			return list.Average();
		}
		return fallback;
	}

	// ---- Technology, strength and costs ----

	public static string TechName(int t)
	{
		return TechNames[Mathf.Clamp(t, 0, TechNames.Length - 1)];
	}

	public static bool TechUnlocked(int t)
	{
		return Year >= TechYear[Mathf.Clamp(t, 0, TechYear.Length - 1)];
	}

	public static int BestTech()
	{
		int best = 0;
		for (int i = 0; i < TechYear.Length; i++)
		{
			if (TechUnlocked(i))
			{
				best = i;
			}
		}
		return best;
	}

	// Strength of a fresh, uncracked release: 100% while the technology is current, less once it's old news.
	public static float Fresh(int tech)
	{
		tech = Mathf.Clamp(tech, 0, TechYear.Length - 1);
		return Mathf.Clamp(100f - 3f * (float)Mathf.Max(0, Year - TechYear[tech] - FreshYears), 40f, 100f);
	}

	public static float Decay(int tech)
	{
		return BaseDecay[Mathf.Clamp(tech, 0, BaseDecay.Length - 1)];
	}

	// A weak team ships it with holes: 1 point off per 5 points of average programming skill below 50 (at most 10).
	public static float TeamPenalty(roomScript room)
	{
		List<characterScript> staff = Staff(room);
		if (staff.Count == 0)
		{
			return 0f;
		}
		return Mathf.Round(Mathf.Clamp((50f - staff.Average((characterScript c) => c.s_programmieren)) / 5f, 0f, 10f));
	}

	public static float ReleaseStrength(int tech, roomScript room)
	{
		return Mathf.Clamp(Fresh(tech) - TeamPenalty(room), 1f, 100f);
	}

	// Why a release would start below 100%, or null.
	public static string FreshReason(int tech, roomScript room)
	{
		List<string> reasons = new List<string>();
		int age = Year - TechYear[Mathf.Clamp(tech, 0, TechYear.Length - 1)];
		if (age > FreshYears)
		{
			reasons.Add(TechName(tech) + " is " + age + " years old: crackers' tools for it are ready on day one (-" + Mathf.RoundToInt(100f - Fresh(tech)) + ")");
		}
		float penalty = TeamPenalty(room);
		if (penalty > 0f)
		{
			reasons.Add("the room's programmers are inexperienced (-" + Mathf.RoundToInt(penalty) + ")");
		}
		if (reasons.Count != 0)
		{
			return string.Join("; ", reasons);
		}
		return null;
	}

	// Weekly loss once cracked, used only by your own games with none of them in the shops yet.
	public static float BaseWear(int tech)
	{
		return Decay(tech) * Exposure(0, 0, 0);
	}

	public static int UncrackedWeeks(int licensing)
	{
		if (licensing != 0)
		{
			return UncrackedShared;
		}
		return UncrackedOwn;
	}

	// Weeks until crackers get through the current version (0 once they have).
	public static int UncrackedLeft(OwnCopyProtect p)
	{
		return Mathf.Max(0, UncrackedWeeks(p.Licensing) - (AbsWeek - p.UpdatedWeek));
	}

	// How many people can pick it apart: games of yours in the shops and studios using it. Kept to your own
	// games it's a small target; given away for free it's everywhere.
	public static float Exposure(int licensing, int ownGames, int studios)
	{
		float factor = ((licensing == 0) ? 0.7f : ((licensing == 1) ? 1f : 1.2f));
		return factor * (1f + 0.015f * (float)Mathf.Min(ownGames, 40) + 0.03f * (float)Mathf.Min(studios, 40));
	}

	private static (float work, long cost) FullEngine()
	{
		engineFeatures ef = (G.MainObj ? G.MainObj.GetComponent<engineFeatures>() : null);
		float work = 0f;
		long cost = 0L;
		if ((bool)ef && ef.engineFeatures_RES_POINTS_LEFT != null)
		{
			for (int i = 0; i < ef.engineFeatures_RES_POINTS_LEFT.Length; i++)
			{
				if (ef.IsErforscht(i))
				{
					work += (float)ef.GetDevPointsForEngine(i);
					cost += ef.GetDevCostsForEngine(i);
				}
			}
		}
		return (work: Mathf.Max(200f, work), cost: Math.Max(Math.Max(20000L, MarketPrice() * 2), cost));
	}

	public static float WorkNeeded(int tech, bool update)
	{
		return FullEngine().work * Share[Mathf.Clamp(tech, 0, Share.Length - 1)] * (update ? UpdateShare : 1f);
	}

	public static long Cost(int tech, bool update)
	{
		return (long)((float)FullEngine().cost * Share[Mathf.Clamp(tech, 0, Share.Length - 1)] * (update ? UpdateShare : 1f) / 100f) * 100;
	}

	// ---- The game's copy protection objects ----

	private static IEnumerable<copyProtectScript> AllScripts()
	{
		return from go in GameObject.FindGameObjectsWithTag("CopyProtect")
			select (!go) ? null : go.GetComponent<copyProtectScript>() into s
			where (bool)s
			select s;
	}

	public static OwnCopyProtect Find(int id)
	{
		return S.Products.FirstOrDefault((OwnCopyProtect p) => p.Id == id);
	}

	public static copyProtectScript Script(OwnCopyProtect p)
	{
		GameObject go = GameObject.Find("COPYPROTECT_" + p.Id);
		if (!go)
		{
			return null;
		}
		return go.GetComponent<copyProtectScript>();
	}

	public static float Strength(OwnCopyProtect p)
	{
		copyProtectScript s = Script(p);
		if (!s)
		{
			return 0f;
		}
		return s.effekt;
	}

	// The copy protections you can buy, which rival studios use too.
	private static List<copyProtectScript> Market()
	{
		HashSet<int> ours = new HashSet<int>(S.Products.Select((OwnCopyProtect p) => p.Id));
		return AllScripts().Where((copyProtectScript s) => s.isUnlocked && !ours.Contains(s.myID)).ToList();
	}

	// The newest one in the shops sets the going rate for a licence.
	public static long MarketPrice()
	{
		copyProtectScript newest = (from s in Market()
			orderby s.date_year * 12 + s.date_month descending
			select s).FirstOrDefault();
		if (!newest || newest.price <= 0)
		{
			return 15000L;
		}
		return newest.price;
	}

	public static float MarketBest()
	{
		return Market().Select((copyProtectScript s) => s.effekt).DefaultIfEmpty(0f).Max();
	}

	private static IEnumerable<gameScript> AllGames()
	{
		games g = (G.MS ? G.MS.games_ : null);
		if (!g || g.arrayGamesScripts == null)
		{
			return Enumerable.Empty<gameScript>();
		}
		return g.arrayGamesScripts.Where((gameScript x) => (bool)x);
	}

	public static List<gameScript> MyGamesUsing(OwnCopyProtect p)
	{
		return AllGames().Where((gameScript g) => g.gameCopyProtect == p.Id && g.IsMyGame() && (g.isOnMarket || g.inDevelopment)).ToList();
	}

	public static publisherScript FindStudio(int id)
	{
		publisherScript[] all = (G.MS ? G.MS.arrayPublisherScripts : null);
		if (all == null)
		{
			return null;
		}
		return all.FirstOrDefault((publisherScript p) => (bool)p && p.myID == id);
	}

	public static bool Licensed(OwnCopyProtect p, publisherScript studio)
	{
		if (p.Retired || !studio)
		{
			return false;
		}
		if (p.Licensing != 2)
		{
			return p.Licensees.Contains(studio.myID);
		}
		return true;
	}

	private static void ClearCache()
	{
		try
		{
			if ((bool)G.MS)
			{
				CopyProtectCache(G.MS) = null;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Copy protection cache: " + ex.Message);
		}
	}

	private static void SetNames(copyProtectScript s, string name)
	{
		s.name_EN = (s.name_GE = (s.name_TU = (s.name_CH = (s.name_FR = (s.name_CT = (s.name_RU = (s.name_IT = (s.name_JA = (s.name_UA = (s.name_TH = name))))))))));
	}

	private static readonly string[] TechIconFiles = new string[5] { "cp-disk.png", "cp-codewheel.png", "cp-fingerprint.png", "cp-activation.png", "cp-alwaysonline.png" };

	// The technology's own icon (icons/cp-*.png, drawn by Assets/icons/update-icons.html), or the general one.
	public static Sprite TechIcon(int tech)
	{
		if (tech >= 0 && tech < TechIconFiles.Length)
		{
			Sprite sprite = UIKit.LoadSprite(System.IO.Path.Combine(System.IO.Path.Combine(Plugin.PluginDir, "icons"), TechIconFiles[tech]));
			if ((bool)sprite)
			{
				return sprite;
			}
		}
		return Icon();
	}

	// The copy protection picture from the game's Management menu ("Buy copy protection").
	public static Sprite Icon()
	{
		if ((bool)icon)
		{
			return icon;
		}
		try
		{
			GameObject dropdown = (((bool)G.Gui && (bool)G.Gui.guiMainButtons_ && G.Gui.guiMainButtons_.uiObjects != null && G.Gui.guiMainButtons_.uiObjects.Length > 9) ? G.Gui.guiMainButtons_.uiObjects[9] : null);
			Button button = (dropdown ? dropdown.GetComponentsInChildren<Button>(includeInactive: true).FirstOrDefault((Button b) => b.onClick.GetPersistentEventCount() > 0 && b.onClick.GetPersistentMethodName(0) == "DROPDOWN_BuyCopyProtect") : null);
			if ((bool)button)
			{
				List<Image> images = (from im in button.GetComponentsInChildren<Image>(includeInactive: true)
					where im.gameObject != button.gameObject && (bool)im.sprite && !im.name.StartsWith("Lock") && !im.transform.parent.name.StartsWith("Lock")
					select im).ToList();
				Image picture = images.FirstOrDefault((Image im) => im.name.Contains("Icon")) ?? images.FirstOrDefault();
				if ((bool)picture)
				{
					icon = picture.sprite;
					Plugin.Log.LogInfo("Copy protection icon: " + icon.name);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Copy protection icon: " + ex.Message);
		}
		if (!icon)
		{
			icon = UIKit.Sprite("iconCopyProtect") ?? UIKit.Sprite("iconKopierschutz");
		}
		return icon;
	}

	// ---- Rooms ----

	// Development rooms, found directly (the game's cached room list can lag behind right after loading).
	// Development rooms, from the game's room list (it refreshes the list the frame after rooms change). Searching
	// the whole scene with FindObjectsOfType took ~14 ms in a big save.
	public static List<roomScript> Rooms()
	{
		roomScript[] all = (G.MS ? G.MS.arrayRoomScripts : null);
		if (all == null)
		{
			all = UnityEngine.Object.FindObjectsOfType<roomScript>();
		}
		return all.Where((roomScript r) => (bool)r && r.typ == 1).ToList();
	}

	public static roomScript FindRoom(int id)
	{
		if (id < 0)
		{
			return null;
		}
		return Rooms().FirstOrDefault((roomScript r) => r.myID == id);
	}

	public static string RoomName(roomScript r)
	{
		if (!r)
		{
			return "";
		}
		if (!string.IsNullOrEmpty(r.myName))
		{
			return r.myName;
		}
		List<int> ids = (from x in Rooms()
			select x.myID into x
			orderby x
			select x).ToList();
		roomDataScript rd = (G.MainObj ? G.MainObj.GetComponent<roomDataScript>() : null);
		return (((bool)rd) ? rd.GetName(r.typ) : "Development room") + " " + (ids.IndexOf(r.myID) + 1);
	}

	public static List<characterScript> Staff(roomScript r)
	{
		characterScript[] all = (G.MS ? G.MS.arrayCharactersScripts : null);
		if (!r || all == null)
		{
			return new List<characterScript>();
		}
		return all.Where((characterScript c) => (bool)c && c.roomID == r.myID).ToList();
	}

	// Free development rooms, the fastest first.
	public static List<roomScript> FreeRooms()
	{
		return (from r in Rooms()
			where r.taskID == -1
			select r).OrderByDescending(CopyProtectRoom.Rate).ToList();
	}

	public static string Duration(float work, roomScript r)
	{
		float rate = CopyProtectRoom.Rate(r);
		if (rate <= 0f)
		{
			return "-";
		}
		int weeks = Mathf.CeilToInt(work / rate);
		if (weeks > 1)
		{
			return "about " + weeks + " weeks";
		}
		return "about 1 week";
	}

	private static string RoomProblem(roomScript room)
	{
		if (!room || room.typ != 1)
		{
			return "Pick a development room.";
		}
		if (room.taskID != -1)
		{
			return RoomName(room) + " is busy with other work.";
		}
		if (Staff(room).Count == 0)
		{
			return RoomName(room) + " has no staff.";
		}
		return null;
	}

	// ---- Starting, updating, cancelling ----

	public static string StartNew(string name, int tech, roomScript room)
	{
		name = (name ?? "").Trim();
		if (name.Length == 0)
		{
			return "Give your copy protection a name first.";
		}
		string locked = LockReason();
		if (locked != null)
		{
			return locked;
		}
		if (Project != null)
		{
			return "Your team is already working on " + Project.Name + ".";
		}
		if (!TechUnlocked(tech))
		{
			return TechName(tech) + " is only possible from " + TechYear[tech] + ".";
		}
		int major = 1;
		List<OwnCopyProtect> same = S.Products.Where((OwnCopyProtect p) => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();
		if (same.Count > 0)
		{
			if (same.Any((OwnCopyProtect p) => p.Tech >= tech))
			{
				return "You already have " + name + " with this technology. Update it instead, or pick a newer technology for version " + (same.Max((OwnCopyProtect p) => p.Major) + 1) + ".0.";
			}
			major = same.Max((OwnCopyProtect p) => p.Major) + 1;
			name = same[0].Name;
		}
		string problem = RoomProblem(room);
		if (problem != null)
		{
			return problem;
		}
		long cost = Cost(tech, update: false);
		if (G.MS.NotEnoughMoney(cost))
		{
			return "You need " + G.Money(cost) + " for the development costs.";
		}
		G.MS.Pay(cost, 4);
		S.Project = new CopyProtectProject
		{
			Name = name + " " + major + ".0",
			BaseName = name,
			Tech = tech,
			Major = major,
			Needed = Mathf.Max(1f, WorkNeeded(tech, update: false)),
			Cost = cost,
			TaskId = UnityEngine.Random.Range(1, 100000000)
		};
		CopyProtectRoom.Attach(S.Project, room);
		Version++;
		return null;
	}

	public static int WeeksToUpdate(OwnCopyProtect p)
	{
		return Mathf.Max(0, UpdateCooldown - (AbsWeek - p.UpdatedWeek));
	}

	public static string UpdateProblem(OwnCopyProtect p)
	{
		if (Project != null)
		{
			return "Your team is already working on " + Project.Name + ".";
		}
		if (p.Retired)
		{
			return p.FullName + " is off the market. Bring it back first.";
		}
		int weeks = WeeksToUpdate(p);
		if (weeks > 0)
		{
			return "The last update just came out. The next one is possible in " + weeks + ((weeks == 1) ? " week." : " weeks.");
		}
		return null;
	}

	public static string StartUpdate(OwnCopyProtect p, roomScript room)
	{
		string problem = UpdateProblem(p) ?? RoomProblem(room);
		if (problem != null)
		{
			return problem;
		}
		long cost = Cost(p.Tech, update: true);
		if (G.MS.NotEnoughMoney(cost))
		{
			return "You need " + G.Money(cost) + " for the development costs.";
		}
		G.MS.Pay(cost, 4);
		S.Project = new CopyProtectProject
		{
			Name = p.Name + " " + p.Major + "." + (p.Minor + 1),
			BaseName = p.Name,
			Tech = p.Tech,
			Major = p.Major,
			ProductId = p.Id,
			Needed = Mathf.Max(1f, WorkNeeded(p.Tech, update: true)),
			Cost = cost,
			TaskId = UnityEngine.Random.Range(1, 100000000)
		};
		CopyProtectRoom.Attach(S.Project, room);
		Version++;
		return null;
	}

	public static long Refund(CopyProtectProject pr)
	{
		return (long)((float)pr.Cost * (1f - pr.Fraction));
	}

	// Stop the current project; what wasn't used of the development costs comes back.
	public static long Cancel()
	{
		CopyProtectProject pr = S.Project;
		if (pr == null)
		{
			return 0L;
		}
		long refund = Refund(pr);
		S.Project = null;
		CopyProtectRoom.Detach(pr);
		if (refund > 0 && (bool)G.MS)
		{
			G.MS.Earn(refund, 1);
		}
		Version++;
		return refund;
	}

	// Work from the room's staff; finishing releases the copy protection or its update.
	public static void Work(float points, roomScript room)
	{
		CopyProtectProject pr = S.Project;
		if (pr == null)
		{
			return;
		}
		pr.Progress += points;
		if (pr.Progress < pr.Needed)
		{
			return;
		}
		S.Project = null;
		CopyProtectRoom.Detach(pr);
		Version++;
		try
		{
			roomDataScript rd = (G.MainObj ? G.MainObj.GetComponent<roomDataScript>() : null);
			if ((bool)room && (bool)G.Gui && (bool)rd)
			{
				G.Gui.CreateLeftNews(room.myID, TechIcon(pr.Tech), "<b>" + pr.Name + "</b>", rd.roomData_SPRITE[Mathf.Clamp(room.typ, 0, rd.roomData_SPRITE.Length - 1)]);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("Copy protection news: " + ex.Message);
		}
		try
		{
			Report(pr.IsUpdate ? ApplyUpdate(pr, room) : Release(pr, room));
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogError("Finishing copy protection " + pr.Name + " failed: " + ex2);
		}
	}

	private static string Release(CopyProtectProject pr, roomScript room)
	{
		copyProtectScript s = G.MS.copyProtect_.CreateCopyProtect();
		int id = 1000;
		foreach (copyProtectScript other in AllScripts())
		{
			if (other != s)
			{
				id = Math.Max(id, other.myID + 1);
			}
		}
		OwnCopyProtect p = new OwnCopyProtect
		{
			Id = id,
			Name = pr.BaseName ?? pr.Name,
			Tech = pr.Tech,
			Major = pr.Major,
			Fee = MarketPrice(),
			ReleasedWeek = AbsWeek,
			UpdatedWeek = AbsWeek
		};
		s.myID = id;
		s.date_year = G.MS.year;
		s.date_month = G.MS.month;
		s.price = 0;
		s.dev_costs = 0;
		s.isUnlocked = true;
		s.inBesitz = true;
		s.neverLooseEffect = true;
		s.effekt = ReleaseStrength(pr.Tech, room);
		SetNames(s, p.FullName);
		s.Init();
		S.Products.Add(p);
		ClearCache();
		Version++;
		string text = "<b>" + p.FullName + "</b> is finished! Strength: " + Mathf.RoundToInt(s.effekt) + "%. Nobody has cracked it yet: it holds that for the first " + UncrackedWeeks(p.Licensing) + " weeks.\n\nPick it for your games like any copy protection: it's free to use. Keep it strong with updates, or license it to other studios, in Management > Your Copy Protection.";
		if (pr.Tech == 4)
		{
			G.MS.AddStudioPoints(-2);
			text += "\n\n<color=red>Always-online DRM: players are unhappy. Reputation -2.</color>";
		}
		History("Released the copy protection " + p.FullName + ".");
		return text;
	}

	private static string ApplyUpdate(CopyProtectProject pr, roomScript room)
	{
		OwnCopyProtect p = Find(pr.ProductId);
		copyProtectScript s = ((p != null) ? Script(p) : null);
		if (p == null || !s)
		{
			return "The update was finished, but the copy protection no longer exists.";
		}
		float before = s.effekt;
		p.Minor++;
		p.UpdatedWeek = AbsWeek;
		s.effekt = Mathf.Max(s.effekt, ReleaseStrength(p.Tech, room));
		SetNames(s, p.FullName);
		Version++;
		string text = "<b>" + p.FullName + "</b> is out! Strength: " + Mathf.RoundToInt(before) + "% -> " + Mathf.RoundToInt(s.effekt) + "%.";
		int games = MyGamesUsing(p).Count;
		if (games > 0 || p.Licensees.Count > 0)
		{
			text = text + "\nEvery game that uses it gets the update" + ((p.Licensees.Count > 0) ? (", including those of the " + p.Licensees.Count + ((p.Licensees.Count == 1) ? " studio" : " studios") + " licensing it") : "") + ".";
		}
		// With The Holy Expansion's piracy: the update holds like new again on your games, and a quarter of their
		// pirated copies stop working.
		long gone = 0L;
		foreach (gameScript g in MyGamesUsing(p).Where((gameScript x) => x.isOnMarket))
		{
			gone += HolyCompat.BreakPirates(g, 0.25f, 0f);
		}
		if (gone > 0)
		{
			text = text + "\nIt broke the pirated copies of your games that use it: pirates -" + G.MS.GetMoney(gone, showDollar: false) + ".";
		}
		if (p.Tech == 4)
		{
			G.MS.AddStudioPoints(-1);
			text += "\n<color=red>Another always-online update: reputation -1.</color>";
		}
		return text;
	}

	// ---- Managing ----

	public static string SetLicensing(OwnCopyProtect p, int mode)
	{
		if (p.Licensing == mode)
		{
			return null;
		}
		p.Licensing = mode;
		if (mode == 1 && p.Fee <= 0)
		{
			p.Fee = MarketPrice();
		}
		Version++;
		switch (mode)
		{
		case 1:
			return p.FullName + " is now offered to other studios for " + G.Money(p.Fee) + " per licence.";
		case 2:
			return p.FullName + " is now free for every studio. Each studio that picks it up brings you fans.";
		default:
			return p.FullName + " is now for your own games only. Studios that already bought a licence keep it.";
		}
	}

	public static void SetFee(OwnCopyProtect p, long fee)
	{
		p.Fee = Math.Max(0L, fee);
		Version++;
	}

	public static string SetRetired(OwnCopyProtect p, bool retired)
	{
		p.Retired = retired;
		copyProtectScript s = Script(p);
		if ((bool)s)
		{
			s.isUnlocked = !retired;
		}
		ClearCache();
		Version++;
		if (!retired)
		{
			return p.FullName + " is back: you can pick it for new games again.";
		}
		return p.FullName + " is off the market: no new games can use it and no studio will license it. Games that already use it keep it.";
	}

	public static string RandomName()
	{
		for (int i = 0; i < 30; i++)
		{
			string a = NameA[UnityEngine.Random.Range(0, NameA.Length)];
			string b = NameB[UnityEngine.Random.Range(0, NameB.Length)];
			string n = a + b;
			if (a != b && S.Products.All((OwnCopyProtect p) => !string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase)))
			{
				return n;
			}
		}
		return "SafeDisc";
	}

	// ---- Every week ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly()
	{
		if (!G.Singleplayer)
		{
			return;
		}
		try
		{
			SetUpResearch();
			CheckProject();
			if (On && Researched && !S.Announced)
			{
				S.Announced = true;
				News("You can now develop your own copy protection: open a development room's menu, or Management > Your Copy Protection.");
			}
			foreach (OwnCopyProtect p in S.Products.ToList())
			{
				copyProtectScript s = Script(p);
				if ((bool)s)
				{
					s.isUnlocked = !p.Retired;
					Wear(p, s);
					DropClosedStudios(p);
					if (!p.Retired && p.Licensing != 0)
					{
						Licensing(p, s);
					}
					Trouble(p, s);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Copy protection week failed: " + ex);
		}
		Version++;
	}

	// The room working on it was torn down or given other work: stop and refund.
	private static void CheckProject()
	{
		CopyProtectProject pr = S.Project;
		if (pr == null)
		{
			return;
		}
		roomScript room = Rooms().FirstOrDefault((roomScript r) => r.taskID == pr.TaskId);
		if ((bool)room)
		{
			pr.RoomId = room.myID;
			return;
		}
		long refund = Cancel();
		News("Work on " + pr.Name + " stopped: no room is working on it any more." + ((refund > 0) ? (" " + G.Money(refund) + " came back.") : ""));
	}

	private static void Wear(OwnCopyProtect p, copyProtectScript s)
	{
		s.effekt = Mathf.Max(0f, s.effekt - WeeklyWear(p));
	}

	// Strength lost this week: nothing while it's uncracked, then more the more people have it.
	public static float WeeklyWear(OwnCopyProtect p)
	{
		if (UncrackedLeft(p) > 0)
		{
			return 0f;
		}
		return CrackedWear(p);
	}

	public static float CrackedWear(OwnCopyProtect p)
	{
		int ownGames = MyGamesUsing(p).Count((gameScript g) => g.isOnMarket);
		float wear = Decay(p.Tech) * Exposure(p.Licensing, ownGames, p.Licensees.Count);
		if (p.Retired)
		{
			wear *= 0.5f;
		}
		return wear;
	}

	private static void DropClosedStudios(OwnCopyProtect p)
	{
		p.Licensees.RemoveAll((int id) =>
		{
			publisherScript studio = FindStudio(id);
			return !studio || studio.Geschlossen();
		});
	}

	private static float Appeal(float strength, long fee, long market)
	{
		float s = Mathf.Clamp01(strength / 100f);
		return s * s * (float)market / ((float)market * 0.5f + Mathf.Max(0f, fee));
	}

	// Rival studios weigh it against the copy protections in the shops; a few may sign up each week.
	private static void Licensing(OwnCopyProtect p, copyProtectScript s)
	{
		if (s.effekt < WeakStrength)
		{
			if (p.Licensees.Count == 0)
			{
				return;
			}
			List<int> leaving = p.Licensees.Where((int _) => UnityEngine.Random.value < 0.04f).ToList();
			foreach (int id in leaving)
			{
				p.Licensees.Remove(id);
			}
			if (leaving.Count > 0)
			{
				publisherScript first = FindStudio(leaving[0]);
				News((first ? first.GetName() : "A studio") + " dropped " + p.FullName + ": cracked copies keep turning up.");
			}
			return;
		}
		long market = MarketPrice();
		long fee = ((p.Licensing == 2) ? 0 : p.Fee);
		float mine = Appeal(s.effekt, fee, market);
		float best = Market().Select((copyProtectScript m) => Appeal(m.effekt, m.price, market)).DefaultIfEmpty(0f).Max();
		float chance = mine / (mine + best + 0.0001f) / 90f;
		publisherScript[] all = G.MS.arrayPublisherScripts;
		if (all == null)
		{
			return;
		}
		int signed = 0;
		string firstName = null;
		foreach (publisherScript studio in all)
		{
			if (signed >= 3)
			{
				break;
			}
			if ((bool)studio && !studio.isPlayer && studio.developer && studio.isUnlocked && !studio.IsTochterfirma() && !studio.Geschlossen() && !p.Licensees.Contains(studio.myID) && UnityEngine.Random.value < chance)
			{
				p.Licensees.Add(studio.myID);
				p.LicensesSold++;
				S.TotalLicenses++;
				signed++;
				if (firstName == null)
				{
					firstName = studio.GetName();
				}
				if (fee > 0)
				{
					G.MS.Earn(fee, 2);
					p.Revenue += fee;
					S.TotalRevenue += fee;
				}
				else
				{
					G.MS.AddFans(Mathf.RoundToInt(UnityEngine.Random.Range(200f, 900f) * (1f + (float)Mathf.Max(0, Year - 1980) / 20f)), -1);
				}
			}
		}
		if (signed == 0)
		{
			return;
		}
		if (fee > 0)
		{
			News(firstName + " licensed " + p.FullName + " for " + G.Money(fee) + "." + ((signed > 1) ? (" (" + signed + " studios this week)") : ""));
		}
		else
		{
			News(firstName + " now protects its games with your free " + p.FullName + ". Players take notice of your studio.");
			if (p.LicensesSold % 5 == 0)
			{
				G.MS.AddStudioPoints(1);
			}
		}
	}

	// A weak copy protection gets cracked now and then: bad press until you release an update.
	private static void Trouble(OwnCopyProtect p, copyProtectScript s)
	{
		if (s.effekt >= 25f || AbsWeek - p.TroubleWeek < 12 || UnityEngine.Random.value > 0.15f)
		{
			return;
		}
		List<gameScript> mine = MyGamesUsing(p).Where((gameScript g) => g.isOnMarket).ToList();
		if (mine.Count == 0 && p.Licensees.Count == 0)
		{
			return;
		}
		p.TroubleWeek = AbsWeek;
		if (mine.Count > 0)
		{
			gameScript game = mine[UnityEngine.Random.Range(0, mine.Count)];
			News("Crackers broke " + p.FullName + ": copies of <color=blue>" + game.GetNameSimple() + "</color> are everywhere. Release an update!");
		}
		else
		{
			News("Crackers broke " + p.FullName + ". The studios licensing it are getting nervous: release an update!");
		}
	}

	// ---- Rival studios only use it with a licence ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(publisherScript), "SetCopyProtect")]
	private static void RivalPick(publisherScript __instance, gameScript script_)
	{
		if (S.Products.Count == 0 || !script_ || script_.arcade || !__instance || __instance.isPlayer)
		{
			return;
		}
		try
		{
			// Your own subsidiaries use it as if they'd bought a licence.
			bool subsidiary = __instance.IsMyTochterfirma();
			copyProtectScript best = null;
			copyProtectScript mineBest = null;
			OwnCopyProtect mineProduct = null;
			foreach (copyProtectScript s in AllScripts())
			{
				if (!s.isUnlocked)
				{
					continue;
				}
				OwnCopyProtect own = Find(s.myID);
				if (own == null)
				{
					if (!best || s.effekt > best.effekt)
					{
						best = s;
					}
				}
				else if ((Licensed(own, __instance) || (subsidiary && !own.Retired)) && (!mineBest || s.effekt > mineBest.effekt))
				{
					mineBest = s;
					mineProduct = own;
				}
			}
			// A studio that paid for a licence uses it while it holds up; a free one only if it's about as good.
			bool paid = mineProduct != null && (subsidiary || mineProduct.Licensees.Contains(__instance.myID));
			bool useMine = (bool)mineBest && mineBest.effekt >= WeakStrength && (paid || !best || mineBest.effekt >= best.effekt - 15f);
			if (useMine)
			{
				script_.gameCopyProtect = mineBest.myID;
				script_.gameCopyProtectScript_ = null;
				mineProduct.RivalGames++;
			}
			else if (Find(script_.gameCopyProtect) != null)
			{
				script_.gameCopyProtect = (best ? best.myID : (-1));
				script_.gameCopyProtectScript_ = null;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Rival copy protection pick failed: " + ex);
		}
	}

	// ---- Research room ----

	private static readonly AccessTools.FieldRef<Menu_Forschung, string> SearchText = AccessTools.FieldRefAccess<Menu_Forschung, string>("searchStringA");

	private static readonly System.Reflection.MethodInfo CreateItem = AccessTools.Method(typeof(Menu_Forschung), "CreateItem");

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Forschung), "Init")]
	private static void ResearchList(Menu_Forschung __instance, int i)
	{
		if (i != 5 || !ResearchOffered)
		{
			return;
		}
		try
		{
			string search = (SearchText(__instance) ?? "").ToLower();
			if ((bool)__instance.myInputField && __instance.myInputField.text.Length > 0 && !ResearchName.ToLower().Contains(search))
			{
				return;
			}
			CreateItem.Invoke(__instance, new object[1] { ResearchSlot });
			__instance.DROPDOWN_Sort();
			if ((bool)G.Gui)
			{
				G.Gui.KeinEintrag(__instance.uiObjects[0], __instance.uiObjects[5]);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Adding copy protection research failed: " + ex);
		}
	}

	// The count on the Other research button, and automatic research picking the next item.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Forschung), "GetAmountForschung")]
	private static void ResearchCount(int i, bool getUnerforschtesObjekt, ref int __result)
	{
		if (i != 5 || !ResearchOffered)
		{
			return;
		}
		if (getUnerforschtesObjekt)
		{
			if (__result == -1)
			{
				__result = ResearchSlot;
			}
		}
		else
		{
			__result++;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(forschungSonstiges), "GetTooltip")]
	private static void ResearchInfo(int i, ref string __result)
	{
		if (i == ResearchSlot && string.IsNullOrEmpty(__result))
		{
			__result = ResearchTooltip;
		}
	}

	// ---- Messages ----

	private static void News(string text)
	{
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsInfo(text);
		}
	}

	private static void History(string text)
	{
		if ((bool)G.Gui)
		{
			G.Gui.AddHistory(text);
		}
	}

	public static void Report(string text)
	{
		if (!G.Gui || string.IsNullOrEmpty(text))
		{
			return;
		}
		bool closeMenu = false;
		if (!G.Gui.menuOpen)
		{
			G.Gui.OpenMenu(hideChars: false);
			closeMenu = true;
		}
		G.Gui.MessageBox(text, closeMenu);
		if (G.Gui.uiObjects != null && G.Gui.uiObjects.Length > 40 && (bool)G.Gui.uiObjects[40])
		{
			G.Gui.uiObjects[40].transform.SetAsLastSibling();
		}
	}

	// ---- Saving ----

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "SaveHistory")]
	private static void Save(ES3Writer writer)
	{
		if (!G.Singleplayer)
		{
			return;
		}
		try
		{
			writer.Write<string>(SaveKey, JsonConvert.SerializeObject(S));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Saving copy protection failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		S = new CopyProtectState();
		CopyProtectRoom.ClearTasks();
		Version++;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(savegameScript), "LoadHistory")]
	private static void Load(ES3File ___es3file)
	{
		try
		{
			if (___es3file != null && ___es3file.KeyExists(SaveKey))
			{
				S = JsonConvert.DeserializeObject<CopyProtectState>(___es3file.Load<string>(SaveKey)) ?? new CopyProtectState();
				S.Products = S.Products ?? new List<OwnCopyProtect>();
				foreach (OwnCopyProtect p in S.Products)
				{
					p.Licensees = p.Licensees ?? new List<int>();
				}
				Plugin.Log.LogInfo("Copy protection loaded: " + S.Products.Count + " products" + ((S.Project != null) ? (", working on " + S.Project.Name) : ""));
			}
			else
			{
				S = new CopyProtectState();
			}
			SetUpResearch();
			if (S.Project != null)
			{
				CopyProtectRoom.Restore(S.Project);
			}
			ClearCache();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading copy protection failed: " + ex);
			S = new CopyProtectState();
		}
		Version++;
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		S = new CopyProtectState();
		try
		{
			CopyProtectRoom.ClearTasks();
			SetUpResearch();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Copy protection research setup failed: " + ex);
		}
		Version++;
	}
}
