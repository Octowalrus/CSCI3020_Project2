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
			var allItems = await _httpClient.GetFromJsonAsync<List<Item>>("/api/items")
				?? [];
			var search = SearchEntry.Text?.Trim();
			var sort = SortPicker.SelectedIndex switch
			{
				1 => "name-desc",
				2 => "newest",
				3 => "oldest",
				_ => "name"
			};

			IEnumerable<Item> filtered = allItems;
			if (!string.IsNullOrWhiteSpace(search))
				filtered = filtered.Where(item =>
					item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
					(item.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
			if (DescriptionFilterPicker.SelectedIndex == 1)
				filtered = filtered.Where(item => !string.IsNullOrWhiteSpace(item.Description));
			else if (DescriptionFilterPicker.SelectedIndex == 2)
				filtered = filtered.Where(item => string.IsNullOrWhiteSpace(item.Description));

			filtered = sort switch
			{
				"name-desc" => filtered.OrderByDescending(item => item.Name),
				"newest" => filtered.OrderByDescending(item => item.Id),
				"oldest" => filtered.OrderBy(item => item.Id),
				_ => filtered.OrderBy(item => item.Name)
			};

			var grouped = filtered
				.GroupBy(item => new { item.Name, item.Description, item.Price })
				.Select(group => new GroupedItem
				{
					Id = group.First().Id,
					Name = group.Key.Name,
					Description = group.Key.Description,
					Price = group.Key.Price,
					StockCount = group.Count()
				})
				.OrderBy(item => item.Name)
				.ToList();

			if (sort == "name-desc")
				grouped = grouped.OrderByDescending(item => item.Name).ToList();
			else if (sort == "newest")
				grouped = grouped.OrderByDescending(item => item.Id).ToList();
			else if (sort == "oldest")
				grouped = grouped.OrderBy(item => item.Id).ToList();

			_totalPages = Math.Max(1, (int)Math.Ceiling(grouped.Count / (double)PageSize));
			_page = Math.Clamp(_page, 1, _totalPages);
			ItemsView.ItemsSource = grouped
				.Skip((_page - 1) * PageSize)
				.Take(PageSize)
				.ToList();
			PageLabel.Text = $"Page {_page} of {_totalPages}";
			PreviousButton.IsEnabled = _page > 1;
			NextButton.IsEnabled = _page < _totalPages;
			StatusLabel.Text = $"Connected - {grouped.Sum(item => item.StockCount)} item(s), {grouped.Count} grouped";
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
		if (sender is not Button { CommandParameter: GroupedItem item })
			return;

		_editingId = item.Id;
		ItemNameEntry.Text = item.Name;
		ItemDescriptionEntry.Text = item.Description;
		ItemPriceEntry.Text = item.Price.ToString("0.00");
		StatusLabel.Text = $"Editing {item.Name}";
	}

	private async void OnDeleteItemClicked(object sender, EventArgs e)
	{
		if (sender is not Button { CommandParameter: GroupedItem item } || item.Id is null)
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

	private async void OnAddCopyClicked(object sender, EventArgs e)
	{
		if (sender is not Button { CommandParameter: GroupedItem item })
			return;

		try
		{
			var copy = new Item
			{
				Name = item.Name,
				Description = item.Description,
				Price = item.Price
			};
			var response = await _httpClient.PostAsJsonAsync("/api/items", copy);
			response.EnsureSuccessStatusCode();
			StatusLabel.Text = $"Added one copy of {item.Name}";
			await LoadItemsAsync();
		}
		catch (Exception exception)
		{
			StatusLabel.Text = $"Add copy failed: {exception.Message}";
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

public sealed class GroupedItem
{
	public string? Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public string? Description { get; init; }
	public decimal Price { get; init; }
	public int StockCount { get; init; }
	public string DisplayName => $"{Name} (x{StockCount})";
	public string DisplayPrice =>
		Price.ToString("C", System.Globalization.CultureInfo.CurrentCulture);
}

