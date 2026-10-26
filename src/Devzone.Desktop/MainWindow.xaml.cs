using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Devzone.Application.Catalog;
using Devzone.Application.Customers;
using Devzone.Application.Purchasing;
using Devzone.Application.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Devzone.Desktop;

public partial class MainWindow : Window
{
    private static readonly string[] NavigationItems = ["Dashboard", "Products", "Categories", "Stock Movements", "Purchases", "Customers"];
    private readonly AuthenticatedUser _user;
    private readonly IServiceProvider _services;
    private readonly string _businessType;
    private readonly string _currencyCode;
    private Button? _selectedNavigation;
    private readonly Dictionary<string, Button> _navigationButtons = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow(AuthenticatedUser user, string companyName, string businessType, string currencyCode, IServiceProvider services)
    {
        InitializeComponent();
        _user = user;
        _services = services;
        _businessType = businessType;
        _currencyCode = currencyCode;
        CompanyLabel.Text = companyName;
        UserLabel.Text = $"{user.Username} · Owner";

        foreach (var item in NavigationItems)
        {
            var button = new Button { Content = item, Style = (Style)FindResource("NavButton") };
            button.Click += (_, _) => Navigate(item, button);
            NavigationPanel.Children.Add(button);
            _navigationButtons[item] = button;
        }

        Loaded += async (_, _) =>
        {
            try
            {
                await ShowDashboardAsync();
            }
            catch (Exception exception)
            {
                ShowDashboardLoadError(exception);
            }
        };
    }

    private async void Navigate(string page, Button selected)
    {
        if (_selectedNavigation is not null)
            _selectedNavigation.Background = Brushes.Transparent;
        _selectedNavigation = selected;
        selected.Background = (Brush)FindResource("SecondaryBackgroundBrush");
        PageTitle.Text = page;
        PageHeading.Text = page;
        PageDescription.Text = page switch
        {
            "Dashboard" => "Live inventory summary for your company.",
            "Products" => "Create, find, edit, and archive your products.",
            "Categories" => "Organize products into categories.",
            "Purchases" => "Receive supplier stock and track purchase costs.",
            "Customers" => "Manage customer profiles, balances, receipts, charges, and account ledgers.",
            _ => "Review and record every stock adjustment."
        };
        StatusMessage.Text = string.Empty;
        StatusMessage.Foreground = (Brush)FindResource("SecondaryTextBrush");
        try
        {
            switch (page)
            {
                case "Dashboard": await ShowDashboardAsync(); break;
                case "Products": await ShowProductsAsync(); break;
                case "Categories": await ShowCategoriesAsync(); break;
                case "Stock Movements": await ShowStockMovementsAsync(); break;
                case "Purchases": await ShowPurchasesAsync(); break;
                case "Customers": await ShowCustomersAsync(); break;
            }
        }
        catch (Exception exception)
        {
            ShowError(exception.Message);
        }
    }

