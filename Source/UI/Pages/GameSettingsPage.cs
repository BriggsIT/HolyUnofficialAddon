using System;
using System.Collections.Generic;
using HolyAddon.Core;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.UI.Pages;

// The new-game settings, changeable mid-game, plus the Editor's own options.
// The game reads every one of these settings live and stores them in the save, so changing them later is safe.
internal class GameSettingsPage : Page
{
	public override string Title => G.Text(856, "Game settings");

	public override string Icon => "iconOptions";

	// The addon window's own size (HomePage); the settings scroll.
	public override float Height => 742f;

	public override void Build(EditorWindow w)
	{
		mainScript ms = G.MS;
		BackButton(w);
		w.Label("Changes apply straight away.", 116f, 10f, w.Width - 130f, 28f);
		// Everything else scrolls, in two columns of toggles.
		RectTransform area = w.ScrollArea(13f, 50f, w.Width - 26f, w.Height - 75f - 60f);
		w.Into = area;
		float W = Inner(w);
		float y = 4f;
		w.Header("This game (saved with your save file)", 0f, y, W);
		y += 32f;
		Row(w, ref y, G.Text(798, "Level of difficulty"), Texts((802, "Very easy"), (803, "Easy"), (804, "Medium"), (805, "Hard"), (1685, "Very hard"), (806, "Legendary")), ms.difficulty, (int v) => ms.difficulty = v, "Affects sales, reviews, costs and rival studios from now on. Note: the Legendary achievements check this setting.");
		Row(w, ref y, G.Text(800, "Game speed"), Texts((1335, "Marathon"), (807, "Slow"), (808, "Normal"), (809, "Fast"), (810, "Very Fast")), SpeedIndex(ms), (int v) => ms.speedSetting = ms.gameSpeeds[Mathf.Clamp(v, 0, ms.gameSpeeds.Length - 1)], "How long a week lasts. Slower settings give you more real time per week. With \"Adjust working speed\" on, your staff speed up or slow down to match.");
		Row(w, ref y, G.Text(2072, "Development time"), Texts((2073, "Standard"), (2074, "Realistic"), (2269, "Short development time")), ms.devTimeSetting, (int v) => ms.devTimeSetting = v, "Realistic: big games take much longer to make. Short: all game development runs twice as fast. Also applies to games already in development.");
		Row(w, ref y, G.Text(2266, "Random events"), Texts((2073, "Standard"), (1001, "Historical Random Events"), (837, "No random events")), ms.settings_randomEvents, (int v) => ms.settings_randomEvents = v, "Which world events can happen from now on.");
		Row(w, ref y, G.Text(2397, "Competition"), Texts((2398, "Low"), (2399, "Normal"), (2400, "Tough")), ms.settings_competition, (int v) => ms.settings_competition = v, "How strong rival studios' games are.");
		w.Toggle(G.Text(1363, "Adjust working speed"), L, y + 2f, W - 16f, ms.settings_arbeitsgeschwindigkeitAnpassen, (bool v) => ms.settings_arbeitsgeschwindigkeitAnpassen = v, "When on, employees work faster or slower to match the game speed setting, so a slower game doesn't make development feel slower.");
		y += 44f;
		w.Header("Addon options (all saves)", 0f, y, W);
		y += 32f;
		Text leniency = w.Label("Furniture collision leniency", 10f, y, 250f, 30f);
		UIKit.SetTooltip(leniency.gameObject, "Collision boxes are often bigger than the furniture you see, so an object that looks clear of a wall still turns red. This lets a box push that far (in floor tiles) into walls and other furniture before it counts as blocked. Off = the game's rules.");
		w.Slider(270f, y, W - 270f, 0f, 0.5f, Plugin.CollisionLeniency.Value, (float v) => Plugin.CollisionLeniency.Value = Mathf.Round(v * 20f) / 20f, (float v) => (Mathf.Round(v * 20f) < 1f) ? "Off" : ((Mathf.Round(v * 20f) / 20f).ToString("0.00") + " tile"));
		y += 38f;
		w.Toggle("Furniture can overlap furniture", L, y + 2f, LW, Plugin.FurnitureOverlap.Value, (bool v) => Plugin.FurnitureOverlap.Value = v, "Furniture may overlap other furniture as much as you like. Walls and doors still follow the leniency above, so employees can always get through.");
		w.Toggle("Enable cheats", R, y + 2f, W - R, Plugin.EnableCheats.Value, (bool v) => Plugin.EnableCheats.Value = v, "Shows the Employees editor on the addon's home page: set any employee's skills, motivation and sickness.");
		y += 34f;
		w.Toggle("Performance tweaks", R, y + 2f, W - R, Plugin.PerformanceTweaks.Value, (bool v) => Plugin.PerformanceTweaks.Value = v, "Lighter versions of some of the game's busiest code, same results: money and number text is built in one go and reused, so there's far less garbage and fewer garbage-collection pauses; and a new game is added to the game list instead of the game searching the whole scene for all its games again. With The Holy Expansion, its studio support no longer searches the whole scene every second (most of the regular stutter).");
		w.Toggle("Ignore room rules", L, y + 2f, LW, Plugin.IgnoreRoomRules.Value, (bool v) => Plugin.IgnoreRoomRules.Value = v, "Any object in any room or hallway (a toilet in the dev room, a desk in the lobby...).");
		y += 36f;
		w.Toggle("New awards ceremony", R, y + 2f, W - R, Plugin.AwardsCeremony.Value, (bool v) => Plugin.AwardsCeremony.Value = v, "The December MGH Games Awards get 12 awards with up to five nominees each (plus Worst Game of the Year) on a new ceremony screen. The game's own categories keep exactly their prizes; the new ones give less, and a whole ceremony never pays more than sweeping the game's own awards could. Off = the game's own awards.");
		w.Toggle("Keep edited skills", L, y + 2f, LW, Plugin.KeepEditedSkills.Value, (bool v) => Plugin.KeepEditedSkills.Value = v, "The game pushes skills outside an employee's profession back down to 50 (60 with the right perk) as soon as they use them. When on, a skill you raised in the employee editor stays put.");
		y += 36f;
		w.Toggle("Stutter finder", R, y + 2f, W - R, Plugin.StutterFinderOn.Value, (bool v) => Plugin.StutterFinderOn.Value = v, "Finds what makes the game stutter: times the game's recurring jobs, menus and every script's per-frame work, notes each frame slower than " + Plugin.StutterThresholdMs.Value + " ms with what ran in it, and tracks memory and garbage collection. Writes BepInEx\\StutterReport.txt every minute and when switched off. Switching it on pauses the game for a few seconds while it hooks in; leave it off when you're not looking for stutter. Switching it off stops the timing at once, and its hooks are removed the next time you start the game.");
		w.Toggle("Press reviews", L, y + 2f, LW, Plugin.PressReviews.Value, (bool v) => Plugin.PressReviews.Value = v, "Reviews come from named outlets with logos and reviewers, with text that knows the game's genre, topic and platform, plus a strip of every outlet's score. Purely cosmetic: the game's scores don't change. English game language only.");
		y += 36f;
		w.Toggle("Contracts button", L, y + 2f, LW, Plugin.ContractsEnabled.Value, (bool v) => Plugin.ContractsEnabled.Value = v, "Show the Contracts button on the bottom bar, to hire outside studios for research or work on your games. Contracts already running carry on either way.");
		w.Toggle("Contract cards on screen", R, y + 2f, W - R, Plugin.ContractTracker.Value, (bool v) => Plugin.ContractTracker.Value = v, "Cards like the game's own game cards, one per running contract, with its progress. Drag a card to move them.");
		y += 36f;
		w.Toggle("Own copy protection", L, y + 2f, LW, Plugin.CopyProtection.Value, (bool v) => Plugin.CopyProtection.Value = v, "Research \"Copy protection\" under Other research, then build your own in a development room, update it and license it to other studios (Management > Your Copy Protection). Copy protections you already made keep working when this is off.");
		w.Toggle("More update types", R, y + 2f, W - R, Plugin.ExtraUpdates.Value, (bool v) => Plugin.ExtraUpdates.Value = v, "Updates & Addons gets ports to more platforms, engine updates, new gameplay features and age rating edits for games you have on the market. Updates already done keep their effect only while this is on.");
		y += 36f;
		w.Toggle("MMO Management", L, y + 2f, LW, Plugin.MmoManagement.Value, (bool v) => Plugin.MmoManagement.Value = v, "MMO Management in the development room menu: graphics overhauls, 64-bit clients, engine migrations, world expansions and content patches for your MMOs. Their effects on players only apply while this is on.");
		w.Toggle("Dated MMOs lose players", R, y + 2f, W - R, Plugin.MmoDatedPenalty.Value, (bool v) => Plugin.MmoDatedPenalty.Value = v, "Modern MMOs whose graphics and tech fall well behind today's games lose players faster and draw fewer new ones. Old-school MMOs never do.");
		y += 36f;
		w.Toggle("MMO bots and anti-bot", L, y + 2f, LW, Plugin.MmoBots.Value, (bool v) => Plugin.MmoBots.Value = v, "Bots in MMOs (yours and rivals'): counted among the subscribers, they drive real players away. Remove them with updates and ban waves; keep them out with anti-bot systems you build for a genre or license from other studios.");
		w.Toggle("Yearly convention", R, y + 2f, W - R, Plugin.Convention.Value, (bool v) =>
		{
			Plugin.Convention.Value = v;
			Core.Updates.GameUpdates.Touch();
		}, "Your own convention for all your games every August: plan it from a Support room or the August pop-up, a Support room prepares it, and what you put on stage gets hype. Replaces The Holy Expansion's Fan Convention and the MMO FanFest. Off = those two as before.");
		y += 36f;
		w.Toggle("Your Digital Store", L, y + 2f, LW, Plugin.StoreEnabled.Value, (bool v) => Plugin.StoreEnabled.Value = v, "Your own digital PC store (Management > Your Digital Store) from 2003, with rival stores opening over the years. Its effects only apply while this is on.");
		w.Toggle("Rival stores take a cut", R, y + 2f, W - R, Plugin.StoreRivalFees.Value, (bool v) => Plugin.StoreRivalFees.Value = v, "Rival digital stores take their cut (10-30%) of your games' digital PC sales made through them, as in real life. Your own store and store exclusives don't pay it.");
		y += 36f;
		w.Toggle("Timed exclusives", L, y + 2f, LW, Plugin.TimedExclusives.Value, (bool v) => Plugin.TimedExclusives.Value = v, "The new-game screen offers timed exclusives for multiplatform games: released on one platform first (its maker pays a deal), on the others after 3 to 24 months.");
		w.Toggle("Faster new-game screen", R, y + 2f, W - R, Plugin.FasterNewGameScreen.Value, (bool v) => Plugin.FasterNewGameScreen.Value = v, "The new-game screen's gameplay feature list and cost sums skip a lot of repeated work, so the screen opens, searches and reacts much faster in big saves. Same results as the game's own. Idea from Aerin the Lion's Unofficial MGT2 Performance Patch.");
		y += 36f;
		w.Toggle("Sales Events (replace Holy's)", L, y + 2f, LW, Plugin.MergedSales.Value, (bool v) =>
		{
			Plugin.MergedSales.Value = v;
			Core.Updates.GameUpdates.Touch();
		}, "One system for seasonal sales: the Summer and Winter Sales (plus your store's Spring and Autumn Sales once you own one), with a pop-up as each starts where you set your games' discount and your store's sale. Replaces The Holy Expansion's Seasonal Sales, with the same discounts and numbers. Off = Holy's Seasonal Sales as before.");
		w.Toggle("Sales start in 2003", R, y + 2f, W - R, Plugin.HolySeasonalFrom2003.Value, (bool v) => Plugin.HolySeasonalFrom2003.Value = v, "The Summer and Winter Sales start in 2003, when digital stores arrive, instead of 1998 (The Holy Expansion's start).");
		y += 36f;
		w.Toggle("Turbo and Ultra speed buttons", L, y + 2f, LW, Plugin.ExtraSpeedButtons.Value, (bool v) => Plugin.ExtraSpeedButtons.Value = v, "Extra speed buttons next to pause / play / fast, bottom right. Keys " + KeyName(Plugin.TurboKey.Value) + " and " + KeyName(Plugin.UltraKey.Value) + ".");
		w.Toggle("Automatic seasons need a room", R, y + 2f, W - R, Plugin.LiveServiceInRoom.Value, (bool v) => Plugin.LiveServiceInRoom.Value = v, "The Holy Expansion's \"Start the next season automatically\" prepares each live-service season in a free Marketing room (250 work, like a season you start yourself) instead of starting it instantly. It waits for a free room if all are busy.");
		y += 36f;
		w.Toggle("Industry events", L, y + 2f, LW, Plugin.IndustryEvents.Value, (bool v) =>
		{
			Plugin.IndustryEvents.Value = v;
			Core.Updates.GameUpdates.Touch();
		}, "News headlines that move the market, and decisions (pop-ups) about other studios, deals and your games, from 1976 on. Plus the Industry window on the bottom bar: every studio and where you stand with it.");
		w.Toggle("Rivals (replace Holy's)", R, y + 2f, W - R, Plugin.MergedRival.Value, (bool v) =>
		{
			Plugin.MergedRival.Value = v;
			Core.Updates.GameUpdates.Touch();
		}, "Rivals come from your standing with studios. The one you get on worst with is your arch-rival: it makes moves against you every few months (poaching your best people, copycats, release clashes, trash talk), and your moves against any rival are on its page in the Industry window. Replaces The Holy Expansion's Rival Studio, whose rival carries over. Needs industry events. Off = Holy's Rival Studio as before.");
		y += 36f;
		Row(w, ref y, "Industry decisions come up", new List<string> { "Calm: now and then", "Normal: every 2-3 months on average", "Busy: often" }, Mathf.Clamp(Plugin.IndustryPace.Value, 0, 2), (int v) => Plugin.IndustryPace.Value = Mathf.Clamp(v, 0, 2), "How often industry decisions (pop-ups) come up, at random.");
		w.Toggle("Show upcoming landmarks", L, y + 2f, LW, Plugin.LandmarksShowUpcoming.Value, (bool v) => Plugin.LandmarksShowUpcoming.Value = v, "The Landmarks tab in the Industry window also lists the landmark games nobody has made yet, with what it takes to make them. Off: only the ones already made, so you discover the rest.");
		w.Toggle("Design Assistant", R, y + 2f, W - R, Plugin.DesignAssistant.Value, (bool v) => Plugin.DesignAssistant.Value = v, "On the game design screen: fit colours on genres, topics, age groups and design sliders; the Assist button (first page, right of the licence) that fills in the parts you choose with the cog left of the licence; a Platforms button on the platform page; and the game's Auto Design Settings also sets the work priorities. GameDev Assistant's buttons are hidden while it's on.");
		y += 36f;
		w.Toggle("Reveal Every Fit", L, y + 2f, LW, Plugin.DesignRevealAll.Value, (bool v) => Plugin.DesignRevealAll.Value = v, "The design assistant knows every fit and the right design settings, as the game's sandbox options do: topics, age groups, sub-genres, design sliders and work priorities. Off: only what your studio has learned from its games' review reports, as the game does.");
		w.Toggle("Colour-Blind Colours", R, y + 2f, W - R, Plugin.DesignColourBlind.Value, (bool v) => Plugin.DesignColourBlind.Value = v, "The design assistant's colours, and the game's own fit colours on topics, age groups and design sliders, in blue for a good fit and orange for a bad one instead of green and red.");
		y += 40f;
		SpeedSlider(w, ref y, "Turbo speed", Plugin.TurboSpeed, 4f);
		SpeedSlider(w, ref y, "Ultra speed", Plugin.UltraSpeed, 6f);
		w.Label("Very high speeds can make employees jump around while walking.", 10f, y, W - 20f, 26f, TextAnchor.MiddleLeft, bold: false, null, 13);
		EditorWindow.ScrollHeight(area, y + 34f);
		w.Into = null;
	}

