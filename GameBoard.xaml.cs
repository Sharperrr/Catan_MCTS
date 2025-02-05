using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HexGridControl;
using Natak_Front_end;
using System.Xml.Serialization;

namespace Natak_Front_end
{
    /// <summary>
    /// Interaction logic for GameBoard.xaml
    /// </summary>

    public partial class GameBoard : Window
    {
        private readonly RestClient _client;

        public Game currentGame;

        Random random = new Random();

        public List<Point>? availableVillageLocations = null;
        public List<Point>? availableTownLocations = null;
        public List<Road>? availableRoadLocations = null;
        public GameBoard()
        {
            InitializeComponent();
            _client = new RestClient("https://localhost:7207/api/v1/natak/");

            var gameId = GameManager.Instance.GameId;
            GameIdText.Text = $"Game ID: {gameId}";

            PlayRandomGame(gameId);
        }

        //for this test, the game will always be a 3 player game
        private async void PlayRandomGame(string gameId)
        {
            //setup phase
            await FetchGameState(gameId);
            await PlaySetupPhase(gameId);
            int turns = 0;
            while (currentGame.winner == null)
            {
                await PlayRandomTurn(gameId, currentGame.currentPlayerColour);
                GameIdText.Text = $"Turns: {turns}; Game ID: {gameId}";
                turns++;
            }
        }

