using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using HolyAddon.UI;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;

namespace HolyAddon.Core.Logos;

// The addon's own company logos (logos/logos.json and its PNGs, drawn by Assets/logos/logos.html).
// The game numbers logos by their place in GUI_Main.logoSprites, and NPC studios only ever get the numbers in their
// data files, so no rival can end up with one of these. They sit at fixed numbers from 1000 (the slots between the last
// game/Holy logo and 1000 stay empty and the logo pickers skip them), so a save keeps the right logo even when Holy
// adds logos of its own. Picking one also fills in the company name that goes with it.
[HarmonyPatch]
internal static class CompanyLogos
{
	public const int Base = 1000;

	private class Entry
	{
		public string File { get; set; }

		public string Name { get; set; }
	}

	private class LogoList
	{
		public List<Entry> Logos { get; set; }
	}

	private static readonly AccessTools.FieldRef<loadResources, GUI_Main> LoaderGui = AccessTools.FieldRefAccess<loadResources, GUI_Main>("guiMain_");

	private static readonly List<string> names = new List<string>();

	private static int first = Base;

	public static bool Ours(int id)
	{
		return id >= first && id < first + names.Count;
	}

	public static string NameOf(int id)
	{
		return Ours(id) ? names[id - first] : null;
	}

	// After the game (and Holy, which adds its logos in its own postfix) has filled logoSprites.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(loadResources), "LoadLogos")]
	[HarmonyAfter("holyexpansion.mgt2")]
	[HarmonyPriority(Priority.Last)]
	private static void Load(loadResources __instance)
	{
		names.Clear();
		try
		{
			GUI_Main gui = LoaderGui(__instance) ?? G.Gui;
			string dir = Path.Combine(Plugin.PluginDir, "logos");
			string file = Path.Combine(dir, "logos.json");
			if (!gui || gui.logoSprites == null || !File.Exists(file))
			{
				return;
			}
			List<Entry> list = JsonConvert.DeserializeObject<LogoList>(File.ReadAllText(file))?.Logos;
			if (list == null || list.Count == 0)
			{
				return;
			}
			Sprite[] old = gui.logoSprites;
			first = Math.Max(Base, old.Length);
			if (first != Base)
			{
				Plugin.Log.LogWarning("There are already " + old.Length + " company logos, so the addon's logos start at " + first + " instead of " + Base + ": saves that use them may show other logos.");
			}
			Sprite[] sprites = new Sprite[first + list.Count];
			Array.Copy(old, sprites, old.Length);
			for (int i = 0; i < list.Count; i++)
			{
				string png = Path.Combine(dir, list[i].File ?? "");
				sprites[first + i] = LoadSprite(png);
				names.Add(string.IsNullOrEmpty(list[i].Name) ? Path.GetFileNameWithoutExtension(png) : list[i].Name);
			}
			gui.logoSprites = sprites;
			Sprite sample = sprites[first];
			Plugin.Log.LogInfo("Added " + list.Count + " company logos (" + first + "-" + (first + list.Count - 1) + ")" + (((bool)sample) ? (", " + sample.texture.width + "px with " + sample.texture.mipmapCount + " mip levels") : ""));
		}
		catch (Exception ex)
		{
			names.Clear();
			Plugin.Log.LogError("Company logos failed: " + ex);
		}
	}