    private async Task ShowDashboardAsync()
    {
        MainContent.Children.Clear();
        MainContent.Children.Add(new TextBlock
        {
            Text = "Loading dashboard…",
            Foreground = (Brush)FindResource("SecondaryTextBrush"),
            Margin = new Thickness(0, 4, 0, 12)
        });
        var workflow = GetBusinessWorkflow(_businessType);
        PageDescription.Text = $"{_businessType} workspace · {workflow.Description}";
        using var scope = _services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IProductCatalogService>();
        var summary = await catalog.GetDashboardSummaryAsync(_user.CompanyId);
        var products = await catalog.SearchProductsAsync(_user.CompanyId);
        var categories = await catalog.GetCategoriesAsync(_user.CompanyId);
        var purchases = await scope.ServiceProvider.GetRequiredService<IPurchasingService>()
            .GetPurchasesAsync(_user.CompanyId);
        var customers = await scope.ServiceProvider.GetRequiredService<ICustomerService>()
            .SearchAsync(_user.CompanyId);
        MainContent.Children.Clear();
        var cards = new UniformGrid { Columns = 2, Rows = 2, Margin = new Thickness(0, 0, 0, 18) };
        foreach (var metric in GetBusinessMetrics(_businessType, summary, products, categories, purchases, customers))
            cards.Children.Add(CreateMetricCard(metric.Title, metric.Value, metric.Detail));
        MainContent.Children.Add(cards);
        var quickLinks = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        foreach (var page in workflow.Pages)
        {
            var quickLink = CreateActionButton(page);
            quickLink.Margin = new Thickness(0, 0, 8, 8);
            quickLink.Click += (_, _) =>
            {
                if (_navigationButtons.TryGetValue(page, out var target))
                    Navigate(page, target);
            };
            quickLinks.Children.Add(quickLink);
        }
        MainContent.Children.Add(quickLinks);
        var note = new Border { Style = (Style)FindResource("CardBorder"), Background = (Brush)FindResource("SecondaryBackgroundBrush") };
        note.Child = new TextBlock
        {
            Text = workflow.Guidance,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)FindResource("SecondaryTextBrush")
        };
        MainContent.Children.Add(note);
    }

    private void ShowDashboardLoadError(Exception exception)
    {
        MainContent.Children.Clear();
        MainContent.Children.Add(new Border
        {
            Style = (Style)FindResource("CardBorder"),
            BorderBrush = Brushes.IndianRed,
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Dashboard data could not be loaded", FontSize = 17, FontWeight = FontWeights.SemiBold },
                    new TextBlock { Text = exception.GetBaseException().Message, TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.Firebrick, Margin = new Thickness(0, 8, 0, 0) },
                    CreateActionButton("Retry dashboard")
                }
            }
        });
        if (MainContent.Children[0] is Border border && border.Child is StackPanel panel && panel.Children[2] is Button retry)
        {
            retry.Margin = new Thickness(0, 14, 0, 0);
            retry.HorizontalAlignment = HorizontalAlignment.Left;
            retry.Click += async (_, _) =>
            {
                try { await ShowDashboardAsync(); }
                catch (Exception retryException) { ShowDashboardLoadError(retryException); }
            };
        }
    }

    private (string Title, string Value, string Detail)[] GetBusinessMetrics(
        string businessType,
        DashboardSummary summary,
        IReadOnlyList<ProductItem> products,
        IReadOnlyList<CategoryItem> categories,
        IReadOnlyList<PurchaseItem> purchases,
        IReadOnlyList<CustomerItem> customers)
    {
        var monthStart = new DateTimeOffset(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthPurchases = purchases.Where(purchase => purchase.ReceivedAt >= monthStart).ToArray();
        var supplierCount = purchases.Select(purchase => purchase.SupplierName)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var inventoryValue = $"{_currencyCode} {summary.InventoryCostValue:N2}";
        var recentPurchaseCount = monthPurchases.Length.ToString("N0");

        return businessType switch
        {
            "Grocery or convenience store" =>
            [
                ("Tracked items", summary.ProductCount.ToString("N0"), "Products in the catalog"),
                ("Reorder soon", summary.LowStockCount.ToString("N0"), "Items at or below minimum stock"),
                ("Out of stock", summary.OutOfStockCount.ToString("N0"), "Items needing replenishment"),
                ("Suppliers", supplierCount.ToString("N0"), "Suppliers in purchase history")
            ],
            "Clothing and fashion" =>
            [
                ("Product styles", summary.ProductCount.ToString("N0"), "Active products tracked"),
                ("Product groups", categories.Count.ToString("N0"), "Catalog categories"),
                ("Low stock", summary.LowStockCount.ToString("N0"), "Styles at or below minimum"),
                ("Stock cost value", inventoryValue, "Based on quantity × cost")
            ],
            "Electronics" =>
            [
                ("Products tracked", summary.ProductCount.ToString("N0"), "Active products and accessories"),
                ("Categories", categories.Count.ToString("N0"), "Catalog categories"),
                ("Low stock", summary.LowStockCount.ToString("N0"), "Products at or below minimum"),
                ("Receipts this month", recentPurchaseCount, "Supplier stock purchases")
            ],
            "Restaurant or café" =>
            [
                ("Tracked supplies", summary.ProductCount.ToString("N0"), "Products and ingredients"),
                ("Reorder soon", summary.LowStockCount.ToString("N0"), "Items at or below minimum stock"),
                ("Receipts this month", recentPurchaseCount, "Incoming supply purchases"),
                ("Stock cost value", inventoryValue, "Based on quantity × cost")
            ],
            "Wholesale and distribution" =>
            [
                ("Products available", summary.ProductCount.ToString("N0"), "Active catalog products"),
                ("Suppliers", supplierCount.ToString("N0"), "Suppliers in purchase history"),
                ("Purchases this month", recentPurchaseCount, "Received purchase records"),
                ("Low stock", summary.LowStockCount.ToString("N0"), "Products at or below minimum")
            ],
            "Pharmacy" =>
            [
                ("Products tracked", summary.ProductCount.ToString("N0"), "Active catalog products"),
                ("Out of stock", summary.OutOfStockCount.ToString("N0"), "Products needing replenishment"),
                ("Low stock", summary.LowStockCount.ToString("N0"), "Products at or below minimum"),
                ("Stock cost value", inventoryValue, "Based on quantity × cost")
            ],
            "Beauty or salon" =>
            [
                ("Customers", customers.Count.ToString("N0"), "Active customer records"),
                ("Retail and supplies", summary.ProductCount.ToString("N0"), "Active products tracked"),
                ("Low stock", summary.LowStockCount.ToString("N0"), "Products at or below minimum"),
                ("Purchases this month", recentPurchaseCount, "Received supplier purchases")
            ],
            "Home services" =>
            [
                ("Customers", customers.Count.ToString("N0"), "Active customer records"),
                ("Tracked materials", summary.ProductCount.ToString("N0"), "Products and service supplies"),
                ("Low stock", summary.LowStockCount.ToString("N0"), "Materials at or below minimum"),
                ("Categories", categories.Count.ToString("N0"), "Product groups")
            ],
            "Retail shop" =>
            [
                ("Active products", summary.ProductCount.ToString("N0"), "Products in the catalog"),
                ("Categories", categories.Count.ToString("N0"), "Catalog groups"),
                ("Low stock", summary.LowStockCount.ToString("N0"), "Products at or below minimum"),
                ("Stock cost value", inventoryValue, "Based on quantity × cost")
            ],
            _ =>
            [
                ("Active products", summary.ProductCount.ToString("N0"), "Products in the catalog"),
                ("Low stock", summary.LowStockCount.ToString("N0"), "At or below minimum level"),
                ("Out of stock", summary.OutOfStockCount.ToString("N0"), "Needs replenishment"),
                ("Inventory cost value", inventoryValue, "Based on current quantity × cost")
            ]
        };
    }

    private static (string Description, string Guidance, string[] Pages) GetBusinessWorkflow(string businessType) => businessType switch
    {
        "Grocery or convenience store" => ("Monitor item availability and incoming stock.",
            "Track grocery items in Products, replenish from Purchases, and use Stock Movements to record spoilage or corrections.",
            ["Products", "Purchases", "Stock Movements"]),
        "Clothing and fashion" => ("Keep product lines and stock levels organized.",
            "Use product names, SKUs, and categories to organize apparel. Purchases and stock movements update the same inventory records.",
            ["Products", "Categories", "Stock Movements"]),
        "Electronics" => ("Track products, categories, and stock receipts.",
            "Maintain electronics and accessories in Products, organize them with Categories, and record supplier receipts in Purchases.",
            ["Products", "Categories", "Purchases"]),
        "Restaurant or café" => ("Keep ingredients and supplies visible.",
            "Use Products for ingredients and supplies, Purchases for incoming stock, and Stock Movements for usage or corrections. Point-of-sale and recipe costing are not available yet.",
            ["Products", "Purchases", "Stock Movements"]),
        "Wholesale and distribution" => ("Review stock availability and supplier receipts.",
            "Organize bulk products and categories, receive supplier stock through Purchases, and review quantity changes in Stock Movements.",
            ["Products", "Purchases", "Stock Movements"]),
        "Pharmacy" => ("Keep product availability and stock records current.",
            "Use Products, Categories, Purchases, and Stock Movements for inventory control. Prescription and dispensing workflows are not available yet.",
            ["Products", "Categories", "Purchases"]),
        "Beauty or salon" => ("Manage retail items, supplies, and customer records.",
            "Track products and supplies, record stock receipts, and manage customer balances. Appointment booking is not available yet.",
            ["Products", "Customers", "Purchases"]),
        "Home services" => ("Keep customers and commonly used supplies organized.",
            "Manage customer records and track service materials as products. Scheduling and job management are not available yet.",
            ["Customers", "Products", "Stock Movements"]),
        "Retail shop" => ("Monitor products, stock levels, and replenishment.",
            "Use Products and Categories to maintain your catalog, Purchases to receive stock, and Stock Movements to review adjustments.",
            ["Products", "Categories", "Purchases"]),
        _ => ("Manage products, inventory, purchasing, and customers.",
            "Choose Products, Purchases, Stock Movements, or Customers to manage the workflows currently available in DevTrack.",
            ["Products", "Purchases", "Customers"])
    };

    private async Task ShowProductsAsync(string? search = null)
    {
        using var scope = _services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IProductCatalogService>();
        var categories = await catalog.GetCategoriesAsync(_user.CompanyId);
        var products = await catalog.SearchProductsAsync(_user.CompanyId, search);
        MainContent.Children.Clear();

        var toolbar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 14) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var addButton = CreateActionButton("Add product");
        addButton.Click += async (_, _) =>
        {
            var dialog = new ProductEditorWindow(categories, _currencyCode) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                await SaveProductAsync(dialog.Draft!);
                await ShowProductsAsync(search);
            }
        };
        actions.Children.Add(addButton);
        var editButton = CreateActionButton("Edit selected");
        actions.Children.Add(editButton);
        var adjustButton = CreateActionButton("Adjust stock");
        actions.Children.Add(adjustButton);
        var archiveButton = CreateActionButton("Archive");
        actions.Children.Add(archiveButton);
        DockPanel.SetDock(actions, Dock.Right);
        toolbar.Children.Add(actions);
        var searchBox = new TextBox { Width = 220, Height = 36, Padding = new Thickness(10, 7, 10, 7), Text = search ?? string.Empty, VerticalContentAlignment = VerticalAlignment.Center };
        toolbar.Children.Add(searchBox);
        var searchButton = CreateActionButton("Search");
        searchButton.Click += async (_, _) => await ShowProductsAsync(searchBox.Text);
        actions.Children.Insert(0, searchButton);
        MainContent.Children.Add(toolbar);

        var grid = CreateDataGrid();
        grid.Columns.Add(TextColumn("SKU", nameof(ProductItem.Sku), 110));
        grid.Columns.Add(TextColumn("Product", nameof(ProductItem.Name), 2));
        grid.Columns.Add(TextColumn("Category", nameof(ProductItem.CategoryName), 140));
        grid.Columns.Add(TextColumn("On hand", nameof(ProductItem.OnHand), 90));
        grid.Columns.Add(TextColumn("Cost", nameof(ProductItem.CostPrice), 100));
        grid.Columns.Add(TextColumn("Selling price", nameof(ProductItem.SellingPrice), 120));
        grid.Columns.Add(TextColumn("Minimum", nameof(ProductItem.MinimumStock), 90));
        grid.ItemsSource = products;
        MainContent.Children.Add(grid);

        editButton.Click += async (_, _) =>
        {
            if (grid.SelectedItem is not ProductItem product) { ShowError("Select a product first."); return; }
            var dialog = new ProductEditorWindow(categories, _currencyCode, product) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                await SaveProductAsync(dialog.Draft!);
                await ShowProductsAsync(searchBox.Text);
            }
        };
        adjustButton.Click += async (_, _) =>
        {
            if (grid.SelectedItem is not ProductItem product) { ShowError("Select a product first."); return; }
            await AdjustStockAsync(product.Id, product.Name);
            await ShowProductsAsync(searchBox.Text);
        };
        archiveButton.Click += async (_, _) =>
        {
            if (grid.SelectedItem is not ProductItem product) { ShowError("Select a product first."); return; }
            if (MessageBox.Show(this, $"Archive {product.Name}? It will remain in transaction history.", "Archive product", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            using var actionScope = _services.CreateScope();
            await actionScope.ServiceProvider.GetRequiredService<IProductCatalogService>().ArchiveProductAsync(_user.CompanyId, product.Id, _user.Username);
            await ShowProductsAsync(searchBox.Text);
        };
    }

    private async Task ShowCategoriesAsync()
    {
        using var scope = _services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IProductCatalogService>();
        var categories = await catalog.GetCategoriesAsync(_user.CompanyId);
        MainContent.Children.Clear();
        var addButton = CreateActionButton("Add category");
        addButton.HorizontalAlignment = HorizontalAlignment.Left;
        addButton.Margin = new Thickness(0, 0, 0, 14);
        addButton.Click += async (_, _) =>
        {
            var dialog = new CategoryEditorWindow { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                using var saveScope = _services.CreateScope();
                await saveScope.ServiceProvider.GetRequiredService<IProductCatalogService>()
                    .SaveCategoryAsync(_user.CompanyId, dialog.CategoryName, dialog.Description, _user.Username);
                await ShowCategoriesAsync();
            }
            catch (Exception exception) { ShowError(exception.Message); }
        };
        MainContent.Children.Add(addButton);
        var grid = CreateDataGrid();
        grid.Columns.Add(TextColumn("Category", nameof(CategoryItem.Name), 2));
        grid.Columns.Add(TextColumn("Description", nameof(CategoryItem.Description), 3));
        grid.Columns.Add(TextColumn("Products", nameof(CategoryItem.ProductCount), 100));
        grid.ItemsSource = categories;
        MainContent.Children.Add(grid);
    }

    private async Task ShowStockMovementsAsync()
    {
        using var scope = _services.CreateScope();
        var catalog = scope.ServiceProvider.GetRequiredService<IProductCatalogService>();
        var products = await catalog.SearchProductsAsync(_user.CompanyId);
        var movements = await catalog.GetStockMovementsAsync(_user.CompanyId);
        MainContent.Children.Clear();
        var adjustButton = CreateActionButton("New stock adjustment");
        adjustButton.HorizontalAlignment = HorizontalAlignment.Left;
        adjustButton.Margin = new Thickness(0, 0, 0, 14);
        adjustButton.IsEnabled = products.Count > 0;
        adjustButton.Click += async (_, _) =>
        {
            var dialog = new StockAdjustmentWindow(products) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                using var saveScope = _services.CreateScope();
                await saveScope.ServiceProvider.GetRequiredService<IProductCatalogService>().AdjustStockAsync(
                    _user.CompanyId, dialog.ProductId, dialog.QuantityChange, dialog.Reason, _user.Username);
                await ShowStockMovementsAsync();
            }
            catch (Exception exception) { ShowError(exception.Message); }
        };
        MainContent.Children.Add(adjustButton);
        var grid = CreateDataGrid();
        grid.Columns.Add(TextColumn("Date", nameof(StockMovementItem.OccurredAt), 160));
        grid.Columns.Add(TextColumn("SKU", nameof(StockMovementItem.ProductSku), 100));
        grid.Columns.Add(TextColumn("Product", nameof(StockMovementItem.ProductName), 2));
        grid.Columns.Add(TextColumn("Change", nameof(StockMovementItem.QuantityChange), 90));
        grid.Columns.Add(TextColumn("Balance after", nameof(StockMovementItem.BalanceAfter), 100));
        grid.Columns.Add(TextColumn("Reason", nameof(StockMovementItem.Reason), 2));
        grid.Columns.Add(TextColumn("By", nameof(StockMovementItem.Username), 110));
        grid.ItemsSource = movements;
        MainContent.Children.Add(grid);
    }

    private async Task ShowCustomersAsync(string? search = null)
    {
        using var scope = _services.CreateScope();
        var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var customers = await customerService.SearchAsync(_user.CompanyId, search);
        MainContent.Children.Clear();

        var toolbar = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 14) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var addButton = CreateActionButton("Add customer");
        addButton.Click += async (_, _) =>
        {
            var dialog = new CustomerEditorWindow(_currencyCode) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                await SaveCustomerAsync(dialog.Draft!);
                await ShowCustomersAsync(search);
            }
        };
        actions.Children.Add(addButton);
        var editButton = CreateActionButton("Edit selected");
        var receiptButton = CreateActionButton("Record receipt");
        var chargeButton = CreateActionButton("Record charge");
        var ledgerButton = CreateActionButton("View ledger");
        actions.Children.Add(editButton);
        actions.Children.Add(receiptButton);
        actions.Children.Add(chargeButton);
        actions.Children.Add(ledgerButton);
        DockPanel.SetDock(actions, Dock.Right);
        toolbar.Children.Add(actions);
        var searchBox = new TextBox { Width = 220, Height = 36, Padding = new Thickness(10, 7, 10, 7), Text = search ?? string.Empty, VerticalContentAlignment = VerticalAlignment.Center };
        toolbar.Children.Add(searchBox);
        var searchButton = CreateActionButton("Search");
        searchButton.Click += async (_, _) => await ShowCustomersAsync(searchBox.Text);
        actions.Children.Insert(0, searchButton);
        MainContent.Children.Add(toolbar);

        var grid = CreateDataGrid();
        grid.Columns.Add(TextColumn("Code", nameof(CustomerItem.Code), 100));
        grid.Columns.Add(TextColumn("Customer", nameof(CustomerItem.Name), 2));
        grid.Columns.Add(TextColumn("Phone", nameof(CustomerItem.Phone), 130));
        grid.Columns.Add(TextColumn("Email", nameof(CustomerItem.Email), 2));
        grid.Columns.Add(TextColumn("Credit limit", nameof(CustomerItem.CreditLimit), 110));
        grid.Columns.Add(TextColumn("Balance", nameof(CustomerItem.Balance), 110));
        grid.ItemsSource = customers;
        MainContent.Children.Add(grid);

        editButton.Click += async (_, _) =>
        {
            if (grid.SelectedItem is not CustomerItem customer) { ShowError("Select a customer first."); return; }
            var dialog = new CustomerEditorWindow(_currencyCode, customer) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                await SaveCustomerAsync(dialog.Draft!);
                await ShowCustomersAsync(searchBox.Text);
            }
        };
        receiptButton.Click += async (_, _) => await RecordCustomerActivityAsync(grid.SelectedItem as CustomerItem, false, searchBox.Text);
        chargeButton.Click += async (_, _) => await RecordCustomerActivityAsync(grid.SelectedItem as CustomerItem, true, searchBox.Text);
        ledgerButton.Click += async (_, _) =>
        {
            if (grid.SelectedItem is not CustomerItem customer) { ShowError("Select a customer first."); return; }
            using var ledgerScope = _services.CreateScope();
            var ledger = await ledgerScope.ServiceProvider.GetRequiredService<ICustomerService>()
                .GetLedgerAsync(_user.CompanyId, customer.Id);
            var dialog = new CustomerLedgerWindow(customer, ledger) { Owner = this };
            dialog.ShowDialog();
        };
    }

    private async Task ShowPurchasesAsync()
    {
        using var scope = _services.CreateScope();
        var purchasing = scope.ServiceProvider.GetRequiredService<IPurchasingService>();
        var catalog = scope.ServiceProvider.GetRequiredService<IProductCatalogService>();
        var purchases = await purchasing.GetPurchasesAsync(_user.CompanyId);
        var products = await catalog.SearchProductsAsync(_user.CompanyId);
        MainContent.Children.Clear();
        var receiveButton = CreateActionButton("Receive purchase");
        receiveButton.HorizontalAlignment = HorizontalAlignment.Left;
        receiveButton.Margin = new Thickness(0, 0, 0, 14);
        receiveButton.IsEnabled = products.Count > 0;
        receiveButton.Click += async (_, _) =>
        {
            var dialog = new PurchaseReceiveWindow(products, _currencyCode) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            try
            {
                using var receiveScope = _services.CreateScope();
                await receiveScope.ServiceProvider.GetRequiredService<IPurchasingService>()
                    .ReceiveAsync(_user.CompanyId, dialog.Draft!, _user.Username);
                StatusMessage.Text = "Purchase received; inventory and average cost updated.";
                await ShowPurchasesAsync();
            }
            catch (Exception exception) { ShowError(exception.Message); }
        };
        MainContent.Children.Add(receiveButton);
        var grid = CreateDataGrid();
        grid.Columns.Add(TextColumn("Received", nameof(PurchaseItem.ReceivedAt), 155));
        grid.Columns.Add(TextColumn("Reference", nameof(PurchaseItem.Reference), 120));
        grid.Columns.Add(TextColumn("Supplier", nameof(PurchaseItem.SupplierName), 150));
        grid.Columns.Add(TextColumn("SKU", nameof(PurchaseItem.ProductSku), 90));
        grid.Columns.Add(TextColumn("Product", nameof(PurchaseItem.ProductName), 2));
        grid.Columns.Add(TextColumn("Quantity", nameof(PurchaseItem.Quantity), 85));
        grid.Columns.Add(TextColumn("Unit cost", nameof(PurchaseItem.UnitCost), 95));
        grid.Columns.Add(TextColumn("Total", nameof(PurchaseItem.TotalCost), 100));
        grid.Columns.Add(TextColumn("By", nameof(PurchaseItem.Username), 90));
        grid.ItemsSource = purchases;
        MainContent.Children.Add(grid);
    }

    private async Task SaveCustomerAsync(CustomerDraft draft)
    {
        try
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ICustomerService>().SaveAsync(_user.CompanyId, draft, _user.Username);
            StatusMessage.Text = "Customer saved.";
        }
        catch (Exception exception) { ShowError(exception.Message); }
    }

    private async Task RecordCustomerActivityAsync(CustomerItem? customer, bool isCharge, string search)
    {
        if (customer is null) { ShowError("Select a customer first."); return; }
        var dialog = new CustomerBalanceEntryWindow(customer, isCharge, _currencyCode) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            if (isCharge)
                await service.RecordChargeAsync(_user.CompanyId, customer.Id, dialog.Amount, dialog.Description, _user.Username);
            else
                await service.RecordReceiptAsync(_user.CompanyId, customer.Id, dialog.Amount, dialog.Description, _user.Username);
            await ShowCustomersAsync(search);
            StatusMessage.Text = isCharge ? "Customer charge recorded." : "Customer receipt recorded.";
        }
        catch (Exception exception) { ShowError(exception.Message); }
    }

    private async Task SaveProductAsync(ProductDraft draft)
    {
        try
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IProductCatalogService>().SaveProductAsync(_user.CompanyId, draft, _user.Username);
            StatusMessage.Text = "Product saved.";
        }
        catch (Exception exception) { ShowError(exception.Message); }
    }

    private async Task AdjustStockAsync(Guid productId, string productName)
    {
        var product = new ProductItem(productId, string.Empty, null, productName, null, 0, 0, 0, 0, true);
        var dialog = new StockAdjustmentWindow([product]) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IProductCatalogService>().AdjustStockAsync(
                _user.CompanyId, productId, dialog.QuantityChange, dialog.Reason, _user.Username);
            StatusMessage.Text = "Stock updated.";
        }
        catch (Exception exception) { ShowError(exception.Message); }
    }

    private Border CreateMetricCard(string title, string value, string detail)
    {
        var card = new Border { Style = (Style)FindResource("CardBorder"), Margin = new Thickness(0, 0, 12, 12) };
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = title, Foreground = (Brush)FindResource("SecondaryTextBrush") });
        content.Children.Add(new TextBlock { Text = value, FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 3) });
        content.Children.Add(new TextBlock { Text = detail, FontSize = 12, Foreground = (Brush)FindResource("SecondaryTextBrush") });
        card.Child = content;
        return card;
    }

    private static DataGrid CreateDataGrid() => new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        CanUserAddRows = false,
        CanUserDeleteRows = false,
        CanUserSortColumns = true,
        SelectionMode = DataGridSelectionMode.Single,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
        MinHeight = 250,
        MaxHeight = 560
    };

    private static DataGridTextColumn TextColumn(string header, string path, object width) => new()
    {
        Header = header,
        Binding = new System.Windows.Data.Binding(path),
        Width = width is int value && value <= 10
            ? new DataGridLength(value, DataGridLengthUnitType.Star)
            : new DataGridLength(Convert.ToDouble(width, CultureInfo.InvariantCulture))
    };

    private Button CreateActionButton(string label) => new()
    {
        Content = label,
        Style = (Style)FindResource("NavButton"),
        Background = (Brush)FindResource("SecondaryBackgroundBrush"),
        BorderBrush = (Brush)FindResource("BorderBrush"),
        BorderThickness = new Thickness(1),
        Margin = new Thickness(0, 0, 8, 0),
        Padding = new Thickness(12, 8, 12, 8)
    };

    private void ShowError(string message)
    {
        StatusMessage.Text = message;
        StatusMessage.Foreground = Brushes.Firebrick;
    }
}

