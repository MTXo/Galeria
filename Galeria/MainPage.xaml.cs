using Microsoft.Maui.Storage;
using Microsoft.UI.Xaml;
using System.IO;
using System.Linq;
using System.Text.Json;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Galeria;

public partial class MainPage : ContentPage
{
    private const int MinThumbnailSize = 190;
    private const string SavedFoldersKey = "SavedFolders";
    private readonly List<string> _savedFolders = new();
    private readonly List<string> _allImages = new(); // Lista wszystkich zdjęć
    private bool _foldersPanelVisible = false;

    public MainPage()
    {
        InitializeComponent();
        this.SizeChanged += MainPage_SizeChanged;
        LoadSavedFolders();
        LoadImagesFromSavedFoldersAsync();
    }

    // --------------------- Ładowanie i zapis folderów ---------------------
    private void LoadSavedFolders()
    {
        var json = Preferences.Default.Get(SavedFoldersKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
        {
            var loaded = JsonSerializer.Deserialize<List<string>>(json);
            if (loaded != null)
            {
                _savedFolders.Clear();
                _savedFolders.AddRange(loaded);
            }
        }
        UpdateFoldersListUI();
    }

    private void SaveFolders()
    {
        var json = JsonSerializer.Serialize(_savedFolders);
        Preferences.Default.Set(SavedFoldersKey, json);
    }

    // --------------------- Dodawanie folderu ---------------------
    private async void AddFolderButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");

            var mauiWindow = Microsoft.Maui.Controls.Application.Current!.Windows[0];
            var nativeWindow = mauiWindow.Handler!.PlatformView as Microsoft.UI.Xaml.Window;

            if (nativeWindow != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var folder = await picker.PickSingleFolderAsync();
            if (folder?.Path != null && !_savedFolders.Contains(folder.Path))
            {
                _savedFolders.Add(folder.Path);
                SaveFolders();
                UpdateFoldersListUI();
                await LoadImagesFromFolderAsync(folder.Path);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Błąd", $"Nie udało się wybrać folderu:\n{ex.Message}", "OK");
        }
    }

    // --------------------- Usuwanie folderu ---------------------
    private void RemoveFolderButton_Clicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string path)
        {
            RemoveFolder(path);
        }
    }

    private void RemoveFolder(string folderPath)
    {
        _savedFolders.Remove(folderPath);
        SaveFolders();

        FoldersListView.ItemsSource = null;
        FoldersListView.ItemsSource = _savedFolders.ToList();

        GalleryLayout.Children.Clear();
        _allImages.Clear(); // Czyścimy listę wszystkich zdjęć
        LoadImagesFromSavedFoldersAsync();
    }

    private void UpdateFoldersListUI()
    {
        FoldersListView.ItemsSource = null;
        FoldersListView.ItemsSource = _savedFolders.ToList();
    }

    // --------------------- Pokazywanie/ukrywanie panelu ---------------------
    private void ToggleFoldersPanel_Clicked(object sender, EventArgs e)
    {
        if (_foldersPanelVisible)
        {
            FoldersPanel.IsVisible = false;
            _foldersPanelVisible = false;
        }
        else
        {
            FoldersListView.ItemsSource = _savedFolders.ToList();
            FoldersPanel.IsVisible = true;
            _foldersPanelVisible = true;
        }
    }

    private void CloseFoldersPanel_Clicked(object sender, EventArgs e)
    {
        FoldersPanel.IsVisible = false;
        _foldersPanelVisible = false;
    }

    // --------------------- Ładowanie zdjęć ---------------------
    private async void LoadImagesFromSavedFoldersAsync()
    {
        foreach (var folder in _savedFolders)
            await LoadImagesFromFolderAsync(folder);
    }

    private async Task LoadImagesFromFolderAsync(string folderPath)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(folderPath)) return;

            var files = Directory.GetFiles(folderPath)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                _allImages.Add(file); // Dodajemy do listy wszystkich zdjęć
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var grid = new Grid
                    {
                        WidthRequest = MinThumbnailSize,
                        HeightRequest = MinThumbnailSize,
                        Margin = new Microsoft.Maui.Thickness(3),
                        BackgroundColor = Colors.LightGray
                    };

                    var image = new Image
                    {
                        Source = ImageSource.FromFile(file),
                        Aspect = Aspect.AspectFill
                    };

                    var tap = new TapGestureRecognizer();
                    tap.Tapped += (s, e) => ShowFullScreenImage(file);
                    grid.GestureRecognizers.Add(tap);
                    grid.Children.Add(image);

                    GalleryLayout.Children.Add(grid);
                });
            }
        });
    }

    // --------------------- Responsywność ---------------------
    private void MainPage_SizeChanged(object? sender, EventArgs e)
    {
        if (GalleryLayout == null || this.Width <= 0) return;

        int columns = Math.Max(1, (int)(this.Width / MinThumbnailSize));
        double size = (this.Width - (columns + 1) * 6) / columns;

        foreach (var child in GalleryLayout.Children.OfType<Grid>())
        {
            child.WidthRequest = size;
            child.HeightRequest = size;
        }
    }

    // --------------------- Pełny ekran zdjęcia ---------------------
    private async void ShowFullScreenImage(string filePath)
    {
        var overlay = new Grid
        {
            BackgroundColor = new Color(0, 0, 0, 0.85f),
            InputTransparent = false
        };

        var image = new Image
        {
            Source = ImageSource.FromFile(filePath),
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var closeBtn = new Button
        {
            Text = "✕",
            FontSize = 36,
            TextColor = Colors.White,
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Microsoft.Maui.Thickness(20),
            Command = new Command(async () => await Navigation.PopModalAsync())
        };

        overlay.Children.Add(image);
        overlay.Children.Add(closeBtn);

        var page = new ContentPage { BackgroundColor = Colors.Transparent, Content = overlay };
        await Navigation.PushModalAsync(page);
    }

    // --------------------- WYSZUKIWARKA PO NAZWIE I FOLDERZE ---------------------
    private void SearchEntry_TextChanged(object sender, TextChangedEventArgs e)
    {
        string query = e.NewTextValue?.Trim().ToLower() ?? string.Empty;

        GalleryLayout.Children.Clear();

        var filtered = string.IsNullOrEmpty(query)
            ? _allImages
            : _allImages.Where(f =>
                Path.GetFileName(f).ToLower().Contains(query) || // nazwa pliku
                Path.GetDirectoryName(f).ToLower().Contains(query) // nazwa folderu
            );

        foreach (var file in filtered)
        {
            var grid = new Grid
            {
                WidthRequest = MinThumbnailSize,
                HeightRequest = MinThumbnailSize,
                Margin = new Microsoft.Maui.Thickness(3),
                BackgroundColor = Colors.LightGray
            };

            var image = new Image
            {
                Source = ImageSource.FromFile(file),
                Aspect = Aspect.AspectFill
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (s, ev) => ShowFullScreenImage(file);
            grid.GestureRecognizers.Add(tap);
            grid.Children.Add(image);

            GalleryLayout.Children.Add(grid);
        }

        // Zachowujemy responsywność miniaturek
        MainPage_SizeChanged(null, null);
    }
}