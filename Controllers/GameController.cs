using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Natak_Front_end.Models;
using Natak_Front_end.Services;
using Serilog;
using System.Windows;
using System.Windows.Threading;
using Natak_Front_end.Core;
using Natak_Front_end.Agents;

namespace Natak_Front_end.Controllers
{
    public class GameController
    {
        private readonly ApiService _apiService;
        private static readonly ILogger gameSummaryLogger;
        private static readonly Random random = new Random();
        private readonly string gameId;

        private string errorMsg = "";
        private bool isGameStuck = false;
        
        private Game currentGame;
        private DateTime gameStartTime;
        public int turns = 0;

        private Models.Point? currentThiefLocation = null;

        private int PlayerCount;
        private List<PlayerColour> PlayerOrder = new List<PlayerColour>();

        private int redPoints = 0;
        private int bluePoints = 0;
        private int orangePoints = 0;
        private int whitePoints = 0;

        private readonly Dictionary<PlayerColour, IAgent> playerAgents;

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

        public GameController(ApiService apiService, string gameId, int playerCount)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            this.gameId = gameId ?? throw new ArgumentNullException(nameof(gameId));
            PlayerCount = playerCount;

            /*Log.Logger = new LoggerConfiguration()
                .WriteTo.File(
                    @"C:\Studying\7 semestras\Kursinis Darbas\Front-end\Natak_Front-end\Catan_MCTS\Logs\build_logs.csv",
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff},{Message}{NewLine}",
                    rollingInterval: RollingInterval.Day
                )
                .CreateLogger();*/

            //LogBuildAction(turns, PlayerColour.no_colour, "Test", true, "", 99, 20, "Test: 1");

            playerAgents = new Dictionary<PlayerColour, IAgent>
            {
                { PlayerColour.Red, new MCTSAgent(_apiService, gameId) },
                { PlayerColour.Blue, new RandomAgent(_apiService, gameId) },
                { PlayerColour.Orange, new RandomAgent(_apiService, gameId) },
                { PlayerColour.White, new RandomAgent(_apiService, gameId) }
            };
        }

        public async Task StartGameAsync()
        {
            try
            {
                await PlayGame();
            }
            catch (Exception ex)
            {
                errorMsg = $"Error during game: {ex.Message}";
                await EndGame(errorMsg);
                throw;
            }
        }
        public async Task EndGame(string error = "")
        {
            await LogEndGameResults(error);
            isGameStuck = true;
            GameEnded?.Invoke();
        }

        private async Task PlayGame()
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
                IAgent currentAgent = playerAgents[currentGame.currentPlayerColour];
                await currentAgent.PlayTurn(this, gameId, currentGame.currentPlayerColour, currentThiefLocation);

                if (isGameStuck)
                {
                    return;
                }
                else
                {
                    currentGame = await _apiService.FetchGameState(gameId, (int)currentGame.currentPlayerColour);
                }

                if (turns == 90)
                {
                    Console.WriteLine("Break time");
                }

                if (turns % PlayerCount == 0)
                {
                    redPoints = currentGame.players[0].visibleVictoryPoints;
                    bluePoints = currentGame.players[1].visibleVictoryPoints;
                    orangePoints = currentGame.players[2].visibleVictoryPoints;
                    if(PlayerCount == 4)
                    {
                        whitePoints = currentGame.players[3].visibleVictoryPoints;
                    }
                    GameStateUpdated?.Invoke($"{currentGame.currentPlayerColour} Player's turn | Round: {turns / PlayerCount} | Game ID: {gameId}\nRed points: {redPoints}\nBlue points: {bluePoints}\nOrange points: {orangePoints}\nWhite points: {whitePoints}");
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
                if (PlayerOrder.Count != PlayerCount)
                {
                    PlayerOrder.Add(currentGame.currentPlayerColour);
                }

                IAgent currentAgent = playerAgents[currentGame.currentPlayerColour];
                await currentAgent.PlaySetupTurn(this, gameId, currentGame.currentPlayerColour);
                currentGame = await _apiService.EndTurn(gameId, (int)currentGame.currentPlayerColour);
            }
        }

        public async Task<Game> BuildVillage(String gameId, PlayerColour playerColour, Models.Point point)
        {
            currentGame = await _apiService.BuildVillage(gameId, (int)playerColour, point);
            VillageBuilt?.Invoke(point, currentGame.currentPlayerColour);
            return currentGame;
        }

        public async Task<Game> BuildTown(String gameId, PlayerColour playerColour, Models.Point point)
        {
            currentGame = await _apiService.BuildTown(gameId, (int)playerColour, point);
            TownBuilt?.Invoke(point, currentGame.currentPlayerColour);
            return currentGame;
        }

        public async Task<Game> BuildRoad(String gameId, PlayerColour playerColour, Models.Point point1, Models.Point point2)
        {
            currentGame = await _apiService.BuildRoad(gameId, (int)playerColour, point1, point2);
            RoadBuilt?.Invoke(point1, point2, currentGame.currentPlayerColour);
            return currentGame;
        }

        public async Task<Game> MoveThief(String gameId, PlayerColour playerColour, Models.Point point)
        {
            currentGame = await _apiService.MoveThief(gameId, (int)playerColour, point);
            ThiefMoved?.Invoke(point);
            return currentGame;
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

            if (PlayerCount == 4)
            {
                currentGame = await _apiService.FetchGameState(gameId, (int)PlayerColour.White);
                whitePoints = currentGame.players[3].visibleVictoryPoints;
                villagePoints = 5 - currentGame.player.remainingVillages;
                townPoints = (4 - currentGame.player.remainingTowns) * 2;
                if (currentGame.player.playableGrowthCards.ContainsKey(GrowthCardType.Victory_point))
                    cardPoints = currentGame.player.playableGrowthCards[GrowthCardType.Victory_point];
                longestRoadPoints = currentGame.player.hasLongestRoad ? 2 : 0;
                largestArmyPoints = currentGame.player.hasLargestArmy ? 2 : 0;
            }
            else
            {
                whitePoints = 0;
                villagePoints = 0;
                townPoints = 0;
                cardPoints = 0;
                longestRoadPoints = 0;
                largestArmyPoints = 0;
            }
            string whiteDetailedPoints = "Villages: " + villagePoints + ", Towns: " + townPoints + ", Cards: " + cardPoints + ", Longest road: " + longestRoadPoints + ", Largest Army: " + largestArmyPoints;

            DateTime gameEndTime = DateTime.Now;
            double durationSeconds = (gameEndTime - gameStartTime).TotalSeconds;

            PlayerColour? winner = currentGame.winner;
            PlayerColour p4;
            if (PlayerCount == 4)
            {
                p4 = PlayerOrder[3];
            }
            else
            {
                p4 = PlayerColour.no_colour;
            }

            gameSummaryLogger.Information(
                "{GameId},{DurationSeconds},{RoundCount},{Player1},{Player2},{Player3},{Player4},{Winner},{RedPoints},{BluePoints},{OrangePoints},{WhitePoints},{RedDetailedPoints},{BlueDetailedPoints},{OrangeDetailedPoints},{WhiteDetailedPoints},{ErrorMessage}",
                gameId,
                durationSeconds,
                turns / PlayerCount,
                PlayerOrder[0],
                PlayerOrder[1],
                PlayerOrder[2],
                p4,
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
