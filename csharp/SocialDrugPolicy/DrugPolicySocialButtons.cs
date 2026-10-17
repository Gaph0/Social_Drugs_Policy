using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SocialDrugPolicy
{
    /// <summary>
    /// Adds two buttons at the bottom of the drug policy window, beside the Close
    /// button: one allows every Social drug for recreation, one sets the "only if
    /// tolerance below" limit of every Social drug to that drug's minimum tolerance
    /// for addiction.
    /// </summary>
    /// <remarks>
    /// Dialog_ManagePolicies draws the policy name and its dismiss, copy, rename and
    /// make-default icons in the strip above the table, so that strip is not free.
    /// The only space no vanilla widget occupies is the row the Close button sits in:
    /// the base dialog ends the contents rect 10 pixels above CloseButSize and the
    /// Close button is centred, leaving the right end of that row empty.
    /// </remarks>
    [HarmonyPatch(typeof(Dialog_ManageDrugPolicies), "DoContentsRect")]
    internal static class Patch_Dialog_ManageDrugPolicies_DoContentsRect
    {
        private const float ButtonHeight = 32f;
        private const float GapBetweenButtons = 8f;
        private const float ButtonPadding = 24f;
        // Distance from the bottom of the table into the row that holds Close.
        private const float GapBelowTable = 12f;

        private static readonly MethodInfo SelectedPolicyGetter =
            AccessTools.PropertyGetter(typeof(Dialog_ManageDrugPolicies), "SelectedPolicy");

        [HarmonyPostfix]
        public static void DrawButtons(Dialog_ManageDrugPolicies __instance, Rect rect)
        {
            DrugPolicy policy = SelectedPolicy(__instance);
            if (policy == null)
            {
                return;
            }

            bool showToleranceButton = ToleranceBelowBridge.Available;
            string allowLabel = "SDP_AllowSocialDrugs".Translate();
            string toleranceLabel = "SDP_LimitSocialTolerance".Translate();

            GameFont fontBefore = Text.Font;
            TextAnchor anchorBefore = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            float allowWidth = Text.CalcSize(allowLabel).x + ButtonPadding;

            // Right-align the pair with the drug table, in the empty half of the row
            // that holds the Close button.
            float rowY = rect.yMax + GapBelowTable;
            Rect allowRect = new Rect(rect.xMax - allowWidth, rowY, allowWidth, ButtonHeight);
            Rect toleranceRect = default(Rect);
            if (showToleranceButton)
            {
                float toleranceWidth = Text.CalcSize(toleranceLabel).x + ButtonPadding;
                toleranceRect = new Rect(
                    allowRect.x - GapBetweenButtons - toleranceWidth, rowY, toleranceWidth, ButtonHeight);
            }

            if (Widgets.ButtonText(allowRect, allowLabel))
            {
                Report("SDP_AllowedSocialDrugs", AllowAllSocialDrugs(policy));
            }
            TooltipHandler.TipRegion(allowRect, "SDP_AllowSocialDrugsTip".Translate());

            if (showToleranceButton)
            {
                if (Widgets.ButtonText(toleranceRect, toleranceLabel))
                {
                    Report("SDP_LimitsSet", SetSocialToleranceLimits(policy));
                }
                TooltipHandler.TipRegion(toleranceRect, "SDP_LimitSocialToleranceTip".Translate());
            }

            Text.Font = fontBefore;
            Text.Anchor = anchorBefore;
        }

        private static DrugPolicy SelectedPolicy(Dialog_ManageDrugPolicies dialog)
        {
            if (SelectedPolicyGetter == null)
            {
                return null;
            }
            return SelectedPolicyGetter.Invoke(dialog, null) as DrugPolicy;
        }

        private static void Report(string messageKey, int changed)
        {
            Messages.Message(
                changed > 0 ? messageKey.Translate(changed) : "SDP_NothingToChange".Translate(),
                changed > 0 ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput,
                false);
        }

        /// <summary>The game's own Social category, as shown on a drug's info card.</summary>
        private static bool IsSocialDrug(ThingDef drug)
        {
            return drug?.ingestible != null && drug.ingestible.drugCategory == DrugCategory.Social;
        }

        /// <summary>Ticks "for recreation" on every Social drug. Returns how many changed.</summary>
        private static int AllowAllSocialDrugs(DrugPolicy policy)
        {
            int changed = 0;
            for (int i = 0; i < policy.Count; i++)
            {
                DrugPolicyEntry entry = policy[i];
                // The recreation checkbox only exists on drugs that give joy, so only
                // those can be allowed this way.
                if (!IsSocialDrug(entry.drug) || !entry.drug.IsPleasureDrug)
                {
                    continue;
                }
                if (!entry.allowedForJoy)
                {
                    entry.allowedForJoy = true;
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>
        /// Sets the tolerance limit of every Social drug to the smallest tolerance at
        /// which a dose can newly addict the taker. Returns how many entries changed.
        /// </summary>
        private static int SetSocialToleranceLimits(DrugPolicy policy)
        {
            int changed = 0;
            for (int i = 0; i < policy.Count; i++)
            {
                DrugPolicyEntry entry = policy[i];
                if (!IsSocialDrug(entry.drug) || !entry.drug.IsAddictiveDrug)
                {
                    continue;
                }
                float limit = ToleranceLimitOf(entry.drug);
                if (limit <= 0f)
                {
                    continue;
                }
                // Skip entries already at the right value, so the report only counts
                // real changes and a repeat click says "Nothing to change."
                if (ToleranceBelowBridge.TryGetToleranceLimit(entry, out float current)
                    && Mathf.Approximately(current, limit))
                {
                    continue;
                }
                if (ToleranceBelowBridge.SetToleranceLimit(entry, limit))
                {
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>
        /// The drug's minimum tolerance for addiction, or 0 when there is no such
        /// value to set: a drug without tolerance, or one whose limit is the top of
        /// the slider, where the slider already means "any".
        /// </summary>
        private static float ToleranceLimitOf(ThingDef drug)
        {
            CompProperties_Drug drugComp = DrugStatsUtility.GetDrugComp(drug);
            if (drugComp == null || DrugStatsUtility.GetToleranceGiver(drug) == null)
            {
                return 0f;
            }
            float limit = drugComp.minToleranceToAddict;
            return limit > 0f && limit < 1f ? limit : 0f;
        }
    }
}
