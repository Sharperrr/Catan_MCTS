using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using RestSharp;

namespace Natak_Front_end
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly RestClient _client;
        public MainWindow()
        {
            InitializeComponent();
            // Base URL of the API
            _client = new RestClient("https://localhost:7207/api/v1/natak/");
        }

        private async void OnCreateGameClick(object sender, RoutedEventArgs e)
        {
            // Define the request
            var request = new RestRequest("", Method.Post); // "" is the relative route if applicable
            request.AddJsonBody(new
            {
                playerCount = 3,
                seed = 0
            });

            try
            {
                // Send the POST request
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    // Parse the response and display the Game ID
                    var data = System.Text.Json.JsonSerializer.Deserialize<CreateGameResponse>(response.Content);
                    GameManager.Instance.GameId = data?.gameId;

                    if (!string.IsNullOrEmpty(GameManager.Instance.GameId))
                    {
                        // Navigate to the GameBoard window and pass the GameId
                        var gameBoard = new GameBoard();
                        gameBoard.Show();

                        // Optionally, close the MainWindow
                        this.Close();
                    }

                    GameIdText.Text = $"Game ID: {data?.gameId}";
                }
                else
                {
                    // Display an error message
                    GameIdText.Text = $"Error: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                MessageBox.Show($"Error: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Response model
    public class CreateGameResponse
    {
        public string gameId { get; set; }
    }
}