internal sealed class PurchaseReceiveWindow : Window
{
    private readonly ComboBox _product = new();
    private readonly TextBox _reference = new();
    private readonly TextBox _supplier = new();
    private readonly TextBox _quantity = new();
    private readonly TextBox _unitCost = new();
    private readonly TextBlock _error = new();

    public PurchaseDraft? Draft { get; private set; }

    public PurchaseReceiveWindow(IReadOnlyList<ProductItem> products, string currencyCode)
    {
        Title = "Receive purchase";
        Width = 470;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        _product.ItemsSource = products;
        _product.DisplayMemberPath = nameof(ProductItem.Name);
        ProductEditorWindow.AddField(panel, "Product", _product);
        ProductEditorWindow.AddField(panel, "Supplier", _supplier);
        ProductEditorWindow.AddField(panel, "Invoice / reference", _reference);
        ProductEditorWindow.AddField(panel, "Quantity received", _quantity);
        ProductEditorWindow.AddField(panel, $"Unit cost ({currencyCode})", _unitCost);
        _error.Foreground = Brushes.Firebrick;
        panel.Children.Add(_error);
        var receive = new Button { Content = "Receive and update stock", Height = 38, Margin = new Thickness(0, 14, 0, 0), IsDefault = true };
        receive.Click += (_, _) => Save();
        panel.Children.Add(receive);
        Content = panel;
    }

