using HarmonyLib;
using Verse;

namespace SocialDrugPolicy
{
    /// <summary>
    /// Entry point. The whole mod is the two buttons the patch adds to the drug
    /// policy window, so all this does is apply that patch.
    /// </summary>
    public sealed class SocialDrugPolicyMod : Mod
    {
        public const string HarmonyId = "gapho.socialdrugpolicy";

        public SocialDrugPolicyMod(ModContentPack content) : base(content)
        {
            new Harmony(HarmonyId).PatchAll();
            Log.Message("[Social Drug Policy] Harmony patches applied.");
        }
    }
}
