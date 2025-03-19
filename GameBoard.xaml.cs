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
using Serilog;

using Natak_Front_end.Models;
using Natak_Front_end.Requests;
using System.IO;
using System.Windows.Media.Effects;
using System.Drawing;
using System.Runtime.Intrinsics;

namespace Natak_Front_end
{
    /// <summary>
    /// Interaction logic for GameBoard.xaml
    /// </summary>

    public partial class GameBoard : Window
    {
        private readonly RestClient _client;
        private static readonly ILogger gameSummaryLogger;
        public readonly TaskCompletionSource<bool> gameCompletedSource = new TaskCompletionSource<bool>();

        public Game currentGame;

        public int turns = 0;

        private static readonly Random random = new Random();

        public List<Models.Point>? availableVillageLocations = null;
        public List<Models.Point>? availableTownLocations = null;
        public List<Road>? availableRoadLocations = null;

        private Ellipse thiefIndicator;
        private Models.Point? currentThiefLocation = null;

        private bool isDrawingEnabled = false;

        private Dictionary<(int x, int y), (Polygon hex, TextBlock numberText, Brush color, int number)> boardTiles = new Dictionary<(int x, int y), (Polygon, TextBlock, Brush, int)>();

        private double HexSize = 70;
        private double HorizontalSpacing;
        private double VerticalSpacing;

        private double VillageSize = 25;
        private double RoadSize = 30;

        private Dictionary<PlayerColour, Brush> GamePieceColours = new Dictionary<PlayerColour, Brush>
        {
            { PlayerColour.Red, Brushes.Red },
            { PlayerColour.Blue, Brushes.Blue },
            { PlayerColour.Orange, Brushes.Orange },
            { PlayerColour.White, Brushes.White }
        };

        static GameBoard()
        {
            // Initialize the game summary logger
            gameSummaryLogger = new LoggerConfiguration()
                .WriteTo.File(
                    @"C:\Studying\7 semestras\Kursinis Darbas\Front-end\Natak_Front-end\Catan_MCTS\Logs\game_summaries.csv",
                    outputTemplate: "{Message}{NewLine}",
                    rollingInterval: RollingInterval.Infinite // Keep one file for all games
                )
                .CreateLogger();

            // Write the header for the game summaries log
            //gameSummaryLogger.Information("GameId,DurationSeconds,RoundCount,RedPoints,BluePoints,OrangePoints,WhitePoints");
        }

        public GameBoard()
        {
            InitializeComponent();
            _client = new RestClient("https://localhost:7207/api/v1/natak/");

            var gameId = GameManager.Instance.GameId;
            GameIdText.Text = $"Game ID: {gameId}";

            Log.Logger = new LoggerConfiguration()
            .WriteTo.File(@"C:\Studying\7 semestras\Kursinis Darbas\Front-end\Natak_Front-end\Catan_MCTS\Logs\build_logs.csv",
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff},{Message}{NewLine}",
                rollingInterval: RollingInterval.Day) // Creates a new file each day
            .CreateLogger();

            LogBuildAction(turns, PlayerColour.no_colour, "Test", true, "", 99, 20, "Test: 1");

            VerticalSpacing = HexSize * 2;
            HorizontalSpacing = HexSize * Math.Sqrt(3);

            DrawBoard();

            PlayRandomGame(gameId);
        }

        protected override void OnClosed(EventArgs e)
        {
            Log.CloseAndFlush();
            base.OnClosed(e);
        }


        static void LogBuildAction(int turn, PlayerColour player, string building, bool succeeded, string failReason,
                              int availableLocations, int remainingPieces, string resources)
        {
            Log.Information("{Turn},{Player},{Building},{Succeeded},{FailReason},{AvailableLocations},{RemainingPieces},{Resources}",
                turn, player, building, succeeded ? "Yes" : "No", failReason, availableLocations, remainingPieces, resources);
        }

