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

			ProductsView.ItemsSource = products;
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
		if (sender is not Button { CommandParameter: Item product } || product.Id is null)
			return;
		if (_cart.Any(item => item.Id == product.Id))
		{
			StoreStatusLabel.Text = $"{product.Name} is already in the cart.";
			return;
		}

		_cart.Add(new CartItem
		{
			Id = product.Id,
			Name = product.Name,
			Price = product.Price
		});
		UpdateCart();
	}

	private void OnRemoveFromCartClicked(object sender, EventArgs e)
	{
		if (sender is not Button { CommandParameter: CartItem cartItem })
			return;

		_cart.Remove(cartItem);
		UpdateCart();
	}

	private async void OnCheckoutClicked(object sender, EventArgs e)
	{
		if (_cart.Count == 0)
			return;

		var total = _cart.Sum(item => item.Price);
		var confirmed = await DisplayAlertAsync(
			"Complete checkout",
			$"Purchase {_cart.Count} item(s) for {total.ToString("C", CultureInfo.CurrentCulture)}?",
			"Checkout",
			"Cancel");
		if (!confirmed)
			return;

		try
		{
			var response = await _httpClient.PostAsJsonAsync(
				"/api/items/checkout",
				new CheckoutRequest { ItemIds = _cart.Select(item => item.Id).ToList() });
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
		var total = _cart.Sum(item => item.Price);
		CartTotalLabel.Text = $"Total: {total.ToString("C", CultureInfo.CurrentCulture)}";
		CheckoutButton.IsEnabled = _cart.Count > 0;
	}
}

public sealed class CartItem
{
	public string Id { get; init; } = string.Empty;
	public string Name { get; init; } = string.Empty;
	public decimal Price { get; init; }
	public string DisplayPrice =>
		Price.ToString("C", CultureInfo.CurrentCulture);
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
