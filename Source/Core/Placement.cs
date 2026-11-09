using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace HolyAddon.Core;

// Relaxed furniture placement. Every placement check lives in objectScript:
// - OnCollisionEnter/Stay/Exit: an object whose collision box touches a wall or another object turns red and can't be
//   placed, and a placed object that still touches something gets picked back up. Collision boxes are often bigger than
//   the model, so a desk that looks clear of the wall is still blocked. Here only contacts deeper than the chosen
//   leniency count (optionally furniture may overlap furniture freely).
// - IsCorrectRoomForThisObject: which room types an object may go in (also re-checked in PlatziereObject).
// Walls and doors always block past the leniency; building ownership, map limits and award counts are never touched.
[HarmonyPatch]
internal static class Placement
{
	// Collision state of the object in your hand: collider -> whether that contact is deep enough to block.
	private static readonly Dictionary<objectScript, Dictionary<Collider, bool>> held = new Dictionary<objectScript, Dictionary<Collider, bool>>();

	private static float Leniency => Mathf.Clamp(Plugin.CollisionLeniency.Value, 0f, 0.5f);

	private static bool Relaxed
	{
		get
		{
			if (G.Singleplayer)
			{
				if (!(Leniency > 0f))
				{
					return Plugin.FurnitureOverlap.Value;
				}
				return true;
			}
			return false;
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(objectScript), "IsCorrectRoomForThisObject")]
	private static void AnyRoom(objectScript __instance, Vector3 pos, ref bool __result)
	{
		if (__result || !G.Singleplayer || !Plugin.IgnoreRoomRules.Value)
		{
			return;
		}
		mapScript map = __instance.mapS_;
		int x = Mathf.RoundToInt(pos.x);
		int z = Mathf.RoundToInt(pos.z);
		if (!map || !map.IsInMapLimit(x, z))
		{
			return;
		}
		// Hallways, outdoor floor and real rooms are fine; tiles that belong to no room still are not.
		int id = map.mapRoomID[x, z];
		__result = id == 1 || id == mapScript.ID_FLOOROUTSIDE || (bool)map.mapRoomScript[x, z];
	}

	// How far the two collision boxes push into each other (0 when they only touch).
	private static float Depth(Collision collision)
	{
		float depth = 0f;
		for (int i = 0; i < collision.contactCount; i++)
		{
			depth = Mathf.Max(depth, 0f - collision.GetContact(i).separation);
		}
		return depth;
	}

	private static bool Blocking(objectScript self, Collision collision)
	{
		objectScript other = collision.collider.GetComponentInParent<objectScript>();
		if ((bool)other && other != self && !other.autoInventarItem && Plugin.FurnitureOverlap.Value)
		{
			if (collision.contactCount > 0)
			{
				Physics.IgnoreCollision(collision.GetContact(0).thisCollider, collision.collider, ignore: true);
			}
			return false;
		}
		return Depth(collision) > Leniency;
	}

	// Auto-furnishing relies on the normal collision rules to throw out clashing items, so its objects keep them.
	private static bool Handles(objectScript self, Collision collision)
	{
		if (Relaxed && !self.autoInventarItem && collision != null)
		{
			return (bool)collision.collider;
		}
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(objectScript), "OnCollisionEnter")]
	private static bool Enter(objectScript __instance, Collision collision)
	{
		return Touch(__instance, collision);
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(objectScript), "OnCollisionStay")]
	private static bool Stay(objectScript __instance, Collision collision)
	{
		return Touch(__instance, collision);
	}

	// Returns whether the game's own collision code should still run.
	private static bool Touch(objectScript self, Collision collision)
	{
		if (!Handles(self, collision))
		{
			return true;
		}
		bool blocking = Blocking(self, collision);
		if (!self.picked)
		{
			// A placed object: let the game pick it back up only for a real (deep) overlap.
			return blocking;
		}
		if (!held.TryGetValue(self, out Dictionary<Collider, bool> contacts))
		{
			contacts = new Dictionary<Collider, bool>();
			held[self] = contacts;
		}
		contacts[collision.collider] = blocking;
		Apply(self, contacts);
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(objectScript), "OnCollisionExit")]
	private static bool Exit(objectScript __instance, Collision collision)
	{
		if (!Handles(__instance, collision) || !__instance.picked)
		{
			return true;
		}
		if (held.TryGetValue(__instance, out Dictionary<Collider, bool> contacts))
		{
			contacts.Remove(collision.collider);
			Apply(__instance, contacts);
		}
		return false;
	}

	// Mirror the game's own bookkeeping: colided blocks placement, and MouseMovement only recolours the outline when it is false.
	private static void Apply(objectScript self, Dictionary<Collider, bool> contacts)
	{
		foreach (Collider gone in contacts.Keys.Where((Collider c) => !c).ToList())
		{
			contacts.Remove(gone);
		}
		int count = contacts.Values.Count((bool b) => b);
		self.colided = count > 0;
		self.collisionAmount = count;
		if (self.colided && (bool)self.mCamS_)
		{
			self.mCamS_.SetOutlineColor(1, 0.3f, 1);
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(objectScript), "PlatziereObject")]
	private static void Placed(objectScript __instance)
	{
		if (!__instance.picked)
		{
			held.Remove(__instance);
		}
	}
}