    private void Save()
    {
        if (_product.SelectedItem is not ProductItem product ||
            !decimal.TryParse(_quantity.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity) ||
            !decimal.TryParse(_unitCost.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var unitCost) ||
            quantity <= 0 || unitCost < 0)
        {
            _error.Text = "Select a product and enter a positive quantity and valid unit cost.";
            return;
        }
        Draft = new PurchaseDraft(product.Id, _reference.Text, _supplier.Text, quantity, unitCost);
        DialogResult = true;
    }
}

internal sealed class CustomerEditorWindow : Window
{
    private readonly TextBox _code = new();
    private readonly TextBox _name = new();
    private readonly TextBox _phone = new();
    private readonly TextBox _email = new();
    private readonly TextBox _address = new();
    private readonly TextBox _creditLimit = new();
    private readonly TextBox _openingBalance = new();
    private readonly TextBlock _error = new();
    private readonly CustomerItem? _existing;

    public CustomerDraft? Draft { get; private set; }

    public CustomerEditorWindow(string currencyCode, CustomerItem? existing = null)
    {
        _existing = existing;
        Title = existing is null ? "Add customer" : "Edit customer";
        Width = 470;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        ProductEditorWindow.AddField(panel, "Customer code", _code);
        ProductEditorWindow.AddField(panel, "Customer name", _name);
        ProductEditorWindow.AddField(panel, "Phone", _phone);
        ProductEditorWindow.AddField(panel, "Email", _email);
        ProductEditorWindow.AddField(panel, "Address", _address);
        ProductEditorWindow.AddField(panel, $"Credit limit ({currencyCode})", _creditLimit);
        ProductEditorWindow.AddField(panel, existing is null ? $"Opening balance ({currencyCode}; positive = owed to you)" : "Opening balance (already recorded)", _openingBalance);
        _creditLimit.Text = "0";
        _openingBalance.Text = "0";
        if (existing is not null)
        {
            _code.Text = existing.Code;
            _name.Text = existing.Name;
            _phone.Text = existing.Phone ?? string.Empty;
            _email.Text = existing.Email ?? string.Empty;
            _address.Text = existing.Address ?? string.Empty;
            _creditLimit.Text = existing.CreditLimit.ToString(CultureInfo.CurrentCulture);
            _openingBalance.Text = existing.Balance.ToString(CultureInfo.CurrentCulture);
            _openingBalance.IsEnabled = false;
        }
        _error.Foreground = Brushes.Firebrick;
        panel.Children.Add(_error);
        var save = new Button { Content = "Save customer", Height = 38, Margin = new Thickness(0, 14, 0, 0), IsDefault = true };
        save.Click += Save;
        panel.Children.Add(save);
        Content = panel;
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(_creditLimit.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var creditLimit) ||
            !decimal.TryParse(_openingBalance.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var openingBalance))
        {
            _error.Text = "Enter valid numbers for credit limit and opening balance.";
            return;
        }
        Draft = new CustomerDraft(_existing?.Id, _code.Text, _name.Text, _phone.Text, _email.Text,
            _address.Text, creditLimit, _existing is null ? openingBalance : 0);
        DialogResult = true;
    }
}

