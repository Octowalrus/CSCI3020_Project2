using System.Net.Http.Json;

namespace MauiApp;

public partial class MainPage : ContentPage
{
	private readonly HttpClient _httpClient = new()
	{
		BaseAddress = new Uri("http://10.0.2.2:8080")
	};

	public MainPage()
	{
		InitializeComponent();
	}

	private async void OnCheckApiClicked(object sender, EventArgs e)
	{
		try
		{
			var items = await _httpClient.GetFromJsonAsync<List<Item>>("/api/items");
			ItemsView.ItemsSource = items;
			StatusLabel.Text = $"Connected - {items?.Count ?? 0} item(s)";
		}
		catch (Exception exception)
		{
			StatusLabel.Text = $"API unavailable: {exception.Message}";
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

		try
		{
			var item = new Item
			{
				Name = name,
				Description = ItemDescriptionEntry.Text
			};
			var response = await _httpClient.PostAsJsonAsync("/api/items", item);
			response.EnsureSuccessStatusCode();
			ItemNameEntry.Text = string.Empty;
			ItemDescriptionEntry.Text = string.Empty;
			OnCheckApiClicked(sender, e);
		}
		catch (Exception exception)
		{
			StatusLabel.Text = $"Create failed: {exception.Message}";
		}
	}
}

public sealed class Item
{
	public string? Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public string? Description { get; set; }
}

