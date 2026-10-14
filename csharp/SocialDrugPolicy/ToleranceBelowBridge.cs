using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace SocialDrugPolicy
{
    /// <summary>
    /// Bridge to the mod "Only If 'Tolerance' Below in Drug Policy"
    /// (packageId seohyeon.onlyIfToleranceBelow). That mod adds a "tolerance below"
    /// field to every drug policy entry through Prepatcher and offers a setter for
    /// other mods. We reach it by reflection so this mod also loads, and the button
    /// hides itself, when that mod is not installed.
    /// </summary>
    internal static class ToleranceBelowBridge
    {
        private const string PackageId = "seohyeon.onlyIfToleranceBelow";
        private const string ApiTypeName = "DrugPolicyEnhanced.ExposedPrepatcherFields";
        private const string SetterName = "Set_OnlyIfToleranceBelow";
        private const string GetterName = "Get_OnlyIfToleranceBelow";

        private static bool resolved;
        private static MethodInfo setter;
        private static MethodInfo getter;

        /// <summary>True when the other mod is loaded and its setter can be called.</summary>
        internal static bool Available
        {
            get
            {
                if (!resolved)
                {
                    resolved = true;
                    System.Type api = GenTypes.GetTypeInAnyAssembly(ApiTypeName);
                    if (api != null)
                    {
                        setter = api.GetMethod(SetterName, BindingFlags.Public | BindingFlags.Static);
                        getter = api.GetMethod(GetterName, BindingFlags.Public | BindingFlags.Static);
                    }
                    // Only worth a warning when the mod that provides the api is
                    // loaded: without the mod the button is expected to be hidden.
                    if (setter == null && ModsConfig.IsActive(PackageId))
                    {
                        Log.Warning("[Social Drug Policy] Cannot find " + ApiTypeName + "." + SetterName
                            + ", so the tolerance-limit button is hidden. The mod \"Only If 'Tolerance'"
                            + " Below in Drug Policy\" provides it.");
                    }
                }
                return setter != null;
            }
        }

        /// <summary>Reads the tolerance limit of the entry. False when it cannot be read.</summary>
        internal static bool TryGetToleranceLimit(DrugPolicyEntry entry, out float limit)
        {
            limit = 0f;
            if (!Available || getter == null)
            {
                return false;
            }
            try
            {
                limit = (float)getter.Invoke(null, new object[] { entry });
                return true;
            }
            catch (Exception e)
            {
                // This runs inside the window draw loop: an uncaught reflection error
                // would spam red errors every frame, so it is logged once instead.
                Log.ErrorOnce("[Social Drug Policy] Reading the tolerance limit failed: " + e, 758234012);
                return false;
            }
        }

        /// <summary>Writes the tolerance limit into the entry. False when it cannot be written.</summary>
        internal static bool SetToleranceLimit(DrugPolicyEntry entry, float limit)
        {
            if (!Available)
            {
                return false;
            }
            try
            {
                setter.Invoke(null, new object[] { entry, limit });
                return true;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Social Drug Policy] Setting the tolerance limit failed: " + e, 758234011);
                return false;
            }
        }
    }
}
