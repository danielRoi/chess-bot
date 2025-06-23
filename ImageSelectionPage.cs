using Microsoft.Maui.Controls;
using System.Threading.Tasks;

namespace ChessApp
{
    public class ImageSelectionPage : ContentPage
    {
        private readonly TaskCompletionSource<int> _selectionTcs;

        public ImageSelectionPage(bool isWhite)
        {
            Title = "Choose Promotion";
            _selectionTcs = new TaskCompletionSource<int>();

            var imageGrid = new Grid
            {
                RowDefinitions = { new RowDefinition { Height = GridLength.Auto } },
                ColumnDefinitions =
                {
                    new ColumnDefinition(), new ColumnDefinition(),
                    new ColumnDefinition(), new ColumnDefinition()
                },
                Padding = 20,
                ColumnSpacing = 10
            };

            string[] pieces = { "queen", "rook", "bishop", "knight" };

            for (int i = 0; i < 4; i++)
            {
                string pieceName = pieces[i];
                var image = new Image
                {
                    Source = ImageSource.FromFile($"{(isWhite ? "white" : "black")}_{pieceName}.png"),
                    HeightRequest = 80,
                    WidthRequest = 80
                };

                int selectedIndex = i;

                var tap = new TapGestureRecognizer();
                // Use an async lambda to properly handle the PopModalAsync call
                tap.Tapped += async (s, e) =>
                {
                    if (_selectionTcs.TrySetResult(selectedIndex))
                    {
                        // Use this page's Navigation property to pop the modal
                        await Navigation.PopModalAsync();
                    }
                };

                image.GestureRecognizers.Add(tap);
                imageGrid.Add(image, i, 0); // Explicitly add to column `i` and row `0`
            }

            Content = new StackLayout
            {
                Children =
                {
                    new Label { Text = "Choose a piece to promote to:", FontSize = 20, HorizontalOptions = LayoutOptions.Center, Padding=new Thickness(0,20) },
                    imageGrid
                },
                VerticalOptions = LayoutOptions.Center
            };
        }

        public Task<int> GetSelectionAsync() => _selectionTcs.Task;

        // Override OnDisappearing to handle cancellation
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            // If the user closes the modal without selecting, set a result to avoid a hang.
            // Using -1 to indicate cancellation.
            _selectionTcs.TrySetResult(-1);
        }
    }
}