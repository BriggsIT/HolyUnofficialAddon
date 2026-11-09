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
			UpdateKind.StoreBuild => "store-build.png",
			UpdateKind.StoreFeature => "store-feature.png",
			_ => "update-port.png",
		};
		return File(file) ?? UIKit.Sprite("iconUpdate") ?? UIKit.Sprite("iconOptions");
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

	private static Sprite File(string file)
	{
		return UIKit.LoadSprite(Path.Combine(Path.Combine(Plugin.PluginDir, "icons"), file));
	}
}
