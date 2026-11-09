using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace HolyAddon.Core.Contracts;

// One research item inside a research contract.
internal class ResearchJob
{
	public int Type;

	public int Slot;

	public string Name;

	public float Points;

	public bool Done;

	// Researched by your own studio before the contract got to it; its share is refunded at the end.
	public bool Skipped;
}

// One job handed to an outside studio. Kind is "research", "addons" or one of the game services in GameServices.
internal class Contract
{
	public int Id;

	public string Kind;

	public int FirmId;

	public string FirmName;

	public List<ResearchJob> Research = new List<ResearchJob>();

	// "addons": which studio (GameServices.Studios) and which of its improvements, in delivery order.
	public int Studio = -1;

	public List<int> Addons = new List<int>();

	public List<int> AddonsDone = new List<int>();

	public List<int> AddonsSkipped = new List<int>();

	public int GameId = -1;

	public string Item;

	public long Price;

	public int WeeksTotal;

	public int WeeksDone;

	// The planned effect: research points, quality points, bugs or development work, depending on Kind.
	public float Amount;

	// How much of the effect has been delivered so far.
	public float Applied;

	// Development work that couldn't be applied yet because the current feature was nearly done.
	public float Carry;

	// Hidden: weaker studios tend to deliver a little less than promised, top studios a little more.
	public float Multiplier = 1f;

	public float Quality;

	public bool Late;

	public string Started;

	public string Result;

	// What the quote promised, e.g. "About +7 sound" (shown on the contract's card).
	public string Effect;

	// Its card on screen is minimised.
	public bool Folded;

	// Saves from 0.4.0 held a single research item in these.
	public int ResearchType = -1;

	public int ResearchSlot = -1;

	[JsonIgnore]
	public float Progress => (WeeksTotal <= 0) ? 1f : Mathf.Clamp01((float)WeeksDone / (float)WeeksTotal);

	[JsonIgnore]
	public bool IsResearch => Kind == "research";

	[JsonIgnore]
	public bool IsAddons => Kind == "addons";
}

internal class ContractState
{
	public int NextId = 1;

	public List<Contract> Active = new List<Contract>();

	public List<Contract> History = new List<Contract>();
}

// All contracts of the current save: paid up front, progressed every game week, saved with the game.
[HarmonyPatch]
internal static class ContractBook
{
	private const string SaveKey = "mgt2.editor.contracts";

	private const int HistoryLength = 20;

	public static ContractState State = new ContractState();

	private static readonly System.Random rng = new System.Random();

	public static IEnumerable<Contract> Active => State.Active;

	public static IEnumerable<Contract> History => State.History;

	// Bumped whenever contracts change, so the on-screen tracker knows to redraw.
	public static int Version { get; private set; }

	// The on-screen cards redraw when this changes.
	public static void Touch()
	{
		Version++;
	}

	public static bool FirmBusy(int firmId)
	{
		return State.Active.Any((Contract c) => c.FirmId == firmId);
	}

	public static Contract Add(Contract c)
	{
		c.Id = State.NextId++;
		c.Started = (((bool)G.Gui) ? G.Gui.GetDate() : "");
		c.Multiplier = Mathf.Clamp(0.75f + 0.25f * c.Quality + (float)rng.NextDouble() * 0.2f, 0.7f, 1.25f);
		State.Active.Add(c);
		Version++;
		return c;
	}

	// The player calls it off: half of what hasn't been delivered yet comes back.
	public static void Cancel(Contract c)
	{
		long refund = Remaining(c) / 2;
		Finish(c, "Cancelled" + ((refund > 0) ? (", " + G.Money(refund) + " refunded") : ""), refund, news: false);
	}

	private static long Remaining(Contract c)
	{
		if (c.IsResearch)
		{
			float total = c.Research.Sum((ResearchJob j) => j.Points);
			float open = c.Research.Where((ResearchJob j) => !j.Done || j.Skipped).Sum((ResearchJob j) => j.Points);
			return (total > 0f) ? ((long)((double)c.Price * (double)(open / total))) : 0;
		}
		if (c.IsAddons)
		{
			int count = Mathf.Max(1, c.Addons.Count);
			int open = c.Addons.Count - c.AddonsDone.Count + c.AddonsSkipped.Count;
			return (long)((double)c.Price * (double)open / (double)count);
		}
		float done = ((c.Amount > 0f) ? Mathf.Clamp01(c.Applied / (c.Amount * c.Multiplier)) : 1f);
		return (long)((double)c.Price * (1.0 - (double)done));
	}

