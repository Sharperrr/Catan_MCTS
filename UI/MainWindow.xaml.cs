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
using Natak_Front_end.Core;
using Natak_Front_end.Services;
using RestSharp;

namespace Natak_Front_end
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;
        public MainWindow()
        {
            InitializeComponent();
            _apiService = new ApiService();

        }

        private async void OnCreateGameClick(object sender, RoutedEventArgs e)
        {
            int numberOfGames = 100; // Adjust the number of games
            int successfulGames = 0;
            int failedGames = 0;

            for(int gameIndex = 0; gameIndex < numberOfGames; gameIndex++)
            {
                GameManager.Instance.GameId = null;
                try
                {
                    // Send the POST request
                    var response = await _apiService.CreateGame(4, 0);
                    GameManager.Instance.GameId = response?.gameId;

                    if (!string.IsNullOrEmpty(GameManager.Instance.GameId))
                    {
                        var gameBoard = new GameBoard();
                        gameBoard.Show();

                        await gameBoard.gameCompletedSource.Task;

                        successfulGames++;
                        GameIdText.Text = $"Completed Games {successfulGames}/{numberOfGames} | Last Game ID: {response?.gameId}";
                    }
                    else
                    {
                        GameIdText.Text = $"Game {gameIndex + 1}/{numberOfGames} failed: No Game ID received.";
                        failedGames++;
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