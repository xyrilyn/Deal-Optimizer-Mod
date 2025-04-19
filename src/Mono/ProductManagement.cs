using System.Text;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Product;
using ScheduleOne.UI.Phone.ProductManagerApp;
using HarmonyLib;
using UnityEngine;

namespace DealOptimizer_Mono
{
	public partial class Core
	{
        private static readonly float WINDOW_WIDTH = 500f;
        private static readonly float WINDOW_HEIGHT = 600f;
        private static readonly float WINDOW_X = (Screen.width / 4f) - (WINDOW_WIDTH);
        private static readonly float WINDOW_Y = (Screen.height / 2f) - (WINDOW_HEIGHT * 0.5f);

        private bool stylesInitialized = false;
        private Rect productWindow;
        private GUIStyle productInfoWindowStyle;
        private GUIStyle productInfoHeaderStyle;
        private GUIStyle productInfoSubHeaderStyle;
        private GUIStyle productInfoTextFieldStyle;
        private GUIStyle productInfoBodyStyle;
        private static float productInfoBodyHeight = 0f;
        private Vector2 productInfoScrollPosition = Vector2.zero;

        private static int toolbarMode = 0;
        private string[] toolbarModes = { "Product Acceptance", "Customer Contracts" };

        private static int toolbarSorting = 0;
        private string[] toolbarSortings = { "Name", "Dealer", "Payment" };

        private static string productInfoBody = "";

        private static ProductDefinition selectedProductForEvaluation = null;
        private static EQuality selectedQualityForEvaluation = EQuality.Standard;
        private static string selectedPriceForEvaluationText = "";
        private static string productMarketValue = "";

        private void InitializeProductManagerAppUI()
        {
            if (stylesInitialized) return;

            productWindow = new Rect(WINDOW_X, WINDOW_Y, WINDOW_WIDTH, WINDOW_HEIGHT);

            productInfoWindowStyle = new GUIStyle(GUI.skin.window);

            productInfoHeaderStyle = new GUIStyle("label");
            productInfoHeaderStyle.fontSize = 24;
            productInfoHeaderStyle.normal.textColor = Color.white;
            productInfoHeaderStyle.alignment = TextAnchor.MiddleLeft;

            productInfoSubHeaderStyle = new GUIStyle("label");
            productInfoSubHeaderStyle.fontSize = 18;
            productInfoSubHeaderStyle.normal.textColor = Color.white;
            productInfoSubHeaderStyle.alignment = TextAnchor.MiddleLeft;

            productInfoTextFieldStyle = new GUIStyle(GUI.skin.textField);
            productInfoTextFieldStyle.fontSize = 18;
            productInfoTextFieldStyle.alignment = TextAnchor.MiddleCenter;

            productInfoBodyStyle = new GUIStyle("label");
            productInfoBodyStyle.fontSize = 18;
            productInfoBodyStyle.normal.textColor = Color.white;
            productInfoBodyStyle.alignment = TextAnchor.UpperLeft;
            productInfoBodyStyle.richText = true;

            stylesInitialized = true;
        }