	private static void Finish(Contract c, string result, long refund, bool news)
	{
		if (refund > 0 && (bool)G.MS)
		{
			G.MS.Earn(refund, 1);
		}
		c.Result = result;
		State.Active.Remove(c);
		State.History.Insert(0, c);
		if (State.History.Count > HistoryLength)
		{
			State.History.RemoveRange(HistoryLength, State.History.Count - HistoryLength);
		}
		Version++;
		if (news)
		{
			News(c.FirmName + ": " + result);
		}
	}

	public static string Title(Contract c)
	{
		if (c.IsResearch)
		{
			return "Research: " + Names(c.Research.Select((ResearchJob j) => j.Name).ToList());
		}
		if (c.IsAddons)
		{
			return GameServices.StudioName(c.Studio) + ": " + c.Item;
		}
		return GameServices.Name(c.Kind) + ": " + c.Item;
	}

	// "Kaiju", "Kaiju and Pirates", "Kaiju, Pirates and 4 more".
	public static string Names(List<string> names)
	{
		if (names.Count == 0)
		{
			return "nothing";
		}
		if (names.Count == 1)
		{
			return names[0];
		}
		if (names.Count == 2)
		{
			return names[0] + " and " + names[1];
		}
		return names[0] + ", " + names[1] + " and " + (names.Count - 2) + " more";
	}

	private static void News(string text)
	{
		if ((bool)G.Gui)
		{
			G.Gui.CreateTopNewsInfo(text);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "WochenUpdates")]
	private static void Weekly()
	{
		if (!G.Singleplayer)
		{
			return;
		}
		foreach (Contract c in State.Active.ToList())
		{
			try
			{
				Week(c);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("Contract " + c.Id + " failed: " + ex);
			}
		}
		Version++;
	}

	private static void Week(Contract c)
	{
		publisherScript firm = Firms.Find(c.FirmId);
		if (!firm || firm.Geschlossen())
		{
			long refund = Remaining(c);
			Finish(c, "the studio closed before finishing " + Title(c) + ". " + G.Money(refund) + " refunded.", refund, news: true);
			return;
		}
		c.FirmName = firm.GetName();
		if (c.IsResearch)
		{
			WeekResearch(c, firm);
		}
		else if (c.IsAddons)
		{
			WeekAddons(c, firm);
		}
		else
		{
			WeekService(c, firm);
		}
	}

	// Research items arrive one after another as the studio works through the points.
	private static void WeekResearch(Contract c, publisherScript firm)
	{
		foreach (ResearchJob job in c.Research)
		{
			if (!job.Done && ResearchCatalog.IsDone(job.Type, job.Slot))
			{
				job.Done = true;
				job.Skipped = true;
			}
		}
		if (c.Research.All((ResearchJob j) => j.Done))
		{
			Wrap(c, firm);
			return;
		}
		c.WeeksDone++;
		bool last = c.WeeksDone >= c.WeeksTotal;
		if (last && !c.Late && rng.NextDouble() < (double)((1f - c.Quality) * 0.3f))
		{
			// Weaker studios sometimes run late, once.
			c.Late = true;
			c.WeeksTotal += 1 + rng.Next(2);
			last = false;
			News(c.FirmName + " is running late on " + Title(c).Replace("Research: ", "researching ") + ".");
		}
		float total = c.Research.Sum((ResearchJob j) => j.Points);
		float delivered = (last ? float.MaxValue : (total * (float)c.WeeksDone / (float)Mathf.Max(1, c.WeeksTotal)));
		float running = 0f;
		List<string> finished = new List<string>();
		foreach (ResearchJob job in c.Research)
		{
			running += job.Points;
			if (!job.Done && running <= delivered + 0.01f)
			{
				ResearchCatalog.Complete(job.Type, job.Slot);
				job.Done = true;
				finished.Add(job.Name);
			}
		}
		if (c.Research.All((ResearchJob j) => j.Done))
		{
			Wrap(c, firm);
		}
		else if (finished.Count > 0)
		{
			News(c.FirmName + " finished researching " + Names(finished) + ".");
		}
	}

	private static void Wrap(Contract c, publisherScript firm)
	{
		List<ResearchJob> skipped = c.Research.Where((ResearchJob j) => j.Skipped).ToList();
		float total = Mathf.Max(1f, c.Research.Sum((ResearchJob j) => j.Points));
		long refund = (long)((double)c.Price * (double)(skipped.Sum((ResearchJob j) => j.Points) / total));
		List<string> names = (from j in c.Research
			where !j.Skipped
			select j.Name).ToList();
		Bump(firm);
		string result = ((names.Count > 0) ? ("finished researching " + Names(names) + ".") : "had nothing left to research.");
		if (refund > 0)
		{
			result = result + " " + G.Money(refund) + " refunded for " + Names(skipped.Select((ResearchJob j) => j.Name).ToList()) + ", which you researched yourself first.";
		}
		Finish(c, result, refund, news: true);
	}

