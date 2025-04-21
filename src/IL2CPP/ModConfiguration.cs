using MelonLoader;

namespace DealOptimizer_IL2CPP
{
    public class ModConfiguration
    {
        // 01_Counteroffer
        public static MelonPreferences_Entry<bool> CounterofferUIEnabled;
        public static MelonPreferences_Entry<bool> PricePerUnitDisplay;
        public static MelonPreferences_Entry<bool> MaximumDailySpendDisplay;

        // 02_Counteroffer_Optimization
        public static MelonPreferences_Entry<bool> CounterofferOptimizationEnabled;
        public static MelonPreferences_Entry<int> MinimumSuccessProbability;

        // 03_Street Deals
        public static MelonPreferences_Entry<bool> StreetDealOptimizationEnabled;

        // 04_Product_Evaluator
        public static MelonPreferences_Entry<bool> ProductEvaluatorEnabled;

        // 09_Debug
        public static MelonPreferences_Entry<bool> PrintCalculationsToConsole;

        public static void SetupConfiguration()
        {
            var categoryCounteroffer = MelonPreferences.CreateCategory("DealOptimizer_IL2CPP_01_Counteroffer", "Counteroffer Settings");
            CounterofferUIEnabled = categoryCounteroffer.CreateEntry("CounterofferUI", true, "Enable Counteroffer UI");
            PricePerUnitDisplay = categoryCounteroffer.CreateEntry("PricePerUnitDisplay", true, "Display price per unit in UI");
            MaximumDailySpendDisplay = categoryCounteroffer.CreateEntry("MaximumDailySpendDisplay", true, "Display customer's max daily spend in UI");

            var categoryCounterofferOptimization = MelonPreferences.CreateCategory("DealOptimizer_IL2CPP_02_Counteroffer_Optimization", "Counteroffer Optimization Settings");
            CounterofferOptimizationEnabled = categoryCounterofferOptimization.CreateEntry("CounterofferOptimizationEnabled", true, "Enable optimization for Counteroffers");
            MinimumSuccessProbability = categoryCounterofferOptimization.CreateEntry("MinimumSuccessProbability", 98, "Min. success % for optimization");
            
            var categoryStreetDeals = MelonPreferences.CreateCategory("DealOptimizer_IL2CPP_03_Street_Deals", "Street Deals Settings");
            StreetDealOptimizationEnabled = categoryStreetDeals.CreateEntry("StreetDealOptimizationEnabled", true, "Enable optimization for Street Deals");

            var categoryProductEvaluator = MelonPreferences.CreateCategory("DealOptimizer_IL2CPP_04_Product_Evaluator", "Product Evaluator Settings");
            ProductEvaluatorEnabled = categoryProductEvaluator.CreateEntry("ProductEvaluatorEnabled", false, "Enable Product Evaluator feature");

            var categoryDebug = MelonPreferences.CreateCategory("DealOptimizer_IL2CPP_09_Debug", "Debug Settings (May Cause Lag)");
            PrintCalculationsToConsole = categoryDebug.CreateEntry("PrintCalculationsToConsole", false, "Print all calculation steps");
        }

        public static bool CheckDependency()
        {
            string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string modDirectory = Path.GetDirectoryName(assemblyLocation);
            string dllPath = Path.Combine(modDirectory, "ModManager&PhoneApp.dll");
            return File.Exists(dllPath);
        }
    }
}