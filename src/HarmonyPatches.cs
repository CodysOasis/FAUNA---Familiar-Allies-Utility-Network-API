using HarmonyLib;
using StardewValley;
using System.Collections.Generic;
using System.Linq;

namespace FAUNA
{
    public static class HarmonyPatches
    {
        public static void Apply(string modId)
        {
            var harmony = new Harmony(modId);
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Utility), nameof(Utility.GetNpcsWithinDistance))]
    public static class Patch_GetNpcsWithinDistance
    {
        public static IEnumerable<NPC> Postfix(
            IEnumerable<NPC> __result)
        {
            return __result.Where(npc => npc is not FamiliarEntity);
        }
    }
}