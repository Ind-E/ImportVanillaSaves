using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;

namespace ImportVanillaSaves.ImportVanillaSavesCode;

[HarmonyPatch(
    typeof(UserDataPathProvider),
    nameof(UserDataPathProvider.IsRunningModded),
    MethodType.Getter
)]
internal static class LoadVanillaSavesPatch
{
    public static bool ShouldLoadVanillaSaves;

    [HarmonyPostfix]
    public static bool Postfix(bool result) => !ShouldLoadVanillaSaves && result;
}
