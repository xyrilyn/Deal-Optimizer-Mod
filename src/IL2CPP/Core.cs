using System.Text;
using HarmonyLib;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Levelling;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Handover;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Messages;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static Il2CppScheduleOne.UI.Handover.HandoverScreen;

[assembly: MelonInfo(typeof(DealOptimizer_IL2CPP.Core), "DealOptimizer_IL2CPP", "1.2.1", "xyrilyn, zocke1r", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DealOptimizer_IL2CPP
{
    public partial class Core : MelonMod
    {
        private bool listening = false;

        private GUIStyle displayTextStyle;
        private static string counterOfferDisplayText = "";

        private static class DealCalculator
        {
            public static float CalculateSuccessProbability(Customer customer, ProductDefinition product, int quantity, float price, bool printCalcToConsole = false)
            {
                CustomerData customerData = customer.CustomerData;

                float valueProposition = Customer.GetValueProposition(Registry.GetItem<ProductDefinition>(customer.OfferedContractInfo.Products.entries[0].ProductID),
                    customer.OfferedContractInfo.Payment / (float)customer.OfferedContractInfo.Products.entries[0].Quantity);
                float productEnjoyment = customer.GetProductEnjoyment(product, customerData.Standards.GetCorrespondingQuality());
                float enjoymentNormalized = Mathf.InverseLerp(-1f, 1f, productEnjoyment);
                float newValueProposition = Customer.GetValueProposition(product, price / (float)quantity);
                float quantityRatio = Mathf.Pow((float)quantity / (float)customer.OfferedContractInfo.Products.entries[0].Quantity, 0.6f);
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

            public static int FindOptimalPrice(Customer customer, ProductDefinition product, int quantity, float currentPrice, float maxSpend, float minSuccessProbability = 0.98f)
            {
                int low = (int)currentPrice;
                int high = (int)maxSpend;
                int bestFailingPrice = (int)currentPrice;
                int maxIterations = 30;
                int iterations = 0;

                bool printCalcToConsole = GetConfigurationFlag(Flags.PrintCalculationsToConsole);
                if (printCalcToConsole)
                {
                    Melon<Core>.Logger.Msg($"Binary Search Start - Price: {currentPrice}, MaxSpend: {maxSpend}, Quantity: {quantity}, MinProbability: {minSuccessProbability}");
                }

                while (iterations < maxIterations && low < high)
                {
                    int mid = (low + high) / 2;
                    float probability = CalculateSuccessProbability(customer, product, quantity, mid);
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
                            bestFailingPrice = CalculateSuccessProbability(customer, product, quantity, mid + 1) > minSuccessProbability ? mid + 1 : mid;
                            break;
                        }
                    }
                    else
                    {
                        bestFailingPrice = mid;
                        high = mid;
                    }
                    iterations++;
                }

                if (printCalcToConsole)
                {
                    Melon<Core>.Logger.Msg($"Binary Search Complete:");
                    Melon<Core>.Logger.Msg($"  Final bestFailingPrice: {bestFailingPrice}");
                    Melon<Core>.Logger.Msg($"  Final range: low={low}, high={high}");
                }

                return bestFailingPrice;
            }
        }

        private static class CustomerHelper
        {
            public static Customer GetCustomerFromConversation(MSGConversation conversation)
            {
                string contactName = conversation.contactName;
                var unlockedCustomers = Customer.UnlockedCustomers;
                return unlockedCustomers.Find((Il2CppSystem.Predicate<Customer>)((cust) =>
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
            public static void UpdateCounterOfferDisplayText(string text, string additionalText)
            {
                counterOfferDisplayText = text + '\n' + additionalText;
            }

            public static string GenerateAdditionalText(OfferData offerData, Decimal maxSpend)
            {
                bool isPricePerUnitDisplayEnabled = GetConfigurationFlag(Flags.PricePerUnitDisplay);
                bool isMaxDailySpendDisplayEnabled = GetConfigurationFlag(Flags.MaximumDailySpendDisplay);

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
                OptimizeInitialOfferThenEvaluate(product, quantity, price, _conversation);
            }
        }

        [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.ChangeQuantity))]
        static class CounterofferInterfacePostfixChangeQuantity
        {
            static void Postfix(int change)
            {
                OptimizeCounterofferThenEvaluate();
            }
        }

        //[HarmonyPatch(typeof(CounterOfferProductSelector), "ProductSelected")]
        //static class CounterOfferProductSelectorPostfixProductSelected
        //{
        //    static void Postfix(ProductDefinition def)
        //    {
        //        OptimizeCounterofferThenEvaluate();
        //    }
        //}

        [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.ChangePrice))]
        static class CounterofferInterfacePostfixChangePrice
        {
            static void Postfix(float change)
            {
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

            CounterofferInterface counterofferInterface = messagesApp.CounterofferInterface;
            string priceText = counterofferInterface.PriceInput.text;
            float currentPrice = priceText == "" ? 0 : float.Parse(priceText);

            int optimalPrice = DealCalculator.FindOptimalPrice(customer, product, quantity, price, maxSpend);

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

            OfferData currentOfferData = GetCurrentOfferData();

            int optimalPrice = DealCalculator.FindOptimalPrice(
                customer,
                currentOfferData.Product,
                currentOfferData.Quantity,
                currentOfferData.Product.MarketValue * currentOfferData.Quantity * 0.5f,
                maxSpend
                );

            counterofferInterface.ChangePrice(optimalPrice - (int)currentPrice); // Will trigger offer evaluation
        }

        private static void EvaluateAfterPriceChange()
        {
            bool valid = IsEnvironmentValid();
            if (!valid) return;

            OfferData offerData = GetCurrentOfferData();
            EvaluateCounterOffer(offerData);
        }

        private class OfferData
        {
            public Customer Customer { get; }
            public ProductDefinition Product { get; }
            public int Quantity { get; }
            public float Price { get; }

            public OfferData(Customer customer, ProductDefinition product, int quantity, float price)
            {
                Customer = customer;
                Product = product;
                Quantity = quantity;
                Price = price;
            }
        }

        private static OfferData GetCurrentOfferData()
        {
            MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;

            Customer customer = CustomerHelper.GetCustomerFromMessagesApp(messagesApp);

            string quantityText = messagesApp.CounterofferInterface.ProductLabel.text;
            int quantity = int.Parse(quantityText.Split("x ")[0]);

            string productName = quantityText.Split("x ")[1];
            ProductDefinition product = ProductManager.DiscoveredProducts.Find((Il2CppSystem.Predicate<ProductDefinition>)((product) =>
            {
                return product.Name == productName;
            }));

            string priceText = messagesApp.CounterofferInterface.PriceInput.text;
            float price = priceText == "" ? 0 : float.Parse(priceText);

            return new OfferData(customer, product, quantity, price);
        }

        private static bool EvaluateCounterOffer(OfferData offerData)
        {
            bool printCalcToConsole = GetConfigurationFlag(Flags.PrintCalculationsToConsole);
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
                DisplayHelper.UpdateCounterOfferDisplayText("Guaranteed Failure", $"Exceeded Max Spend ({maxSpendDecimal})");
                return false;
            }

            float probability = DealCalculator.CalculateSuccessProbability(offerData.Customer, offerData.Product, offerData.Quantity, offerData.Price, printCalcToConsole);
            decimal probabilityPercent = Math.Round((decimal)(probability * 100), 3);

            if (printCalcToConsole)
            {
                Melon<Core>.Logger.Msg("========================== Evaluation End ==========================");
            }

            if (probability >= 1f)
            {
                DisplayHelper.UpdateCounterOfferDisplayText("Guaranteed Success", DisplayHelper.GenerateAdditionalText(offerData, maxSpendDecimal));
                return true;
            }
            else if (probability <= 0f)
            {
                DisplayHelper.UpdateCounterOfferDisplayText("Guaranteed Failure", DisplayHelper.GenerateAdditionalText(offerData, maxSpendDecimal));
                return false;
            }
            else
            {
                DisplayHelper.UpdateCounterOfferDisplayText($"Probability of success: {probabilityPercent}%", DisplayHelper.GenerateAdditionalText(offerData, maxSpendDecimal));
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
            displayTextStyle = new GUIStyle("label");
            displayTextStyle.fontSize = 18;
            displayTextStyle.normal.textColor = Color.black;
            displayTextStyle.normal.background = Texture2D.whiteTexture;
            displayTextStyle.alignment = TextAnchor.MiddleCenter;

            SetupConfiguration();

            LoggerInstance.Msg("Initialized Mod");
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

            if (!homeScreenOpened && counterofferInterfaceOpened)
            {
                GUI.Label(new Rect((Screen.width / 2) - 190, (Screen.height / 2) - 250, 380, 70), counterOfferDisplayText, displayTextStyle);
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

                OfferData offerData = GetCurrentOfferData();
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