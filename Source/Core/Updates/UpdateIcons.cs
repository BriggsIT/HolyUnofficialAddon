using System.IO;
using HolyAddon.UI;
using UnityEngine;

namespace HolyAddon.Core.Updates;

// The icons of the addon's update types (icons/update-*.png and icons/mmo-*.png next to the DLL, drawn by
// Assets/icons/update-icons.html), with a game sprite as a fallback.
internal static class UpdateIcons
{
	public static Sprite For(UpdateKind kind)
	{
		string file = kind switch
		{
			UpdateKind.Port => "update-port.png",
			UpdateKind.Engine => "update-engine.png",
			UpdateKind.Features => "update-features.png",
			UpdateKind.Rating => "update-rating.png",
			UpdateKind.MmoGraphics => "mmo-graphics.png",
			UpdateKind.MmoClient64 => "mmo-client64.png",
			UpdateKind.MmoMigration => "mmo-migration.png",
			UpdateKind.MmoExpansion => "mmo-expansion.png",
			UpdateKind.MmoPatch => "mmo-patch.png",
			UpdateKind.MmoPort => "update-port.png",
			UpdateKind.MmoFeatures => "update-features.png",
			UpdateKind.MmoRating => "update-rating.png",
			UpdateKind.MmoBotBust => "mmo-botbust.png",
			UpdateKind.MmoWelcomeBack => "mmo-welcome.png",
			UpdateKind.MmoClassic => "mmo-classic.png",
			UpdateKind.MmoFreeTrial => "mmo-trial.png",
			UpdateKind.MmoStability => "mmo-stability.png",
			UpdateKind.MmoClassicPhase => "mmo-classic.png",
			UpdateKind.StoreBuild => "store-build.png",
			UpdateKind.StoreFeature => "store-feature.png",
			UpdateKind.StoreConsole => "store-console.png",
			UpdateKind.AntiBotBuild => "antibot-build.png",
			UpdateKind.AntiBotUpdate => "antibot-update.png",
			_ => "update-port.png",
		};
		return File(file) ?? UIKit.Sprite("iconUpdate") ?? UIKit.Sprite("iconOptions");
	}

	// Anti-bot systems (the window and a system's picture).
	public static Sprite AntiBot()
	{
		return File("antibot.png") ?? For(UpdateKind.AntiBotBuild);
	}

	public static Sprite Autopilot()
	{
		return File("mmo-autopilot.png") ?? Mmo();
	}

	public static Sprite FanFest()
	{
		return File("mmo-fanfest.png") ?? Mmo();
	}

	// The MMO management window's own icon.
	public static Sprite Mmo()
	{
		return File("mmo-window.png") ?? UIKit.Sprite("iconGames") ?? UIKit.Sprite("iconOptions");
	}

	// The game's MMO addon (expansion pack) shortcut.
	public static Sprite ExpansionPack()
	{
		return File("mmo-addon.png") ?? Mmo();
	}

	// The digital store's window icon.
	public static Sprite Store()
	{
		return File("store-window.png") ?? UIKit.Sprite("iconGames") ?? UIKit.Sprite("iconOptions");
	}

	public static Sprite StoreFeature(int id)
	{
		return File("store-f" + id + ".png") ?? File("store-feature.png") ?? Store();
	}

	// Store logos: yours (store/logo-<n>.png, never given to anyone else) and the rival stores' (store/rival-<id>.png).
	public static Sprite StoreLogo(int n)
	{
		return UIKit.LoadSprite(Path.Combine(Path.Combine(Plugin.PluginDir, "store"), "logo-" + n + ".png")) ?? Store();
	}

	public static Sprite RivalLogo(string id)
	{
		return UIKit.LoadSprite(Path.Combine(Path.Combine(Plugin.PluginDir, "store"), "rival-" + id + ".png")) ?? Store();
	}

	// MGH Games Awards trophies (icons/award-<category>.png).
	public static Sprite Award(string category)
	{
		return File("award-" + category + ".png") ?? File("award-goty.png") ?? UIKit.Sprite("iconMadGamesAwards");
	}

	// The awards ceremony backdrop (Assets/awards/awards-art.html).
	public static Sprite AwardsBackdrop()
	{
		return File("awards-bg.png");
	}

	// Banner art for the tops of windows (Assets/awards/banners.html): "store", "charts".
	public static Sprite Banner(string id)
	{
		return File("banner-" + id + ".png");
	}

	// One of the game's own icons by name, or ours if the game hasn't loaded it.
	public static Sprite Game(string name, Sprite fallback = null)
	{
		return UIKit.Sprite(name) ?? fallback;
	}

	private static Sprite File(string file)
	{
		return UIKit.LoadSprite(Path.Combine(Path.Combine(Plugin.PluginDir, "icons"), file));
	}
}
