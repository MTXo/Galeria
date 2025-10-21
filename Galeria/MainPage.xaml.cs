using Microsoft.Maui.Controls;
using System.IO;

namespace Galeria;

public partial class MainPage : ContentPage
{
    private const int MinThumbnailSize = 190;
    public MainPage()
    {
        InitializeComponent();

        this.SizeChanged += MainPage_SizeChanged;

        LoadImagesAsync(@"X:\downloads"); // zmień na swoją ścieżkę
    }

    private async void LoadImagesAsync(string folderPath)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(folderPath))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    DisplayAlert("Błąd", "Folder nie istnieje", "OK"));
                return;
            }

            var files = Directory.GetFiles(folderPath)
                .Where(f => f.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var grid = new Grid
                    {
                        WidthRequest = MinThumbnailSize,
                        HeightRequest = MinThumbnailSize,
                        Margin = 3,
                        BackgroundColor = Colors.LightGray
                    };

                    var image = new Image
                    {
                        Source = $"file:///{file}",
                        Aspect = Aspect.AspectFill
                    };

                    // Dodaj gest kliknięcia
                    var tapGesture = new TapGestureRecognizer();
                    tapGesture.Tapped += (s, e) =>
                    {
                        ShowFullScreenImage(file);
                    };
                    grid.GestureRecognizers.Add(tapGesture);

                    grid.Children.Add(image);
                    GalleryLayout.Children.Add(grid);
                });
            }
        });
    }

    private void MainPage_SizeChanged(object? sender, EventArgs e)
    {
        if (GalleryLayout == null || this.Width <= 0)
            return;

        // Obliczamy szerokość miniaturki na podstawie szerokości ekranu
        int columns = Math.Max(1, (int)(this.Width / MinThumbnailSize));
        double thumbnailSize = (this.Width - (columns + 1) * 6) / columns;

        foreach (var child in GalleryLayout.Children)
        {
            if (child is Grid grid)
            {
                grid.WidthRequest = thumbnailSize;
                grid.HeightRequest = thumbnailSize;
            }
        }
    }

    private async void ShowFullScreenImage(string filePath)
    {
        // Tworzymy półprzezroczysty overlay
        var overlay = new Grid
        {
            BackgroundColor = new Color(0, 0, 0, 90), // półprzezroczyste czarne tło
            InputTransparent = false
        };

        var image = new Image
        {
            Source = $"file:///{filePath}",
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        // Dodajemy przycisk zamykania
        var closeButton = new Button
        {
            Text = "✕",
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            FontSize = 30,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(10),
            Command = new Command(async () =>
            {
                await Navigation.PopModalAsync();
            })
        };

        overlay.Children.Add(image);
        overlay.Children.Add(closeButton);

        var popupPage = new ContentPage
        {
            BackgroundColor = Colors.Transparent, // nie przesłania całego ekranu
            Content = overlay
        };

        await Navigation.PushModalAsync(popupPage, true);
    }


}
