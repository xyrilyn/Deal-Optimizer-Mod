using System.Text;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Levelling;
using ScheduleOne.Messaging;
using ScheduleOne.NPCs;
using ScheduleOne.Product;
using ScheduleOne.Quests;
using ScheduleOne.UI.Handover;
using ScheduleOne.UI.Phone;
using ScheduleOne.UI.Phone.Messages;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static ScheduleOne.UI.Handover.HandoverScreen;
using ScheduleOne.UI.Phone.ProductManagerApp;
using static DealOptimizer_Mono.UIUtils;

[assembly: MelonInfo(typeof(DealOptimizer_Mono.Core), "DealOptimizer_Mono", "1.3.2", "xyrilyn, zocke1r", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DealOptimizer_Mono
{
    public partial class Core : MelonMod
    {
        private bool listening = false;

        private GUIStyle counterofferUIDisplayTextStyle;
        private static string counterofferUIDisplayText = "";

        private static class DealCalculator
        {
            public static float CalculateSuccessProbability(OfferData customerOfferData, OfferData playerOfferData, bool printCalcToConsole = false)
            {
                return CalculateSuccessProbability(
                    customerOfferData.Customer,
                    customerOfferData.Product,
                    customerOfferData.Quantity,
                    customerOfferData.Price,
                    playerOfferData.Product,
                    playerOfferData.Quantity,
                    playerOfferData.Price,
                    printCalcToConsole
                    );
            }
            
            public static float CalculateSuccessProbability(Customer customer, ProductDefinition customerProduct, int customerQuantity, float customerPrice, ProductDefinition playerProduct, int playerQuantity, float playerPrice, bool printCalcToConsole = false)
            {
                CustomerData customerData = customer.CustomerData;

                float valueProposition = Customer.GetValueProposition(customerProduct, customerPrice / customerQuantity);
                float productEnjoyment = customer.GetProductEnjoyment(playerProduct, customerData.Standards.GetCorrespondingQuality());
                float enjoymentNormalized = Mathf.InverseLerp(-1f, 1f, productEnjoyment);
                float newValueProposition = Customer.GetValueProposition(playerProduct, playerPrice / playerQuantity);
                float quantityRatio = Mathf.Pow(playerQuantity / customerQuantity, 0.6f);
                float quantityMultiplier = Mathf.Lerp(0f, 2f, quantityRatio * 0.5f);
                float penaltyMultiplier = Mathf.Lerp(1f, 0f, Mathf.Abs(quantityMultiplier - 1f));

                if (printCalcToConsole)
                {
                    Melon<Core>.Logger.Msg($"Product Enjoyment              : {productEnjoyment}");
                    Melon<Core>.Logger.Msg($"Product Enjoyment (Normalized) : {enjoymentNormalized}");
                    Melon<Core>.Logger.Msg($"Customer Value Proposition     : {valueProposition}");
                    Melon<Core>.Logger.Msg($"Player Value Proposition       : {newValueProposition}");
                    Melon<Core>.Logger.Msg($"Qty Change Penalty Multiplier  : {penaltyMultiplier}");
                }

                if (newValueProposition * penaltyMultiplier > valueProposition)
                {
                    if (printCalcToConsole)
                    {
                        Melon<Core>.Logger.Msg($"Guaranteed Success - Player Value Proposition > Customer Value Proposition");
                    }
                    return 1f;
                }
                if (newValueProposition < 0.12f)
                {
                    if (printCalcToConsole)
                    {
                        Melon<Core>.Logger.Msg($"Guaranteed Failure - Player Value Proposition Too Low (<0.12)");
                    }
                    return 0f;
                }

                float customerWeightedValue = productEnjoyment * valueProposition;
                float proposedWeightedValue = enjoymentNormalized * penaltyMultiplier * newValueProposition;

                if (printCalcToConsole)
                {
                    Melon<Core>.Logger.Msg($"Customer Product Enjoyment-Weighted Value Proposition : {customerWeightedValue}");
                    Melon<Core>.Logger.Msg($"Player Product Enjoyment-Weighted Value Proposition   : {proposedWeightedValue}");
                }

                if (proposedWeightedValue > customerWeightedValue)
                {
                    if (printCalcToConsole)
                    {
                        Melon<Core>.Logger.Msg($"Guaranteed Success - Player Product Enjoyment-Weighted Value Proposition > Customer Product Enjoyment-Weighted Value Proposition");
                    }
                    return 1f;
                }

                float valueDifference = customerWeightedValue - proposedWeightedValue;
                float threshold = Mathf.Lerp(0f, 1f, valueDifference / 0.2f);
                float bonus = Mathf.Lerp(0f, 0.2f, Mathf.Max(customer.CurrentAddiction, customer.NPC.RelationData.NormalizedRelationDelta));
                float thresholdMinusBonus = threshold - bonus;

                if (printCalcToConsole)
                {
                    Melon<Core>.Logger.Msg($"Success Threshold : {threshold}");
                    Melon<Core>.Logger.Msg($"Bonus             : {bonus}");
                    Melon<Core>.Logger.Msg($"Threshold - Bonus : {thresholdMinusBonus}");
                }

                return Mathf.Clamp01((0.9f - thresholdMinusBonus) / 0.9f);
            }

            public static (float maxSpend, float dailyAverage) CalculateSpendingLimits(Customer customer, bool printCalcToConsole = false)
            {
                CustomerData customerData = customer.CustomerData;

                float normalizedRelationship = customer.NPC.RelationData.RelationDelta / 5f;
                float adjustedWeeklySpend = customerData.GetAdjustedWeeklySpend(normalizedRelationship);
                var orderDays = customerData.GetOrderDays(customer.CurrentAddiction, normalizedRelationship);
                float dailyAverage = adjustedWeeklySpend / orderDays.Count;
                float maxSpend = dailyAverage * 3f;

                if (printCalcToConsole)
                {
                    string[] days = new string[orderDays.Count];
                    for (int i = 0; i < orderDays.Count; i++)
                    {
                        days[i] = orderDays[i].ToString();
                    }

                    Melon<Core>.Logger.Msg($"Weekly Spend (Base)    : {Mathf.Lerp(customerData.MinWeeklySpend, customerData.MaxWeeklySpend, normalizedRelationship)}");
                    Melon<Core>.Logger.Msg($"Order Limit Multiplier : {LevelManager.GetOrderLimitMultiplier(NetworkSingleton<LevelManager>.Instance.GetFullRank())}");
                    Melon<Core>.Logger.Msg($"Adjusted Weekly Spend  : {adjustedWeeklySpend}");
                    Melon<Core>.Logger.Msg($"Order Days             : {String.Join(", ", days)}");
                    Melon<Core>.Logger.Msg($"Order Days Count       : {orderDays.Count}");
                    Melon<Core>.Logger.Msg($"Daily Average          : {dailyAverage}");
                    Melon<Core>.Logger.Msg($"Max Spend              : {maxSpend}");
                }

                return (maxSpend, dailyAverage);
            }

            public static int FindOptimalPrice(Customer customer, ProductDefinition customerProduct, int customerQuantity, float customerPrice, ProductDefinition playerProduct, int playerQuantity, float playerPrice, float maxSpend)
            {
                int low = (int)playerPrice;
                int high = (int)maxSpend;
                int bestPrice = (int)playerPrice;
                int maxIterations = 30;
                int iterations = 0;
                float minSuccessProbability = GetConfigurationInt(Options.MinimumSuccessProbability) / 100f;

                bool printCalcToConsole = GetConfigurationFlag(Options.PrintCalculationsToConsole);
                if (printCalcToConsole)
                {
                    Melon<Core>.Logger.Msg($"Binary Search Start - Price: {playerPrice}, MaxSpend: {maxSpend}, Quantity: {playerQuantity}, MinProbability: {minSuccessProbability}");
                }

                while (iterations < maxIterations && low < high)
                {
                    int mid = (low + high) / 2;
                    float probability = CalculateSuccessProbability(customer, customerProduct, customerQuantity, customerPrice, playerProduct, playerQuantity, mid);
                    bool success = probability >= minSuccessProbability;

                    if (printCalcToConsole)
                    {
                        Melon<Core>.Logger.Msg($"Binary Search Iteration {iterations}:");
                        Melon<Core>.Logger.Msg($"  Testing price: {mid}");
                        Melon<Core>.Logger.Msg($"  Success probability: {probability}");
                        Melon<Core>.Logger.Msg($"  Success: {success}");
                        Melon<Core>.Logger.Msg($"  Range: low={low}, high={high}");
                    }

                    if (success)
                    {
                        low = mid + 1;
                        if (low == high)
                        {
                            bestPrice = CalculateSuccessProbability(customer, customerProduct, customerQuantity, customerPrice, playerProduct, playerQuantity, mid + 1) > minSuccessProbability ? mid + 1 : mid;
                            break;
                        }
                    }
                    else
                    {
                        bestPrice = mid;
                        high = mid;
                    }
                    iterations++;
                }

                if (printCalcToConsole)
                {
                    Melon<Core>.Logger.Msg($"Binary Search Complete:");
                    Melon<Core>.Logger.Msg($"  Final bestPrice: {bestPrice}");
                    Melon<Core>.Logger.Msg($"  Final range: low={low}, high={high}");
                }

                return bestPrice;
            }
        }

        private static class CustomerHelper
        {
            public static Customer GetCustomerFromConversation(MSGConversation conversation)
            {
                string contactName = conversation.contactName;
                var unlockedCustomers = Customer.UnlockedCustomers;
                return unlockedCustomers.Find((Predicate<Customer>)((cust) =>
                {
                    NPC npc = cust.NPC;
                    return npc.fullName == contactName;
                }));
            }

            public static Customer GetCustomerFromMessagesApp(MessagesApp messagesApp)
            {
                return GetCustomerFromConversation(messagesApp.currentConversation);
            }
        }

        private static class DisplayHelper
        {
            public static void UpdateCounterofferUIDisplayText(string text, string additionalText)
            {
                counterofferUIDisplayText = text + '\n' + additionalText;
            }

            public static string GenerateAdditionalText(OfferData offerData, Decimal maxSpend)
            {
                bool isPricePerUnitDisplayEnabled = GetConfigurationFlag(Options.PricePerUnitDisplay);
                bool isMaxDailySpendDisplayEnabled = GetConfigurationFlag(Options.MaximumDailySpendDisplay);

                if (!isPricePerUnitDisplayEnabled && !isMaxDailySpendDisplayEnabled)
                {
                    return "";
                }

                StringBuilder sb = new StringBuilder();

                if (isPricePerUnitDisplayEnabled)
                {
                    if (sb.Length != 0)
                    {
                        sb.AppendLine();
                    }
                    sb.Append($"Price per unit: {offerData.Price / offerData.Quantity}");
                }

                if (isMaxDailySpendDisplayEnabled)
                {
                    if (sb.Length != 0)
                    {
                        sb.AppendLine();
                    }
                    sb.Append($"Max Spend: {maxSpend}");
                }

                return sb.ToString();
            }
        }

        [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.Open))]
        static class CounterofferInterfacePostfixOpen
        {
            static void Postfix(ProductDefinition product, int quantity, float price, MSGConversation _conversation, Action<ProductDefinition, int, float> _orderConfirmedCallback)
            {
                if (!GetConfigurationFlag(Options.CounterofferOptimizationEnabled))
                {
                    return;
                }

                OptimizeInitialOfferThenEvaluate(product, quantity, price, _conversation);
            }
        }

        [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.ChangeQuantity))]
        static class CounterofferInterfacePostfixChangeQuantity
        {
            static void Postfix(int change)
            {
                if (!GetConfigurationFlag(Options.CounterofferOptimizationEnabled))
                {
                    return;
                }

                OptimizeCounterofferThenEvaluate();
            }
        }

        [HarmonyPatch(typeof(CounterOfferProductSelector), "ProductSelected")]
        static class CounterOfferProductSelectorPostfixProductSelected
        {
            static void Postfix(ProductDefinition def)
            {
                if (!GetConfigurationFlag(Options.CounterofferOptimizationEnabled))
                {
                    return;
                }

                OptimizeCounterofferThenEvaluate();
            }
        }

        [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.ChangePrice))]
        static class CounterofferInterfacePostfixChangePrice
        {
            static void Postfix(float change)
            {
                if (!GetConfigurationFlag(Options.CounterofferOptimizationEnabled))
                {
                    return;
                }

                EvaluateAfterPriceChange();
            }
        }

        private static bool IsEnvironmentValid()
        {
            MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;
            if (messagesApp == null || !messagesApp.CounterofferInterface.IsOpen)
            {
                return false;
            }

            try
            {
                Customer customer = CustomerHelper.GetCustomerFromMessagesApp(messagesApp);
            }
            catch (Exception ex)
            {
                Melon<Core>.Logger.Error("Exception occurred when checking environment", ex);
                return false;
            }

            return true;
        }

        private static void OptimizeInitialOfferThenEvaluate(ProductDefinition product, int quantity, float price, MSGConversation _conversation)
        {
            bool valid = IsEnvironmentValid();
            if (!valid) return;

            MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;

            Customer customer = CustomerHelper.GetCustomerFromConversation(_conversation);
            var (maxSpend, _) = DealCalculator.CalculateSpendingLimits(customer);

            OfferData customerContractData = GetCustomerContractData(customer);

            int optimalPrice = DealCalculator.FindOptimalPrice(
                customer,
                customerContractData.Product, customerContractData.Quantity, customerContractData.Price,
                product, quantity, price,
                maxSpend
                );

            messagesApp.CounterofferInterface.ChangePrice(optimalPrice - (int)price); // Will trigger offer evaluation
        }


        private static void OptimizeCounterofferThenEvaluate()
        {
            bool valid = IsEnvironmentValid();
            if (!valid) return;

            MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;

            Customer customer = CustomerHelper.GetCustomerFromMessagesApp(messagesApp);
            var (maxSpend, _) = DealCalculator.CalculateSpendingLimits(customer);

            CounterofferInterface counterofferInterface = messagesApp.CounterofferInterface;
            string priceText = counterofferInterface.PriceInput.text;
            float currentPrice = priceText == "" ? 0 : float.Parse(priceText);

            OfferData customerContractData = GetCustomerContractData(customer);
            OfferData currentOfferData = GetPlayerOfferData();

            int optimalPrice = DealCalculator.FindOptimalPrice(
                customer,
                customerContractData.Product, customerContractData.Quantity, customerContractData.Price,
                currentOfferData.Product, currentOfferData.Quantity, currentOfferData.Product.MarketValue * currentOfferData.Quantity * 0.5f,
                maxSpend
                );

            counterofferInterface.ChangePrice(optimalPrice - (int)currentPrice); // Will trigger offer evaluation
        }

        private static void EvaluateAfterPriceChange()
        {
            bool valid = IsEnvironmentValid();
            if (!valid) return;

            OfferData offerData = GetPlayerOfferData();
            EvaluateCounterOffer(offerData);
        }

        private class OfferData(Customer customer, ProductDefinition product, int quantity, float price)
        {
            public Customer Customer { get; } = customer;
            public ProductDefinition Product { get; } = product;
            public int Quantity { get; } = quantity;
            public float Price { get; } = price;
        }

        private static OfferData GetCustomerContractData(Customer customer)
        {
            ProductDefinition product = Registry.GetItem<ProductDefinition>(customer.OfferedContractInfo.Products.entries[0].ProductID);
            int quantity = customer.OfferedContractInfo.Products.entries[0].Quantity;
            float price = customer.OfferedContractInfo.Payment;

            return new OfferData(customer, product, quantity, price);
        }

        private static OfferData GetPlayerOfferData()
        {
            MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;

            Customer customer = CustomerHelper.GetCustomerFromMessagesApp(messagesApp);

            string quantityText = messagesApp.CounterofferInterface.ProductLabel.text;
            int quantity = int.Parse(quantityText.Split("x ")[0]);

            ProductDefinition product = Traverse.Create(messagesApp.CounterofferInterface).Field("selectedProduct").GetValue<ProductDefinition>();
            if (product == null)
            {
                // Traverse.Create can fail (reason unknown), so we use this as a fallback
                string productName = quantityText.Split("x ")[1];
                product = ProductManager.DiscoveredProducts.Find((Predicate<ProductDefinition>)((product) =>
                {
                    return product.Name == productName;
                }));
            }

            string priceText = messagesApp.CounterofferInterface.PriceInput.text;
            float price = priceText == "" ? 0 : float.Parse(priceText);

            return new OfferData(customer, product, quantity, price);
        }

        private static bool EvaluateCounterOffer(OfferData offerData)
        {
            bool printCalcToConsole = GetConfigurationFlag(Options.PrintCalculationsToConsole);
            if (printCalcToConsole)
            {
                Melon<Core>.Logger.Msg("========================= Evaluation Start =========================");
                Melon<Core>.Logger.Msg($"Customer Name: {offerData.Customer.name}");
                Melon<Core>.Logger.Msg($"Product: {offerData.Product.ID}");
                Melon<Core>.Logger.Msg($"Quantity: {offerData.Quantity}");
                Melon<Core>.Logger.Msg($"Price: {offerData.Price}");
            }

            var (maxSpend, dailyAverage) = DealCalculator.CalculateSpendingLimits(offerData.Customer, printCalcToConsole);
            decimal maxSpendDecimal = Math.Round((decimal)maxSpend, 2);

            if (offerData.Price >= maxSpend)
            {
                if (printCalcToConsole)
                {
                    Melon<Core>.Logger.Msg($"Guaranteed Failure: Exceeded Max Spend ({maxSpendDecimal})");
                    Melon<Core>.Logger.Msg("========================== Evaluation End ==========================");
                }
                DisplayHelper.UpdateCounterofferUIDisplayText("Guaranteed Failure", $"Exceeded Max Spend ({maxSpendDecimal})");
                return false;
            }

            OfferData customerContractData = GetCustomerContractData(offerData.Customer);
            float probability = DealCalculator.CalculateSuccessProbability(customerContractData, offerData, printCalcToConsole);
            decimal probabilityPercent = Math.Round((decimal)(probability * 100), 3);

            if (printCalcToConsole)
            {
                Melon<Core>.Logger.Msg("========================== Evaluation End ==========================");
            }

            if (probability >= 1f)
            {
                DisplayHelper.UpdateCounterofferUIDisplayText("Guaranteed Success", DisplayHelper.GenerateAdditionalText(offerData, maxSpendDecimal));
                return true;
            }
            else if (probability <= 0f)
            {
                DisplayHelper.UpdateCounterofferUIDisplayText("Guaranteed Failure", DisplayHelper.GenerateAdditionalText(offerData, maxSpendDecimal));
                return false;
            }
            else
            {
                DisplayHelper.UpdateCounterofferUIDisplayText($"Probability of success: {probabilityPercent}%", DisplayHelper.GenerateAdditionalText(offerData, maxSpendDecimal));
                return UnityEngine.Random.Range(0f, 1f) < probability;
            }
        }

        private static bool DefinitelyLessThan(float a, float b)
        {
            return (b - a) > ((Math.Abs(a) < Math.Abs(b) ? Math.Abs(b) : Math.Abs(a)) * 1E-15);
        }

        [HarmonyPatch(typeof(HandoverScreen), nameof(HandoverScreen.Open))]
        static class HandoverScreenPostfixOpen
        {
            static void Postfix(Contract contract, Customer customer, EMode mode, Action<EHandoverOutcome, List<ItemInstance>, float> callback, Func<List<ItemInstance>, float, float> successChanceMethod)
            {
                if (!GetConfigurationFlag(Options.StreetDealOptimizationEnabled))
                {
                    return;
                }

                var (maxSpend, _) = DealCalculator.CalculateSpendingLimits(customer);
                HandoverScreen handoverScreen = Singleton<HandoverScreen>.Instance;

                handoverScreen.PriceSelector.SetPrice((int)maxSpend);

                float price = handoverScreen.PriceSelector.Price;
                if (!DefinitelyLessThan(price, maxSpend))
                {
                    handoverScreen.PriceSelector.SetPrice((int)maxSpend - 1);
                }
            }
        }

        public override void OnInitializeMelon()
        {
            InitializeCounterofferUI();

            SetupConfiguration();

            LoggerInstance.Msg("Initialized Mod");
        }

        private void InitializeCounterofferUI()
        {
            counterofferUIDisplayTextStyle = new GUIStyle("label");
            counterofferUIDisplayTextStyle.fontSize = 18;
            counterofferUIDisplayTextStyle.normal.textColor = Color.black;
            counterofferUIDisplayTextStyle.normal.background = Texture2D.whiteTexture;
            counterofferUIDisplayTextStyle.alignment = TextAnchor.MiddleCenter;
        }

        public override void OnGUI()
        {
            bool gameStarted = SceneManager.GetActiveScene() != null && SceneManager.GetActiveScene().name == "Main";
            if (!gameStarted)
            {
                return;
            }

            Phone phone = PlayerSingleton<Phone>.Instance;
            bool phoneOpened = phone != null && phone.IsOpen;
            if (!phoneOpened)
            {
                return;
            }

            bool homeScreenOpened = PlayerSingleton<HomeScreen>.Instance.isOpen;
            bool counterofferInterfaceOpened = PlayerSingleton<MessagesApp>.Instance != null && PlayerSingleton<MessagesApp>.Instance.CounterofferInterface.IsOpen;

            if (GetConfigurationFlag(Options.CounterofferOptimizationEnabled) && GetConfigurationFlag(Options.CounterofferUIEnabled) && !homeScreenOpened && counterofferInterfaceOpened)
            {
                GUI.Label(new Rect((Screen.width / 2) - 190, (Screen.height / 2) - 250, 380, 70), counterofferUIDisplayText, counterofferUIDisplayTextStyle);
            }

            bool productManagerAppOpened = ProductManagerApp.Instance.isOpen;

            if (GetConfigurationFlag(Options.ProductEvaluatorEnabled) && !homeScreenOpened && productManagerAppOpened && selectedProductForEvaluation != null)
            {
                InitializeProductManagerAppUI();

                GUI.BeginGroup(productWindow);
                Color originalColor = GUI.backgroundColor; // Store original
                GUI.backgroundColor = new Color(0, 0, 0);
                productWindow = ClampToScreen(GUI.Window(515, productWindow, DrawProductWindowContents, "Product Evaluator (experimental)", productInfoWindowStyle));
                GUI.backgroundColor = originalColor; // Reset original
                GUI.EndGroup();
            }
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (listening)
            {
                listening = false;
            }
        }

        public override void OnUpdate()
        {
            bool gameStarted = SceneManager.GetActiveScene() != null && SceneManager.GetActiveScene().name == "Main";
            MessagesApp messagesAppInstance = PlayerSingleton<MessagesApp>.Instance;

            if (gameStarted && !listening && messagesAppInstance != null)
            {
                Subscribe();
            }
        }

        private UnityAction Subscribe()
        {
            UnityAction<string> changeListener = (UnityAction<string>)((string unused) =>
            {
                bool valid = IsEnvironmentValid();
                if (!valid) return;

                OfferData offerData = GetPlayerOfferData();
                EvaluateCounterOffer(offerData);
            });

            MessagesApp messagesAppInstance = PlayerSingleton<MessagesApp>.Instance;
            messagesAppInstance.CounterofferInterface.PriceInput.onValueChanged.AddListener(changeListener);

            LoggerInstance.Msg("Attached listener");
            listening = true;

            return (UnityAction)(() => PlayerSingleton<MessagesApp>.Instance.CounterofferInterface.PriceInput.onValueChanged.RemoveListener(changeListener));
        }
    }
}