internal sealed class CustomerBalanceEntryWindow : Window
{
    private readonly TextBox _amount = new();
    private readonly TextBox _description = new();
    private readonly TextBlock _error = new();

    public decimal Amount { get; private set; }
    public string Description => _description.Text;

    public CustomerBalanceEntryWindow(CustomerItem customer, bool isCharge, string currencyCode)
    {
        Title = isCharge ? "Record customer charge" : "Record customer receipt";
        Width = 430;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = $"{customer.Name} · Current balance {currencyCode} {customer.Balance:N2}", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) });
        ProductEditorWindow.AddField(panel, $"Amount ({currencyCode})", _amount);
        ProductEditorWindow.AddField(panel, isCharge ? "Description" : "Receipt reference / description", _description);
        _error.Foreground = Brushes.Firebrick;
        panel.Children.Add(_error);
        var save = new Button { Content = isCharge ? "Post charge" : "Record receipt", Height = 38, Margin = new Thickness(0, 14, 0, 0), IsDefault = true };
        save.Click += (_, _) =>
        {
            if (!decimal.TryParse(_amount.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) || amount <= 0)
            {
                _error.Text = "Enter a positive amount.";
                return;
            }
            Amount = amount;
            DialogResult = true;
        };
        panel.Children.Add(save);
        Content = panel;
    }
}