        //for this test, the game will always be a 4 player game
        private async void PlayRandomGame(string gameId)
        {
            turns = 0;
            currentGame = null;
            DateTime gameStartTime = DateTime.Now;

            //setup phase
            await FetchGameState(gameId);

            foreach (Hex tile in currentGame.board.hexes)
            {
                string hexcode = "";
                switch (tile.resource)
                {
                    case ResourceType.None:
                        currentThiefLocation = (Models.Point)tile.point.Clone();
                        double rowOffset;
                        double oddOffset = HorizontalSpacing / 2;
                        if (currentThiefLocation.y == 2 || currentThiefLocation.y == 3)
                        {
                            rowOffset = 1;
                        }
                        else if (currentThiefLocation.y == 4)
                        {
                            rowOffset = 2;
                        }
                        else
                        {
                            rowOffset = 0;
                        }
                        if (currentThiefLocation.y % 2 == 0)
                        {
                            DrawThiefIndicator(HorizontalSpacing * (currentThiefLocation.x + rowOffset), VerticalSpacing * (1 + currentThiefLocation.y * 0.75));
                        }
                        else
                        {
                            DrawThiefIndicator(HorizontalSpacing * (currentThiefLocation.x + rowOffset) + oddOffset, VerticalSpacing * (1 + currentThiefLocation.y * 0.75));
                        }
                        hexcode = "#faedca";
                        break;
                    case ResourceType.Wood:
                        hexcode = "#0a4d02";
                        break;
                    case ResourceType.Clay:
                        hexcode = "#4d0d02";
                        break;
                    case ResourceType.Animal:
                        hexcode = "#8ffa6e";
                        break;
                    case ResourceType.Food:
                        hexcode = "#fce549";
                        break;
                    case ResourceType.Metal:
                        hexcode = "#b1c2c4";
                        break;
                }
                SetHexColorAndNumber(tile.point.x, tile.point.y, hexcode, tile.rollNumber);
            }

            await PlaySetupPhase(gameId);

            int redPoints = 0;
            int bluePoints = 0;
            int orangePoints = 0;
            int whitePoints = 0;
            while (currentGame.gameState != GameState.Game_end)
            {
                await PlayRandomTurn(gameId, currentGame.currentPlayerColour);
                if(turns % 40 == 0)
                {
                    redPoints = currentGame.players[0].visibleVictoryPoints;
                    bluePoints = currentGame.players[1].visibleVictoryPoints;
                    orangePoints = currentGame.players[2].visibleVictoryPoints;
                    whitePoints = currentGame.players[3].visibleVictoryPoints;
                    GameIdText.Text = $"{currentGame.currentPlayerColour} Player's turn | Round: {turns / 4} | Game ID: {gameId}\nRed points: {redPoints}\nBlue points: {bluePoints}\nOrange points: {orangePoints}\nWhite points: {whitePoints}";
                }
                turns++;
                if(turns == 2000)
                {
                    break;
                }
            }
            int cardPoints = 0;

            await FetchGameState(gameId, (int)PlayerColour.Red);
            redPoints = currentGame.players[0].visibleVictoryPoints;
            int villagePoints = 5 - currentGame.player.remainingVillages;
            int townPoints = (4 - currentGame.player.remainingTowns) * 2;
            if(currentGame.player.playableGrowthCards.ContainsKey(GrowthCardType.Victory_point))
                cardPoints = currentGame.player.playableGrowthCards[GrowthCardType.Victory_point];
            int longestRoadPoints = currentGame.player.hasLongestRoad ? 2 : 0;
            int largestArmyPoints = currentGame.player.hasLargestArmy ? 2 : 0;
            string redDetailedPoints = "Villages: " + villagePoints + ", Towns: " + townPoints + ", Cards: " + cardPoints + ", Longest road: " + longestRoadPoints + ", Largest Army: " + largestArmyPoints;

            await FetchGameState(gameId, (int)PlayerColour.Blue);
            bluePoints = currentGame.players[1].visibleVictoryPoints;
            villagePoints = 5 - currentGame.player.remainingVillages;
            townPoints = (4 - currentGame.player.remainingTowns) * 2;
            if (currentGame.player.playableGrowthCards.ContainsKey(GrowthCardType.Victory_point))
                cardPoints = currentGame.player.playableGrowthCards[GrowthCardType.Victory_point];
            longestRoadPoints = currentGame.player.hasLongestRoad ? 2 : 0;
            largestArmyPoints = currentGame.player.hasLargestArmy ? 2 : 0;
            string blueDetailedPoints = "Villages: " + villagePoints + ", Towns: " + townPoints + ", Cards: " + cardPoints + ", Longest road: " + longestRoadPoints + ", Largest Army: " + largestArmyPoints;
            
            await FetchGameState(gameId, (int)PlayerColour.Orange);
            orangePoints = currentGame.players[2].visibleVictoryPoints;
            villagePoints = 5 - currentGame.player.remainingVillages;
            townPoints = (4 - currentGame.player.remainingTowns) * 2;
            if (currentGame.player.playableGrowthCards.ContainsKey(GrowthCardType.Victory_point))
                cardPoints = currentGame.player.playableGrowthCards[GrowthCardType.Victory_point];
            longestRoadPoints = currentGame.player.hasLongestRoad ? 2 : 0;
            largestArmyPoints = currentGame.player.hasLargestArmy ? 2 : 0;
            string orangeDetailedPoints = "Villages: " + villagePoints + ", Towns: " + townPoints + ", Cards: " + cardPoints + ", Longest road: " + longestRoadPoints + ", Largest Army: " + largestArmyPoints;

            await FetchGameState(gameId, (int)PlayerColour.White);
            whitePoints = currentGame.players[3].visibleVictoryPoints;
            villagePoints = 5 - currentGame.player.remainingVillages;
            townPoints = (4 - currentGame.player.remainingTowns) * 2;
            if (currentGame.player.playableGrowthCards.ContainsKey(GrowthCardType.Victory_point))
                cardPoints = currentGame.player.playableGrowthCards[GrowthCardType.Victory_point];
            longestRoadPoints = currentGame.player.hasLongestRoad ? 2 : 0;
            largestArmyPoints = currentGame.player.hasLargestArmy ? 2 : 0;
            string whiteDetailedPoints = "Villages: " + villagePoints + ", Towns: " + townPoints + ", Cards: " + cardPoints + ", Longest road: " + longestRoadPoints + ", Largest Army: " + largestArmyPoints;

            DateTime gameEndTime = DateTime.Now;
            double durationSeconds = (gameEndTime - gameStartTime).TotalSeconds;

            gameSummaryLogger.Information(
                "{GameId},{DurationSeconds},{RoundCount},{RedPoints},{BluePoints},{OrangePoints},{WhitePoints},{RedDetailedPoints},{BlueDetailedPoints},{OrangeDetailedPoints},{WhiteDetailedPoints}",
                gameId,
                durationSeconds,
                turns / 4,
                redPoints,
                bluePoints,
                orangePoints,
                whitePoints,
                redDetailedPoints,
                blueDetailedPoints,
                orangeDetailedPoints,
                whiteDetailedPoints
            );

            GameIdText.Text = $"{currentGame.winner} won! | Turns: {turns / 4} | Game ID: {gameId}\nRed points: {redPoints}\nBlue points: {bluePoints}\nOrange points: {orangePoints}\nWhite points: {whitePoints}";

            gameCompletedSource.SetResult(true);
            Dispatcher.Invoke(() => Close());
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
                    Models.Point randomPoint = availableVillageLocations[randomIndex];

                    //System.Diagnostics.Debug.WriteLine($"Selected Point: X = {randomPoint.x}, Y = {randomPoint.y}");
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

                    //System.Diagnostics.Debug.WriteLine($"Selected Point 1: X = {randomRoad.firstPoint.x}, Y = {randomRoad.firstPoint.y}");
                    //System.Diagnostics.Debug.WriteLine($"Selected Point 2: X = {randomRoad.secondPoint.x}, Y = {randomRoad.secondPoint.y}");
                    await BuildRoad(gameId, randomRoad.firstPoint, randomRoad.secondPoint);
                }
                else
                {
                    MessageBox.Show("No available road locations to select from.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                await EndTurn(gameId);
            }

            await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
        }