        private async Task PlaySetupPhase(string gameId)
        {
            while (currentGame.gameState == GameState.Setup_village)
            {
                await GetAvailableVillageLocations(gameId);
                if (availableVillageLocations != null && availableVillageLocations.Count > 0)
                {
                    // Get a random index from the list
                    int randomIndex = random.Next(availableVillageLocations.Count);
                    Point randomPoint = availableVillageLocations[randomIndex];

                    //Console.WriteLine($"Selected Point: X = {randomPoint.x}, Y = {randomPoint.y}");
                    await BuildVillage(gameId, randomPoint);
                }
                else
                {
                    MessageBox.Show("No available village locations to select from.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                await GetAvailableRoadLocations(gameId);
                if (availableRoadLocations != null && availableRoadLocations.Count > 0)
                {
                    // Get a random index from the list
                    int randomIndex = random.Next(availableRoadLocations.Count);
                    Road randomRoad = availableRoadLocations[randomIndex];

                    //Console.WriteLine($"Selected Point 1: X = {randomRoad.firstPoint.x}, Y = {randomRoad.firstPoint.y}");
                    //Console.WriteLine($"Selected Point 2: X = {randomRoad.secondPoint.x}, Y = {randomRoad.secondPoint.y}");
                    await BuildRoad(gameId, randomRoad.firstPoint, randomRoad.secondPoint);
                }
                else
                {
                    MessageBox.Show("No available road locations to select from.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                await EndTurn(gameId);
            }

        }

        private async Task PlayRandomTurn(string gameId, PlayerColour player)
        {
            bool isTurnOver = false;
            List<ActionType> actions = currentGame.actions;

            while (!isTurnOver)
            {
                int randomIndex = random.Next(actions.Count);
                ActionType selectedAction = actions[randomIndex];

                switch(selectedAction)
                {
                    case ActionType.Build_a_village:
                        await GetAvailableVillageLocations(gameId);
                        if (availableVillageLocations != null && availableVillageLocations.Count > 0)
                        {
                            await FetchGameState(gameId, (int)player);
                            Dictionary<ResourceType, int> resources = currentGame.player.resourceCards;
                            if (resources.ContainsKey(ResourceType.Wood) && resources.ContainsKey(ResourceType.Clay) && resources.ContainsKey(ResourceType.Food) && resources.ContainsKey(ResourceType.Animal))
                            {
                                if (resources[ResourceType.Wood] >= 1 && resources[ResourceType.Clay] >= 1 && resources[ResourceType.Food] >= 1 && resources[ResourceType.Animal] >= 1)
                                {
                                    // Get a random index from the list
                                    randomIndex = random.Next(availableVillageLocations.Count);
                                    Point randomVillagePoint = availableVillageLocations[randomIndex];

                                    //Console.WriteLine($"Selected Point: X = {randomPoint.x}, Y = {randomPoint.y}");
                                    await BuildVillage(gameId, randomVillagePoint);
                                }
                                else
                                {
                                    Console.WriteLine($"Not enough resources to build a village");
                                }
                            }
                        }
                        else
                        {
                            actions.RemoveAt(randomIndex);
                            Console.WriteLine($"No available village locations to select from.");
                        }
                        break;
                    case ActionType.Build_a_road:
                        await GetAvailableRoadLocations(gameId);
                        if (availableRoadLocations != null && availableRoadLocations.Count > 0)
                        {
                            await FetchGameState(gameId, (int)player);
                            Dictionary<ResourceType, int> resources = currentGame.player.resourceCards;
                            if (resources.ContainsKey(ResourceType.Wood) && resources.ContainsKey(ResourceType.Clay))
                            {
                                if (resources[ResourceType.Wood] >= 1 && resources[ResourceType.Clay] >= 1)
                                {
                                    // Get a random index from the list
                                    randomIndex = random.Next(availableRoadLocations.Count);
                                    Road randomRoad = availableRoadLocations[randomIndex];

                                    //Console.WriteLine($"Selected Point 1: X = {randomRoad.firstPoint.x}, Y = {randomRoad.firstPoint.y}");
                                    //Console.WriteLine($"Selected Point 2: X = {randomRoad.secondPoint.x}, Y = {randomRoad.secondPoint.y}");
                                    await BuildRoad(gameId, randomRoad.firstPoint, randomRoad.secondPoint);
                                }
                                else
                                {
                                    Console.WriteLine($"Not enough resources to build a road");
                                }
                            }
                            
                        }
                        else
                        {
                            actions.RemoveAt(randomIndex);
                            Console.WriteLine($"No available road locations to select from.");
                        }
                        break;
                    case ActionType.Build_a_town:
                        await GetAvailableTownLocations(gameId);
                        if (availableTownLocations != null && availableTownLocations.Count > 0)
                        {
                            await FetchGameState(gameId, (int)player);
                            Dictionary<ResourceType, int> resources = currentGame.player.resourceCards;
                            if (resources.ContainsKey(ResourceType.Food) && resources.ContainsKey(ResourceType.Metal))
                            {
                                if (resources[ResourceType.Food] >= 2 && resources[ResourceType.Metal] >= 3)
                                {
                                    // Get a random index from the list
                                    randomIndex = random.Next(availableTownLocations.Count);
                                    Point randomTownPoint = availableTownLocations[randomIndex];

                                    //Console.WriteLine($"Selected Point: X = {randomPoint.x}, Y = {randomPoint.y}");
                                    await BuildTown(gameId, randomTownPoint);
                                }
                                else
                                {
                                    Console.WriteLine($"Not enough resources to build a town");
                                }
                            }   

                        }
                        else
                        {
                            actions.RemoveAt(randomIndex);
                            Console.WriteLine($"No available town locations to select from.");
                        }
                        break;
                    case ActionType.Roll_the_dice:
                        await RollDice(gameId);
                        actions = currentGame.actions;
                        break;
                    case ActionType.End_turn:
                        await EndTurn(gameId);
                        isTurnOver = true;
                        break;
                    case ActionType.Make_trade:
                        actions.RemoveAt(randomIndex);
                        break;
                    case ActionType.Play_card:
                        await FetchGameState(gameId, (int)player);
                        if(currentGame.player.playableGrowthCards != null && currentGame.player.playableGrowthCards.Count > 0)
                        {
                            //only play soldier cards
                            if (currentGame.player.playableGrowthCards[GrowthCardType.Soldier] > 0)
                            {
                                await PlaySoldierCard(gameId);
                            }
                            else
                            {
                                Console.WriteLine($"I've got cards, but i don't feel like playing them");
                                actions.RemoveAt(randomIndex);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"I've got no playable cards");
                            actions.RemoveAt(randomIndex);
                        }
                        break;
                    case ActionType.Discard_resources:
                        foreach (Player discardingPlayer in currentGame.players)
                        {
                            await FetchGameState(gameId, (int)discardingPlayer.colour);
                            Dictionary<ResourceType, int> availableResources = new Dictionary<ResourceType, int>(); //klaidingas priskyrimas

                            //availableResources = currentGame.player.resourceCards;

                            //Dictionary<string, int> resourcesToDiscard = new Dictionary<string, int>();
                            Dictionary<ResourceType, int> resourcesToDiscard = new Dictionary<ResourceType, int>();
                            for (int i = 1; i < 6; i++)
                            {
                                //resourcesToDiscard[i.ToString()] = 0;
                                resourcesToDiscard[(ResourceType)i] = 0;
                            }

                            int discardCheck = 0;

                            while (discardCheck < discardingPlayer.cardsToDiscard)
                            {
                                var randomKey = availableResources.Keys.ElementAt(random.Next(availableResources.Count));
                                //int discardResource = (int)randomKey - 1;
                                //resourcesToDiscard[discardResource.ToString()]++;
                                resourcesToDiscard[randomKey]++;
                                availableResources[randomKey]--;
                                //availableResources[(ResourceType)discardResource]--;
                                discardCheck++;
                            }

                            if(discardCheck > 0)
                            {
                                await DiscardResources(gameId, discardingPlayer.colour, resourcesToDiscard);
                            }
                        }
                        break;
                    case ActionType.Move_thief:
                        randomIndex = random.Next(currentGame.board.hexes.Count);
                        Point randomPoint = currentGame.board.hexes[randomIndex].point;
                        await MoveThief(gameId, randomPoint);
                        actions = currentGame.actions;
                        break;
                    case ActionType.Steal_resource:
                        PlayerColour randomPlayer = player;
                        while(randomPlayer == player)
                        {
                            randomIndex = random.Next(currentGame.players.Count);
                            randomPlayer = currentGame.players[randomIndex].colour;
                        }
                        await StealResource(gameId, randomPlayer);
                        actions = currentGame.actions;
                        break;
                }

            }
        }

        private async Task FetchGameState(string gameId, int player = 1) //to get detailed player details call FetchGameState(gameId, (int)PlayerColour);
        {
            // Create the GET request
            var request = new RestRequest($"{gameId}/{player}", Method.Get);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    // Deserialize response into a meaningful structure
                    var game = JsonSerializer.Deserialize<Game>(response.Content);

                    // Update the UI with the game state
                    if (game != null)
                    {
                        currentGame = game;
                    }
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to fetch game state. Status: {response.StatusCode}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error fetching game state: {ex.Message}",
                    "Request Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async Task GetAvailableVillageLocations(string gameId)
        {
            var request = new RestRequest($"{gameId}/available-village-locations", Method.Get);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    // Deserialize response into a meaningful structure
                    List<Point> locations = JsonSerializer.Deserialize<List<Point>>(response.Content);

                    // Update the UI with the game state
                    if (locations != null)
                    {
                        availableVillageLocations = locations;
                    }
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to fetch available village loactions. Status: {response.StatusCode}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error fetching available village loactions: {ex.Message}",
                    "Request Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async Task GetAvailableTownLocations(string gameId)
        {
            var request = new RestRequest($"{gameId}/available-town-locations", Method.Get);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    // Deserialize response into a meaningful structure
                    List<Point> locations = JsonSerializer.Deserialize<List<Point>>(response.Content);

                    // Update the UI with the game state
                    if (locations != null)
                    {
                        availableTownLocations = locations;
                    }
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to fetch available town loactions. Status: {response.StatusCode}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error fetching available town loactions: {ex.Message}",
                    "Request Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async Task GetAvailableRoadLocations(string gameId)
        {
            var request = new RestRequest($"{gameId}/available-road-locations", Method.Get);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    // Deserialize response into a meaningful structure
                    List<Road> locations = JsonSerializer.Deserialize<List<Road>>(response.Content);

                    // Update the UI with the game state
                    if (locations != null)
                    {
                        availableRoadLocations = locations;
                    }
                }
                else
                {
                    MessageBox.Show(
                        $"Failed to fetch available road loactions. Status: {response.StatusCode}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error fetching available road loactions: {ex.Message}",
                    "Request Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private async Task BuildVillage(string gameId, Point point)
        {
            // Define the request body
            var requestBody = new BuildingRequest { point = point };

            // Create the POST request
            var request = new RestRequest($"{gameId}/build/village", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to build village. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error building village: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task BuildTown(string gameId, Point point)
        {
            // Define the request body
            var requestBody = new BuildingRequest { point = point };

            // Create the POST request
            var request = new RestRequest($"{gameId}/build/town", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to build town. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error building town: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task BuildRoad(string gameId, Point point1, Point point2)
        {
            // Define the request body
            var requestBody = new RoadBuildRequest { firstPoint = point1, secondPoint = point2 };

            // Create the POST request
            var request = new RestRequest($"{gameId}/build/road", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to build road. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error building road: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RollDice(string gameId)
        {
            // Create the POST request
            var request = new RestRequest($"{gameId}/roll", Method.Post);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to roll dice. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error rolling dice: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task EndTurn(string gameId)
        {
            // Create the POST request
            var request = new RestRequest($"{gameId}/end-turn", Method.Post);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to end turn. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error ending turn: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task BuyGrowthCard(string gameId)
        {
            // Create the POST request
            var request = new RestRequest($"{gameId}/buy/growth-card", Method.Post);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to buy a growth card. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error, failed to buy a growth card: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task DiscardResources(string gameId, PlayerColour player, Dictionary<ResourceType, int> resourcesToDiscard)//int <-> string
        {
            // Define the request body
            var requestBody = new DiscardRequest { Resources = resourcesToDiscard };

            // Create the POST request
            var request = new RestRequest($"{gameId}/{player}/discard-resources", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to discard resources. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    await FetchGameState(gameId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error failed to discard resources: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task MoveThief(string gameId, Point point)
        {
            // Define the request body
            var requestBody = new MoveThiefRequest { moveThiefTo = point };

            // Create the POST request
            var request = new RestRequest($"{gameId}/move-thief", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to move thief. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error failed to move thief: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task StealResource(string gameId, PlayerColour playerToStealFrom)
        {
            // Define the request body
            var requestBody = new StealResourceRequest { victimColour = (int)playerToStealFrom };

            // Create the POST request
            var request = new RestRequest($"{gameId}/steal-resource", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    await FetchGameState(gameId);
                    //MessageBox.Show($"Failed to steal a resource. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error failed to steal a resource: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task TradeWithBank(string gameId, ResourceType sell, ResourceType buy)
        {
            // Define the request body
            var requestBody = new BankTradeRequest { resourceToGive = sell, resourceToGet = buy };

            // Create the POST request
            var request = new RestRequest($"{gameId}/trade/bank", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to make a trade with the bank. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error trading with the bank: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PlaySoldierCard(string gameId)
        {
            // Create the POST request
            var request = new RestRequest($"{gameId}/play-growth-card/soldier", Method.Post);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId);
                }
                else
                {
                    MessageBox.Show($"Failed to play a soldier. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error, failed to play a soldier card: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*
        private void DrawHexBoard()
        {
            double hexRadius = 75; // Radius of the hexagon
            double hexHeight = hexRadius * 2;
            double hexWidth = Math.Sqrt(3) * hexRadius;
            double xOffset = hexWidth; // Horizontal offset between hexes
            double yOffset = hexHeight * 0.75; // Vertical offset between rows

            // Define the hexagon layout (Catan standard: 3, 4, 5, 4, 3 hexes per row)
            int[] rowHexCounts = { 3, 4, 5, 4, 3 };

            double startX = 220; // Starting X position
            double startY = 125;

            for (int row = 0; row < rowHexCounts.Length; row++)
            {
                int hexCount = rowHexCounts[row];

                if(row == 3)
                {
                    for (int col = 0; col < hexCount; col++)
                    {
                        double x = startX + (col+1) * xOffset - row * (hexWidth / 2);
                        double y = startY + row * yOffset;

                        // Draw the hexagon
                        var hex = CreateHexagon(x, y, hexRadius);
                        GameBoardCanvas.Children.Add(hex);
                        Console.WriteLine(GameBoardCanvas.Children.Count);
                    }
                }
                else if (row == 4)
                {
                    for (int col = 0; col < hexCount; col++)
                    {
                        double x = startX + (col+2) * xOffset - row * (hexWidth / 2);
                        double y = startY + row * yOffset;

                        // Draw the hexagon
                        var hex = CreateHexagon(x, y, hexRadius);
                        GameBoardCanvas.Children.Add(hex);
                        Console.WriteLine(GameBoardCanvas.Children.Count);
                    }
                }
                else
                {
                    for (int col = 0; col < hexCount; col++)
                    {
                        double x = startX + col * xOffset - row * (hexWidth / 2);
                        double y = startY + row * yOffset;

                        // Draw the hexagon
                        var hex = CreateHexagon(x, y, hexRadius);
                        GameBoardCanvas.Children.Add(hex);
                        Console.WriteLine(GameBoardCanvas.Children.Count);
                    }
                }
                
            }
        }
        */
        /*
        private Polygon CreateHexagon(double centerX, double centerY, double radius)
        {
            // Define points for a regular hexagon with flat sides on the left and right
            var points = new PointCollection();
            for (int i = 0; i < 6; i++)
            {
                double angle = Math.PI / 3 * i + Math.PI / 6; // Rotate by 30 degrees
                points.Add(new Point(
                    centerX + radius * Math.Cos(angle),
                    centerY + radius * Math.Sin(angle)
                ));
            }

            // Create the hexagon shape
            return new Polygon
            {
                Points = points,
                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Fill = Brushes.Bisque
            };
        }
        */
        public class Game
        {
            public string id { get; set; }
            public int playerCount { get; set; }
            public PlayerColour currentPlayerColour { get; set; }
            public GameState gameState { get; set; }
            public List<ActionType>? actions { get; set; }
            public DetailedPlayer player { get; set; }
            public List<Player> players {  get; set; }
            public Board board { get; set; }
            public PlayerColour? winner {  get; set; }
            public List<int> lastRoll { get; set; }
            public PlayerColour? largestArmyPlayer {  get; set; }
            public PlayerColour? longestRoadPlayer { get; set; }
            public Dictionary<ResourceType, int> remainingResourceCards { get; set; }
            public int remainingGrowthCards { get; set; }
            public TradeOffer tradeOffer { get; set; }
        }

        public class DetailedPlayer
        {
            public int victoryPoints { get; set; }
            public Dictionary<ResourceType, int> resourceCards { get; set; }
            public Dictionary<GrowthCardType, int> playableGrowthCards { get; set; }
            public Dictionary<GrowthCardType, int> onHoldGrowthCards { get; set; }
            public PlayerColour colour { get; set; }
            public int soldiersPlayed { get; set; }
            public int visibleVictoryPoints { get; set; }
            public int totalResourceCards { get; set; }
            public int totalGrowthCards { get; set; }
            public bool hasLargestArmy { get; set; }
            public bool hasLongestRoad { get; set; }
            public int cardsToDiscard { get; set; }
            public int remainingVillages { get; set; }
            public int remainingTowns { get; set; }
            public int remainingRoads { get; set; }
            public List<PlayerColour> embargoedPlayerColours { get; set; }
        }

        public class Player
        {
            public PlayerColour colour { get; set; }
            public int soldiersPlayed { get; set; }
            public int visibleVictoryPoints { get; set; }
            public int totalResourceCards { get; set; }
            public int totalGrowthCards { get; set; }
            public bool hasLargestArmy { get; set; }
            public bool hasLongestRoad { get; set; }
            public int cardsToDiscard { get; set; }
            public int remainingVillages { get; set; }
            public int remainingTowns { get; set; }
            public int remainingRoads { get; set; }
            public List<PlayerColour> embargoedPlayerColours { get; set; }
        }

        public class Board
        {
            public List<Hex> hexes {  get; set; }
            public List<Road> roads { get; set; }
            public List<Building> villages { get; set; }
            public List<Building> towns { get; set; }
            public List<Port> ports { get; set; }
        }

        public class Building
        {
            public PlayerColour playerColour { get; set; }
            public Point point { get; set; }
        }

        public class Hex
        {
            public Point point {  get; set; }
            public ResourceType resource {  get; set; }
            public int rollNumber { get; set; }
        }

        public class Point
        {
            public int x {  get; set; }
            public int y { get; set; }
        }

        public class Port
        {
            public Point point { get; set; }
            public PortType type { get; set; }
        }

        public class Road
        {
            public PlayerColour playerColour { get; set; }
            public Point firstPoint { get; set; }
            public Point secondPoint { get; set; }
        }

        public class TradeOffer
        {
            public bool isActive { get; set; }
            public Dictionary<ResourceType, int> offer {  get; set; }
            public Dictionary<ResourceType, int> request { get; set; }
            public List<PlayerColour> rejectedBy { get; set; }
        }

        public class BuildingRequest
        {
            public Point point { get; set; }
        }

        public class RoadBuildRequest
        {
            public Point firstPoint { get; set; }
            public Point secondPoint { get; set; }
        }

        public class DiscardRequest
        {
            public Dictionary<ResourceType, int> Resources { get; set; }
            //public Dictionary<ResourceType, int> resources { get; set; }
        }

        public class MoveThiefRequest
        {
            public Point moveThiefTo { get; set; }
        }

        public class StealResourceRequest
        {
            public int victimColour { get; set; }
        }

        public class BankTradeRequest
        {
            public ResourceType resourceToGive { get; set; }
            public ResourceType resourceToGet { get; set; }
        }

        //public class GameState
        //{
        //    public string BoardState { get; set; }
        //    public override string ToString()
        //    {
        //        return $"Board State: {BoardState}";
        //    }
        //}

    }
}
