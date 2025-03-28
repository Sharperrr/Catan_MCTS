using Natak_Front_end.Core;
using Natak_Front_end.Services;
using Natak_Front_end.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Serilog;
using Natak_Front_end.Controllers;
using System.Windows;

namespace Natak_Front_end.Agents
{
    public class RandomAgent : IAgent
    {
        private readonly ApiService _apiService;
        private readonly string _gameId;
        private readonly Random _random;

        private Game _currentGame;
        private List<Models.Point> _availableVillageLocations;
        private List<Models.Point> _availableTownLocations;
        private List<Road> _availableRoadLocations;
        private Models.Point _currentThiefLocation;
        private int _discardLoopCounter;
        private const int DISCARD_LOOP_LIMIT = 20;

        public RandomAgent(ApiService apiService, string gameId)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _gameId = gameId ?? throw new ArgumentNullException(nameof(gameId));
            _random = new Random();
        }
        public async Task PlaySetupTurn(GameController gameController, string gameId, PlayerColour playerColour)
        {
            _availableVillageLocations = await _apiService.GetAvailableVillageLocations(gameId);

            if (_availableVillageLocations != null && _availableVillageLocations.Count > 0)
            {
                int randomIndex = _random.Next(_availableVillageLocations.Count);
                Models.Point randomPoint = _availableVillageLocations[randomIndex];
                _currentGame = await gameController.BuildVillage(gameId, playerColour, randomPoint);
            }
            else
            {
                MessageBox.Show("No available village locations to select from.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            _availableRoadLocations = await _apiService.GetAvailableRoadLocations(gameId);
            if (_availableRoadLocations != null && _availableRoadLocations.Count > 0)
            {
                int randomIndex = _random.Next(_availableRoadLocations.Count);
                Road randomRoad = _availableRoadLocations[randomIndex];
                _currentGame = await gameController.BuildRoad(gameId, playerColour, randomRoad.firstPoint, randomRoad.secondPoint);
            }
            else
            {
                MessageBox.Show("No available road locations to select from.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public async Task PlayTurn(GameController gameController, string gameId, PlayerColour playerColour, Models.Point thiefLocation)
        {
            _currentThiefLocation = thiefLocation;
            bool isTurnOver = false;
            bool hasPlayedCard = false;
            _currentGame = await _apiService.FetchGameState(gameId, (int)playerColour);
            List<ActionType> actions = new List<ActionType>(_currentGame.actions);
            actions.Add(ActionType.Buy_a_card);

            _discardLoopCounter = 0;

            _availableVillageLocations = await _apiService.GetAvailableVillageLocations(gameId);
            _availableRoadLocations = await _apiService.GetAvailableRoadLocations(gameId);
            _availableTownLocations = await _apiService.GetAvailableTownLocations(gameId);

            while (!isTurnOver)
            {
                if (_currentGame.gameState == GameState.Game_end)
                {
                    break;
                }

                int randomIndex = _random.Next(actions.Count);
                ActionType selectedAction = actions[randomIndex];

                switch (selectedAction)
                {
                    case ActionType.Build_a_village:
                        if (_availableVillageLocations != null && _availableVillageLocations.Count > 0 && _currentGame.player.remainingVillages > 0)
                        {
                            Dictionary<ResourceType, int> resources = _currentGame.player.resourceCards;
                            if (resources.ContainsKey(ResourceType.Wood) && resources.ContainsKey(ResourceType.Clay) && resources.ContainsKey(ResourceType.Food) && resources.ContainsKey(ResourceType.Animal))
                            {
                                if (resources[ResourceType.Wood] >= 1 && resources[ResourceType.Clay] >= 1 && resources[ResourceType.Food] >= 1 && resources[ResourceType.Animal] >= 1)
                                {
                                    randomIndex = _random.Next(_availableVillageLocations.Count);
                                    Models.Point randomVillagePoint = _availableVillageLocations[randomIndex];
                                    _currentGame = await gameController.BuildVillage(_gameId, playerColour, randomVillagePoint);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Not enough resources to build a village");
                                    actions.RemoveAt(randomIndex);
                                    //string logResources = string.Join(", ", _currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                    //LogBuildAction(turns, player, "Village", false, "Resources", availableVillageLocations.Count, _currentGame.player.remainingVillages, logResources);
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Not enough resources to build a village");
                                actions.RemoveAt(randomIndex);
                                //string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                //LogBuildAction(turns, player, "Village", false, "Resources", availableVillageLocations.Count, currentGame.player.remainingVillages, logResources);
                            }
                        }
                        else
                        {
                            actions.RemoveAt(randomIndex);
                            /*if (_availableVillageLocations == null || _availableVillageLocations.Count == 0)
                            {
                                LogBuildAction(turns, player, "Village", false, "Location", availableVillageLocations.Count, currentGame.player.remainingVillages, "");
                                System.Diagnostics.Debug.WriteLine($"No available village locations to select from.");
                            }
                            else if (currentGame.player.remainingVillages == 0)
                            {
                                LogBuildAction(turns, player, "Village", false, "Pieces", availableVillageLocations.Count, currentGame.player.remainingVillages, "");
                                System.Diagnostics.Debug.WriteLine($"Player doesn't have any more village pieces.");

                            }*/
                        }
                        break;
                    case ActionType.Build_a_road:
                        if (_availableRoadLocations != null && _availableRoadLocations.Count > 0 && _currentGame.player.remainingRoads > 0)
                        {
                            Dictionary<ResourceType, int> resources = _currentGame.player.resourceCards;
                            if (resources.ContainsKey(ResourceType.Wood) && resources.ContainsKey(ResourceType.Clay))
                            {
                                if (resources[ResourceType.Wood] >= 1 && resources[ResourceType.Clay] >= 1)
                                {
                                    randomIndex = _random.Next(_availableRoadLocations.Count);
                                    Road randomRoad = _availableRoadLocations[randomIndex];
                                    _currentGame = await gameController.BuildRoad(_gameId, playerColour, randomRoad.firstPoint, randomRoad.secondPoint);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Not enough resources to build a road");
                                    actions.RemoveAt(randomIndex);
                                    //string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                    //LogBuildAction(turns, player, "Road", false, "Resources", availableRoadLocations.Count, currentGame.player.remainingRoads, logResources);
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Not enough resources to build a road");
                                actions.RemoveAt(randomIndex);
                                //string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                //LogBuildAction(turns, player, "Road", false, "Resources", availableRoadLocations.Count, currentGame.player.remainingRoads, logResources);
                            }

                        }
                        else
                        {
                            actions.RemoveAt(randomIndex);
                            /*if (availableRoadLocations == null || availableRoadLocations.Count == 0)
                            {
                                LogBuildAction(turns, player, "Road", false, "Location", availableRoadLocations.Count, currentGame.player.remainingRoads, "");
                                System.Diagnostics.Debug.WriteLine($"No available road locations to select from.");
                            }
                            else if (currentGame.player.remainingRoads == 0)
                            {
                                LogBuildAction(turns, player, "Road", false, "Pieces", availableRoadLocations.Count, currentGame.player.remainingRoads, "");
                                System.Diagnostics.Debug.WriteLine($"Player doesn't have any more road pieces.");
                            }*/
                        }
                        break;
                    case ActionType.Build_a_town:
                        if (_availableTownLocations != null && _availableTownLocations.Count > 0 && _currentGame.player.remainingTowns > 0)
                        {
                            Dictionary<ResourceType, int> resources = _currentGame.player.resourceCards;
                            if (resources.ContainsKey(ResourceType.Food) && resources.ContainsKey(ResourceType.Metal))
                            {
                                if (resources[ResourceType.Food] >= 2 && resources[ResourceType.Metal] >= 3)
                                {
                                    randomIndex = _random.Next(_availableTownLocations.Count);
                                    Models.Point randomTownPoint = _availableTownLocations[randomIndex];
                                    _currentGame = await gameController.BuildTown(_gameId, playerColour, randomTownPoint);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Not enough resources to build a town");
                                    actions.RemoveAt(randomIndex);
                                    //string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                    //LogBuildAction(turns, player, "Town", false, "Resources", availableTownLocations.Count, currentGame.player.remainingTowns, logResources);
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Not enough resources to build a town");
                                actions.RemoveAt(randomIndex);
                                //string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                //LogBuildAction(turns, player, "Town", false, "Resources", availableTownLocations.Count, currentGame.player.remainingTowns, logResources);
                            }

                        }
                        else
                        {
                            actions.RemoveAt(randomIndex);
                            /*if (availableTownLocations == null || availableTownLocations.Count == 0)
                            {
                                LogBuildAction(turns, player, "Town", false, "Location", availableTownLocations.Count, currentGame.player.remainingTowns, "");
                                System.Diagnostics.Debug.WriteLine($"No available town locations to select from.");
                            }
                            else if (currentGame.player.remainingTowns == 0)
                            {
                                LogBuildAction(turns, player, "Town", false, "Pieces", availableTownLocations.Count, currentGame.player.remainingTowns, "");
                                System.Diagnostics.Debug.WriteLine($"Player doesn't have any more town pieces.");
                            }*/
                        }
                        break;
                    case ActionType.Buy_a_card:
                        if (_currentGame.player.resourceCards.ContainsKey(ResourceType.Food) && _currentGame.player.resourceCards.ContainsKey(ResourceType.Metal) && _currentGame.player.resourceCards.ContainsKey(ResourceType.Animal))
                        {
                            if (_currentGame.player.resourceCards[ResourceType.Food] >= 1 && _currentGame.player.resourceCards[ResourceType.Metal] >= 1 && _currentGame.player.resourceCards[ResourceType.Animal] >= 1)
                            {
                                _currentGame = await _apiService.BuyGrowthCard(gameId, (int)_currentGame.currentPlayerColour);
                                System.Diagnostics.Debug.WriteLine("BOUGHT GROWTH CARD");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Not enough resources to buy a growth card");
                                actions.RemoveAt(randomIndex);
                                //string logResources = string.Join(", ", _currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                                //LogBuildAction(turns, player, "Card", false, "Resources", -1, -1, logResources);
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Not enough resources to buy a growth card");
                            actions.RemoveAt(randomIndex);
                            //string logResources = string.Join(", ", currentGame.player.resourceCards.Select(r => $"{r.Key}: {r.Value}"));
                            //LogBuildAction(turns, player, "Card", false, "Resources", -1, -1, logResources);
                        }
                        break;
                    case ActionType.Roll_the_dice:
                        _currentGame = await _apiService.RollDice(gameId, (int)_currentGame.currentPlayerColour);
                        actions = new List<ActionType>(_currentGame.actions);
                        actions.Add(ActionType.Buy_a_card);
                        break;
                    case ActionType.End_turn:
                        _currentGame = await _apiService.EndTurn(gameId, (int)_currentGame.currentPlayerColour);
                        isTurnOver = true;
                        break;
                    case ActionType.Make_trade:
                        List<Models.Point> playerBuildings = new List<Models.Point>(
                            _currentGame.board.villages
                                .Where(v => v.playerColour == _currentGame.currentPlayerColour)
                                .Select(v => v.point)
                                .Concat(
                                    _currentGame.board.towns
                                        .Where(t => t.playerColour == _currentGame.currentPlayerColour)
                                        .Select(t => t.point)
                                )
                        );

                        List<PortType> playerPorts = new List<PortType>();

                        foreach (Models.Point building in playerBuildings)
                        {
                            foreach (Port port in _currentGame.board.ports)
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

                        Dictionary<ResourceType, int> usableResources = new Dictionary<ResourceType, int>(_currentGame.player.resourceCards);

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
                            ResourceType sellResource = tradeableResources[_random.Next(tradeableResources.Count)];

                            ResourceType[] allResourceTypes = Enum.GetValues(typeof(ResourceType))
                            .Cast<ResourceType>()
                            .Where(rt => rt != sellResource && rt != ResourceType.None)
                            .ToArray();

                            ResourceType buyResource = allResourceTypes[_random.Next(allResourceTypes.Length)];

                            System.Diagnostics.Debug.WriteLine("I have:");
                            foreach (var resource in usableResources)
                            {
                                System.Diagnostics.Debug.WriteLine($"{resource.Key}: {resource.Value}");
                            }
                            System.Diagnostics.Debug.WriteLine($"I'm gonna trade {sellResource} for some {buyResource}!");
                            _currentGame = await _apiService.TradeWithBank(gameId, (int)_currentGame.currentPlayerColour, (int)sellResource, (int)buyResource);
                        }

                        //actions.RemoveAt(randomIndex);
                        break;
                    case ActionType.Play_card:
                        if (hasPlayedCard)
                        {
                            actions.Remove(ActionType.Play_card);
                            break;
                        }
                        if (_currentGame.player.playableGrowthCards != null &&
                            _currentGame.player.playableGrowthCards.Any(kv => kv.Key != GrowthCardType.Victory_point && kv.Value > 0))
                        {
                            List<GrowthCardType> validCards = _currentGame.player.playableGrowthCards.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
                            if (validCards.Contains(GrowthCardType.Victory_point))
                            {
                                validCards.Remove(GrowthCardType.Victory_point);
                            }
                            if (validCards.Count > 0)
                            {
                                int randomCardIndex = _random.Next(validCards.Count);
                                GrowthCardType randomCard = validCards[randomCardIndex];
                                switch (randomCard)
                                {
                                    case GrowthCardType.Soldier:
                                        _currentGame = await _apiService.PlaySoldierCard(gameId, (int)_currentGame.currentPlayerColour);
                                        hasPlayedCard = true;
                                        actions = new List<ActionType>(_currentGame.actions);
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

                                        int index1 = _random.Next(allResourceTypes.Length);
                                        ResourceType resource1 = allResourceTypes[index1];

                                        int index2;
                                        do
                                        {
                                            index2 = _random.Next(allResourceTypes.Length);
                                        } while (index2 == index1); // Keep trying until we get a different index

                                        ResourceType resource2 = allResourceTypes[index2];
                                        _currentGame = await _apiService.PlayWealthCard(gameId, (int)_currentGame.currentPlayerColour, (int)resource1, (int)resource2);
                                        actions.Remove(ActionType.Play_card);
                                        hasPlayedCard = true;
                                        break;
                                    case GrowthCardType.Gatherer:
                                        ResourceType[] resourceTypes = Enum.GetValues(typeof(ResourceType))
                                        .Cast<ResourceType>()
                                        .Where(rt => rt != ResourceType.None)
                                        .ToArray();

                                        ResourceType resource = resourceTypes[_random.Next(resourceTypes.Length)];
                                        _currentGame = await _apiService.PlayGathererCard(gameId, (int)_currentGame.currentPlayerColour, (int)resource);
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
                        foreach (Player discardingPlayer in _currentGame.players)
                        {
                            _currentGame = await _apiService.FetchGameState(gameId, (int)discardingPlayer.colour);
                            if (discardingPlayer.cardsToDiscard == 0)
                            {
                                continue;
                            }
                            else
                            {
                                _discardLoopCounter++;

                                if (_discardLoopCounter > DISCARD_LOOP_LIMIT)
                                {
                                    await gameController.EndGame("Discard bug");
                                    return;
                                }

                                Dictionary<ResourceType, int> availableResources = new Dictionary<ResourceType, int>(_currentGame.player.resourceCards);
                                Dictionary<ResourceType, int> resourcesToDiscard = new Dictionary<ResourceType, int>();
                                int cardsToDiscard = _currentGame.player.cardsToDiscard;

                                while (cardsToDiscard > 0)
                                {
                                    var validResources = availableResources.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();

                                    if (validResources.Count == 0)
                                    {
                                        break;
                                    }

                                    var randomKey = validResources[_random.Next(validResources.Count)];

                                    if (!resourcesToDiscard.ContainsKey(randomKey))
                                    {
                                        resourcesToDiscard[randomKey] = 0;
                                    }
                                    availableResources[randomKey]--;
                                    resourcesToDiscard[randomKey]++;
                                    cardsToDiscard--;
                                }

                                _currentGame = await _apiService.DiscardResources(gameId, (int)discardingPlayer.colour, resourcesToDiscard);
                            }
                        }
                        _currentGame = await _apiService.FetchGameState(gameId, (int)_currentGame.currentPlayerColour);
                        actions = new List<ActionType>(_currentGame.actions);
                        break;
                    case ActionType.Move_thief:
                        randomIndex = _random.Next(_currentGame.board.hexes.Count);
                        Models.Point randomPoint = _currentGame.board.hexes[randomIndex].point;
                        while (randomPoint.x == _currentThiefLocation.x && randomPoint.y == _currentThiefLocation.y)
                        {
                            randomIndex = _random.Next(_currentGame.board.hexes.Count);
                            randomPoint = _currentGame.board.hexes[randomIndex].point;
                        }
                        _currentGame = await gameController.MoveThief(_gameId, playerColour, randomPoint);
                        _currentThiefLocation = (Models.Point)randomPoint.Clone();
                        actions = new List<ActionType>(_currentGame.actions);
                        break;
                    case ActionType.Steal_resource:
                        PlayerColour randomPlayer = playerColour;
                        while (randomPlayer == playerColour)
                        {
                            randomIndex = _random.Next(_currentGame.players.Count);
                            randomPlayer = _currentGame.players[randomIndex].colour;
                        }
                        _currentGame = await _apiService.StealResource(gameId, (int)_currentGame.currentPlayerColour, (int)randomPlayer);
                        actions = new List<ActionType>(_currentGame.actions);
                        break;
                }

            }
        }

        private void LogBuildAction(PlayerColour player, string building, bool succeeded, string failReason,
                                    int availableLocations, int remainingPieces, string resources)
        {
            Log.Information("{Turn},{Player},{Building},{Succeeded},{FailReason},{AvailableLocations},{RemainingPieces},{Resources}",
                -1, player, building, succeeded ? "Yes" : "No", failReason, availableLocations, remainingPieces, resources);
        }
    }
}