internal sealed class CustomerLedgerWindow : Window
{
    public CustomerLedgerWindow(CustomerItem customer, IReadOnlyList<CustomerLedgerItem> entries)
    {
        Title = $"Account ledger · {customer.Name}";
        Width = 900;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = $"{customer.Code} · {customer.Name} · Current balance {customer.Balance:N2}", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        var grid = new DataGrid
        {
            AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false,
            HeadersVisibility = DataGridHeadersVisibility.Column, GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            ItemsSource = entries
        };
        grid.Columns.Add(new DataGridTextColumn { Header = "Date", Binding = new System.Windows.Data.Binding(nameof(CustomerLedgerItem.OccurredAt)), Width = 160 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Type", Binding = new System.Windows.Data.Binding(nameof(CustomerLedgerItem.Type)), Width = 140 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Change", Binding = new System.Windows.Data.Binding(nameof(CustomerLedgerItem.BalanceChange)), Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Balance", Binding = new System.Windows.Data.Binding(nameof(CustomerLedgerItem.BalanceAfter)), Width = 110 });
        grid.Columns.Add(new DataGridTextColumn { Header = "Description", Binding = new System.Windows.Data.Binding(nameof(CustomerLedgerItem.Description)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = "By", Binding = new System.Windows.Data.Binding(nameof(CustomerLedgerItem.Username)), Width = 100 });
        panel.Children.Add(grid);
        Content = panel;
    }
}

internal sealed class ProductEditorWindow : Window
{
    private readonly TextBox _name = new();
    private readonly TextBox _sku = new();
    private readonly TextBox _barcode = new();
    private readonly TextBox _cost = new();
    private readonly TextBox _price = new();
    private readonly TextBox _minimum = new();
    private readonly ComboBox _category = new();
    private readonly TextBlock _error = new();
    private readonly Guid? _id;

    public ProductDraft? Draft { get; private set; }

    public ProductEditorWindow(IReadOnlyList<CategoryItem> categories, string currencyCode, ProductItem? product = null)
    {
        _id = product?.Id;
        Title = product is null ? "Add product" : "Edit product";
        Width = 470;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        var fields = new StackPanel { Margin = new Thickness(24) };
        AddField(fields, "Product name", _name);
        AddField(fields, "SKU", _sku);
        AddField(fields, "Barcode (optional)", _barcode);
        _category.ItemsSource = categories;
        _category.DisplayMemberPath = nameof(CategoryItem.Name);
        _category.SelectedValuePath = nameof(CategoryItem.Id);
        AddField(fields, "Category", _category);
        AddField(fields, $"Cost ({currencyCode})", _cost);
        AddField(fields, $"Selling price ({currencyCode})", _price);
        AddField(fields, "Minimum stock", _minimum);
        if (product is null)
        {
            _cost.Text = "0";
            _price.Text = "0";
            _minimum.Text = "0";
        }
        _error.Foreground = Brushes.Firebrick;
        _error.Margin = new Thickness(0, 8, 0, 0);
        fields.Children.Add(_error);
        var save = new Button { Content = "Save product", Height = 38, Margin = new Thickness(0, 14, 0, 0), IsDefault = true };
        save.Click += Save;
        fields.Children.Add(save);
        Content = fields;
        if (product is not null)
        {
            _name.Text = product.Name;
            _sku.Text = product.Sku;
            _barcode.Text = product.Barcode ?? string.Empty;
            _cost.Text = product.CostPrice.ToString(CultureInfo.CurrentCulture);
            _price.Text = product.SellingPrice.ToString(CultureInfo.CurrentCulture);
            _minimum.Text = product.MinimumStock.ToString(CultureInfo.CurrentCulture);
            _category.SelectedItem = categories.FirstOrDefault(category => category.Name == product.CategoryName);
        }
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        if (!decimal.TryParse(_cost.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var cost) ||
            !decimal.TryParse(_price.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var price) ||
            !decimal.TryParse(_minimum.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var minimum))
        {
            _error.Text = "Enter valid numbers for cost, selling price, and minimum stock.";
            return;
        }
        Draft = new ProductDraft(_id, _sku.Text, _barcode.Text, _name.Text,
            _category.SelectedValue is Guid categoryId ? categoryId : null, cost, price, minimum);
        DialogResult = true;
    }

    internal static void AddField(Panel panel, string label, Control control)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 4), FontSize = 12 });
        control.Height = 34;
        control.Margin = new Thickness(0, 0, 0, 2);
        if (control is TextBox textBox) textBox.Padding = new Thickness(8, 5, 8, 5);
        panel.Children.Add(control);
    }
}