        void DrawProductWindowContents(int windowId)
        {
            float windowBorder = 2;
            float offsetY = 20;
            float paddingX = 20;
            float scrollbarOffset = 10;

            productInfoScrollPosition = GUI.BeginScrollView(
                new Rect(0, offsetY, WINDOW_WIDTH - windowBorder, WINDOW_HEIGHT - offsetY - windowBorder),
                productInfoScrollPosition,
                new Rect(0, 0, WINDOW_WIDTH - (paddingX * 2), 220 + productInfoBodyHeight)
                );

            GUILayout.BeginArea(new Rect(paddingX, 0, WINDOW_WIDTH - (paddingX * 2) - scrollbarOffset, 220 + productInfoBodyHeight));
            GUILayout.BeginVertical(GUILayout.Width(WINDOW_WIDTH - (paddingX * 2) - scrollbarOffset), GUILayout.ExpandHeight(true));

            GUILayout.BeginHorizontal("Header");
            GUILayout.Label(selectedProductForEvaluation.name, productInfoHeaderStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal("Selected Quality");
            GUILayout.Label($"Selected Quality: {selectedQualityForEvaluation}", productInfoSubHeaderStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal("Quality Buttons");
            GUILayout.FlexibleSpace();
            Color originalColor = GUI.backgroundColor; // Store original
            GUI.backgroundColor = ItemQuality.GetColor(EQuality.Trash);
            if (GUILayout.Button("Trash", GUILayout.Width(80), GUILayout.Height(26)))
            {
                selectedQualityForEvaluation = EQuality.Trash;
                EvaluateProductForAllCustomers(selectedProductForEvaluation);
            }
            GUI.backgroundColor = ItemQuality.GetColor(EQuality.Poor);
            if (GUILayout.Button("Poor", GUILayout.Width(80), GUILayout.Height(26)))
            {
                selectedQualityForEvaluation = EQuality.Poor;
                EvaluateProductForAllCustomers(selectedProductForEvaluation);
            }
            GUI.backgroundColor = ItemQuality.GetColor(EQuality.Standard);
            if (GUILayout.Button("Standard", GUILayout.Width(80), GUILayout.Height(26)))
            {
                selectedQualityForEvaluation = EQuality.Standard;
                EvaluateProductForAllCustomers(selectedProductForEvaluation);
            }
            GUI.backgroundColor = ItemQuality.GetColor(EQuality.Premium);
            if (GUILayout.Button("Premium", GUILayout.Width(80), GUILayout.Height(26)))
            {
                selectedQualityForEvaluation = EQuality.Premium;
                EvaluateProductForAllCustomers(selectedProductForEvaluation);
            }
            GUI.backgroundColor = ItemQuality.GetColor(EQuality.Heavenly);
            if (GUILayout.Button("Heavenly", GUILayout.Width(80), GUILayout.Height(26)))
            {
                selectedQualityForEvaluation = EQuality.Heavenly;
                EvaluateProductForAllCustomers(selectedProductForEvaluation);
            }
            GUI.backgroundColor = originalColor; // Reset original
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal("Market Value and Price");
            GUILayout.Label($"Market Value: ${productMarketValue}", productInfoSubHeaderStyle);
            GUILayout.BeginHorizontal("Asking Price");
            GUILayout.FlexibleSpace();
            GUILayout.Label("Price: ", productInfoSubHeaderStyle);
            selectedPriceForEvaluationText = GUILayout.TextField(selectedPriceForEvaluationText, 3, productInfoTextFieldStyle, GUILayout.Width(80), GUILayout.Height(26));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal("Mode");
            GUILayout.Label("Mode: ", productInfoSubHeaderStyle, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            toolbarMode = GUILayout.Toolbar(toolbarMode, toolbarModes, GUILayout.Width(280), GUILayout.Height(26));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal("Sorting");
            GUILayout.Label("Sort By: ", productInfoSubHeaderStyle, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            toolbarSorting = GUILayout.Toolbar(toolbarSorting, toolbarSortings, GUILayout.Width(280), GUILayout.Height(26));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (GUI.changed)
            {
                EvaluateProductForAllCustomers(selectedProductForEvaluation);
            }

            productInfoBodyHeight = productInfoBodyStyle.CalcHeight(new GUIContent(productInfoBody), WINDOW_WIDTH - (paddingX * 2));
            GUILayout.Label(productInfoBody, productInfoBodyStyle, GUILayout.Width(WINDOW_WIDTH - (paddingX * 2)), GUILayout.Height(productInfoBodyHeight));
            

            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndArea();

            GUI.EndScrollView();

            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        [HarmonyPatch(typeof(ProductManagerApp), nameof(ProductManagerApp.SelectProduct))]
        static class ProductManagerAppPostfixSelectProduct
        {
            static void Postfix(ProductEntry entry)
            {
                if (!GetConfigurationFlag(Flags.ProductEvaluatorEnabled))
                {
                    return;
                }

                if (entry == null)
                {
                    selectedProductForEvaluation = null;
                    productMarketValue = "";
                    selectedPriceForEvaluationText = "";
                    productInfoBody = "";
                    return;
                }

                selectedProductForEvaluation = entry.Definition;
                productMarketValue = entry.Definition.MarketValue.ToString();
                selectedPriceForEvaluationText = entry.Definition.Price.ToString();
                EvaluateProductForAllCustomers(selectedProductForEvaluation);
            }
        }

        private static float CalculateCustomerProductAppeal(Customer customer, ProductDefinition product, EQuality quality, float? price = null)
        {
            float productEnjoyment = customer.GetProductEnjoyment(product, quality);
            float num2 = (price ?? product.Price) / product.MarketValue;
            float num3 = Mathf.Lerp(1f, -1f, num2 / 2f);
            float value = productEnjoyment + num3;
            return value;
        }

        private static (float, int) CalculateCustomerContract(Customer customer, ProductDefinition product, float? price = null, Dealer dealer = null)
        {
            CustomerData customerData = customer.CustomerData;
            EQuality correspondingQuality = customerData.Standards.GetCorrespondingQuality();

            float productPrice = (price ?? product.Price);
            float productAppeal = CalculateCustomerProductAppeal(customer, product, correspondingQuality, productPrice);

            float normalizedRelationship = customer.NPC.RelationData.RelationDelta / 5f;
            var orderDaysCount = 7;
            if (dealer == null)
            {
                var orderDays = customerData.GetOrderDays(customer.CurrentAddiction, normalizedRelationship);
                orderDaysCount = orderDays.Count;
            }

            float adjustedWeeklySpend = customerData.GetAdjustedWeeklySpend(normalizedRelationship) / orderDaysCount;
            float productEnjoyment = customer.GetProductEnjoyment(product, correspondingQuality);
            float adjustedPrice = productPrice * Mathf.Lerp(0.66f, 1.5f, productEnjoyment);
            adjustedWeeklySpend *= Mathf.Lerp(0.66f, 1.5f, productEnjoyment);

            int quantityToBuy = Mathf.RoundToInt(adjustedWeeklySpend / productPrice);
            quantityToBuy = Mathf.Clamp(quantityToBuy, 1, 1000);

            if (quantityToBuy >= 14)
            {
                quantityToBuy = Mathf.RoundToInt((float)(quantityToBuy / 5)) * 5;
            }

            float payment = (float)(Mathf.RoundToInt(adjustedPrice * (float)quantityToBuy / 5f) * 5);

            return (payment, quantityToBuy);
        }

        private static (bool, string) CheckCustomerAcceptance(CustomerCandidate customerCandidate)
        {
            if (customerCandidate.ProductAppeal < Customer.MIN_ORDER_APPEAL)
            {
                return (false, $"Product appeal too low");
            }

            EQuality minQuality = customerCandidate.Customer.CustomerData.Standards.GetCorrespondingQuality();
            if (selectedQualityForEvaluation < minQuality)
            {
                string rgbaStr = ColorUtility.ToHtmlStringRGB(ItemQuality.GetColor(minQuality));
                return (false, $"Requires <color=#{rgbaStr}>{minQuality}</color> quality or better");
            }

            return (true, "");
        }

        private static (bool, string) CheckDealerAcceptance(CustomerCandidate customerCandidate)
        {
            Dealer dealer = customerCandidate.Customer.AssignedDealer;
            if (dealer == null)
            {
                return (true, "");
            }

            int quantity = customerCandidate.Quantity;
            EQuality quality = customerCandidate.Customer.CustomerData.Standards.GetCorrespondingQuality();

            int dealerProductCount = dealer.GetProductCount(selectedProductForEvaluation.ID, quality, EQuality.Heavenly);

            if (quantity > dealerProductCount)
            {
                return (false, $"{dealer.name} does not have enough product to sell");
            }

            return (true, "");
        }

        private class CustomerCandidate
        {
            public Customer Customer { get; }
            public float ProductAppeal { get; }
            public float Payment { get; }
            public int Quantity {  get; }

            public CustomerCandidate(Customer customer, float productAppeal, float payment, int quantity)
            {
                Customer = customer;
                ProductAppeal = productAppeal;
                Payment = payment;
                Quantity = quantity;
            }
        }

        private static void EvaluateProductForAllCustomers(ProductDefinition product)
        {
            productInfoBody = ""; // Reset content

            float price = selectedPriceForEvaluationText == "" ? product.Price : int.Parse(selectedPriceForEvaluationText);

            List<CustomerCandidate> customerCandidates = new List<CustomerCandidate>(Customer.UnlockedCustomers.Count);
            var unlockedCustomers = Customer.UnlockedCustomers;
            foreach (Customer customer in unlockedCustomers)
            {
                float customerProductAppeal = CalculateCustomerProductAppeal(customer, product, selectedQualityForEvaluation, price);

                float payment = -1f;
                int quantity = -1;
                switch (toolbarMode)
                {
                    case 0:
                        (payment, quantity) = CalculateCustomerContract(customer, product, price);
                        break;
                    case 1:
                        (payment, quantity) = CalculateCustomerContract(customer, product, price, customer.AssignedDealer);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                customerCandidates.Add(new CustomerCandidate(customer, customerProductAppeal, payment, quantity));
            }

            IOrderedEnumerable<CustomerCandidate> sortedCustomerCandidates;
            switch (toolbarSorting)
            {
                case 0:
                    sortedCustomerCandidates = customerCandidates.OrderBy(
                        custCand => custCand.Customer.name);
                    break;
                case 1:
                    sortedCustomerCandidates = customerCandidates.OrderBy(
                        custCand => custCand.Customer.AssignedDealer == null ? "Player" : custCand.Customer.AssignedDealer.name);
                    break;
                case 2:
                    sortedCustomerCandidates = customerCandidates.OrderByDescending(
                        custCand => custCand.Payment);
                    break;
                default:
                    sortedCustomerCandidates = customerCandidates.OrderByDescending(
                        custCand => custCand.ProductAppeal);
                    break;
            }

            StringBuilder sb = new StringBuilder();

            foreach (CustomerCandidate sortedCustomer in sortedCustomerCandidates)
            {
                Customer customer = sortedCustomer.Customer;

                //var (maxSpend, _) = DealCalculator.CalculateSpendingLimits(customer);
                //int optimalPrice = DealCalculator.FindOptimalPrice(
                //    customer,
                //    selectedProductForEvaluation,
                //    1,
                //    selectedProductForEvaluation.MarketValue,
                //    selectedProductForEvaluation,
                //    1,
                //    price * 0.5f,
                //    maxSpend
                //    );

                switch (toolbarMode)
                {
                    case 0:
                        (bool valid, string reason) = CheckCustomerAcceptance(sortedCustomer);
                        if (!valid)
                        {
                            sb.AppendLine($"{sortedCustomer.Customer.name} : {reason}");
                            break;
                        }

                        (valid, reason) = CheckDealerAcceptance(sortedCustomer);
                        if (!valid)
                        {
                            sb.AppendLine($"{sortedCustomer.Customer.name} : {reason}");
                            break;
                        }
                        break;
                    case 1:
                        Dealer dealer = sortedCustomer.Customer.AssignedDealer;
                        if (dealer == null)
                        {
                            sb.AppendLine($"{sortedCustomer.Customer.name} : {sortedCustomer.Quantity}x for ${sortedCustomer.Payment}"); // from player
                        }
                        else
                        {
                            sb.AppendLine($"{sortedCustomer.Customer.name} : {sortedCustomer.Quantity}x for ${sortedCustomer.Payment} from {dealer.name}");
                        }
                        break;
                    case 2:
                        sb.AppendLine($"{sortedCustomer.Customer.name} : {sortedCustomer.Quantity}x for ${sortedCustomer.Payment}");
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            if (toolbarMode == 0 && sb.ToString() == "")
            {
                productInfoBody = "No issues found at the current asking price and quality";
                return;
            }

            productInfoBody = sb.ToString();
        }
    }
}
