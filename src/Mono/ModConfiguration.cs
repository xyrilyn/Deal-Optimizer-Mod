
using Newtonsoft.Json;

namespace DealOptimizer_Mono
{
    public partial class Core
    {
        private static ModConfiguration modConfiguration;

        private class ModConfiguration
        {
            public Dictionary<string, string> Options { get; }

            public ModConfiguration()
            {
                Options = new Dictionary<string, string>();
            }

            public ModConfiguration(Dictionary<string, string> options)
            {
                Options = options;
            }
        }

        public static class Options
        {
            public static readonly string CounterofferUIEnabled = "CounterofferUIEnabled";
            public static readonly string PricePerUnitDisplay = "PricePerUnitDisplay";
            public static readonly string MaximumDailySpendDisplay = "MaximumDailySpendDisplay";

            public static readonly string CounterofferOptimizationEnabled = "CounterofferOptimizationEnabled";
            public static readonly string MinimumSuccessProbability = "MinimumSuccessProbability";

            public static readonly string StreetDealOptimizationEnabled = "StreetDealOptimizationEnabled";

            public static readonly string ProductEvaluatorEnabled = "ProductEvaluatorEnabled";

            public static readonly string PrintCalculationsToConsole = "PrintCalculationsToConsole";
        }

        private static readonly ModConfiguration defaultModConfiguration = new ModConfiguration(
            new Dictionary<string, string>
            {
                [Options.CounterofferUIEnabled] = "true",
                [Options.PricePerUnitDisplay] = "true",
                [Options.MaximumDailySpendDisplay] = "true",

                [Options.CounterofferOptimizationEnabled] = "true",
                [Options.MinimumSuccessProbability] = "98",

                [Options.StreetDealOptimizationEnabled] = "true",

                [Options.ProductEvaluatorEnabled] = "false",

                [Options.PrintCalculationsToConsole] = "false",
            }
        );

        private void SetupConfiguration()
        {
            string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string modDirectory = Path.GetDirectoryName(assemblyLocation);
            string configPath = Path.Combine(modDirectory, "DealOptimizer", "DealOptimizer_Config.json");

            LoggerInstance.Msg($"Config Path: {configPath}");
            Directory.CreateDirectory(Path.GetDirectoryName(configPath));

            if (!File.Exists(configPath))
            {
                modConfiguration = defaultModConfiguration;
                using (StreamWriter file = File.CreateText(configPath))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(file, defaultModConfiguration);
                }
            }
            else
            {
                try
                {
                    using (StreamReader reader = new StreamReader(configPath))
                    {
                        string json = reader.ReadToEnd();
                        modConfiguration = JsonConvert.DeserializeObject<ModConfiguration>(json);
                    }
                }
                catch (Exception ex)
                {
                    LoggerInstance.Error($"Invalid mod configuration (will use defaults as fallback)", ex);
                    modConfiguration = defaultModConfiguration;
                }
            }
        }

        private static bool GetConfigurationFlag(string name)
        {
            return bool.Parse(modConfiguration.Options.GetValueOrDefault(name, defaultModConfiguration.Options[name]));
        }

        private static int GetConfigurationInt(string name)
        {
            return int.Parse(modConfiguration.Options.GetValueOrDefault(name, defaultModConfiguration.Options[name]));
        }
    }
}