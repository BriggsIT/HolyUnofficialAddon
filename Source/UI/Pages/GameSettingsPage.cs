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

	public override float Width => 660f;

	public override float Height => 968f;

	public override void Build(EditorWindow w)
	{
		mainScript ms = G.MS;
		BackButton(w);
		w.Label("Changes apply straight away.", 116f, 10f, w.Width - 130f, 28f);
		float y = 50f;
		w.Header("This game (saved with your save file)", 14f, y, w.Width - 28f);
		y += 32f;
		Row(w, ref y, G.Text(798, "Level of difficulty"), Texts((802, "Very easy"), (803, "Easy"), (804, "Medium"), (805, "Hard"), (1685, "Very hard"), (806, "Legendary")), ms.difficulty, (int v) => ms.difficulty = v, "Affects sales, reviews, costs and rival studios from now on. Note: the Legendary achievements check this setting.");
		Row(w, ref y, G.Text(800, "Game speed"), Texts((1335, "Marathon"), (807, "Slow"), (808, "Normal"), (809, "Fast"), (810, "Very Fast")), SpeedIndex(ms), (int v) => ms.speedSetting = ms.gameSpeeds[Mathf.Clamp(v, 0, ms.gameSpeeds.Length - 1)], "How long a week lasts. Slower settings give you more real time per week. With \"Adjust working speed\" on, your staff speed up or slow down to match.");
		Row(w, ref y, G.Text(2072, "Development time"), Texts((2073, "Standard"), (2074, "Realistic"), (2269, "Short development time")), ms.devTimeSetting, (int v) => ms.devTimeSetting = v, "Realistic: big games take much longer to make. Short: all game development runs twice as fast. Also applies to games already in development.");
		Row(w, ref y, G.Text(2266, "Random events"), Texts((2073, "Standard"), (1001, "Historical Random Events"), (837, "No random events")), ms.settings_randomEvents, (int v) => ms.settings_randomEvents = v, "Which world events can happen from now on.");
		Row(w, ref y, G.Text(2397, "Competition"), Texts((2398, "Low"), (2399, "Normal"), (2400, "Tough")), ms.settings_competition, (int v) => ms.settings_competition = v, "How strong rival studios' games are.");
		w.Toggle(G.Text(1363, "Adjust working speed"), 24f, y + 2f, w.Width - 48f, ms.settings_arbeitsgeschwindigkeitAnpassen, (bool v) => ms.settings_arbeitsgeschwindigkeitAnpassen = v, "When on, employees work faster or slower to match the game speed setting, so a slower game doesn't make development feel slower.");
		y += 44f;
		w.Header("Addon options (all saves)", 14f, y, w.Width - 28f);
		y += 32f;
		Text leniency = w.Label("Furniture collision leniency", 24f, y, 250f, 30f);
		UIKit.SetTooltip(leniency.gameObject, "Collision boxes are often bigger than the furniture you see, so an object that looks clear of a wall still turns red. This lets a box push that far (in floor tiles) into walls and other furniture before it counts as blocked. Off = the game's rules.");
		w.Slider(280f, y, w.Width - 300f, 0f, 0.5f, Plugin.CollisionLeniency.Value, (float v) => Plugin.CollisionLeniency.Value = Mathf.Round(v * 20f) / 20f, (float v) => (Mathf.Round(v * 20f) < 1f) ? "Off" : ((Mathf.Round(v * 20f) / 20f).ToString("0.00") + " tile"));
		y += 38f;
		w.Toggle("Furniture can overlap furniture", 24f, y + 2f, w.Width - 48f, Plugin.FurnitureOverlap.Value, (bool v) => Plugin.FurnitureOverlap.Value = v, "Furniture may overlap other furniture as much as you like. Walls and doors still follow the leniency above, so employees can always get through.");
		y += 34f;
		w.Toggle("Ignore room rules", 24f, y + 2f, w.Width - 48f, Plugin.IgnoreRoomRules.Value, (bool v) => Plugin.IgnoreRoomRules.Value = v, "Any object in any room or hallway (a toilet in the dev room, a desk in the lobby...).");
		y += 36f;
		w.Toggle("Keep edited skills", 24f, y + 2f, w.Width - 48f, Plugin.KeepEditedSkills.Value, (bool v) => Plugin.KeepEditedSkills.Value = v, "The game pushes skills outside an employee's profession back down to 50 (60 with the right perk) as soon as they use them. When on, a skill you raised in the employee editor stays put.");
		y += 36f;
		w.Toggle("Press reviews", 24f, y + 2f, w.Width - 48f, Plugin.PressReviews.Value, (bool v) => Plugin.PressReviews.Value = v, "Reviews come from named outlets with logos and reviewers, with text that knows the game's genre, topic and platform, plus a strip of every outlet's score. Purely cosmetic: the game's scores don't change. English game language only.");
		y += 36f;
		w.Toggle("Contracts button", 24f, y + 2f, 280f, Plugin.ContractsEnabled.Value, (bool v) => Plugin.ContractsEnabled.Value = v, "Show the Contracts button on the bottom bar, to hire outside studios for research or work on your games. Contracts already running carry on either way.");
		w.Toggle("Contract cards on screen", 316f, y + 2f, w.Width - 340f, Plugin.ContractTracker.Value, (bool v) => Plugin.ContractTracker.Value = v, "Cards like the game's own game cards, one per running contract, with its progress. Drag a card to move them.");
		y += 36f;
		w.Toggle("Own copy protection", 24f, y + 2f, 280f, Plugin.CopyProtection.Value, (bool v) => Plugin.CopyProtection.Value = v, "Research \"Copy protection\" under Other research, then build your own in a development room, update it and license it to other studios (Management > Your copy protection). Copy protections you already made keep working when this is off.");
		w.Toggle("More update types", 316f, y + 2f, w.Width - 340f, Plugin.ExtraUpdates.Value, (bool v) => Plugin.ExtraUpdates.Value = v, "Updates & Addons gets ports to more platforms, engine updates, new gameplay features and age rating edits for games you have on the market. Updates already done keep their effect only while this is on.");
		y += 36f;
		w.Toggle("MMO Management", 24f, y + 2f, 280f, Plugin.MmoManagement.Value, (bool v) => Plugin.MmoManagement.Value = v, "MMO Management in the development room menu: graphics overhauls, 64-bit clients, engine migrations, world expansions and content patches for your MMOs. Their effects on players only apply while this is on.");
		w.Toggle("Dated MMOs lose players", 316f, y + 2f, w.Width - 340f, Plugin.MmoDatedPenalty.Value, (bool v) => Plugin.MmoDatedPenalty.Value = v, "Modern MMOs whose graphics and tech fall well behind today's games lose players faster and draw fewer new ones. Old-school MMOs never do.");
		y += 36f;
		w.Toggle("Your digital store", 24f, y + 2f, 280f, Plugin.StoreEnabled.Value, (bool v) => Plugin.StoreEnabled.Value = v, "Your own digital PC store (Management > Your digital store) from 2003, with rival stores opening over the years. Its effects only apply while this is on.");
		w.Toggle("Rival stores take a cut", 316f, y + 2f, w.Width - 340f, Plugin.StoreRivalFees.Value, (bool v) => Plugin.StoreRivalFees.Value = v, "Rival digital stores take their cut (10-30%) of your games' digital PC sales made through them, as in real life. Your own store and store exclusives don't pay it.");
		y += 36f;
		w.Toggle("Timed exclusives", 24f, y + 2f, 280f, Plugin.TimedExclusives.Value, (bool v) => Plugin.TimedExclusives.Value = v, "The new-game screen offers timed exclusives for multiplatform games: released on one platform first (its maker pays a deal), on the others after 3 to 24 months.");
		w.Toggle("Holy's seasonal sales from 2003", 316f, y + 2f, w.Width - 340f, Plugin.HolySeasonalFrom2003.Value, (bool v) => Plugin.HolySeasonalFrom2003.Value = v, "The Holy Expansion's Summer and Winter Sales start in 2003, when digital stores arrive, instead of 1998.");
		y += 36f;
		w.Toggle("Turbo and Ultra speed buttons", 24f, y + 2f, w.Width - 48f, Plugin.ExtraSpeedButtons.Value, (bool v) => Plugin.ExtraSpeedButtons.Value = v, "Extra speed buttons next to pause / play / fast, bottom right. Keys " + KeyName(Plugin.TurboKey.Value) + " and " + KeyName(Plugin.UltraKey.Value) + ".");
		y += 40f;
		SpeedSlider(w, ref y, "Turbo speed", Plugin.TurboSpeed, 4f);
		SpeedSlider(w, ref y, "Ultra speed", Plugin.UltraSpeed, 6f);
		w.Label("Very high speeds can make employees jump around while walking.", 24f, y, w.Width - 48f, 26f, TextAnchor.MiddleLeft, bold: false, null, 13);
	}

	private static void Row(EditorWindow w, ref float y, string label, List<string> options, int value, Action<int> onChange, string tooltip)
	{
		Text text = w.Label(label, 24f, y, 250f, 30f);
		UIKit.SetTooltip(text.gameObject, tooltip);
		w.Dropdown(280f, y, w.Width - 300f, options, value, (int v) =>
		{
			UIKit.Sound();
			onChange(v);
		});
		y += 40f;
	}

	private static void SpeedSlider(EditorWindow w, ref float y, string label, BepInEx.Configuration.ConfigEntry<float> entry, float min)
	{
		w.Label(label, 24f, y, 250f, 30f);
		Slider slider = w.Slider(280f, y, w.Width - 300f, min, 30f, entry.Value, (float v) => entry.Value = Mathf.Round(v), (float v) => Mathf.Round(v) + "x");
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