	// Studio improvements are delivered one by one, evenly across the contract.
	private static void WeekAddons(Contract c, publisherScript firm)
	{
		gameScript game = GameServices.Find(c.GameId);
		if (!game || !game.inDevelopment || game.isOnMarket)
		{
			long refund = Remaining(c);
			Finish(c, Title(c) + " ended because the game left development. " + G.Money(refund) + " refunded.", refund, news: true);
			return;
		}
		c.WeeksDone++;
		int due = Mathf.Min(c.Addons.Count, Mathf.CeilToInt((float)c.Addons.Count * (float)c.WeeksDone / (float)Mathf.Max(1, c.WeeksTotal)));
		List<string> finished = new List<string>();
		foreach (int slot in c.Addons)
		{
			if (c.AddonsDone.Count >= due)
			{
				break;
			}
			if (c.AddonsDone.Contains(slot))
			{
				continue;
			}
			c.AddonsDone.Add(slot);
			if (GameServices.AddonDone(game, c.Studio, slot))
			{
				c.AddonsSkipped.Add(slot);
				continue;
			}
			GameServices.ApplyAddon(c, game, slot);
			finished.Add(GameServices.AddonName(c.Studio, slot));
		}
		if (c.AddonsDone.Count >= c.Addons.Count)
		{
			long refund = (long)((double)c.Price * (double)c.AddonsSkipped.Count / (double)Mathf.Max(1, c.Addons.Count));
			Bump(firm);
			int delivered = c.Addons.Count - c.AddonsSkipped.Count;
			Finish(c, "finished " + delivered + ((delivered == 1) ? " improvement" : " improvements") + " on " + c.Item + "." + ((refund > 0) ? (" " + G.Money(refund) + " refunded for ones your own rooms finished first.") : ""), refund, news: true);
		}
		else if (finished.Count > 0)
		{
			News(c.FirmName + " finished " + Names(finished) + " on " + c.Item + ".");
		}
	}

	private static void WeekService(Contract c, publisherScript firm)
	{
		gameScript game = GameServices.Find(c.GameId);
		if (!game || !game.inDevelopment || game.isOnMarket)
		{
			long refund = Remaining(c);
			Finish(c, Title(c) + " ended because the game left development. " + G.Money(refund) + " refunded.", refund, news: true);
			return;
		}
		c.WeeksDone++;
		float share = c.Amount * c.Multiplier / (float)c.WeeksTotal;
		c.Applied += GameServices.Apply(c, game, share);
		if (c.WeeksDone < c.WeeksTotal)
		{
			return;
		}
		// Development work still waiting on a feature boundary gets one last try, then is refunded.
		c.Applied += GameServices.Apply(c, game, 0f);
		long unused = ((c.Carry > 0.5f) ? ((long)((double)c.Price * (double)(c.Carry / Mathf.Max(1f, c.Amount * c.Multiplier)))) : 0);
		Bump(firm);
		Finish(c, "finished " + GameServices.Name(c.Kind).ToLowerInvariant() + " on " + c.Item + ": " + GameServices.Describe(c.Kind, c.Applied, game) + "." + ((unused > 0) ? (" " + G.Money(unused) + " refunded for work it couldn't do.") : ""), unused, news: true);
	}

	// A finished job warms the studio to you a little.
	private static void Bump(publisherScript firm)
	{
		firm.relation = Mathf.Min(100f, firm.relation + 2f);
	}

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
			writer.Write<string>(SaveKey, JsonConvert.SerializeObject(State));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Saving contracts failed: " + ex);
		}
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(savegameScript), "Load")]
	private static void BeforeLoad()
	{
		State = new ContractState();
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
				State = JsonConvert.DeserializeObject<ContractState>(___es3file.Load<string>(SaveKey)) ?? new ContractState();
				foreach (Contract c in State.Active.Concat(State.History))
				{
					Migrate(c);
				}
				Plugin.Log.LogInfo("Contracts loaded: " + State.Active.Count + " active");
			}
			else
			{
				State = new ContractState();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("Loading contracts failed: " + ex);
			State = new ContractState();
		}
		Version++;
	}

	private static void Migrate(Contract c)
	{
		c.Research = c.Research ?? new List<ResearchJob>();
		c.Addons = c.Addons ?? new List<int>();
		c.AddonsDone = c.AddonsDone ?? new List<int>();
		c.AddonsSkipped = c.AddonsSkipped ?? new List<int>();
		if (c.IsResearch && c.Research.Count == 0 && c.ResearchType >= 0)
		{
			c.Research.Add(new ResearchJob
			{
				Type = c.ResearchType,
				Slot = c.ResearchSlot,
				Name = c.Item,
				Points = Mathf.Max(1f, c.Amount)
			});
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(mainScript), "InitNewGame")]
	private static void NewGame()
	{
		State = new ContractState();
		Version++;
	}
}
