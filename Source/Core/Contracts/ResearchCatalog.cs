using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HolyAddon.Core.Contracts;

internal class ResearchItem
{
	public int Type;

	public int Slot;

	public string Name;

	public long InHousePrice;

	public float PointsLeft;

	public Sprite Icon;
}

// Everything a research room could work on right now, per category, and how to finish an item the way the game does.
// The category numbers match taskForschung.typ: 0 genres, 1 topics, 2 engine features, 3 gameplay features,
// 4 hardware, 5 other, 6 hardware features. An item is researched once its RES_POINTS_LEFT reaches 0.
internal static class ResearchCatalog
{
	public static readonly int[] Order = new int[7] { 1, 0, 3, 2, 4, 6, 5 };

	public static string CategoryName(int type)
	{
		return type switch
		{
			0 => "Genres",
			1 => "Topics",
			2 => "Engine features",
			3 => "Gameplay features",
			4 => "Hardware",
			5 => "Other research",
			6 => "Hardware features",
			_ => "Research",
		};
	}

	private static T Get<T>() where T : Component
	{
		if (!G.MainObj)
		{
			return null;
		}
		return G.MainObj.GetComponent<T>();
	}

	public static List<ResearchItem> Items(int type)
	{
		List<ResearchItem> list = new List<ResearchItem>();
		forschungSonstiges other = Get<forschungSonstiges>();
		switch (type)
		{
		case 0:
		{
			genres g = Get<genres>();
			if ((bool)g)
			{
				for (int i = 0; i < g.genres_RES_POINTS_LEFT.Length; i++)
				{
					if (g.genres_UNLOCK[i] && !g.IsErforscht(i) && !g.BereitsInAnderenRaumAktiv(i))
					{
						list.Add(Item(type, i, g.GetName(i), g.GetPrice(i), g.genres_RES_POINTS_LEFT[i], g.GetPic(i)));
					}
				}
			}
			break;
		}
		case 1:
		{
			themes t = Get<themes>();
			if ((bool)t && (bool)G.TS)
			{
				for (int i = 0; i < t.themes_RES_POINTS_LEFT.Length; i++)
				{
					if (!t.IsErforscht(i) && !t.BereitsInAnderenRaumAktiv(i))
					{
						list.Add(Item(type, i, Clean(G.TS.GetThemes(i)), t.GetPrice(i), t.themes_RES_POINTS_LEFT[i], t.icon));
					}
				}
			}
			break;
		}
		case 2:
		{
			engineFeatures e = Get<engineFeatures>();
			if ((bool)e)
			{
				for (int i = 0; i < e.engineFeatures_RES_POINTS_LEFT.Length; i++)
				{
					if (e.engineFeatures_UNLOCK[i] && !e.IsErforscht(i) && !e.BereitsInAnderenRaumAktiv(i))
					{
						list.Add(Item(type, i, e.GetName(i), e.GetPrice(i), e.engineFeatures_RES_POINTS_LEFT[i], e.GetTypPic(i)));
					}
				}
			}
			break;
		}
		case 3:
		{
			gameplayFeatures f = Get<gameplayFeatures>();
			if ((bool)f)
			{
				for (int i = 0; i < f.gameplayFeatures_RES_POINTS_LEFT.Length; i++)
				{
					if (f.gameplayFeatures_UNLOCK[i] && !f.IsErforscht(i) && !f.BereitsInAnderenRaumAktiv(i))
					{
						list.Add(Item(type, i, f.GetName(i), f.GetPrice(i), f.gameplayFeatures_RES_POINTS_LEFT[i], f.GetTypSprite(i)));
					}
				}
			}
			break;
		}
		case 4:
		{
			hardware h = Get<hardware>();
			if ((bool)h && (bool)other && other.IsErforscht(39))
			{
				for (int i = 0; i < h.hardware_RES_POINTS_LEFT.Length; i++)
				{
					if (h.hardware_UNLOCK[i] && !h.IsErforscht(i) && !h.BereitsInAnderenRaumAktiv(i))
					{
						list.Add(Item(type, i, h.GetName(i), h.GetPrice(i), h.hardware_RES_POINTS_LEFT[i], h.GetTypPic(i)));
					}
				}
			}
			break;
		}
		case 6:
		{
			hardwareFeatures h = Get<hardwareFeatures>();
			if ((bool)h && (bool)other && other.IsErforscht(39))
			{
				for (int i = 0; i < h.hardFeat_RES_POINTS_LEFT.Length; i++)
				{
					if (h.hardFeat_UNLOCK[i] && !h.IsErforscht(i) && !h.BereitsInAnderenRaumAktiv(i))
					{
						list.Add(Item(type, i, h.GetName(i), h.GetPrice(i), h.hardFeat_RES_POINTS_LEFT[i], h.GetSprite(i)));
					}
				}
			}
			break;
		}
		case 5:
			if ((bool)other)
			{
				// Game sizes unlock in order (0 > 1 > 2 > 3 > 40); the rest are always on offer, as in the research room.
				int[] sizes = new int[5] { 0, 1, 2, 3, 40 };
				foreach (int s in sizes)
				{
					if (!other.IsErforscht(s))
					{
						AddOther(list, other, s);
						break;
					}
				}
				foreach (int s in new int[11] { 35, 36, 28, 29, 30, 31, 32, 33, 34, 38, 39 })
				{
					AddOther(list, other, s);
				}
				// Copy protection: the Editor's own research (unused by the game), offered once it exists.
				if (CopyProtection.CopyProtectDev.ResearchOffered)
				{
					AddOther(list, other, CopyProtection.CopyProtectDev.ResearchSlot);
				}
			}
			break;
		}
		// Items already handed to a studio aren't offered twice.
		HashSet<int> taken = new HashSet<int>(from c in ContractBook.Active
			where c.IsResearch
			from j in c.Research
			where j.Type == type && !j.Done
			select j.Slot);
		return list.Where((ResearchItem r) => !taken.Contains(r.Slot)).OrderBy((ResearchItem r) => r.Name).ToList();
	}

