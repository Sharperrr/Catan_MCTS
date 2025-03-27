using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Natak_Front_end.Models;
using Natak_Front_end.Services;
using Serilog;
using System.Windows;
using System.Windows.Threading;

namespace Natak_Front_end.Controllers
{
    public class GameController
    {
        private readonly ApiService _apiService;
        private static readonly ILogger gameSummaryLogger;
        private static readonly Random random = new Random();
        private readonly string gameId;

        private string errorMsg = "";
        private int discardLoopCounter = 0;
        private const int DISCARD_LOOP_LIMIT = 20;

        private Game currentGame;
        private DateTime gameStartTime;
        public int turns = 0;

        public List<Models.Point>? availableVillageLocations = null;
        public List<Models.Point>? availableTownLocations = null;
        public List<Road>? availableRoadLocations = null;
        private Models.Point? currentThiefLocation = null;

        private List<PlayerColour> PlayerOrder = new List<PlayerColour>();

        private int redPoints = 0;
        private int bluePoints = 0;
        private int orangePoints = 0;
        private int whitePoints = 0;

        // Events to notify the UI of game state changes
        public event Action<Models.Point, PlayerColour> VillageBuilt;
        public event Action<Models.Point, PlayerColour> TownBuilt;
        public event Action<Models.Point, Models.Point, PlayerColour> RoadBuilt;
        public event Action<Models.Point> ThiefMoved;
        public event Action GameEnded;
        public event Action<string> GameStateUpdated;

        static GameController()
        {
            gameSummaryLogger = new LoggerConfiguration()
                .WriteTo.File(
                    @"C:\Studying\7 semestras\Kursinis Darbas\Front-end\Natak_Front-end\Catan_MCTS\Logs\game_summaries.csv",
                    outputTemplate: "{Message}{NewLine}",
                    rollingInterval: RollingInterval.Infinite // Keep one file for all games
                )
                .CreateLogger();
  
            gameSummaryLogger.Information("GameId,Duration(s),Rounds,P1colour,P2colour,P3colour,P4colour,Winner,RedPoints,BluePoints,OrangePoints,WhitePoints,RedDetailedPoints,BlueDetailedPoints,OrangeDetailedPoints,WhiteDetailedPoints,ErrorMessage");
        }

        public GameController(ApiService apiService, string gameId)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            this.gameId = gameId ?? throw new ArgumentNullException(nameof(gameId));

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(
                    @"C:\Studying\7 semestras\Kursinis Darbas\Front-end\Natak_Front-end\Catan_MCTS\Logs\build_logs.csv",
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff},{Message}{NewLine}",
                    rollingInterval: RollingInterval.Day
                )
                .CreateLogger();

