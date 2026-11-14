using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HolyAddon.Core;
using UnityEngine;

namespace HolyAddon;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency("holyexpansion.mgt2", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
	// Called "Editor" (GUID mgt2.editor) up to 0.6.0. Save keys keep the old "mgt2.editor." prefix so saves still load.
	public const string Guid = "holyunofficialaddon.mgt2";

	public const string Name = "Holy Unofficial Addon";

	public const string Version = "0.23.3";

	private const string OldGuid = "mgt2.editor";

	internal static ManualLogSource Log;

	internal static string PluginDir;

	internal static ConfigEntry<KeyboardShortcut> Hotkey;

	internal static ConfigEntry<string> ButtonIcon;

	internal static ConfigEntry<bool> KeepEditedSkills;

	internal static ConfigEntry<bool> EnableCheats;

	internal static ConfigEntry<bool> ExtraSpeedButtons;

	internal static ConfigEntry<float> TurboSpeed;

	internal static ConfigEntry<float> UltraSpeed;

	internal static ConfigEntry<KeyCode> TurboKey;

	internal static ConfigEntry<KeyCode> UltraKey;

	internal static ConfigEntry<float> CollisionLeniency;

	internal static ConfigEntry<bool> FurnitureOverlap;

	internal static ConfigEntry<bool> IgnoreRoomRules;

	internal static ConfigEntry<bool> PressReviews;

	internal static ConfigEntry<int> OutletsPerGame;

	internal static ConfigEntry<bool> ContractsEnabled;

	internal static ConfigEntry<bool> ContractTracker;

	internal static ConfigEntry<bool> CopyProtection;

	internal static ConfigEntry<bool> ExtraUpdates;

	internal static ConfigEntry<bool> MmoManagement;

	internal static ConfigEntry<bool> MmoDatedPenalty;

	internal static ConfigEntry<bool> MmoBots;

	internal static ConfigEntry<bool> MmoFanFest;

	internal static ConfigEntry<bool> HolySeasonalFrom2003;

	internal static ConfigEntry<bool> MergedSales;

	internal static ConfigEntry<bool> Convention;

	internal static ConfigEntry<bool> LiveServiceInRoom;

	internal static ConfigEntry<bool> IndustryEvents;

	internal static ConfigEntry<int> IndustryPace;

	internal static ConfigEntry<bool> StoreEnabled;

	internal static ConfigEntry<bool> StoreRivalFees;

	internal static ConfigEntry<bool> TimedExclusives;

	internal static ConfigEntry<bool> FasterNewGameScreen;

	internal static ConfigEntry<bool> StutterFinderOn;

	internal static ConfigEntry<bool> AwardsCeremony;

	internal static ConfigEntry<bool> PerformanceTweaks;

	internal static ConfigEntry<int> StutterThresholdMs;

	internal static ConfigEntry<float> TrackerX;

	internal static ConfigEntry<float> TrackerY;

	internal static ConfigEntry<float> TrackerLeft;

	internal static ConfigEntry<float> TrackerTop;

	private void Awake()
	{
		Log = Logger;
		PluginDir = Path.GetDirectoryName(Info.Location);
		MoveOldSettings();
		Hotkey = Config.Bind("General", "Hotkey", KeyboardShortcut.Empty, "Optional key that opens or closes the addon's window (the bottom bar button always works).");
		// 0.1.0 saved a game sprite (iconRename) as the button icon; drop that setting so the Editor's own icon shows.
		ConfigDefinition oldIcon = new ConfigDefinition("General", "ButtonIcon");
		Config.Bind(oldIcon, "");
		Config.Remove(oldIcon);
		Config.Save();
		ButtonIcon = Config.Bind("General", "Icon", "", "Leave empty for the addon's own icon (icon.png next to HolyUnofficialAddon.dll), or enter a game sprite name such as iconOptions or iconSkill.");
		EnableCheats = Config.Bind("General", "EnableCheats", false, "Shows the Employees editor on the addon's home page (set any employee's skills, motivation and sickness). Off by default.");
		KeepEditedSkills = Config.Bind("Employees", "KeepEditedSkills", true, "Skills outside an employee's profession normally get pushed back down to 50 (60 with the right perk) as soon as they use them. When on, a skill you raised above that in the employee editor stays where you put it (it just won't grow further).");
		ExtraSpeedButtons = Config.Bind("Speed", "ExtraSpeedButtons", true, "Show Turbo and Ultra speed buttons next to the game's pause / play / fast buttons.");
		TurboSpeed = Config.Bind("Speed", "TurboSpeed", 6f, new ConfigDescription("How many times normal speed the Turbo button runs at (The Holy Expansion's turbo is 6).", new AcceptableValueRange<float>(4f, 30f)));
		UltraSpeed = Config.Bind("Speed", "UltraSpeed", 12f, new ConfigDescription("How many times normal speed the Ultra button runs at. Very high values can make employees jump around while walking.", new AcceptableValueRange<float>(6f, 30f)));
		TurboKey = Config.Bind("Speed", "TurboKey", KeyCode.Alpha4, "Key for Turbo speed (the game uses 1, 2 and 3 for pause, play and fast).");
		UltraKey = Config.Bind("Speed", "UltraKey", KeyCode.Alpha5, "Key for Ultra speed.");
		CollisionLeniency = Config.Bind("Building", "CollisionLeniency", 0f, new ConfigDescription("How far (in floor tiles) an object's collision box may push into a wall or another object before it counts as blocked. Collision boxes are often bigger than the furniture you see. 0 = the game's rules.", new AcceptableValueRange<float>(0f, 0.5f)));
		FurnitureOverlap = Config.Bind("Building", "FurnitureOverlap", false, "Let furniture overlap other furniture as much as you like (walls and doors still follow CollisionLeniency).");
		IgnoreRoomRules = Config.Bind("Building", "IgnoreRoomRules", false, "Let any object go in any room or hallway (a toilet in the dev room, a desk in the lobby...).");
		// 0.2.0 had a single FurniturePlacement setting; anyone who turned it on wanted looser placement, so start them on a medium leniency.
		ConfigDefinition oldPlacement = new ConfigDefinition("Building", "FurniturePlacement");
		string old = Config.Bind(oldPlacement, "Normal").Value;
		Config.Remove(oldPlacement);
		if (old != "Normal")
		{
			CollisionLeniency.Value = 0.25f;
			FurnitureOverlap.Value = old == "AnyRoomAndOverlap";
		}
		Config.Save();
		PressReviews = Config.Bind("Reviews", "PressReviews", true, "Game reviews come from named outlets with logos, reviewers and genre-aware text, with a strip of every outlet's score (English game language only; scores are not changed). Text lives in reviews/reviews.json.");
		OutletsPerGame = Config.Bind("Reviews", "OutletsPerGame", 4, new ConfigDescription("How many outlets review each game.", new AcceptableValueRange<int>(2, 5)));
		ContractsEnabled = Config.Bind("Contracts", "ShowContractsButton", true, "Show the Contracts button on the bottom bar: hire outside studios to research for you or work on your games in development. Running contracts carry on either way.");
		ContractTracker = Config.Bind("Contracts", "ShowTracker", true, "Show a small on-screen panel with your running contracts and their progress (only while a contract is running).");
		// 0.5.0 placed a single panel by its bottom-left corner; the cards are placed by their top-left corner.
		ConfigDefinition oldFold = new ConfigDefinition("Contracts", "TrackerFolded");
		Config.Bind(oldFold, false);
		Config.Remove(oldFold);
		TrackerX = Config.Bind("Contracts", "TrackerX", 14f, "Old (0.5.0) panel position from the left; only used once to place the contract cards.");
		TrackerY = Config.Bind("Contracts", "TrackerY", 110f, "Old (0.5.0) panel position from the bottom; only used once to place the contract cards.");
		TrackerLeft = Config.Bind("Contracts", "CardsLeft", -1f, "Where the contract cards sit: distance from the left edge of the screen (drag a card to change it).");
		TrackerTop = Config.Bind("Contracts", "CardsTop", -1f, "Where the contract cards sit: distance from the top edge of the screen.");
		CopyProtection = Config.Bind("CopyProtection", "Enabled", true, "Research \"Copy protection\" (Other research) and develop your own in a development room, then update it and license it to other studios (Management > Your copy protection). Copy protections you already made keep working when this is off.");
		ExtraUpdates = Config.Bind("Updates", "MoreUpdateTypes", true, "Updates & Addons gets ports to more platforms of the same kind, engine updates, new gameplay features and age rating edits for games you have on the market. Their sales effects only apply while this is on.");
		MmoManagement = Config.Bind("MMO", "MmoManagement", true, "MMO Management in the development room menu: graphics overhauls, 64-bit clients, engine migrations, world expansions and content patches for your MMOs, plus an old-school setting. Their effects on players only apply while this is on.");
		MmoDatedPenalty = Config.Bind("MMO", "DatedPenalty", true, "Modern MMOs whose graphics and tech fall well behind today's games lose players faster and draw fewer new ones. Old-school MMOs never do.");
		MmoBots = Config.Bind("MMO", "Bots", true, "Bots in MMOs (yours and rivals'): they're counted among the subscribers and drive real players away. Remove them with updates and ban waves, keep them out with anti-bot systems you build for a genre or license from other studios.");
		MmoFanFest = Config.Bind("MMO", "FanFest", true, "A yearly fan festival for your MMOs (second week of October) as a pop-up: pick the MMO, venue, announcement and extras, or skip it.");
		StoreEnabled = Config.Bind("Store", "Enabled", true, "Your own digital PC store (Management > Your digital store) from 2003, with rival stores opening over the years. Its effects only apply while this is on.");
		StoreRivalFees = Config.Bind("Store", "RivalStoreFees", true, "Rival digital stores take their cut (10-30%) of your games' digital PC sales made through them, as in real life. Your own store and store exclusives don't pay it.");
		TimedExclusives = Config.Bind("Release", "TimedExclusives", true, "The new-game screen offers timed exclusives for multiplatform games: released on one platform first (the maker pays a deal), on the others after 3 to 24 months.");
		FasterNewGameScreen = Config.Bind("Performance", "FasterNewGameScreen", true, "The new-game screen builds its gameplay feature list without recalculating every cost once per feature, and looks platforms up from a cache instead of searching the whole scene. Same results, much less waiting in big late-game saves. Idea from Aerin the Lion's Unofficial MGT2 Performance Patch.");
		PerformanceTweaks = Config.Bind("Performance", "GameTweaks", true, "Lighter versions of some of the game's busiest code, with the same results: money and number text is built in one go and reused (far less garbage, so fewer garbage-collection pauses), and a new game is added to the game list instead of the game searching the whole scene for every game again. With The Holy Expansion, its studio support finds rooms and tasks without searching the whole scene every second (that was most of the game's regular stutter).");
		Core.Performance.GameTweaks.Init();
		AwardsCeremony = Config.Bind("Awards", "Ceremony", true, "The December MGH Games Awards get 12 awards with up to five nominees each (plus the Worst Game of the Year) on a new ceremony screen. The game's own categories keep their prizes; the new ones give less, and one ceremony never pays more than sweeping the game's own awards could.");
		StutterFinderOn = Config.Bind("Performance", "StutterFinder", false, "Times the game's recurring jobs, menus and every script's per-frame work, notes each slow frame with what ran in it, and tracks memory and garbage collection. Writes BepInEx\\StutterReport.txt every minute. Switching it on pauses the game for a few seconds while it hooks in, and it adds a little overhead, so leave it off when you're not looking for stutter. Switching it off stops the timing at once; its hooks stay idle until you restart the game.");
		StutterThresholdMs = Config.Bind("Performance", "StutterThresholdMs", 50, new ConfigDescription("How long a frame must take (in milliseconds) for the stutter finder to note it. 50 ms = a drop below 20 frames a second.", new AcceptableValueRange<int>(16, 1000)));
		HolySeasonalFrom2003 = Config.Bind("Holy", "SeasonalSalesFrom2003", true, "The Summer and Winter Sales (the addon's sales events, or The Holy Expansion's Seasonal Sales) start in 2003, when digital stores arrive, instead of 1998.");
		MergedSales = Config.Bind("Sales", "SalesEvents", true, "One system for seasonal sales: the Summer and Winter Sales (plus your store's Spring and Autumn Sales once you own one), with a pop-up as each starts where you set your games' discount and your store's sale. Replaces The Holy Expansion's Seasonal Sales (same discounts and numbers). Off = Holy's Seasonal Sales as before.");
		Convention = Config.Bind("Events", "YearlyConvention", true, "Your own yearly convention for all your games (doors open in August, week 4): plan it from a Support room or the August pop-up, a Support room prepares it, and what you put on stage gets hype. Replaces The Holy Expansion's Fan Convention and the MMO FanFest. Off = those two as before.");
		LiveServiceInRoom = Config.Bind("Holy", "LiveServiceSeasonsInRoom", true, "The Holy Expansion's \"Start the next season automatically\" prepares the season in a free Marketing room (250 work, as a manual season) instead of starting it instantly; it waits for a free room if needed.");
		IndustryEvents = Config.Bind("Industry", "Events", true, "Industry events: news headlines that move the market, and decisions (pop-ups) about other studios, deals and your games, from 1976 on. Plus the Industry window: every studio and where you stand with it.");
		IndustryPace = Config.Bind("Industry", "Pace", 1, new ConfigDescription("How often decisions come up, at random: 0 = calm, 1 = normal (every 2-3 months on average), 2 = busy.", new AcceptableValueRange<int>(0, 2)));
		Harmony harmony = new Harmony(Guid);
		harmony.PatchAll(typeof(Plugin).Assembly);
		HolyCompat.Apply(harmony);
		GameObject gameObject = new GameObject("HolyAddon");
		Object.DontDestroyOnLoad(gameObject);
		gameObject.hideFlags = HideFlags.HideAndDontSave;
		gameObject.AddComponent<EditorRunner>();
		gameObject.AddComponent<Core.Performance.StutterFinder>();
		Log.LogInfo(Name + " " + Version + " loaded");
	}

	// Settings lived in mgt2.editor.cfg before the rename: carry them over once.
	private void MoveOldSettings()
	{
		try
		{
			string old = Path.Combine(Paths.ConfigPath, OldGuid + ".cfg");
			if (File.Exists(old) && !File.Exists(Config.ConfigFilePath))
			{
				File.Copy(old, Config.ConfigFilePath);
				Config.Reload();
				File.Delete(old);
				Log.LogInfo("Moved the settings from " + OldGuid + ".cfg");
			}
		}
		catch (System.Exception ex)
		{
			Log.LogWarning("Could not move the old settings: " + ex.Message);
		}
	}
}