	// Genres you can pick to narrow the topic list: the ones available in the game so far.
	public static List<(int id, string name)> Genres()
	{
		List<(int, string)> list = new List<(int, string)>();
		genres g = Get<genres>();
		if (!g)
		{
			return list;
		}
		for (int i = 0; i < g.genres_UNLOCK.Length; i++)
		{
			if (g.genres_UNLOCK[i])
			{
				list.Add((i, Clean(g.GetName(i))));
			}
		}
		return list.OrderBy(((int, string) x) => x.Item2).ToList();
	}

	// Whether a topic suits a genre, as the game judges it when you design a game.
	public static bool TopicFits(int topic, int genre)
	{
		themes t = Get<themes>();
		if (!t)
		{
			return true;
		}
		return t.IsThemesFitWithGenre(topic, genre);
	}

	private static void AddOther(List<ResearchItem> list, forschungSonstiges other, int slot)
	{
		if (slot < other.RES_POINTS_LEFT.Length && !other.IsErforscht(slot) && !other.BereitsInAnderenRaumAktiv(slot))
		{
			list.Add(Item(5, slot, other.GetName(slot), other.GetPrice(slot), other.RES_POINTS_LEFT[slot], other.GetPic(slot)));
		}
	}

	private static ResearchItem Item(int type, int slot, string name, long price, float left, Sprite icon)
	{
		return new ResearchItem
		{
			Type = type,
			Slot = slot,
			Name = Clean(name),
			InHousePrice = price,
			PointsLeft = left,
			Icon = icon
		};
	}

	private static string Clean(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "?";
		}
		return Regex.Replace(text, "<[^>]*>", "").Trim();
	}

	private static float[] Left(int type)
	{
		switch (type)
		{
		case 0:
			return Get<genres>()?.genres_RES_POINTS_LEFT;
		case 1:
			return Get<themes>()?.themes_RES_POINTS_LEFT;
		case 2:
			return Get<engineFeatures>()?.engineFeatures_RES_POINTS_LEFT;
		case 3:
			return Get<gameplayFeatures>()?.gameplayFeatures_RES_POINTS_LEFT;
		case 4:
			return Get<hardware>()?.hardware_RES_POINTS_LEFT;
		case 5:
			return Get<forschungSonstiges>()?.RES_POINTS_LEFT;
		case 6:
			return Get<hardwareFeatures>()?.hardFeat_RES_POINTS_LEFT;
		default:
			return null;
		}
	}

	public static Sprite Icon(int type, int slot)
	{
		try
		{
			switch (type)
			{
			case 0:
				return Get<genres>()?.GetPic(slot);
			case 1:
				return Get<themes>()?.icon;
			case 2:
				return Get<engineFeatures>()?.GetTypPic(slot);
			case 3:
				return Get<gameplayFeatures>()?.GetTypSprite(slot);
			case 4:
				return Get<hardware>()?.GetTypPic(slot);
			case 5:
				return Get<forschungSonstiges>()?.GetPic(slot);
			case 6:
				return Get<hardwareFeatures>()?.GetSprite(slot);
			}
		}
		catch (System.Exception)
		{
		}
		return null;
	}

	public static bool IsDone(int type, int slot)
	{
		float[] left = Left(type);
		if (left == null || slot < 0 || slot >= left.Length)
		{
			return true;
		}
		return left[slot] <= 0f;
	}

	// Finish an item like the game does: through the research room's own task if one is on it (news, unlocks,
	// auto-research all follow), otherwise by zeroing its points and announcing it ourselves.
	public static void Complete(int type, int slot)
	{
		float[] left = Left(type);
		if (left == null || slot < 0 || slot >= left.Length || left[slot] <= 0f)
		{
			return;
		}
		roomScript[] rooms = ((bool)G.MS) ? G.MS.arrayRoomScripts : null;
		if (rooms != null)
		{
			foreach (roomScript room in rooms)
			{
				if ((bool)room && room.typ == 2)
				{
					taskForschung task = room.GetTaskForschung();
					if ((bool)task && task.typ == type && task.slot == slot)
					{
						task.Work(left[slot] + 1f);
						return;
					}
				}
			}
		}
		left[slot] = 0f;
		unlockScript unlock = Get<unlockScript>();
		if ((bool)unlock)
		{
			unlock.CheckUnlock(showMessage: true);
		}
	}
}
