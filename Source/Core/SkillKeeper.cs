using HarmonyLib;

namespace HolyAddon.Core;

// characterScript.Learn runs right after every work roll and clamps skills outside the employee's profession
// down to the natural cap (50, or 60 with perk 15). Without this, a skill raised in the Editor drops back
// to 50 the first time the employee uses it. Learn can still raise skills; it just can no longer lower one.
[HarmonyPatch(typeof(characterScript), "Learn")]
internal static class SkillKeeper
{
	private static void Prefix(characterScript __instance, out float[] __state)
	{
		__state = null;
		if (!Plugin.KeepEditedSkills.Value)
		{
			return;
		}
		__state = new float[Employees.SkillCount];
		for (int i = 0; i < Employees.SkillCount; i++)
		{
			__state[i] = Employees.Skill(__instance, i);
		}
	}

	private static void Postfix(characterScript __instance, float[] __state)
	{
		if (__state == null)
		{
			return;
		}
		for (int i = 0; i < Employees.SkillCount; i++)
		{
			if (Employees.Skill(__instance, i) < __state[i])
			{
				Employees.SetSkill(__instance, i, __state[i]);
			}
		}
	}
}
