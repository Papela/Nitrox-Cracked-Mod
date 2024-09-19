using System.Reflection;
using NitroxClient.GameLogic.FMOD;
using NitroxClient.MonoBehaviours;
using NitroxClient.Unity.Helper;
using NitroxModel.GameLogic.FMOD;
using NitroxModel.Helper;

namespace NitroxPatcher.Patches.Dynamic;

public sealed partial class FMOD_StudioEventEmitter_PlayUI_Patch : NitroxPatch, IDynamicPatch
{
    private static readonly MethodInfo TARGET_METHOD = Reflect.Method((FMOD_StudioEventEmitter t) => t.PlayUI());

    public static bool Prefix()
    {
        return !FMODSoundSuppressor.SuppressFMODEvents;
    }

    public static void Postfix(FMOD_StudioEventEmitter __instance, bool __result)
    {
        if (!__result) // Only run if evt was started in original method
        {
            return;
        }

        if (!Resolve<FMODWhitelist>().IsWhitelisted(__instance.asset.path))
        {
            return;
        }

        if (!__instance.TryGetComponentInParent(out NitroxEntity nitroxEntity, true))
        {
            Log.Warn($"[{nameof(FMOD_StudioEventEmitter_PlayUI_Patch)}] - No NitroxEntity found for {__instance.asset.path} at {__instance.GetFullHierarchyPath()}");
            return;
        }

        Resolve<FMODSystem>().SendStudioEmitterPlay(nitroxEntity.Id, __instance.asset.path, false);
    }
}
