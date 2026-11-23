using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http.Json;

namespace MauiApp;

public partial class StorePage : ContentPage
{
	private readonly HttpClient _httpClient = new()
	{
#if ANDROID
		BaseAddress = new Uri("http://10.0.2.2:8080")
#else
		BaseAddress = new Uri("http://localhost:8080")
#endif
	};

	private readonly ObservableCollection<CartItem> _cart = [];
	private readonly List<Item> _stock = [];
	private CancellationTokenSource? _lookupTimer;
	private bool _isLoading;

	public StorePage()
	{
		InitializeComponent();
		CartView.ItemsSource = _cart;
		Loaded += (_, _) => _ = LoadProductsAsync();
	}

	private async Task LoadProductsAsync(string? search = null)
	{
		if (_isLoading)
			return;

		_isLoading = true;
		try
		{
			StoreStatusLabel.Text = "Loading products...";
			IReadOnlyList<Item> products;

			if (string.IsNullOrWhiteSpace(search))
			{
				products = await _httpClient.GetFromJsonAsync<List<Item>>("/api/items")
					?? [];
			}
			else
			{
				var escapedSearch = Uri.EscapeDataString(search.Trim());
				var result = await _httpClient.GetFromJsonAsync<PagedResult<Item>>(
					$"/api/items/search?search={escapedSearch}&sort=name&page=1&pageSize=100");
				products = result?.Items ?? [];
			}

			_stock.Clear();
			_stock.AddRange(products);
			ProductsView.ItemsSource = GroupProducts(products);
			StoreStatusLabel.Text = string.IsNullOrWhiteSpace(search)
				? $"{products.Count} product(s) available"
				: $"{products.Count} result(s) for \"{search.Trim()}\"";
		}
		catch (Exception exception)
		{
			StoreStatusLabel.Text = $"Store unavailable: {exception.Message}";
		}
		finally
		{
			_isLoading = false;
		}
	}

	private async void OnGameLookupTextChanged(object sender, TextChangedEventArgs e)
	{
		_lookupTimer?.Cancel();
		_lookupTimer?.Dispose();
		_lookupTimer = new CancellationTokenSource();
		var token = _lookupTimer.Token;
		ClearLookupButton.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue);

		try
		{
			await Task.Delay(350, token);
			await LoadProductsAsync(e.NewTextValue);
		}
		catch (TaskCanceledException)
		{
		}
	}

	private async void OnGameLookupCompleted(object sender, EventArgs e) =>
		await LoadProductsAsync(GameLookupEntry.Text);

	private async void OnClearLookupClicked(object sender, EventArgs e)
	{
		GameLookupEntry.Text = string.Empty;
		ClearLookupButton.IsEnabled = false;
		await LoadProductsAsync();
	}

	private void OnAddToCartClicked(object sender, EventArgs e)
	{
		if (sender is not Button { CommandParameter: StockGroup product })
			return;

		var availableItem = _stock.FirstOrDefault(item =>
			item.Name == product.Name &&
			item.Description == product.Description &&
			item.Price == product.Price &&
			!_cart.Any(line =>
				line.StockItemIds.Contains(item.Id ?? string.Empty)));
		if (availableItem?.Id is null)
		{
			StoreStatusLabel.Text = $"No more copies of {product.Name} are available.";
			return;
		}

		var existing = _cart.FirstOrDefault(item =>
			item.Name == product.Name &&
			item.Description == product.Description &&
			item.Price == product.Price);
		if (existing is null)
		{
			_cart.Add(new CartItem
			{
				Name = product.Name,
				Description = product.Description,
				Price = product.Price,
				StockItemIds = [availableItem.Id]
			});
		}
		else
		{
			existing.StockItemIds.Add(availableItem.Id);
		}
		CartView.ItemsSource = null;
		CartView.ItemsSource = _cart;
		UpdateCart();
	}

	private void OnRemoveFromCartClicked(object sender, EventArgs e)
	{
		if (sender is not Button { CommandParameter: CartItem cartItem })
			return;

		cartItem.StockItemIds.RemoveAt(cartItem.StockItemIds.Count - 1);
		if (cartItem.StockItemIds.Count == 0)
			_cart.Remove(cartItem);
		CartView.ItemsSource = null;
		CartView.ItemsSource = _cart;
		UpdateCart();
	}

	private async void OnCheckoutClicked(object sender, EventArgs e)
	{
		if (_cart.Count == 0)
			return;

		var quantity = _cart.Sum(item => item.Quantity);
		var total = _cart.Sum(item => item.Price * item.Quantity);
		var confirmed = await DisplayAlertAsync(
			"Complete checkout",
			$"Purchase {quantity} item(s) for {total.ToString("C", CultureInfo.CurrentCulture)}?",
			"Checkout",
			"Cancel");
		if (!confirmed)
			return;

		try
		{
			var response = await _httpClient.PostAsJsonAsync(
				"/api/items/checkout",
				new CheckoutRequest
				{
					ItemIds = _cart
						.SelectMany(item => item.StockItemIds)
						.ToList()
				});
			response.EnsureSuccessStatusCode();
			var result = await response.Content.ReadFromJsonAsync<CheckoutResult>();

			_cart.Clear();
			UpdateCart();
			await LoadProductsAsync();
			StoreStatusLabel.Text =
				$"Checkout complete - {result?.RemovedCount ?? 0} item(s) removed";
		}
		catch (Exception exception)
		{
			StoreStatusLabel.Text = $"Checkout failed: {exception.Message}";
		}
	}

	private void UpdateCart()
	{
		var total = _cart.Sum(item => item.Price * item.Quantity);
		CartTotalLabel.Text = $"Total: {total.ToString("C", CultureInfo.CurrentCulture)}";
		CheckoutButton.IsEnabled = _cart.Count > 0;
	}

	private static IReadOnlyList<StockGroup> GroupProducts(IEnumerable<Item> products)
	{
		return products
			.Where(item => item.Id is not null)
			.GroupBy(item => new
			{
				item.Name,
				item.Description,
				item.Price
			})
			.Select(group => new StockGroup
			{
				Name = group.Key.Name,
				Description = group.Key.Description,
				Price = group.Key.Price,
				StockCount = group.Count()
			})
			.OrderBy(item => item.Name)
			.ToList();
	}
}

public sealed class CartItem
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public decimal Price { get; init; }
	public List<string> StockItemIds { get; init; } = [];
	public int Quantity => StockItemIds.Count;
	public string DisplayPrice =>
		Price.ToString("C", CultureInfo.CurrentCulture);
	public string CartText =>
		$"{Quantity} x {DisplayPrice} = {(Price * Quantity).ToString("C", CultureInfo.CurrentCulture)}";
}

public sealed class StockGroup
{
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public decimal Price { get; init; }
	public int StockCount { get; init; }
	public string DisplayPrice =>
		Price.ToString("C", CultureInfo.CurrentCulture);
	public string StockText =>
		$"{StockCount} in stock";
}

public sealed class CheckoutRequest
{
	public IReadOnlyList<string> ItemIds { get; set; } = [];
}

public sealed class CheckoutResult
{
	public int RemovedCount { get; set; }
	public decimal Total { get; set; }
}
