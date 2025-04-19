using MelonLoader;
using ModManagerPhoneApp;

namespace DealOptimizer_IL2CPP
{
    public partial class Core
    {
        // 01_Counteroffer
        public static MelonPreferences_Entry<bool> CounterofferOptimizationEnabled;
        public static MelonPreferences_Entry<bool> PricePerUnitDisplay;
        public static MelonPreferences_Entry<bool> MaximumDailySpendDisplay;

        // 02_Street Deals
        public static MelonPreferences_Entry<bool> StreetDealOptimizationEnabled;

        // 03_Product_Evaluator
        public static MelonPreferences_Entry<bool> ProductEvaluatorEnabled;

        // 09_Debug
        public static MelonPreferences_Entry<bool> PrintCalculationsToConsole;

        private void SetupConfiguration()
        {
            try
            {
                ModManagerPhoneApp.ModSettingsEvents.OnPreferencesSaved += HandleSettingsUpdate;
                LoggerInstance.Msg("Successfully subscribed to Mod Manager save event.");
            }
            catch (Exception ex) // Catches TypeLoadException if DLL missing, or other errors
            {
                LoggerInstance.Warning($"Could not subscribe to Mod Manager event (Mod Manager may not be installed/compatible): {ex.Message}");
            }

            var categoryCounteroffer = MelonPreferences.CreateCategory("DealOptimizer_IL2CPP_01_Counteroffer", "Counteroffer Settings");
            CounterofferOptimizationEnabled = categoryCounteroffer.CreateEntry("CounterofferOptimizationEnabled", true, "Enable optimization for Counteroffers");
            PricePerUnitDisplay = categoryCounteroffer.CreateEntry("PricePerUnitDisplay", true, "Display price per unit in UI");
            MaximumDailySpendDisplay = categoryCounteroffer.CreateEntry("MaximumDailySpendDisplay", true, "Display customer's max daily spend in UI");

            var categoryStreetDeals = MelonPreferences.CreateCategory("DealOptimizer_IL2CPP_02_Street_Deals", "Street Deals Settings");
            StreetDealOptimizationEnabled = categoryStreetDeals.CreateEntry("StreetDealOptimizationEnabled", true, "Enable optimization for Street Deals");

            var categoryProductEvaluator = MelonPreferences.CreateCategory("DealOptimizer_IL2CPP_03_Product_Evaluator", "Product Evaluator Settings");
            ProductEvaluatorEnabled = categoryProductEvaluator.CreateEntry("ProductEvaluatorEnabled", true, "Enable Product Evaluator feature");

            var categoryDebug = MelonPreferences.CreateCategory("DealOptimizer_IL2CPP_09_Debug", "Debug Settings (May Cause Lag)");
            PrintCalculationsToConsole = categoryDebug.CreateEntry("PrintCalculationsToConsole", false, "Print all calculation steps");
        }

        private void HandleSettingsUpdate()
        {
            LoggerInstance.Msg("Melon preferences updated");
        }
    }
}