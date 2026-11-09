using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HolyAddon.Core.Contracts;

// Quotes for research jobs, and taking on a contract (paid up front, booked as research or development spending).
internal static class Hiring
{
	// A studio quoting for a batch of research items, worked through one after another.
	public static Quote Research(IList<ResearchItem> items, publisherScript firm)
	{
		float q = Firms.Quality(firm);
		float discount = Firms.Discount(firm);
		// A studio works like a research team of a few people: about 40-160 research points a week.
		float rate = 40f + 120f * q;
		float weeks = 0f;
		double price = 0.0;
		foreach (ResearchItem item in items)
		{
			float points = Mathf.Max(1f, item.PointsLeft);
			weeks += points / ((item.Type == 0 && firm.fanGenre == item.Slot) ? (rate * 1.25f) : rate);
			price += (double)item.InHousePrice * (1.4 + 0.8 * (double)q) + (double)points * 40.0 * (0.75 + 0.75 * (double)q);
		}
		// Bigger batches get a bulk discount.
		float bulk = ((items.Count >= 10) ? 0.1f : ((items.Count >= 3) ? 0.05f : 0f));
		int total = Mathf.Clamp(Mathf.CeilToInt(weeks), 1, 52);
		return new Quote
		{
			Firm = firm,
			Quality = q,
			Amount = items.Sum((ResearchItem i) => Mathf.Max(1f, i.PointsLeft)),
			Weeks = total,
			Price = Firms.Round(System.Math.Max(2000.0, price * (1.0 - (double)discount) * (1.0 - (double)bulk))),
			Effect = ((items.Count == 1) ? "Researched" : ("All " + items.Count + " researched")) + " in " + total + ((total == 1) ? " week" : " weeks")
		};
	}

	// Returns null when the player can't afford it (the game's own "not enough money" message is shown).
	public static Contract Hire(Quote quote, string kind, IList<ResearchItem> research, gameScript game, int studio = -1, IList<int> addons = null)
	{
		mainScript ms = G.MS;
		if (!ms || quote == null || !quote.Firm)
		{
			return null;
		}
		if (ms.NotEnoughMoney(quote.Price))
		{
			if ((bool)G.Gui)
			{
				G.Gui.ShowNoMoney();
			}
			return null;
		}
		ms.Pay(quote.Price, (kind == "research") ? 2 : 10);
		Contract c = new Contract
		{
			Kind = kind,
			FirmId = quote.Firm.myID,
			FirmName = quote.Firm.GetName(),
			Price = quote.Price,
			WeeksTotal = Mathf.Max(1, quote.Weeks),
			Amount = quote.Amount,
			Quality = quote.Quality,
			Effect = quote.Effect
		};
		if (research != null)
		{
			foreach (ResearchItem item in research)
			{
				c.Research.Add(new ResearchJob
				{
					Type = item.Type,
					Slot = item.Slot,
					Name = item.Name,
					Points = Mathf.Max(1f, item.PointsLeft)
				});
			}
		}
		if ((bool)game)
		{
			c.GameId = game.myID;
			c.Item = game.GetNameSimple();
		}
		if (addons != null)
		{
			c.Studio = studio;
			c.Addons.AddRange(addons);
		}
		return ContractBook.Add(c);
	}
}