            LogBuildAction(turns, PlayerColour.no_colour, "Test", true, "", 99, 20, "Test: 1");
        }

        public async Task StartGameAsync()
        {
            try
            {
                await PlayRandomGame();
            }
            catch (Exception ex)
            {
                errorMsg = $"Error during game: {ex.Message}";
                await EndGame(errorMsg);
                throw;
            }
        }
        private async Task EndGame(string error = "")
        {
            await LogEndGameResults(error);
            GameEnded?.Invoke();
        }

        private async Task PlayRandomGame()
        {
            turns = 0;
            currentGame = null;
            gameStartTime = DateTime.Now;

            //setup phase
            currentGame = await _apiService.FetchGameState(gameId);

            foreach (Hex tile in currentGame.board.hexes)
            {
                if (tile.resource == ResourceType.None)
                {
                    currentThiefLocation = (Models.Point)tile.point.Clone();
                    ThiefMoved?.Invoke(currentThiefLocation);
                }
            }

            await PlaySetupPhase();

            while (currentGame.gameState != GameState.Game_end)
            {
                await PlayRandomTurn(currentGame.currentPlayerColour);
                if (turns % 40 == 0)
                {
                    redPoints = currentGame.players[0].visibleVictoryPoints;
                    bluePoints = currentGame.players[1].visibleVictoryPoints;
                    orangePoints = currentGame.players[2].visibleVictoryPoints;
                    whitePoints = currentGame.players[3].visibleVictoryPoints;
                    GameStateUpdated?.Invoke($"{currentGame.currentPlayerColour} Player's turn | Round: {turns / 4} | Game ID: {gameId}\nRed points: {redPoints}\nBlue points: {bluePoints}\nOrange points: {orangePoints}\nWhite points: {whitePoints}");
                }
                turns++;
                if (turns == 2000)
                {
                    errorMsg = "Maximum game length exceded";
                    break;
                }
            }

            await EndGame(errorMsg);
        }

        private async Task PlaySetupPhase()
        {
            while (currentGame.gameState == GameState.Setup_village)
            {
                if (PlayerOrder.Count != 4)
                {
                    PlayerOrder.Add(currentGame.currentPlayerColour);
                }
                availableVillageLocations = await _apiService.GetAvailableVillageLocations(gameId);

                if (availableVillageLocations != null && availableVillageLocations.Count > 0)
                {
                    int randomIndex = random.Next(availableVillageLocations.Count);
                    Models.Point randomPoint = availableVillageLocations[randomIndex];
                    await BuildVillage(randomPoint);
                }
                else
                {
                    MessageBox.Show("No available village locations to select from.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                availableRoadLocations = await _apiService.GetAvailableRoadLocations(gameId);
                if (availableRoadLocations != null && availableRoadLocations.Count > 0)
                {
                    int randomIndex = random.Next(availableRoadLocations.Count);
                    Road randomRoad = availableRoadLocations[randomIndex];
                    await BuildRoad(randomRoad.firstPoint, randomRoad.secondPoint);
                }
                else
                {
                    MessageBox.Show("No available road locations to select from.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                currentGame = await _apiService.EndTurn(gameId, (int)currentGame.currentPlayerColour);
            }
        }

        private async Task PlayRandomTurn(PlayerColour player)
        {
            bool isTurnOver = false;
            bool hasPlayedCard = false;
            currentGame = await _apiService.FetchGameState(gameId, (int)player);
            List<ActionType> actions = new List<ActionType>(currentGame.actions);
            actions.Add(ActionType.Buy_a_card);

            discardLoopCounter = 0;

            availableVillageLocations = await _apiService.GetAvailableVillageLocations(gameId);
            availableRoadLocations = await _apiService.GetAvailableRoadLocations(gameId);
            availableTownLocations = await _apiService.GetAvailableTownLocations(gameId);

            while (!isTurnOver)
            {
                if (currentGame.gameState == GameState.Game_end)
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
                                    randomIndex = random.Next(availableVillageLocations.Count);
                                    Models.Point randomVillagePoint = availableVillageLocations[randomIndex];
                                    await BuildVillage(randomVillagePoint);
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
                                    randomIndex = random.Next(availableRoadLocations.Count);
                                    Road randomRoad = availableRoadLocations[randomIndex];
                                    await BuildRoad(randomRoad.firstPoint, randomRoad.secondPoint);
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
                                    randomIndex = random.Next(availableTownLocations.Count);
                                    Models.Point randomTownPoint = availableTownLocations[randomIndex];
                                    await BuildTown(randomTownPoint);
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
                                currentGame = await _apiService.BuyGrowthCard(gameId, (int)currentGame.currentPlayerColour);
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
                        currentGame = await _apiService.RollDice(gameId, (int)currentGame.currentPlayerColour);
                        actions = new List<ActionType>(currentGame.actions);
                        actions.Add(ActionType.Buy_a_card);
                        break;
                    case ActionType.End_turn:
                        currentGame = await _apiService.EndTurn(gameId, (int)currentGame.currentPlayerColour);
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
                                if (building.x == port.point.x && building.y == port.point.y)
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
                        int metalRatio = 4;

                        if (playerPorts.Count > 0)
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
                                clayRatio = 2;
                            }
                            if (playerPorts.Contains(PortType.Food))
                            {
                                foodRatio = 2;
                            }
                            if (playerPorts.Contains(PortType.Animal))
                            {
                                animalRatio = 2;
                            }
                            if (playerPorts.Contains(PortType.Metal))
                            {
                                metalRatio = 2;
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
                            currentGame = await _apiService.TradeWithBank(gameId, (int)currentGame.currentPlayerColour, (int)sellResource, (int)buyResource);
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
                            currentGame.player.playableGrowthCards.Any(kv => kv.Key != GrowthCardType.Victory_point && kv.Value > 0))
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
                                        currentGame = await _apiService.PlaySoldierCard(gameId, (int)currentGame.currentPlayerColour);
                                        hasPlayedCard = true;
                                        actions = new List<ActionType>(currentGame.actions);
                                        break;
                                    case GrowthCardType.Roaming:
                                        // ROAMING CARD IS BUGGED ON THE API END
                                        /*currentGame = await _apiService.PlayRoamingCard(gameId, (int)currentGame.currentPlayerColour);
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
                                        currentGame = await _apiService.PlayWealthCard(gameId, (int)currentGame.currentPlayerColour, (int)resource1, (int)resource2);
                                        actions.Remove(ActionType.Play_card);
                                        hasPlayedCard = true;
                                        break;
                                    case GrowthCardType.Gatherer:
                                        ResourceType[] resourceTypes = Enum.GetValues(typeof(ResourceType))
                                        .Cast<ResourceType>()
                                        .Where(rt => rt != ResourceType.None)
                                        .ToArray();

                                        ResourceType resource = resourceTypes[random.Next(resourceTypes.Length)];
                                        currentGame = await _apiService.PlayGathererCard(gameId, (int)currentGame.currentPlayerColour, (int)resource);
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
                            currentGame = await _apiService.FetchGameState(gameId, (int)discardingPlayer.colour);
                            if (discardingPlayer.cardsToDiscard == 0)
                            {
                                continue;
                            }
                            else
                            {
                                discardLoopCounter++;

                                if (discardLoopCounter > DISCARD_LOOP_LIMIT)
                                {
                                    currentGame.gameState = GameState.Game_end;
                                    errorMsg = "Discard bug";
                                    return;
                                }

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

                                currentGame = await _apiService.DiscardResources(gameId, (int)discardingPlayer.colour, resourcesToDiscard);
                            }
                        }
                        currentGame = await _apiService.FetchGameState(gameId, (int)currentGame.currentPlayerColour);
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
                        await MoveThief(randomPoint);
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
                        currentGame = await _apiService.StealResource(gameId, (int)currentGame.currentPlayerColour, (int)randomPlayer);
                        actions = new List<ActionType>(currentGame.actions);
                        break;
                }

            }
        }

        private async Task BuildVillage(Models.Point point)
        {
            currentGame = await _apiService.BuildVillage(gameId, (int)currentGame.currentPlayerColour, point);
            VillageBuilt?.Invoke(point, currentGame.currentPlayerColour);
        }

        private async Task BuildTown(Models.Point point)
        {
            currentGame = await _apiService.BuildTown(gameId, (int)currentGame.currentPlayerColour, point);
            TownBuilt?.Invoke(point, currentGame.currentPlayerColour);
        }

        private async Task BuildRoad(Models.Point point1, Models.Point point2)
        {
            currentGame = await _apiService.BuildRoad(gameId, (int)currentGame.currentPlayerColour, point1, point2);
            RoadBuilt?.Invoke(point1, point2, currentGame.currentPlayerColour);
        }

        private async Task MoveThief(Models.Point point)
        {
            currentGame = await _apiService.MoveThief(gameId, (int)currentGame.currentPlayerColour, point);
            ThiefMoved?.Invoke(point);
        }

        public async Task InitializeBoard()
        {
            currentGame = await _apiService.FetchGameState(gameId);
            foreach (Hex tile in currentGame.board.hexes)
            {
                string hexcode = tile.resource switch
                {
                    ResourceType.None => "#faedca",
                    ResourceType.Wood => "#0a4d02",
                    ResourceType.Clay => "#4d0d02",
                    ResourceType.Animal => "#8ffa6e",
                    ResourceType.Food => "#fce549",
                    ResourceType.Metal => "#b1c2c4",
                    _ => "#000000"
                };
                // Notify UI to set hex color and number
                GameStateUpdated?.Invoke($"SetHexColorAndNumber:{tile.point.x},{tile.point.y},{hexcode},{tile.rollNumber}");
            }
        }

        // LOGGING

        static void LogBuildAction(int turn, PlayerColour player, string building, bool succeeded, string failReason,
                              int availableLocations, int remainingPieces, string resources)
        {
            Log.Information("{Turn},{Player},{Building},{Succeeded},{FailReason},{AvailableLocations},{RemainingPieces},{Resources}",
                turn, player, building, succeeded ? "Yes" : "No", failReason, availableLocations, remainingPieces, resources);
        }

        private async Task LogEndGameResults(string error)
        {
            int cardPoints = 0;

            currentGame = await _apiService.FetchGameState(gameId, (int)PlayerColour.Red);
            redPoints = currentGame.players[0].visibleVictoryPoints;
            int villagePoints = 5 - currentGame.player.remainingVillages;
            int townPoints = (4 - currentGame.player.remainingTowns) * 2;
            if (currentGame.player.playableGrowthCards.ContainsKey(GrowthCardType.Victory_point))
                cardPoints = currentGame.player.playableGrowthCards[GrowthCardType.Victory_point];
            int longestRoadPoints = currentGame.player.hasLongestRoad ? 2 : 0;
            int largestArmyPoints = currentGame.player.hasLargestArmy ? 2 : 0;
            string redDetailedPoints = "Villages: " + villagePoints + ", Towns: " + townPoints + ", Cards: " + cardPoints + ", Longest road: " + longestRoadPoints + ", Largest Army: " + largestArmyPoints;

            currentGame = await _apiService.FetchGameState(gameId, (int)PlayerColour.Blue);
            bluePoints = currentGame.players[1].visibleVictoryPoints;
            villagePoints = 5 - currentGame.player.remainingVillages;
            townPoints = (4 - currentGame.player.remainingTowns) * 2;
            if (currentGame.player.playableGrowthCards.ContainsKey(GrowthCardType.Victory_point))
                cardPoints = currentGame.player.playableGrowthCards[GrowthCardType.Victory_point];
            longestRoadPoints = currentGame.player.hasLongestRoad ? 2 : 0;
            largestArmyPoints = currentGame.player.hasLargestArmy ? 2 : 0;
            string blueDetailedPoints = "Villages: " + villagePoints + ", Towns: " + townPoints + ", Cards: " + cardPoints + ", Longest road: " + longestRoadPoints + ", Largest Army: " + largestArmyPoints;

            currentGame = await _apiService.FetchGameState(gameId, (int)PlayerColour.Orange);
            orangePoints = currentGame.players[2].visibleVictoryPoints;
            villagePoints = 5 - currentGame.player.remainingVillages;
            townPoints = (4 - currentGame.player.remainingTowns) * 2;
            if (currentGame.player.playableGrowthCards.ContainsKey(GrowthCardType.Victory_point))
                cardPoints = currentGame.player.playableGrowthCards[GrowthCardType.Victory_point];
            longestRoadPoints = currentGame.player.hasLongestRoad ? 2 : 0;
            largestArmyPoints = currentGame.player.hasLargestArmy ? 2 : 0;
            string orangeDetailedPoints = "Villages: " + villagePoints + ", Towns: " + townPoints + ", Cards: " + cardPoints + ", Longest road: " + longestRoadPoints + ", Largest Army: " + largestArmyPoints;

            currentGame = await _apiService.FetchGameState(gameId, (int)PlayerColour.White);
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

            PlayerColour? winner = currentGame.winner;

            gameSummaryLogger.Information(
                "{GameId},{DurationSeconds},{RoundCount},{Player1},{Player2},{Player3},{Player4},{Winner},{RedPoints},{BluePoints},{OrangePoints},{WhitePoints},{RedDetailedPoints},{BlueDetailedPoints},{OrangeDetailedPoints},{WhiteDetailedPoints},{ErrorMessage}",
                gameId,
                durationSeconds,
                turns / 4,
                PlayerOrder[0],
                PlayerOrder[1],
                PlayerOrder[2],
                PlayerOrder[3],
                winner,
                redPoints,
                bluePoints,
                orangePoints,
                whitePoints,
                redDetailedPoints,
                blueDetailedPoints,
                orangeDetailedPoints,
                whiteDetailedPoints,
                error
            );
        }
    }
}
