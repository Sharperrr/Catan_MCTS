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
                playerCount = 4,
                seed = 0
            });

            int numberOfGames = 100; // Adjust the number of games
            int successfulGames = 0;
            int failedGames = 0;

            for(int gameIndex = 0; gameIndex < numberOfGames; gameIndex++)
            {
                GameManager.Instance.GameId = null;
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

                            await gameBoard.gameCompletedSource.Task;

                            successfulGames++;
                            GameIdText.Text = $"Completed Games {successfulGames}/{numberOfGames} | Last Game ID: {data?.gameId}";

                            // Optionally, close the MainWindow
                            //this.Close();
                        }
                        else
                        {
                            GameIdText.Text = $"Game {gameIndex + 1}/{numberOfGames} failed: No Game ID received.";
                            failedGames++;
                        }

                        //GameIdText.Text = $"Game ID: {data?.gameId}";
                    }
                    else
                    {
                        // Display an error message
                        GameIdText.Text = $"Game {gameIndex + 1}/{numberOfGames} failed: {response.StatusCode}";
                        failedGames++;
                        
                        //GameIdText.Text = $"Error: {response.StatusCode}";
                    }
                }
                catch (Exception ex)
                {
                    // Handle any exceptions
                    GameIdText.Text = $"Game {gameIndex + 1}/{numberOfGames} failed: {ex.Message}";
                    failedGames++;

                    MessageBox.Show($"Error: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            MessageBox.Show($"Simulation Complete: {successfulGames} games succeeded, {failedGames} games failed. Check logs for details.",
                "Simulation Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);


        }
    }

    // Response model
    public class CreateGameResponse
    {
        public string gameId { get; set; }
    }
}