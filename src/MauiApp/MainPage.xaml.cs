using System.Net.Http.Json;

namespace MauiApp;

public partial class MainPage : ContentPage
{
	private readonly HttpClient _httpClient = new()
	{
#if ANDROID
		BaseAddress = new Uri("http://10.0.2.2:8080")
#else
		BaseAddress = new Uri("http://localhost:8080")
#endif
	};

	private string? _editingId;
	private int _page = 1;
	private const int PageSize = 10;
	private int _totalPages = 1;
	private bool _isLoading;
	private CancellationTokenSource? _searchTimer;

	public MainPage()
	{
		InitializeComponent();
		Loaded += (_, _) => _ = LoadItemsAsync();
	}

	private async void OnCheckApiClicked(object? sender, EventArgs e) =>
		await LoadItemsAsync(resetPage: true);

	private async Task LoadItemsAsync(bool resetPage = false)
	{
		if (_isLoading)
			return;

		_isLoading = true;
		if (resetPage)
			_page = 1;

		try
		{
			StatusLabel.Text = "Loading...";
			var query = new List<string>
			{
				$"page={_page}",
				$"pageSize={PageSize}"
			};

			var search = SearchEntry.Text?.Trim();
			if (!string.IsNullOrWhiteSpace(search))
				query.Add($"search={Uri.EscapeDataString(search)}");

			if (DescriptionFilterPicker.SelectedIndex == 1)
				query.Add("hasDescription=true");
			else if (DescriptionFilterPicker.SelectedIndex == 2)
				query.Add("hasDescription=false");

			var sort = SortPicker.SelectedIndex switch
			{
				1 => "name-desc",
				2 => "newest",
				3 => "oldest",
				_ => "name"
			};
			query.Add($"sort={sort}");

			var result = await _httpClient.GetFromJsonAsync<PagedResult<Item>>(
				"/api/items/search?" + string.Join("&", query));
			if (result is null)
				throw new InvalidOperationException("The API returned no data.");

			_page = result.Page;
			_totalPages = result.TotalPages;
			ItemsView.ItemsSource = result.Items;
			PageLabel.Text = $"Page {_page} of {_totalPages}";
			PreviousButton.IsEnabled = _page > 1;
			NextButton.IsEnabled = _page < _totalPages;
			StatusLabel.Text = $"Connected - {result.TotalCount} item(s)";
		}
		catch (Exception exception)
		{
			StatusLabel.Text = $"API unavailable: {exception.Message}";
		}
		finally
		{
			_isLoading = false;
		}
	}

	private async void OnCreateItemClicked(object sender, EventArgs e)
	{
		var name = ItemNameEntry.Text?.Trim();
		if (string.IsNullOrWhiteSpace(name))
		{
			StatusLabel.Text = "Enter an item name.";
			return;
		}
		if (!decimal.TryParse(ItemPriceEntry.Text, out var price) || price < 0)
		{
			StatusLabel.Text = "Enter a valid item price.";
			return;
		}

		try
		{
			var item = new Item
			{
				Name = name,
				Description = ItemDescriptionEntry.Text,
				Price = price
			};
			var response = await _httpClient.PostAsJsonAsync("/api/items", item);
			response.EnsureSuccessStatusCode();
			ClearEditor();
			await LoadItemsAsync(resetPage: true);
		}
		catch (Exception exception)
		{
			StatusLabel.Text = $"Create failed: {exception.Message}";
		}
	}

	private async void OnUpdateItemClicked(object sender, EventArgs e)
	{
		var name = ItemNameEntry.Text?.Trim();
		if (string.IsNullOrWhiteSpace(name))
		{
			StatusLabel.Text = "Enter an item name.";
			return;
		}
		if (_editingId is null)
		{
			StatusLabel.Text = "Select an item with Edit before saving.";
			return;
		}
		if (!decimal.TryParse(ItemPriceEntry.Text, out var price) || price < 0)
		{
			StatusLabel.Text = "Enter a valid item price.";
			return;
		}

		try
		{
			var item = new Item
			{
				Name = name,
				Description = ItemDescriptionEntry.Text,
				Price = price
			};
			var response = await _httpClient.PutAsJsonAsync(
				$"/api/items/{_editingId}",
				item);
			response.EnsureSuccessStatusCode();
			ClearEditor();
			await LoadItemsAsync();
		}
		catch (Exception exception)
		{
			StatusLabel.Text = $"Update failed: {exception.Message}";
		}
	}

	private void OnEditItemClicked(object sender, EventArgs e)
	{
		if (sender is not Button { CommandParameter: Item item })
			return;

		_editingId = item.Id;
		ItemNameEntry.Text = item.Name;
		ItemDescriptionEntry.Text = item.Description;
		ItemPriceEntry.Text = item.Price.ToString("0.00");
		StatusLabel.Text = $"Editing {item.Name}";
	}

	private async void OnDeleteItemClicked(object sender, EventArgs e)
	{
		if (sender is not Button { CommandParameter: Item item } || item.Id is null)
			return;

		var confirmed = await DisplayAlertAsync(
			"Delete item",
			$"Delete \"{item.Name}\"?",
			"Delete",
			"Cancel");
		if (!confirmed)
			return;

		try
		{
			var response = await _httpClient.DeleteAsync($"/api/items/{item.Id}");
			response.EnsureSuccessStatusCode();
			if (_editingId == item.Id)
				ClearEditor();
			await LoadItemsAsync();
		}
		catch (Exception exception)
		{
			StatusLabel.Text = $"Delete failed: {exception.Message}";
		}
	}

	private void OnCancelEditClicked(object sender, EventArgs e) => ClearEditor();

	private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
	{
		_searchTimer?.Cancel();
		_searchTimer?.Dispose();
		_searchTimer = new CancellationTokenSource();
		var token = _searchTimer.Token;

		try
		{
			await Task.Delay(350, token);
			await LoadItemsAsync(resetPage: true);
		}
		catch (TaskCanceledException)
		{
		}
	}

	private async void OnFilterChanged(object sender, EventArgs e) =>
		await LoadItemsAsync(resetPage: true);

	private async void OnPreviousClicked(object sender, EventArgs e)
	{
		if (_page <= 1)
			return;
		_page--;
		await LoadItemsAsync();
	}

	private async void OnNextClicked(object sender, EventArgs e)
	{
		if (_page >= _totalPages)
			return;
		_page++;
		await LoadItemsAsync();
	}

	private void ClearEditor()
	{
		_editingId = null;
		ItemNameEntry.Text = string.Empty;
		ItemDescriptionEntry.Text = string.Empty;
		ItemPriceEntry.Text = string.Empty;
		StatusLabel.Text = "Ready";
	}
}

public sealed class Item
{
	public string? Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
	public decimal Price { get; set; }
	public string DisplayPrice =>
		Price.ToString("C", System.Globalization.CultureInfo.CurrentCulture);
}

public sealed class PagedResult<T>
{
	public IReadOnlyList<T> Items { get; set; } = [];
	public long TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
}