	// The logos are 256x256 and the game shows them at roughly 40-130 pixels. mainScript.LoadPNG makes textures without
	// mipmaps, which turns small text jagged, so these get mipmaps and trilinear filtering.
	private static Sprite LoadSprite(string path)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: true);
		if (!texture.LoadImage(File.ReadAllBytes(path)))
		{
			return null;
		}
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.filterMode = FilterMode.Trilinear;
		texture.anisoLevel = 2;
		texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
		return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.zero);
	}

	// Logo picker for your company (new game, sandbox, renaming mid-game): the addon's free logos come first, in order.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_Firmenlogo), "Init")]
	private static void SortPicker(Menu_Firmenlogo __instance)
	{
		if (names.Count == 0 || !G.MS)
		{
			return;
		}
		Transform content = __instance.uiObjects[0].transform;
		foreach (Transform child in content)
		{
			Item_Firmenlogo item = child.GetComponent<Item_Firmenlogo>();
			// The game names free logos "-(100000 + id)" and sorts ascending; used ones stay where the game puts them.
			if ((bool)item && Ours(item.myID) && child.name == "-" + (100000 + item.myID))
			{
				child.name = "-" + (900000 - (item.myID - first));
			}
		}
		G.MS.SortChildrenByFloat(__instance.uiObjects[0]);
	}

	// Logo picker for a subsidiary: the game sorts used ("A") before free ("B"); the addon's free logos go first.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Menu_TochterfirmaLogo), "Init")]
	private static void SortSubsidiaryPicker(Menu_TochterfirmaLogo __instance)
	{
		if (names.Count == 0 || !G.MS)
		{
			return;
		}
		foreach (Transform child in __instance.uiObjects[0].transform)
		{
			Item_FirmenlogoTochterfirma item = child.GetComponent<Item_FirmenlogoTochterfirma>();
			if ((bool)item && Ours(item.myID) && child.name == "B")
			{
				child.name = "0" + (item.myID - first).ToString("D4");
			}
		}
		G.MS.SortChildrenByName(__instance.uiObjects[0]);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Item_Firmenlogo), "SetData")]
	private static void Tooltip(Item_Firmenlogo __instance)
	{
		AddTooltip(__instance.gameObject, __instance.myID);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Item_FirmenlogoTochterfirma), "SetData")]
	private static void SubsidiaryTooltip(Item_FirmenlogoTochterfirma __instance)
	{
		AddTooltip(__instance.gameObject, __instance.myID);
	}

	private static void AddTooltip(GameObject go, int id)
	{
		string name = NameOf(id);
		if (name != null)
		{
			UIKit.SetTooltip(go, "<b>" + name + "</b>");
		}
	}

	// Picking one of the addon's logos puts its company name in the name field of the menu it was picked for.
	[HarmonyPostfix]
	[HarmonyPatch(typeof(Item_Firmenlogo), "BUTTON_Click")]
	private static void Picked(Item_Firmenlogo __instance)
	{
		string name = NameOf(__instance.myID);
		GUI_Main gui = __instance.guiMain_;
		if (name == null || !gui)
		{
			return;
		}
		// Same order as the game's own checks; the multiplayer lobby (201) keeps its name.
		GameObject menu = null;
		if (gui.uiObjects[47].activeSelf)
		{
			menu = gui.uiObjects[47].GetComponent<Menu_Firmenname>().uiObjects[0];
		}
		else if (gui.uiObjects[159].activeSelf)
		{
			menu = gui.uiObjects[159].GetComponent<Menu_NewGame>().uiObjects[0];
		}
		else if (gui.uiObjects[425].activeSelf)
		{
			menu = gui.uiObjects[425].GetComponent<Menu_NewGame_Sandbox>().uiObjects[0];
		}
		SetName(menu, name);
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Item_FirmenlogoTochterfirma), "BUTTON_Click")]
	private static void SubsidiaryPicked(Item_FirmenlogoTochterfirma __instance)
	{
		string name = NameOf(__instance.myID);
		GUI_Main gui = __instance.guiMain_;
		if (name != null && (bool)gui && gui.uiObjects[391].activeSelf)
		{
			SetName(gui.uiObjects[391].GetComponent<Menu_TochterfirmaRename>().uiObjects[0], name);
		}
	}

	private static void SetName(GameObject field, string name)
	{
		InputField input = ((bool)field) ? field.GetComponent<InputField>() : null;
		if ((bool)input)
		{
			input.text = name;
		}
	}
}
