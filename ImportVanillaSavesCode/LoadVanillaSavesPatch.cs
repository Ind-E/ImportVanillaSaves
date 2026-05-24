using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;

namespace ImportVanillaSaves.ImportVanillaSavesCode;

public static class LoadVanillaSavesPatch
{
    public static bool ShouldLoadVanillaSaves;

    [HarmonyPatch(
        typeof(UserDataPathProvider),
        nameof(UserDataPathProvider.IsRunningModded),
        MethodType.Getter
    )]
    public static class PatchGetIsRunningModded
    {
        [HarmonyPrefix]
        public static bool Prefix(ref bool __result)
        {
            if (ShouldLoadVanillaSaves)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
