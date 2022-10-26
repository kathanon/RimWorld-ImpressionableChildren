using HarmonyLib;
using Verse;
using RimWorld;

namespace ImpressionableChildren
{
    [StaticConstructorOnStartup]
    public static class Main
    {
        static Main()
        {
            var harmony = new Harmony(Strings.ID);
            harmony.PatchAll();
        }
    }
}