        private async Task PlayRandomTurn(string gameId, PlayerColour player)
        {
            bool isTurnOver = false;
            bool hasPlayedCard = false;
            await FetchGameState(gameId, (int)player);
            List<ActionType> actions = new List<ActionType>(currentGame.actions);
            actions.Add(ActionType.Buy_a_card);

            await GetAvailableVillageLocations(gameId);
            await GetAvailableRoadLocations(gameId);
            await GetAvailableTownLocations(gameId);

            while (!isTurnOver)
            {
                if(currentGame.gameState == GameState.Game_end)
                {
                    break;
                }
                int randomIndex = random.Next(actions.Count);
                ActionType selectedAction = actions[randomIndex];

                switch (selectedAction)
                {
                    case ActionType.Build_a_village:
                        if (availableVillageLocations != null && availableVillageLocations.Count > 0 && currentGame.player.remainingVillages > 0)
                        {
                            Dictionary<ResourceType, int> resources = currentGame.player.resourceCards;
                            if (resources.ContainsKey(ResourceType.Wood) && resources.ContainsKey(ResourceType.Clay) && resources.ContainsKey(ResourceType.Food) && resources.ContainsKey(ResourceType.Animal))
                            {
                                if (resources[ResourceType.Wood] >= 1 && resources[ResourceType.Clay] >= 1 && resources[ResourceType.Food] >= 1 && resources[ResourceType.Animal] >= 1)
                                {
                                    // Get a random index from the list
                                    randomIndex = random.Next(availableVillageLocations.Count);
                                    Models.Point randomVillagePoint = availableVillageLocations[randomIndex];

                                    //System.Diagnostics.Debug.WriteLine($"Selected Point: X = {randomPoint.x}, Y = {randomPoint.y}");
                                    await BuildVillage(gameId, randomVillagePoint);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Not enough resources to build a village");
                                    actions.RemoveAt(randomIndex);
                                    string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                    LogBuildAction(turns, player, "Village", false, "Resources", availableVillageLocations.Count, currentGame.player.remainingVillages, logResources);
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Not enough resources to build a village");
                                actions.RemoveAt(randomIndex);
                                string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                LogBuildAction(turns, player, "Village", false, "Resources", availableVillageLocations.Count, currentGame.player.remainingVillages, logResources);
                            }
                        }
                        else
                        {
                            actions.RemoveAt(randomIndex);
                            if (availableVillageLocations == null || availableVillageLocations.Count == 0)
                            {
                                LogBuildAction(turns, player, "Village", false, "Location", availableVillageLocations.Count, currentGame.player.remainingVillages, "");
                                System.Diagnostics.Debug.WriteLine($"No available village locations to select from.");
                            }
                            else if (currentGame.player.remainingVillages == 0)
                            {
                                LogBuildAction(turns, player, "Village", false, "Pieces", availableVillageLocations.Count, currentGame.player.remainingVillages, "");
                                System.Diagnostics.Debug.WriteLine($"Player doesn't have any more village pieces.");

                            }
                        }
                        break;
                    case ActionType.Build_a_road:
                        if (availableRoadLocations != null && availableRoadLocations.Count > 0 && currentGame.player.remainingRoads > 0)
                        {
                            Dictionary<ResourceType, int> resources = currentGame.player.resourceCards;
                            if (resources.ContainsKey(ResourceType.Wood) && resources.ContainsKey(ResourceType.Clay))
                            {
                                if (resources[ResourceType.Wood] >= 1 && resources[ResourceType.Clay] >= 1)
                                {
                                    // Get a random index from the list
                                    randomIndex = random.Next(availableRoadLocations.Count);
                                    Road randomRoad = availableRoadLocations[randomIndex];

                                    //System.Diagnostics.Debug.WriteLine($"Selected Point 1: X = {randomRoad.firstPoint.x}, Y = {randomRoad.firstPoint.y}");
                                    //System.Diagnostics.Debug.WriteLine($"Selected Point 2: X = {randomRoad.secondPoint.x}, Y = {randomRoad.secondPoint.y}");
                                    await BuildRoad(gameId, randomRoad.firstPoint, randomRoad.secondPoint);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Not enough resources to build a road");
                                    actions.RemoveAt(randomIndex);
                                    string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                    LogBuildAction(turns, player, "Road", false, "Resources", availableRoadLocations.Count, currentGame.player.remainingRoads, logResources);
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Not enough resources to build a road");
                                actions.RemoveAt(randomIndex);
                                string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                LogBuildAction(turns, player, "Road", false, "Resources", availableRoadLocations.Count, currentGame.player.remainingRoads, logResources);
                            }

                        }
                        else
                        {
                            actions.RemoveAt(randomIndex);
                            if (availableRoadLocations == null || availableRoadLocations.Count == 0)
                            {
                                LogBuildAction(turns, player, "Road", false, "Location", availableRoadLocations.Count, currentGame.player.remainingRoads, "");
                                System.Diagnostics.Debug.WriteLine($"No available road locations to select from.");
                            }
                            else if (currentGame.player.remainingRoads == 0)
                            {
                                LogBuildAction(turns, player, "Road", false, "Pieces", availableRoadLocations.Count, currentGame.player.remainingRoads, "");
                                System.Diagnostics.Debug.WriteLine($"Player doesn't have any more road pieces.");
                            }
                        }
                        break;
                    case ActionType.Build_a_town:
                        if (availableTownLocations != null && availableTownLocations.Count > 0 && currentGame.player.remainingTowns > 0)
                        {
                            Dictionary<ResourceType, int> resources = currentGame.player.resourceCards;
                            if (resources.ContainsKey(ResourceType.Food) && resources.ContainsKey(ResourceType.Metal))
                            {
                                if (resources[ResourceType.Food] >= 2 && resources[ResourceType.Metal] >= 3)
                                {
                                    // Get a random index from the list
                                    randomIndex = random.Next(availableTownLocations.Count);
                                    Models.Point randomTownPoint = availableTownLocations[randomIndex];

                                    //System.Diagnostics.Debug.WriteLine($"Selected Point: X = {randomPoint.x}, Y = {randomPoint.y}");
                                    await BuildTown(gameId, randomTownPoint);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Not enough resources to build a town");
                                    actions.RemoveAt(randomIndex);
                                    string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                    LogBuildAction(turns, player, "Town", false, "Resources", availableTownLocations.Count, currentGame.player.remainingTowns, logResources);
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Not enough resources to build a town");
                                actions.RemoveAt(randomIndex);
                                string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                LogBuildAction(turns, player, "Town", false, "Resources", availableTownLocations.Count, currentGame.player.remainingTowns, logResources);
                            }

                        }
                        else
                        {
                            actions.RemoveAt(randomIndex);
                            if (availableTownLocations == null || availableTownLocations.Count == 0)
                            {
                                LogBuildAction(turns, player, "Town", false, "Location", availableTownLocations.Count, currentGame.player.remainingTowns, "");
                                System.Diagnostics.Debug.WriteLine($"No available town locations to select from.");
                            }
                            else if (currentGame.player.remainingTowns == 0)
                            {
                                LogBuildAction(turns, player, "Town", false, "Pieces", availableTownLocations.Count, currentGame.player.remainingTowns, "");
                                System.Diagnostics.Debug.WriteLine($"Player doesn't have any more town pieces.");
                            }
                        }
                        break;
                    case ActionType.Buy_a_card:
                        if (currentGame.player.resourceCards.ContainsKey(ResourceType.Food) && currentGame.player.resourceCards.ContainsKey(ResourceType.Metal) && currentGame.player.resourceCards.ContainsKey(ResourceType.Animal))
                        {
                            if (currentGame.player.resourceCards[ResourceType.Food] >= 1 && currentGame.player.resourceCards[ResourceType.Metal] >= 1 && currentGame.player.resourceCards[ResourceType.Animal] >= 1)
                            {
                                await BuyGrowthCard(gameId);
                                System.Diagnostics.Debug.WriteLine("BOUGHT GROWTH CARD");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Not enough resources to buy a growth card");
                                actions.RemoveAt(randomIndex);
                                string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                LogBuildAction(turns, player, "Card", false, "Resources", -1, -1, logResources);
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Not enough resources to buy a growth card");
                            actions.RemoveAt(randomIndex);
                            string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                            LogBuildAction(turns, player, "Card", false, "Resources", -1, -1, logResources);
                        }
                        break;
                    case ActionType.Roll_the_dice:
                        await RollDice(gameId);
                        actions = new List<ActionType>(currentGame.actions);
                        actions.Add(ActionType.Buy_a_card);
                        break;
                    case ActionType.End_turn:
                        await EndTurn(gameId);
                        isTurnOver = true;
                        break;
                    case ActionType.Make_trade:
                        List<Models.Point> playerBuildings = new List<Models.Point>(
                            currentGame.board.villages
                                .Where(v => v.playerColour == currentGame.currentPlayerColour)
                                .Select(v => v.point)
                                .Concat(
                                    currentGame.board.towns
                                        .Where(t => t.playerColour == currentGame.currentPlayerColour)
                                        .Select(t => t.point)
                                )
                        );                        
                        
                        List<PortType> playerPorts = new List<PortType>();

                        foreach (Models.Point building in playerBuildings)
                        {
                            foreach (Port port in currentGame.board.ports)
                            {
                                if(building.x == port.point.x && building.y == port.point.y)
                                {
                                    playerPorts.Add(port.type);
                                    break;
                                }
                            }
                        }

                        int woodRatio = 4;
                        int clayRatio = 4;
                        int foodRatio = 4;
                        int animalRatio = 4;
                        int metalRatio  = 4;

                        if(playerPorts.Count > 0)
                        {
                            if (playerPorts.Contains(PortType.Three_to_one))
                            {
                                woodRatio = 3;
                                clayRatio = 3;
                                foodRatio = 3;
                                animalRatio = 3;
                                metalRatio = 3;
                            }
                            if (playerPorts.Contains(PortType.Wood))
                            {
                                woodRatio = 2;
                            }
                            if (playerPorts.Contains(PortType.Clay))
                            {
                                clayRatio= 2;
                            }
                            if (playerPorts.Contains(PortType.Food))
                            {
                                foodRatio= 2;
                            }
                            if (playerPorts.Contains(PortType.Animal))
                            {
                                animalRatio= 2;
                            }
                            if (playerPorts.Contains(PortType.Metal))
                            {
                                metalRatio= 2;
                            }
                        }

                        Dictionary<ResourceType, int> usableResources = new Dictionary<ResourceType, int>(currentGame.player.resourceCards);

                        List<ResourceType> tradeableResources = new List<ResourceType>();

                        foreach (var resourceEntry in usableResources)
                        {
                            ResourceType resource = resourceEntry.Key;
                            int quantity = resourceEntry.Value;

                            int requiredRatio = resource switch
                            {
                                ResourceType.Wood => woodRatio,
                                ResourceType.Clay => clayRatio,
                                ResourceType.Food => foodRatio,
                                ResourceType.Animal => animalRatio,
                                ResourceType.Metal => metalRatio,
                                _ => int.MaxValue
                            };

                            if (quantity > requiredRatio)
                            {
                                tradeableResources.Add(resource);
                            }
                        }

                        if (tradeableResources.Count == 0)
                        {
                            break;
                        }
                        else
                        {
                            ResourceType sellResource = tradeableResources[random.Next(tradeableResources.Count)];

                            ResourceType[] allResourceTypes = Enum.GetValues(typeof(ResourceType))
                            .Cast<ResourceType>()
                            .Where(rt => rt != sellResource && rt != ResourceType.None)
                            .ToArray();

                            ResourceType buyResource = allResourceTypes[random.Next(allResourceTypes.Length)];

                            System.Diagnostics.Debug.WriteLine("I have:");
                            foreach (var resource in usableResources)
                            {
                                System.Diagnostics.Debug.WriteLine($"{resource.Key}: {resource.Value}");
                            }
                            System.Diagnostics.Debug.WriteLine($"I'm gonna trade {sellResource} for some {buyResource}!");
                            await TradeWithBank(gameId, (int)sellResource, (int)buyResource);
                        }

                        //actions.RemoveAt(randomIndex);
                        break;
                    case ActionType.Play_card:
                        if (hasPlayedCard)
                        {
                            actions.Remove(ActionType.Play_card);
                            break;
                        }
                        if (currentGame.player.playableGrowthCards != null &&
                            currentGame.player.playableGrowthCards.Any(kv => kv.Key != GrowthCardType.Victory_point && kv.Value > 0) )
                        {
                            List<GrowthCardType> validCards = currentGame.player.playableGrowthCards.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                            if (validCards.Contains(GrowthCardType.Victory_point))
                            {
                                validCards.Remove(GrowthCardType.Victory_point);
                            }
                            if (validCards.Count > 0)
                            {
                                int randomCardIndex = random.Next(validCards.Count);
                                GrowthCardType randomCard = validCards[randomCardIndex];
                                switch (randomCard)
                                {
                                    case GrowthCardType.Soldier:
                                        await PlaySoldierCard(gameId);
                                        hasPlayedCard = true;
                                        actions = new List<ActionType>(currentGame.actions);
                                        break;
                                    case GrowthCardType.Roaming:
                                        // ROAMING CARD IS BUGGED ON THE API END
                                        /*await PlayRoamingCard(gameId);
                                        hasPlayedCard = true;

                                        for(int i = 0; i < 2; i++)
                                        {
                                            await GetAvailableRoadLocations(gameId);
                                            if (availableRoadLocations != null && availableRoadLocations.Count > 0)// && currentGame.player.remainingRoads > 0)
                                            {
                                                // Get a random index from the list
                                                randomIndex = random.Next(availableRoadLocations.Count);
                                                Road randomRoad = availableRoadLocations[randomIndex];

                                                //System.Diagnostics.Debug.WriteLine($"Selected Point 1: X = {randomRoad.firstPoint.x}, Y = {randomRoad.firstPoint.y}");
                                                //System.Diagnostics.Debug.WriteLine($"Selected Point 2: X = {randomRoad.secondPoint.x}, Y = {randomRoad.secondPoint.y}");
                                                await BuildRoad(gameId, randomRoad.firstPoint, randomRoad.secondPoint);
                                            }
                                            else
                                            {
                                                actions.RemoveAt(randomIndex);
                                                if (availableRoadLocations == null || availableRoadLocations.Count == 0)
                                                {
                                                    LogBuildAction(turns, player, "Road", false, "Location", availableRoadLocations.Count, currentGame.player.remainingRoads, "");
                                                    System.Diagnostics.Debug.WriteLine($"No available road locations to select from.");
                                                }
                                                *//*else if (currentGame.player.remainingRoads == 0)
                                                {
                                                    LogBuildAction(turns, player, "Road", false, "Pieces", availableRoadLocations.Count, currentGame.player.remainingRoads, "");
                                                    System.Diagnostics.Debug.WriteLine($"Player doesn't have any more road pieces.");
                                                }*//*
                                            }
                                        }

                                        actions = new List<ActionType>(currentGame.actions);*/
                                        break;
                                    case GrowthCardType.Wealth:
                                        ResourceType[] allResourceTypes = Enum.GetValues(typeof(ResourceType))
                                        .Cast<ResourceType>()
                                        .Where(rt => rt != ResourceType.None)
                                        .ToArray();

                                        int index1 = random.Next(allResourceTypes.Length);
                                        ResourceType resource1 = allResourceTypes[index1];

                                        int index2;
                                        do
                                        {
                                            index2 = random.Next(allResourceTypes.Length);
                                        } while (index2 == index1); // Keep trying until we get a different index

                                        ResourceType resource2 = allResourceTypes[index2];
                                        await PlayWealthCard(gameId, (int)resource1, (int)resource2);
                                        actions.Remove(ActionType.Play_card);
                                        hasPlayedCard = true;
                                        break;
                                    case GrowthCardType.Gatherer:
                                        ResourceType[] resourceTypes = Enum.GetValues(typeof(ResourceType))
                                        .Cast<ResourceType>()
                                        .Where(rt => rt != ResourceType.None)
                                        .ToArray();

                                        ResourceType resource = resourceTypes[random.Next(resourceTypes.Length)];
                                        await PlayGathererCard(gameId, (int)resource);
                                        actions.Remove(ActionType.Play_card);
                                        hasPlayedCard = true;
                                        break;
                                    case GrowthCardType.Victory_point:
                                        break;
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"I've got no playable cards");
                                actions.RemoveAt(randomIndex);
                            }

                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"I've got no playable cards");
                            actions.RemoveAt(randomIndex);
                        }
                        break;
                    case ActionType.Discard_resources:
                        foreach (Player discardingPlayer in currentGame.players)
                        {
                            await FetchGameState(gameId, (int)discardingPlayer.colour);
                            if (discardingPlayer.cardsToDiscard == 0)
                            {
                                continue;
                            }
                            else
                            {
                                Dictionary<ResourceType, int> availableResources = new Dictionary<ResourceType, int>(currentGame.player.resourceCards);
                                Dictionary<ResourceType, int> resourcesToDiscard = new Dictionary<ResourceType, int>();
                                int cardsToDiscard = currentGame.player.cardsToDiscard;

                                while (cardsToDiscard > 0)
                                {
                                    var validResources = availableResources.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();

                                    if (validResources.Count == 0)
                                    {
                                        break;
                                    }

                                    var randomKey = validResources[random.Next(validResources.Count)];

                                    if (!resourcesToDiscard.ContainsKey(randomKey))
                                    {
                                        resourcesToDiscard[randomKey] = 0;
                                    }
                                    availableResources[randomKey]--;
                                    resourcesToDiscard[randomKey]++;
                                    cardsToDiscard--;
                                }

                                await DiscardResources(gameId, discardingPlayer.colour, resourcesToDiscard);
                            }
                        }
                        await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                        actions = new List<ActionType>(currentGame.actions);
                        break;
                    case ActionType.Move_thief:
                        randomIndex = random.Next(currentGame.board.hexes.Count);
                        Models.Point randomPoint = currentGame.board.hexes[randomIndex].point;
                        while (randomPoint.x == currentThiefLocation.x && randomPoint.y == currentThiefLocation.y)
                        {
                            randomIndex = random.Next(currentGame.board.hexes.Count);
                            randomPoint = currentGame.board.hexes[randomIndex].point;
                        }
                        await MoveThief(gameId, randomPoint);
                        currentThiefLocation = (Models.Point)randomPoint.Clone();
                        actions = new List<ActionType>(currentGame.actions);
                        break;
                    case ActionType.Steal_resource:
                        PlayerColour randomPlayer = player;
                        while (randomPlayer == player)
                        {
                            randomIndex = random.Next(currentGame.players.Count);
                            randomPlayer = currentGame.players[randomIndex].colour;
                        }
                        await StealResource(gameId, randomPlayer);
                        actions = new List<ActionType>(currentGame.actions);
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
                        currentGame = (Game)game.Clone();
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
                    List<Models.Point> locations = JsonSerializer.Deserialize<List<Models.Point>>(response.Content);

                    if (locations != null)
                    {
                        availableVillageLocations = new List<Models.Point>(locations.Select(l => (Models.Point)l.Clone()).ToList());
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
                    List<Models.Point> locations = JsonSerializer.Deserialize<List<Models.Point>>(response.Content);

                    // Update the UI with the game state
                    if (locations != null)
                    {
                        availableTownLocations = new List<Models.Point>(locations.Select(l => (Models.Point)l.Clone()).ToList());
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
                        availableRoadLocations = new List<Road>(locations.Select(l => (Road)l.Clone()).ToList());
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

        private async Task BuildVillage(string gameId, Models.Point point)
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
                    string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                    LogBuildAction(turns, currentGame.currentPlayerColour, "Village", true, "", availableVillageLocations.Count, currentGame.player.remainingVillages, logResources);

                    double vOffset = 0;
                    if (point.y % 2 == 0)
                    {
                        if (point.x % 2 == 0)
                        {
                            vOffset = 0.25;
                        }
                        else
                        {
                            vOffset = 0;
                        }
                        DrawVillage(HorizontalSpacing * (0.5 + point.x * 0.5), VerticalSpacing * (0.75 + point.y / 2 * 1.5 + vOffset) - VillageSize, VillageSize, GamePieceColours[currentGame.currentPlayerColour]);
                    }
                    else
                    {
                        if (point.x % 2 == 0)
                        {
                            vOffset = -0.25;
                        }
                        else
                        {
                            vOffset = 0;
                        }
                        DrawVillage(HorizontalSpacing * (0.5 + point.x * 0.5), VerticalSpacing * (1.75 + (point.y - 1) / 2 * 1.5 + vOffset) - VillageSize, VillageSize, GamePieceColours[currentGame.currentPlayerColour]);
                    }

                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                    await GetAvailableVillageLocations(gameId);
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

        private async Task BuildTown(string gameId, Models.Point point)
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
                    string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                    LogBuildAction(turns, currentGame.currentPlayerColour, "Town", true, "", availableTownLocations.Count, currentGame.player.remainingTowns, logResources);

                    double vOffset = 0;
                    if (point.y % 2 == 0)
                    {
                        if (point.x % 2 == 0)
                        {
                            vOffset = 0.25;
                        }
                        else
                        {
                            vOffset = 0;
                        }
                        DrawTown(HorizontalSpacing * (0.5 + point.x * 0.5), VerticalSpacing * (0.75 + point.y / 2 * 1.5 + vOffset) - VillageSize * 2, VillageSize, GamePieceColours[currentGame.currentPlayerColour]);
                    }
                    else
                    {
                        if (point.x % 2 == 0)
                        {
                            vOffset = -0.25;
                        }
                        else
                        {
                            vOffset = 0;
                        }
                        DrawTown(HorizontalSpacing * (0.5 + point.x * 0.5), VerticalSpacing * (1.75 + (point.y - 1) / 2 * 1.5 + vOffset) - VillageSize * 2, VillageSize, GamePieceColours[currentGame.currentPlayerColour]);
                    }

                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                    await GetAvailableTownLocations(gameId);
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

        private async Task BuildRoad(string gameId, Models.Point point1, Models.Point point2)
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
                    string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                    LogBuildAction(turns, currentGame.currentPlayerColour, "Road", true, "", availableRoadLocations.Count, currentGame.player.remainingRoads, logResources);

                    double x, y;
                    (x, y) = CalculateRoadCoordinates(point1.x, point2.x, point1.y, point2.y);
                    DrawRoad(x, y, RoadSize, GamePieceColours[currentGame.currentPlayerColour]);

                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                    await GetAvailableRoadLocations(gameId);
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
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
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
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
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
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
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

        private async Task DiscardResources(string gameId, PlayerColour player, Dictionary<ResourceType, int> resourcesToDiscard)
        {
            // Define the request body
            var requestBody = new DiscardRequest { resources = resourcesToDiscard };

            var testrequest = $"{gameId}/{(int)player}/discard-resources";

            // Create the POST request
            var request = new RestRequest($"{gameId}/{(int)player}/discard-resources", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                }
                else
                {
                    MessageBox.Show($"Failed to discard resources. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error failed to discard resources: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task MoveThief(string gameId, Models.Point point)
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
                    double rowOffset;
                    double oddOffset = HorizontalSpacing / 2;
                    if (point.y == 2 || point.y == 3)
                    {
                        rowOffset = 1;
                    }
                    else if (point.y == 4)
                    {
                        rowOffset = 2;
                    }
                    else
                    {
                        rowOffset = 0;
                    }
                    if (point.y % 2 == 0)
                    {
                        DrawThiefIndicator(HorizontalSpacing * (point.x + rowOffset), VerticalSpacing * (1 + point.y * 0.75));
                    }
                    else
                    {
                        DrawThiefIndicator(HorizontalSpacing * (point.x + rowOffset) + oddOffset, VerticalSpacing * (1 + point.y * 0.75));
                    }
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
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
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                }
                else
                {
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                    //MessageBox.Show($"Failed to steal a resource. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error failed to steal a resource: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task TradeWithBank(string gameId, int sell, int buy)
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
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                }
                else
                {
                    //MessageBox.Show($"Failed to make a trade with the bank. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
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

        private async Task PlayRoamingCard(string gameId)
        {
            // Create the POST request
            var request = new RestRequest($"{gameId}/play-growth-card/roaming", Method.Post);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                }
                else
                {
                    MessageBox.Show($"Failed to play a roaming card. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error, failed to play a roaming card: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PlayWealthCard(string gameId, int resource1, int resource2)
        {
            // Define the request body
            var requestBody = new WealthCardRequest { firstResource = resource1, secondResource = resource2 };

            // Create the POST request
            var request = new RestRequest($"{gameId}/play-growth-card/wealth", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                }
                else
                {
                    MessageBox.Show($"Failed to play a wealth card. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error, failed to play a wealth card: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task PlayGathererCard(string gameId, int resource)
        {
            // Define the request body
            var requestBody = new GathererCardRequest { resource = resource };

            // Create the POST request
            var request = new RestRequest($"{gameId}/play-growth-card/gatherer", Method.Post);

            // Add the request body
            request.AddJsonBody(requestBody);

            try
            {
                var response = await _client.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    await FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                }
                else
                {
                    MessageBox.Show($"Failed to play a gatherer card. Status: {response.StatusCode}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error, failed to play a gatherer card: {ex.Message}", "Request Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //================================ BOARD DRAWING LOGIC =======================================

        private void DrawBoard()
        {
            if(!isDrawingEnabled) { return; }
            GameBoardCanvas.Children.Clear();

            var hexLayout = new List<(int x, int y)[]>
            {
                new[] { (2, 0), (3, 0), (4, 0) },
                new[] { (1, 1), (2, 1), (3, 1), (4, 1) },
                new[] { (0, 2), (1, 2), (2, 2), (3, 2), (4, 2) },
                new[] { (0, 3), (1, 3), (2, 3), (3, 3) },
                new[] { (0, 4), (1, 4), (2, 4) }
            };

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var row in hexLayout)
            {
                foreach (var (x, y) in row)
                {
                    double centerX = x * HorizontalSpacing + (y % 2 == 1 ? HorizontalSpacing / 2 : 0);
                    double centerY = y * VerticalSpacing * 0.75;
                    minX = Math.Min(minX, centerX - HexSize);
                    maxX = Math.Max(maxX, centerX + HexSize);
                    minY = Math.Min(minY, centerY - HexSize);
                    maxY = Math.Max(maxY, centerY + HexSize);
                }
            }

            double offsetX = (GameBoardCanvas.Width - (maxX - minX)) / 2 - minX;
            double offsetY = (GameBoardCanvas.Height - (maxY - minY)) / 2 - minY;

            foreach (var row in hexLayout)
            {
                foreach (var (x, y) in row)
                {
                    DrawTile(x, y, offsetX, offsetY);
                }
            }
        }

        private void DrawTile(int x, int y, double offsetX, double offsetY)
        {
            if (!isDrawingEnabled) { return; }
            double centerX = (y == 2 || y == 3 ? x + 1 : (y == 4 ? x + 2 : x)) * HorizontalSpacing + (y % 2 == 1 ? HorizontalSpacing / 2 : 0);
            double centerY = y * VerticalSpacing * 0.75 + offsetY;

            Polygon tile = new Polygon
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = Brushes.Gray
            };

            PointCollection points = new PointCollection();
            for (int i = 0; i < 6; i++)
            {
                double angle = 2 * Math.PI / 6 * (i + 0.5);
                double pointX = centerX + HexSize * Math.Cos(angle);
                double pointY = centerY + HexSize * Math.Sin(angle);
                points.Add(new System.Windows.Point(pointX, pointY));
            }
            tile.Points = points;

            TextBlock numberText = new TextBlock
            {
                Text = "",
                FontSize = 40,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            numberText.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 0,          
                ShadowDepth = 0,        
                BlurRadius = 5,         
                Opacity = 1                
            };

            Canvas.SetLeft(numberText, centerX - 14);
            Canvas.SetTop(numberText, centerY - 30);
            GameBoardCanvas.Children.Add(numberText);

            GameBoardCanvas.Children.Add(tile);
            Canvas.SetZIndex(tile, 0);
            Canvas.SetZIndex(numberText, 1);

            boardTiles[(x, y)] = (tile, numberText, Brushes.Gray, 0);
        }

        private Polygon DrawVillage(double centerX, double centerY, double size, Brush fillColor)
        {
            if (!isDrawingEnabled) { return null; }
            Polygon village = new Polygon
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = fillColor
            };

            PointCollection points = new PointCollection
            {
                new System.Windows.Point(centerX - size / 2, centerY + size / 3), // Bottom-left
                new System.Windows.Point(centerX + size / 2, centerY + size / 3), // Bottom-right
                new System.Windows.Point(centerX + size / 2, centerY - size / 2), // Top-right
                new System.Windows.Point(centerX, centerY - size),                // Apex
                new System.Windows.Point(centerX - size / 2, centerY - size / 2)  // Top-left
            };
            village.Points = points;

            GameBoardCanvas.Children.Add(village);
            Canvas.SetZIndex(village, 2); // Ensure villages/towns are above hexes and numbers

            return village;
        }

        private Polygon DrawTown(double centerX, double centerY, double size, Brush fillColor)
        {
            if (!isDrawingEnabled) { return null; }
            Polygon town = new Polygon
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Fill = fillColor
            };

            PointCollection points = new PointCollection
            {
                new System.Windows.Point(centerX - size / 2, centerY + size * 1.5),         // Bottom-left of larger base
                new System.Windows.Point(centerX + size * 1.2, centerY + size * 1.5),         // Bottom-right of larger base
                new System.Windows.Point(centerX + size * 1.2, centerY + size / 2),     // Top-right of larger base
                new System.Windows.Point(centerX + size / 2, centerY + size / 2), // Bottom-right of upper rectangle
                new System.Windows.Point(centerX + size / 2, centerY - size / 6), // Top-right of upper rectangle
                new System.Windows.Point(centerX, centerY - size / 2),                // Apex
                new System.Windows.Point(centerX - size / 2, centerY - size / 6), // Top-left of upper rectangle
                new System.Windows.Point(centerX - size / 2, centerY + size / 2), // Bottom-left of upper rectangle
                new System.Windows.Point(centerX - size / 2, centerY + size / 2)      // Top-left of larger base
            };
            town.Points = points;

            GameBoardCanvas.Children.Add(town);
            Canvas.SetZIndex(town, 2); // Ensure villages/towns are above hexes and numbers

            return town;
        }

        private Ellipse DrawRoad(double centerX, double centerY, double size, Brush fillColor)
        {
            if (!isDrawingEnabled) { return null; }

            Ellipse road = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = fillColor,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            // Position the ellipse so its center is at (centerX, centerY)
            Canvas.SetLeft(road, centerX - size / 2);
            Canvas.SetTop(road, centerY - size / 1.25);

            GameBoardCanvas.Children.Add(road);
            Canvas.SetZIndex(road, 2);

            return road;
        }

        private (double, double) CalculateRoadCoordinates(int x1, int x2, int y1, int y2)
        {
            if (!isDrawingEnabled) { return (0, 0); }
            double vOffset = 0;
            double h1, h2;
            double v1, v2;
            if (y1 % 2 == 0)
            {
                if (x1 % 2 == 0)
                {
                    vOffset = 0.25;
                }
                else
                {
                    vOffset = 0;
                }
                //DrawVillage(HorizontalSpacing * (0.5 + x1 * 0.5), VerticalSpacing * (0.75 + y1 / 2 * 1.5 + vOffset) - VillageSize, VillageSize, Brushes.LimeGreen);
                h1 = HorizontalSpacing * (0.5 + x1 * 0.5);
                v1 = VerticalSpacing * (0.75 + y1 / 2 * 1.5 + vOffset) - VillageSize;
            }
            else
            {
                if (x1 % 2 == 0)
                {
                    vOffset = -0.25;
                }
                else
                {
                    vOffset = 0;
                }
                //DrawVillage(HorizontalSpacing * (0.5 + x1 * 0.5), VerticalSpacing * (1.75 + (y1 - 1) / 2 * 1.5 + vOffset) - VillageSize, VillageSize, Brushes.LimeGreen);
                h1 = HorizontalSpacing * (0.5 + x1 * 0.5);
                v1 = VerticalSpacing * (1.75 + (y1 - 1) / 2 * 1.5 + vOffset) - VillageSize;
            }
            if (y2 % 2 == 0)
            {
                if (x2 % 2 == 0)
                {
                    vOffset = 0.25;
                }
                else
                {
                    vOffset = 0;
                }
                //DrawVillage(HorizontalSpacing * (0.5 + x2 * 0.5), VerticalSpacing * (0.75 + y2 / 2 * 1.5 + vOffset) - VillageSize, VillageSize, Brushes.LimeGreen);
                h2 = HorizontalSpacing * (0.5 + x2 * 0.5);
                v2 = VerticalSpacing * (0.75 + y2 / 2 * 1.5 + vOffset) - VillageSize;
            }
            else
            {
                if (x2 % 2 == 0)
                {
                    vOffset = -0.25;
                }
                else
                {
                    vOffset = 0;
                }
                //DrawVillage(HorizontalSpacing * (0.5 + x2 * 0.5), VerticalSpacing * (1.75 + (y2 - 1) / 2 * 1.5 + vOffset) - VillageSize, VillageSize, Brushes.LimeGreen);
                h2 = HorizontalSpacing * (0.5 + x2 * 0.5);
                v2 = VerticalSpacing * (1.75 + (y2 - 1) / 2 * 1.5 + vOffset) - VillageSize;
            }

            double x = (h1 + h2) / 2;
            double y = (v1 + v2) / 2;
            return (x, y);
        }

        private void DrawThiefIndicator(double centerX, double centerY)
        {
            if (!isDrawingEnabled) { return; }
            // Remove the old thief indicator if it exists
            if (thiefIndicator != null)
            {
                GameBoardCanvas.Children.Remove(thiefIndicator);
            }

            // Define the size of the thief indicator (relative to HexSize)
            double thiefSize = HexSize; // Adjust as needed (e.g., HexSize / 3 for a smaller ellipse)

            // Create the new thief indicator (dark gray ellipse)
            thiefIndicator = new Ellipse
            {
                Width = thiefSize,
                Height = thiefSize,
                Fill = HexToBrush("#25262b"),
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            // Position the ellipse so its center is at (centerX, centerY)
            Canvas.SetLeft(thiefIndicator, centerX - thiefSize / 2);
            Canvas.SetTop(thiefIndicator, centerY - thiefSize / 2);

            // Add the thief to the canvas
            GameBoardCanvas.Children.Add(thiefIndicator);
            Canvas.SetZIndex(thiefIndicator, 3); // Ensure the thief is above hexes (z=0), numbers (z=1), and buildings (z=2)
        }

        private SolidColorBrush HexToBrush(string hexColor) => (SolidColorBrush)new BrushConverter().ConvertFrom(hexColor);

        public void SetHexColorAndNumber(int x, int y, string color, int number)
        {
            if (!isDrawingEnabled) { return; }
            if (boardTiles.TryGetValue((x, y), out var element))
            {
                element.hex.Fill = HexToBrush(color);
                element.numberText.Text = number.ToString();
                boardTiles[(x, y)] = (element.hex, element.numberText, HexToBrush(color), number);
            }
        }

    }
}