internal sealed class CategoryEditorWindow : Window
{
    private readonly TextBox _name = new();
    private readonly TextBox _description = new();

    public string CategoryName => _name.Text;
    public string Description => _description.Text;

    public CategoryEditorWindow()
    {
        Title = "Add category";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        ProductEditorWindow.AddField(panel, "Category name", _name);
        ProductEditorWindow.AddField(panel, "Description (optional)", _description);
        var save = new Button { Content = "Save category", Height = 38, Margin = new Thickness(0, 14, 0, 0), IsDefault = true };
        save.Click += (_, _) => DialogResult = true;
        panel.Children.Add(save);
        Content = panel;
    }
}

internal sealed class StockAdjustmentWindow : Window
{
    private readonly ComboBox _product = new();
    private readonly TextBox _quantity = new();
    private readonly TextBox _reason = new();
    private readonly TextBlock _error = new();

    public Guid ProductId => ((ProductItem)_product.SelectedItem).Id;
    public decimal QuantityChange { get; private set; }
    public string Reason => _reason.Text;

    public StockAdjustmentWindow(IReadOnlyList<ProductItem> products)
    {
        Title = "Adjust stock";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(24) };
        _product.ItemsSource = products;
        _product.DisplayMemberPath = nameof(ProductItem.Name);
        if (products.Count == 1) _product.SelectedIndex = 0;
        ProductEditorWindow.AddField(panel, "Product", _product);
        ProductEditorWindow.AddField(panel, "Quantity change (+ receive, − issue)", _quantity);
        ProductEditorWindow.AddField(panel, "Reason", _reason);
        _error.Foreground = Brushes.Firebrick;
        panel.Children.Add(_error);
        var save = new Button { Content = "Save adjustment", Height = 38, Margin = new Thickness(0, 14, 0, 0), IsDefault = true };
        save.Click += Save;
        panel.Children.Add(save);
        Content = panel;
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        if (_product.SelectedItem is not ProductItem)
        {
            _error.Text = "Select a product.";
            return;
        }
        if (!decimal.TryParse(_quantity.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var change) || change == 0)
        {
            _error.Text = "Enter a non-zero quantity. Use a negative number to issue stock.";
            return;
        }
        QuantityChange = change;
        DialogResult = true;
    }
}
