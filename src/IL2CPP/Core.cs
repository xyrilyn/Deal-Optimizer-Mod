using MelonLoader;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using System.Text;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.GameTime;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.UI.Handover;
using Il2CppScheduleOne.Levelling;
using static Il2CppScheduleOne.UI.Handover.HandoverScreen;

[assembly: MelonInfo(typeof(DealOptimizer_IL2CPP.Core), "DealOptimizer_IL2CPP", "1.0.0", "xyrilyn", null)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace DealOptimizer_IL2CPP
{
    public class Core : MelonMod
    {
        private bool listening = false;

        private GUIStyle displayTextStyle;
        private static string counterOfferDisplayText = "";

        [HarmonyPatch(typeof(CounterofferInterface), nameof(CounterofferInterface.Open))]
        static class CounterofferInterfacePostOpenPatch
        {
            static void Postfix(ProductDefinition product, int quantity, float price, MSGConversation _conversation, Action<ProductDefinition, int, float> _orderConfirmedCallback)
            {
                MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;

                // A long-winded way of retrieving the Customer object
                string contactName = _conversation.contactName;
                NPC sender = messagesApp.currentConversation.sender;
                Il2CppSystem.Collections.Generic.List<Customer> unlockedCustomers = Customer.UnlockedCustomers;
                Customer customer = unlockedCustomers.Find((Il2CppSystem.Predicate<Customer>)((cust) =>
                {
                    NPC npc = cust.NPC;
                    return npc.fullName == contactName;
                }));

                CustomerData customerData = customer.CustomerData;

                float adjustedWeeklySpend = customerData.GetAdjustedWeeklySpend(customer.NPC.RelationData.RelationDelta / 5f);
                Il2CppSystem.Collections.Generic.List<EDay> orderDays = customerData.GetOrderDays(customer.CurrentAddiction, customer.NPC.RelationData.RelationDelta / 5f);
                float num = adjustedWeeklySpend / orderDays.Count;
                float maxSpend = num * 3f;

                CounterofferInterface counterofferInterface = messagesApp.CounterofferInterface;

                // Change price without notifying listeners
                counterofferInterface.ChangePrice((int)(maxSpend - price));

                // Check price against maxSpend again
                string priceText = counterofferInterface.PriceInput.text;
                if (!DefinitelyLessThan(float.Parse(priceText), maxSpend))
                {
                    counterofferInterface.ChangePrice(-1);
                }

                // Check value proposition and increment quantity if value prop is too low to try and increase it
                int iterations = 0;
                bool success = false;
                int quantityToTry = quantity;

                while (!success && iterations < 5)
                {
                    success = checkValueProposition(customer, product, quantityToTry, maxSpend);
                    if (!success)
                    {
                        counterofferInterface.ChangeQuantity(1);
                    }
                    quantityToTry += 1;
                    iterations++;
                }

                // If still unsuccessful after N iterations, reset quantity but leave price as calculated max
                if (!success)
                {
                    counterofferInterface.ChangeQuantity(-counterofferInterface.MaxQuantity);
                }
            }
        }

        private static bool DefinitelyLessThan(float a, float b)
        {
            return (b - a) > ((Math.Abs(a) < Math.Abs(b) ? Math.Abs(b) : Math.Abs(a)) * 1E-15);
        }

        private static bool checkValueProposition(Customer customer, ProductDefinition product, int quantity, float price)
        {
            CustomerData customerData = customer.CustomerData;

            float valueProposition = Customer.GetValueProposition(Registry.GetItem<ProductDefinition>(customer.OfferedContractInfo.Products.entries[0].ProductID), customer.OfferedContractInfo.Payment / (float)customer.OfferedContractInfo.Products.entries[0].Quantity);
            float productEnjoyment = customer.GetProductEnjoyment(product, customerData.Standards.GetCorrespondingQuality());
            float num2 = Mathf.InverseLerp(-1f, 1f, productEnjoyment);
            float valueProposition2 = Customer.GetValueProposition(product, price / (float)quantity);
            float num3 = Mathf.Pow((float)quantity / (float)customer.OfferedContractInfo.Products.entries[0].Quantity, 0.6f);
            float num4 = Mathf.Lerp(0f, 2f, num3 * 0.5f);
            float num5 = Mathf.Lerp(1f, 0f, Mathf.Abs(num4 - 1f));

            if (valueProposition2 * num5 > valueProposition)
            {
                return true;
            }
            if (valueProposition2 < 0.12f)
            {
                return false;
            }

            float num6 = productEnjoyment * valueProposition;
            float num7 = num2 * num5 * valueProposition2;

            if (num7 > num6)
            {
                return true;
            }
            else
            {
                return false;
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
                OfferPriceChangeCheckNoLog();
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
                GUI.Label(new Rect((Screen.width / 2) - 190, (Screen.height / 2) - 250, 380, 50), counterOfferDisplayText, displayTextStyle);
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

        private static void UpdateCounterOfferDisplayText(string text, string reasonText)
        {
            counterOfferDisplayText = text + '\n' + reasonText;
        }

        class OfferData(Customer customer, ProductDefinition product, int quantity, float price)
        {
            public Customer Customer { get; } = customer;
            public ProductDefinition Product { get; } = product;
            public int Quantity { get; } = quantity;
            public float Price { get; } = price;
        }

        private static OfferData GetOfferData()
        {
            MessagesApp messagesApp = PlayerSingleton<MessagesApp>.Instance;

            string contactName = messagesApp.currentConversation.contactName;
            NPC sender = messagesApp.currentConversation.sender;
            Il2CppSystem.Collections.Generic.List<Customer> unlockedCustomers = Customer.UnlockedCustomers;
            Customer customer = unlockedCustomers.Find((Il2CppSystem.Predicate<Customer>)((cust) =>
            {
                NPC npc = cust.NPC;
                return npc.fullName == contactName;
            }));

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
            Customer customer = offerData.Customer;
            ProductDefinition product = offerData.Product;
            int quantity = offerData.Quantity;
            float price = offerData.Price;
            CustomerData customerData = customer.CustomerData;

            float adjustedWeeklySpend = customerData.GetAdjustedWeeklySpend(customer.NPC.RelationData.RelationDelta / 5f);
            Il2CppSystem.Collections.Generic.List<EDay> orderDays = customerData.GetOrderDays(customer.CurrentAddiction, customer.NPC.RelationData.RelationDelta / 5f);
            float num = adjustedWeeklySpend / orderDays.Count;
            decimal maxSpend = Math.Round((decimal)(num * 3f), 2);

            if (price >= num * 3f)
            {
                UpdateCounterOfferDisplayText("Guaranteed Failure", $"Exceeded Max Spend ({maxSpend})");
                return false;
            }

            float valueProposition = Customer.GetValueProposition(Registry.GetItem<ProductDefinition>(customer.OfferedContractInfo.Products.entries[0].ProductID), customer.OfferedContractInfo.Payment / (float)customer.OfferedContractInfo.Products.entries[0].Quantity);
            float productEnjoyment = customer.GetProductEnjoyment(product, customerData.Standards.GetCorrespondingQuality());
            float num2 = Mathf.InverseLerp(-1f, 1f, productEnjoyment);
            float valueProposition2 = Customer.GetValueProposition(product, price / (float)quantity);
            float num3 = Mathf.Pow((float)quantity / (float)customer.OfferedContractInfo.Products.entries[0].Quantity, 0.6f);
            float num4 = Mathf.Lerp(0f, 2f, num3 * 0.5f);
            float num5 = Mathf.Lerp(1f, 0f, Mathf.Abs(num4 - 1f));

            if (valueProposition2 * num5 > valueProposition)
            {
                UpdateCounterOfferDisplayText($"Guaranteed Success", "");
                return true;
            }
            if (valueProposition2 < 0.12f)
            {
                UpdateCounterOfferDisplayText("Guaranteed Failure", $"Value Proposition Too Low ({valueProposition2})");
                return false;
            }

            float num6 = productEnjoyment * valueProposition;
            float num7 = num2 * num5 * valueProposition2;

            if (num7 > num6)
            {
                UpdateCounterOfferDisplayText("Guaranteed Success", "");
                return true;
            }

            float num8 = num6 - num7;
            float num9 = Mathf.Lerp(0f, 1f, num8 / 0.2f);
            float t = Mathf.Max(customer.CurrentAddiction, customer.NPC.RelationData.NormalizedRelationDelta);
            float num10 = Mathf.Lerp(0f, 0.2f, t);

            float thresholdMinusBonus = num9 - num10;

            if (thresholdMinusBonus < 0)
            {
                UpdateCounterOfferDisplayText("Guaranteed Success", "");
            }
            else
            {
                decimal probability = Math.Round((decimal)((0.9 - thresholdMinusBonus) / 0.9 * 100), 3);
                UpdateCounterOfferDisplayText($"Probability of success: {probability}%", "");
            }

            return UnityEngine.Random.Range(0f, 0.9f) + num10 > num9;
        }

        private bool EvaluateCounterOffer(StringBuilder stringBuilder, OfferData offerData)
        {
            Customer customer = offerData.Customer;
            ProductDefinition product = offerData.Product;
            int quantity = offerData.Quantity;
            float price = offerData.Price;
            CustomerData customerData = customer.CustomerData;

            FullRank rank = NetworkSingleton<LevelManager>.Instance.GetFullRank();
            float orderLimitMultiplier = LevelManager.GetOrderLimitMultiplier(rank);

            float adjustedWeeklySpend = customerData.GetAdjustedWeeklySpend(customer.NPC.RelationData.RelationDelta / 5f);
            Il2CppSystem.Collections.Generic.List<EDay> orderDays = customerData.GetOrderDays(customer.CurrentAddiction, customer.NPC.RelationData.RelationDelta / 5f);
            float num = adjustedWeeklySpend / orderDays.Count;

            stringBuilder.Append('\n');
            stringBuilder.Append($"Rank + Tier: {rank.Rank} {rank.Tier}\n");
            stringBuilder.Append($"Order Limit Multiplier: {orderLimitMultiplier}\n");
            stringBuilder.Append($"Adjusted Weekly Spend: {adjustedWeeklySpend}\n");
            stringBuilder.Append($"Order Days: {orderDays.Count}\n");
            stringBuilder.Append($"Average Daily Spend: {num}\n");
            decimal maxSpend = Math.Round((decimal)(num * 3f), 2);
            stringBuilder.Append($"Daily Spend Threshold (3x Avg): {maxSpend}\n");

            if (price >= num * 3f)
            {
                stringBuilder.Append("\nGuaranteed Failure - order must be less than 3x average daily spend\n");
                UpdateCounterOfferDisplayText("Guaranteed Failure", $"Exceeded Max Spend ({maxSpend})");
                return false;
            }

            float valueProposition = Customer.GetValueProposition(Registry.GetItem<ProductDefinition>(customer.OfferedContractInfo.Products.entries[0].ProductID), customer.OfferedContractInfo.Payment / (float)customer.OfferedContractInfo.Products.entries[0].Quantity);
            float productEnjoyment = customer.GetProductEnjoyment(product, customerData.Standards.GetCorrespondingQuality());
            float num2 = Mathf.InverseLerp(-1f, 1f, productEnjoyment);
            float valueProposition2 = Customer.GetValueProposition(product, price / (float)quantity);
            float num3 = Mathf.Pow((float)quantity / (float)customer.OfferedContractInfo.Products.entries[0].Quantity, 0.6f);
            float num4 = Mathf.Lerp(0f, 2f, num3 * 0.5f);
            float num5 = Mathf.Lerp(1f, 0f, Mathf.Abs(num4 - 1f));

            stringBuilder.Append('\n');
            stringBuilder.Append($"Product Enjoyment: {productEnjoyment}\n");
            stringBuilder.Append($"Customer Value Proposition: {valueProposition}\n");
            stringBuilder.Append($"Player Value Proposition (Base): {valueProposition2}\n");
            stringBuilder.Append($"  Player Qty / Cust Qty: {num3}\n");
            stringBuilder.Append($"  Change Penalty: {num4}\n");
            stringBuilder.Append($"  Penalty Multiplier (smaller is worse; 1.0 is equivalent): {num5}\n");
            stringBuilder.Append($"Dealer Value Proposition (Modified): {valueProposition2 * num5}\n");

            if (valueProposition2 * num5 > valueProposition)
            {
                stringBuilder.Append("\nGuaranteed Success - player's offer is equal or better than customer's offer\n");
                UpdateCounterOfferDisplayText($"Guaranteed Success", "");
                return true;
            }
            if (valueProposition2 < 0.12f)
            {
                stringBuilder.Append("\nGuaranteed Failure - player's offer is <12% value of customer's offer\n");
                UpdateCounterOfferDisplayText("Guaranteed Failure", $"Value Proposition Too Low ({valueProposition2})");
                return false;
            }

            float num6 = productEnjoyment * valueProposition;
            float num7 = num2 * num5 * valueProposition2;

            stringBuilder.Append('\n');
            stringBuilder.Append("Include: Product Enjoyment As Multiplier\n");
            stringBuilder.Append($"Customer Value Proposition: {num6}\n");
            stringBuilder.Append($"Player Value Proposition: {num7}\n");

            if (num7 > num6)
            {
                stringBuilder.Append("\nGuaranteed Success - player's value prop is higher\n");
                UpdateCounterOfferDisplayText("Guaranteed Success", "");
                return true;
            }

            float num8 = num6 - num7;
            float num9 = Mathf.Lerp(0f, 1f, num8 / 0.2f);
            float t = Mathf.Max(customer.CurrentAddiction, customer.NPC.RelationData.NormalizedRelationDelta);
            float num10 = Mathf.Lerp(0f, 0.2f, t);

            stringBuilder.Append('\n');
            stringBuilder.Append($"Bonus (Addiction OR Norm. Relationship): {num10}\n");
            stringBuilder.Append($"Threshold: {num9}\n");
            float thresholdMinusBonus = num9 - num10;
            stringBuilder.Append($"Threshold - Bonus: {thresholdMinusBonus}\n");

            if (thresholdMinusBonus < 0)
            {
                stringBuilder.Append("\nGuaranteed Success - bonus guarantees success\n");
                UpdateCounterOfferDisplayText("Guaranteed Success", "");
            }
            else
            {
                decimal probability = Math.Round((decimal)((0.9 - thresholdMinusBonus) / 0.9 * 100), 3);
                stringBuilder.Append($"\nProbability of success: {probability}%\n");
                UpdateCounterOfferDisplayText($"Probability of success: {probability}%", "");
            }

            return UnityEngine.Random.Range(0f, 0.9f) + num10 > num9;
        }

        [HarmonyPatch(typeof(HandoverScreen), nameof(HandoverScreen.Open))]
        static class HandoverScreenPostOpenPatch
        {
            static void Postfix(Contract contract, Customer customer, EMode mode, Action<EHandoverOutcome, List<ItemInstance>, float> callback, Func<List<ItemInstance>, float, float> successChanceMethod)
            {
                CustomerData customerData = customer.CustomerData;

                float adjustedWeeklySpend = customerData.GetAdjustedWeeklySpend(customer.NPC.RelationData.RelationDelta / 5f);
                Il2CppSystem.Collections.Generic.List<EDay> orderDays = customerData.GetOrderDays(customer.CurrentAddiction, customer.NPC.RelationData.RelationDelta / 5f);
                float num = adjustedWeeklySpend / orderDays.Count;
                int maxSpend = (int)(num * 3f);

                HandoverScreen handoverScreen = Singleton<HandoverScreen>.Instance;

                // Change price without notifying listeners
                handoverScreen.PriceSelector.SetPrice(maxSpend);

                // Check price against maxSpend again
                float price = handoverScreen.PriceSelector.Price;
                if (!DefinitelyLessThan(price, maxSpend))
                {
                    handoverScreen.PriceSelector.SetPrice(maxSpend - 1);
                }
            }
        }
    }
}