	// Inside the scrolling area: its width less the scroll bar's side; toggles in two columns.
	private static float Inner(EditorWindow w)
	{
		return w.Width - 26f - 38f;
	}

	private const float L = 8f;

	private const float LW = 262f;

	private const float R = 276f;

	private static void Row(EditorWindow w, ref float y, string label, List<string> options, int value, Action<int> onChange, string tooltip)
	{
		Text text = w.Label(label, 10f, y, 250f, 30f);
		UIKit.SetTooltip(text.gameObject, tooltip);
		w.Dropdown(270f, y, Inner(w) - 270f, options, value, (int v) =>
		{
			UIKit.Sound();
			onChange(v);
		});
		y += 40f;
	}

	private static void SpeedSlider(EditorWindow w, ref float y, string label, BepInEx.Configuration.ConfigEntry<float> entry, float min)
	{
		w.Label(label, 10f, y, 250f, 30f);
		Slider slider = w.Slider(270f, y, Inner(w) - 270f, min, 30f, entry.Value, (float v) => entry.Value = Mathf.Round(v), (float v) => Mathf.Round(v) + "x");
		slider.wholeNumbers = true;
		y += 38f;
	}

	private static List<string> Texts(params (int id, string fallback)[] items)
	{
		List<string> list = new List<string>();
		foreach ((int id, string fallback) in items)
		{
			list.Add(G.Text(id, fallback));
		}
		return list;
	}

	private static int SpeedIndex(mainScript ms)
	{
		int best = 2;
		float bestDiff = float.MaxValue;
		for (int i = 0; i < ms.gameSpeeds.Length; i++)
		{
			float diff = Mathf.Abs(ms.gameSpeeds[i] - ms.speedSetting);
			if (diff < bestDiff)
			{
				bestDiff = diff;
				best = i;
			}
		}
		return best;
	}

	private static string KeyName(KeyCode key)
	{
		string name = key.ToString();
		if (name.StartsWith("Alpha"))
		{
			return name.Substring(5);
		}
		return name;
	}
}
