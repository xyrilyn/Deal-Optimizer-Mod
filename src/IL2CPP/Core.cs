using System.Text;
using HarmonyLib;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
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

[assembly: MelonInfo(typeof(DealOptimizer_IL2CPP.Core), "DealOptimizer_IL2CPP", "2.0.2", "zocke1r", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DealOptimizer_IL2CPP
{
    public class Core : MelonMod
    {
        private bool listening = false;

        private GUIStyle displayTextStyle;
        private static string counterOfferDisplayText = "";

        private static class DealCalculator
        {
            public static float CalculateSuccessProbability(Customer customer, ProductDefinition product, int quantity, float price)
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

                float customerWeightedValue = productEnjoyment * valueProposition;
                float proposedWeightedValue = enjoymentNormalized * penaltyMultiplier * newValueProposition;

                float valueDifference = customerWeightedValue - proposedWeightedValue;
                float threshold = Mathf.Lerp(0f, 1f, valueDifference / 0.2f);
                float bonus = Mathf.Lerp(0f, 0.2f, Mathf.Max(customer.CurrentAddiction, customer.NPC.RelationData.NormalizedRelationDelta));

                float thresholdMinusBonus = threshold - bonus;
                return Mathf.Clamp01((0.9f - thresholdMinusBonus) / 0.9f);
            }

            public static (float maxSpend, float dailyAverage) CalculateSpendingLimits(Customer customer)
            {
                CustomerData customerData = customer.CustomerData;
                float adjustedWeeklySpend = customerData.GetAdjustedWeeklySpend(customer.NPC.RelationData.RelationDelta / 5f);
                var orderDays = customerData.GetOrderDays(customer.CurrentAddiction, customer.NPC.RelationData.RelationDelta / 5f);
                float dailyAverage = adjustedWeeklySpend / orderDays.Count;
                float maxSpend = dailyAverage * 3f;
                return (maxSpend, dailyAverage);
            }

            public static int FindOptimalPrice(Customer customer, ProductDefinition product, int quantity, float currentPrice, float maxSpend, float minSuccessProbability = 0.98f)
            {
                int low = (int)currentPrice;
                int high = (int)maxSpend;
                int bestFailingPrice = (int)currentPrice;
                int maxIterations = 20;
                int iterations = 0;

                Melon<Core>.Logger.Msg($"Binary Search Start - Price: {currentPrice}, MaxSpend: {maxSpend}, Quantity: {quantity}, MinProbability: {minSuccessProbability}");

                while (iterations < maxIterations && low < high)
                {
                    int mid = (low + high) / 2;
                    float probability = CalculateSuccessProbability(customer, product, quantity, mid);
                    bool success = probability >= minSuccessProbability;

                    Melon<Core>.Logger.Msg($"Binary Search Iteration {iterations}:");
                    Melon<Core>.Logger.Msg($"  Testing price: {mid}");
                    Melon<Core>.Logger.Msg($"  Success probability: {probability}");
                    Melon<Core>.Logger.Msg($"  Success: {success}");
                    Melon<Core>.Logger.Msg($"  Range: low={low}, high={high}");

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

                Melon<Core>.Logger.Msg($"Binary Search Complete:");
                Melon<Core>.Logger.Msg($"  Final bestFailingPrice: {bestFailingPrice}");
                Melon<Core>.Logger.Msg($"  Final range: low={low}, high={high}");

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
            public static void UpdateCounterOfferDisplayText(string text, string reasonText)
            {
                counterOfferDisplayText = text + '\n' + reasonText;
            }
        }

        [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.Open))]
        static class CounterofferInterfacePostOpenPatch
        {
            static void Postfix(ProductDefinition product, int quantity, float price, MSGConversation _conversation, Action<ProductDefinition, int, float> _orderConfirmedCallback)
            {
                MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;
                Customer customer = CustomerHelper.GetCustomerFromConversation(_conversation);
                var (maxSpend, _) = DealCalculator.CalculateSpendingLimits(customer);

                int optimalPrice = DealCalculator.FindOptimalPrice(customer, product, quantity, price, maxSpend);

                if (optimalPrice > price)
                {
                    messagesApp.CounterofferInterface.ChangePrice(optimalPrice - (int)price);
                }
            }
        }

        [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.ChangePrice))]
        static class CounterofferInterfacePostChangePricePatch
        {
            static void Postfix(float change)
            {
                OfferPriceChangeCheckNoLog();
            }
        }

        [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.ChangeQuantity))]
        static class CounterofferInterfacePostChangeQuantityPatch
        {
            static void Postfix(int change)
            {
                MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;
                if (messagesApp == null || !messagesApp.CounterofferInterface.IsOpen) return;

                Customer customer = CustomerHelper.GetCustomerFromMessagesApp(messagesApp);
                var (maxSpend, _) = DealCalculator.CalculateSpendingLimits(customer);

                CounterofferInterface counterofferInterface = messagesApp.CounterofferInterface;
                string quantityText = counterofferInterface.ProductLabel.text;
                int quantity = int.Parse(quantityText.Split("x ")[0]);
                string priceText = counterofferInterface.PriceInput.text;
                float currentPrice = priceText == "" ? 0 : float.Parse(priceText);

                ContractInfo contractOffer = customer.OfferedContractInfo;
                ProductDefinition product = Registry.GetItem<ProductDefinition>(contractOffer.Products.entries[0].ProductID);

                int optimalPrice = DealCalculator.FindOptimalPrice(customer, product, quantity, currentPrice * 0.25f, maxSpend);

                if (optimalPrice > currentPrice || change < 0)
                {
                    counterofferInterface.ChangePrice(optimalPrice - (int)currentPrice);
                }
            }
        }

        private static bool DefinitelyLessThan(float a, float b)
        {
            return (b - a) > ((Math.Abs(a) < Math.Abs(b) ? Math.Abs(b) : Math.Abs(a)) * 1E-15);
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

        private static OfferData GetOfferData()
        {
            MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;
            Customer customer = CustomerHelper.GetCustomerFromMessagesApp(messagesApp);
            ContractInfo contractOffer = customer.OfferedContractInfo;
            ProductDefinition product = Registry.GetItem<ProductDefinition>(contractOffer.Products.entries[0].ProductID);

            string quantityText = messagesApp.CounterofferInterface.ProductLabel.text;
            int quantity = int.Parse(quantityText.Split("x ")[0]);

            string priceText = messagesApp.CounterofferInterface.PriceInput.text;
            float price = priceText == "" ? 0 : float.Parse(priceText);

            return new OfferData(customer, product, quantity, price);
        }

        public static void OfferPriceChangeCheckNoLog()
        {
            OfferData offerData = GetOfferData();
            EvaluateCounterOffer(offerData);
        }

        private void OfferPriceChangeCheckWithLog()
        {
            OfferData offerData = GetOfferData();
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append('\n');
            stringBuilder.Append($"Customer Name: {offerData.Customer.name}\n");
            stringBuilder.Append($"Product: {offerData.Product.ID}\n");
            stringBuilder.Append($"Quantity: {offerData.Quantity}\n");
            stringBuilder.Append($"Price: {offerData.Price}\n");

            EvaluateCounterOffer(stringBuilder, offerData);
            LoggerInstance.Msg(stringBuilder.ToString());
        }

        private static bool EvaluateCounterOffer(OfferData offerData)
        {
            var (maxSpend, dailyAverage) = DealCalculator.CalculateSpendingLimits(offerData.Customer);
            decimal maxSpendDecimal = Math.Round((decimal)maxSpend, 2);

            if (offerData.Price >= maxSpend)
            {
                DisplayHelper.UpdateCounterOfferDisplayText("Guaranteed Failure", $"Exceeded Max Spend ({maxSpendDecimal})");
                return false;
            }

            float probability = DealCalculator.CalculateSuccessProbability(offerData.Customer, offerData.Product, offerData.Quantity, offerData.Price);
            decimal probabilityPercent = Math.Round((decimal)(probability * 100), 3);

            if (probability >= 0.95f)
            {
                DisplayHelper.UpdateCounterOfferDisplayText("Guaranteed Success", $"Price per unit: {offerData.Price / offerData.Quantity}\nMax Spend: {maxSpendDecimal}");
                return true;
            }
            else if (probability <= 0.05f)
            {
                DisplayHelper.UpdateCounterOfferDisplayText("Guaranteed Failure", $"Price per unit: {offerData.Price / offerData.Quantity}\nMax Spend: {maxSpendDecimal}");
                return false;
            }
            else
            {
                DisplayHelper.UpdateCounterOfferDisplayText($"Probability of success: {probabilityPercent}%", $"Price per unit: {offerData.Price / offerData.Quantity}\nMax Spend: {maxSpendDecimal}");
                return UnityEngine.Random.Range(0f, 1f) < probability;
            }
        }

        private bool EvaluateCounterOffer(StringBuilder stringBuilder, OfferData offerData)
        {
            var (maxSpend, dailyAverage) = DealCalculator.CalculateSpendingLimits(offerData.Customer);
            decimal maxSpendDecimal = Math.Round((decimal)maxSpend, 2);

            stringBuilder.Append('\n');
            stringBuilder.Append($"Adjusted Weekly Spend: {offerData.Customer.CustomerData.GetAdjustedWeeklySpend(offerData.Customer.NPC.RelationData.RelationDelta / 5f)}\n");
            stringBuilder.Append($"Order Days: {offerData.Customer.CustomerData.GetOrderDays(offerData.Customer.CurrentAddiction, offerData.Customer.NPC.RelationData.RelationDelta / 5f).Count}\n");
            stringBuilder.Append($"Average Daily Spend: {dailyAverage}\n");
            stringBuilder.Append($"Daily Spend Threshold (3x Avg): {maxSpendDecimal}\n");

            if (offerData.Price >= maxSpend)
            {
                stringBuilder.Append("\nGuaranteed Failure - order must be less than 3x average daily spend\n");
                DisplayHelper.UpdateCounterOfferDisplayText("Guaranteed Failure", $"Exceeded Max Spend ({maxSpendDecimal})");
                return false;
            }

            float probability = DealCalculator.CalculateSuccessProbability(offerData.Customer, offerData.Product, offerData.Quantity, offerData.Price);
            decimal probabilityPercent = Math.Round((decimal)(probability * 100), 3);

            stringBuilder.Append('\n');
            stringBuilder.Append($"Success Probability: {probabilityPercent}%\n");

            if (probability >= 0.95f)
            {
                stringBuilder.Append("\nGuaranteed Success - high probability of success\n");
                DisplayHelper.UpdateCounterOfferDisplayText("Guaranteed Success", "");
                return true;
            }
            else if (probability <= 0.05f)
            {
                stringBuilder.Append("\nGuaranteed Failure - low probability of success\n");
                DisplayHelper.UpdateCounterOfferDisplayText("Guaranteed Failure", "");
                return false;
            }
            else
            {
                stringBuilder.Append($"\nProbability of success: {probabilityPercent}%\n");
                DisplayHelper.UpdateCounterOfferDisplayText($"Probability of success: {probabilityPercent}%", "");
                return UnityEngine.Random.Range(0f, 1f) < probability;
            }
        }

        [HarmonyPatch(typeof(HandoverScreen), nameof(HandoverScreen.Open))]
        static class HandoverScreenPostOpenPatch
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
            UnityAction<string> changeListener = (UnityAction<string>)((string unused) => OfferPriceChangeCheckWithLog());

            MessagesApp messagesAppInstance = PlayerSingleton<MessagesApp>.Instance;
            messagesAppInstance.CounterofferInterface.PriceInput.onValueChanged.AddListener(changeListener);

            LoggerInstance.Msg("Attached listener");
            listening = true;

            return (UnityAction)(() => PlayerSingleton<MessagesApp>.Instance.CounterofferInterface.PriceInput.onValueChanged.RemoveListener(changeListener));
        }
    }
}