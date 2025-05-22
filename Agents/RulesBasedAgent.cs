using Natak_Front_end.Controllers;
using Natak_Front_end.Core;
using Natak_Front_end.Models;
using Natak_Front_end.Services;
using Natak_Front_end.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Natak_Front_end.Agents
{
    public class RulesBasedAgent : IAgent
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

        private HashSet<Models.Point> _gameBoardTiles;
        private HashSet<ResourceType> _resourcesFromFirstVillage;
        private bool _isFirstVillage;
        public RulesBasedAgent(ApiService apiService, string gameId)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _gameId = gameId ?? throw new ArgumentNullException(nameof(gameId));
            _random = new Random();
            _resourcesFromFirstVillage = new HashSet<ResourceType>();
            _isFirstVillage = true;
        }

        public async Task PlaySetupTurn(GameController gameController, string gameId, PlayerColour playerColour)
        {
            _currentGame = await _apiService.FetchGameState(gameId, (int)playerColour);

            // make this global later, so I don't have to do this every setup turn of every game
            if(_gameBoardTiles == null)
            {
                _gameBoardTiles = new HashSet<Models.Point>(_currentGame.board.hexes.Select(hex => hex.point));
            }

            _availableVillageLocations = await _apiService.GetAvailableVillageLocations(gameId);

            if (_availableVillageLocations != null && _availableVillageLocations.Count > 0)
            {
                Dictionary<Models.Point, List<Hex>> tilesAroundVillageLocation = new Dictionary<Models.Point, List<Hex>>();
                Dictionary<Models.Point, double> villageLocationScores = new Dictionary<Models.Point, double>();
                Dictionary<Models.Point, double> diversityScores = new Dictionary<Models.Point, double>(); // Only for testing diversity scores
                var pointToHexMap = _currentGame.board.hexes.ToDictionary(hex => hex.point, hex => hex);

                foreach (Models.Point villageLocation in _availableVillageLocations)
                {
                    int r = (int)Math.Floor(0.5 * (villageLocation.x - villageLocation.y) + 1);
                    List<Models.Point> potentialTiles = new List<Models.Point>
                    { 
                        new Models.Point{x = r, y = villageLocation.y},
                        new Models.Point{x = r, y = villageLocation.y - 1},
                        (villageLocation.x + villageLocation.y) % 2 == 0
                            ? new Models.Point{x = r - 1, y = villageLocation.y}
                            : new Models.Point{x = r + 1, y = villageLocation.y - 1} 
                    };

                    List<Hex> validTiles = potentialTiles
                        .Where(point => _gameBoardTiles.Contains(point))
                        .Select(point => pointToHexMap[point])
                        .ToList();

                    if(validTiles.Count > 0)
                    {
                        double locationScore = 0;
                        List<ResourceType> availableResources = new List<ResourceType>();
                        foreach(Hex tile in validTiles)
                        {
                            locationScore += GetTileResourceProductionScore.GetYieldScore(tile.rollNumber);
                            if (!availableResources.Contains(tile.resource) && tile.resource != ResourceType.None)
                            {
                                availableResources.Add(tile.resource);
                            }
                        }

                        HashSet<ResourceType> existingResources = _isFirstVillage ? new HashSet<ResourceType>() : _resourcesFromFirstVillage;
                        double diversityScore = CalculateDiversityScore.GetDiversityScore(availableResources, existingResources);
                        diversityScores[villageLocation] = diversityScore;

                        locationScore += diversityScore;

                        villageLocationScores[villageLocation] = locationScore;
                        tilesAroundVillageLocation[villageLocation] = validTiles;
                    }
                }
                var testMin = diversityScores.MinBy(kvp => kvp.Value).Value;
                var testMax = diversityScores.MaxBy(kvp => kvp.Value).Value;

                var buildingPoint = villageLocationScores.MaxBy(kvp => kvp.Value).Key;
                _currentGame = await gameController.BuildVillage(gameId, playerColour, buildingPoint);

                if (_isFirstVillage)
                {
                    var tiles = tilesAroundVillageLocation[buildingPoint];
                    _resourcesFromFirstVillage = new HashSet<ResourceType>(tiles.Select(tile => tile.resource).Where(resource => resource != ResourceType.None));
                    _isFirstVillage = false;
                }

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
            actions.Remove(ActionType.Build_a_village);
            actions.Remove(ActionType.Build_a_town);

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

                ActionType selectedAction;
                int randomIndex = 0;

                if (_availableVillageLocations != null && _availableVillageLocations.Count > 0 && _currentGame.player.remainingVillages > 0 && ResourceCheck.Village(_currentGame.player.resourceCards))
                {
                    selectedAction = ActionType.Build_a_village;
                }
                else if (_availableTownLocations != null && _availableTownLocations.Count > 0 && _currentGame.player.remainingTowns > 0 && ResourceCheck.Town(_currentGame.player.resourceCards))
                {
                    selectedAction = ActionType.Build_a_town;
                }
                else
                {
                    randomIndex = _random.Next(actions.Count);
                    selectedAction = actions[randomIndex];
                }

                switch (selectedAction)
                {
                    case ActionType.Build_a_village:
                        if (_availableVillageLocations != null && _availableVillageLocations.Count > 0 && _currentGame.player.remainingVillages > 0)
                        {
                            if (ResourceCheck.Village(_currentGame.player.resourceCards))
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
                            if (ResourceCheck.Road(_currentGame.player.resourceCards))
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
                        if (ResourceCheck.Town(_currentGame.player.resourceCards))
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
                        actions.Remove(ActionType.Build_a_village);
                        actions.Remove(ActionType.Build_a_town);
                        break;
                    case ActionType.End_turn:
                        _currentGame = await _apiService.EndTurn(gameId, (int)_currentGame.currentPlayerColour);
                        isTurnOver = true;
                        break;
                    case ActionType.Make_trade:
                        // Don't make random trades
                        actions.RemoveAt(randomIndex);
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
                                        /*_currentGame = await _apiService.PlayRoamingCard(gameId, (int)_currentGame.currentPlayerColour);
                                        hasPlayedCard = true;
                                        actions = new List<ActionType>(_currentGame.actions);*/
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
                        actions.Remove(ActionType.Build_a_village);
                        actions.Remove(ActionType.Build_a_town);
                        break;
                }

            }
        }
    }
}
