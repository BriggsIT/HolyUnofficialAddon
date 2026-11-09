using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HolyAddon.Core.Contracts;

// A studio's offer for one job.
internal class Quote
{
	public publisherScript Firm;

	public long Price;

	public int Weeks;

	public float Amount;

	public string Effect;

	public float Quality;

	public string Unavailable;
}

// The outside studios you can hire: independent NPC developers (your own subsidiaries are The Holy Expansion's to command).
internal static class Firms
{
	public static List<publisherScript> All()
	{
		publisherScript[] all = ((bool)G.MS) ? G.MS.arrayPublisherScripts : null;
		if (all == null)
		{
			return new List<publisherScript>();
		}
		return all.Where((publisherScript p) => (bool)p && p.isUnlocked && !p.isPlayer && p.developer && !p.IsTochterfirma() && !p.Geschlossen()).ToList();
	}

	public static publisherScript Find(int id)
	{
		publisherScript[] all = ((bool)G.MS) ? G.MS.arrayPublisherScripts : null;
		return all?.FirstOrDefault((publisherScript p) => (bool)p && p.myID == id);
	}

	// 0.1 to 1 from the studio's 0-100 star rating.
	public static float Quality(publisherScript p)
	{
		return Mathf.Clamp(p.stars, 10f, 100f) / 100f;
	}

	// Studios that like you knock up to 15% off.
	public static float Discount(publisherScript p)
	{
		return Mathf.Clamp01((p.GetRelation() - 50f) / 50f) * 0.15f;
	}

	public static string Speciality(publisherScript p)
	{
		genres g = ((bool)G.MainObj) ? G.MainObj.GetComponent<genres>() : null;
		if (!g || p.fanGenre < 0)
		{
			return "";
		}
		return g.GetName(p.fanGenre);
	}

	public static string Describe(publisherScript p)
	{
		string text = "Rating " + p.GetStarsAmount() + "/5";
		string speciality = Speciality(p);
		if (speciality.Length > 0)
		{
			text = text + "  ·  " + speciality;
		}
		int discount = Mathf.RoundToInt(Discount(p) * 100f);
		if (discount > 0)
		{
			text = text + "  ·  " + discount + "% off";
		}
		return text;
	}

	public static long Round(double price)
	{
		if (price < 10000.0)
		{
			return (long)(System.Math.Round(price / 100.0) * 100.0);
		}
		return (long)(System.Math.Round(price / 1000.0) * 1000.0);
	}
}
