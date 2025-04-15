
using System.Text.Json;
using MelonLoader;

namespace DealOptimizer_IL2CPP
{
    public partial class Core
    {
        private static ModConfiguration modConfiguration;

        [Serializable]
        private class ModConfiguration
        {
            public Dictionary<string, string> Flags { get; set; }

            public ModConfiguration()
            {
                Flags = new Dictionary<string, string>();
            }

            public ModConfiguration(Dictionary<string, string> flags)
            {
                Flags = flags;
            }
        }

        public static class Flags
        {
            public static readonly string PrintCalculationsToConsole = "PrintCalculationsToConsole";

            public static readonly string PricePerUnitDisplay = "PricePerUnitDisplay";
            public static readonly string MaximumDailySpendDisplay = "MaximumDailySpendDisplay";
        }

        private static readonly ModConfiguration defaultModConfiguration = new ModConfiguration(
            new Dictionary<string, string>
            {
                [Flags.PrintCalculationsToConsole] = "false",
                [Flags.PricePerUnitDisplay] = "true",
                [Flags.MaximumDailySpendDisplay] = "true",
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

                var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };
                string jsonString = JsonSerializer.Serialize(modConfiguration, options);
                File.WriteAllText(configPath, jsonString);
            }
            else
            {
                try
                {
                    string jsonString = File.ReadAllText(configPath);
                    var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true, AllowTrailingCommas = true };
                    modConfiguration = JsonSerializer.Deserialize<ModConfiguration>(jsonString, options);
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
            return bool.Parse(modConfiguration.Flags.GetValueOrDefault(name, defaultModConfiguration.Flags[name]));
        }
    }
}