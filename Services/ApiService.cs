using RestSharp;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Natak_Front_end.Models;
using System.Numerics;
using Natak_Front_end.Requests;
using Natak_Front_end.Core;

namespace Natak_Front_end.Services
{
    public class ApiService
    {
        private readonly RestClient _restClient;

        public ApiService(string baseUrl = "https://localhost:7207/api/v1/natak/")
        {
            _restClient = new RestClient(baseUrl);
        }

        public async Task<Game> FetchGameState(string gameId, int player = 1)
        {
            var request = new RestRequest($"{gameId}/{player}", Method.Get);
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<List<Models.Point>> GetAvailableVillageLocations(string gameId)
        {
            var request = new RestRequest($"{gameId}/available-village-locations", Method.Get);
            return await ExecuteRequestAsync<List<Models.Point>>(request);
        }

        public async Task<List<Models.Point>> GetAvailableTownLocations(string gameId)
        {
            var request = new RestRequest($"{gameId}/available-town-locations", Method.Get);
            return await ExecuteRequestAsync<List<Models.Point>>(request);
        }

        public async Task<List<Road>> GetAvailableRoadLocations(string gameId)
        {
            var request = new RestRequest($"{gameId}/available-road-locations", Method.Get);
            return await ExecuteRequestAsync<List<Road>>(request);
        }

        public async Task<Game> BuildVillage(string gameId, int player, Models.Point point)
        {
            var request = new RestRequest($"{gameId}/{player}/build/village", Method.Post)
                .AddJsonBody(new BuildingRequest { point = point });
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> BuildTown(string gameId, int player, Models.Point point)
        {
            var request = new RestRequest($"{gameId}/{player}/build/town", Method.Post)
                .AddJsonBody(new BuildingRequest { point = point });
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> BuildRoad(string gameId, int player, Models.Point point1, Models.Point point2)
        {
            var request = new RestRequest($"{gameId}/{player}/build/road", Method.Post)
                .AddJsonBody(new RoadBuildRequest { firstPoint = point1, secondPoint = point2 });
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> SaveLoad(string gameId, bool isSaved)
        {
            var request = new RestRequest($"{gameId}/{isSaved}/save-load", Method.Post);
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> RollDice(string gameId, int player)
        {
            var request = new RestRequest($"{gameId}/{player}/roll", Method.Post);
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> EndTurn(string gameId, int player)
        {
            var request = new RestRequest($"{gameId}/{player}/end-turn", Method.Post);
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> BuyGrowthCard(string gameId, int player)
        {
            var request = new RestRequest($"{gameId}/{player}/buy/growth-card", Method.Post);
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> DiscardResources(string gameId, int player, Dictionary<ResourceType, int> resourcesToDiscard)
        {
            var request = new RestRequest($"{gameId}/{player}/discard-resources", Method.Post)
                .AddJsonBody(new DiscardRequest { resources = resourcesToDiscard });
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> MoveThief(string gameId, int player, Models.Point point)
        {
            var request = new RestRequest($"{gameId}/{player}/move-thief", Method.Post)
                .AddJsonBody(new MoveThiefRequest { moveThiefTo = point });
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> StealResource(string gameId, int player, int victim)
        {
            var request = new RestRequest($"{gameId}/{player}/steal-resource", Method.Post)
                .AddJsonBody(new StealResourceRequest { victimColour = victim });
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> TradeWithBank(string gameId, int player, int sell, int buy)
        {
            var request = new RestRequest($"{gameId}/{player}/trade/bank", Method.Post)
                .AddJsonBody(new BankTradeRequest { resourceToGive = sell, resourceToGet = buy });
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> PlaySoldierCard(string gameId, int player)
        {
            var request = new RestRequest($"{gameId}/{player}/play-growth-card/soldier", Method.Post);
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> PlayRoamingCard(string gameId, int player)
        {
            var request = new RestRequest($"{gameId}/{player}/play-growth-card/roaming", Method.Post);
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> PlayWealthCard(string gameId, int player, int resource1, int resource2)
        {
            var request = new RestRequest($"{gameId}/{player}/play-growth-card/wealth", Method.Post)
                .AddJsonBody(new WealthCardRequest { firstResource = resource1, secondResource = resource2 });
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<Game> PlayGathererCard(string gameId, int player, int resource)
        {
            var request = new RestRequest($"{gameId}/{player}/play-growth-card/gatherer", Method.Post)
                .AddJsonBody(new GathererCardRequest { resource = resource });
            return await ExecuteRequestAsync<Game>(request);
        }

        public async Task<CreateGameResponse> CreateGame(int playerCount, int seed)
        {
            var request = new RestRequest("", Method.Post)
                .AddJsonBody(new CreateGameRequest { playerCount = playerCount, seed = seed});
            return await ExecuteRequestAsync<CreateGameResponse>(request);
        }


        private async Task<T> ExecuteRequestAsync<T>(RestRequest request)
        {
            try
            {
                var response = await _restClient.ExecuteAsync(request);
                if (response.IsSuccessful)
                {
                    if (string.IsNullOrWhiteSpace(response.Content))
                    {
                        // If content is empty, and T is a reference type, return default(T) (which is null for reference types)
                        // Or, if T is a struct/value type, it will be its default value.
                        // You might want to handle this more specifically based on what SaveLoad is expected to return.
                        return default(T);
                    }
                    return JsonSerializer.Deserialize<T>(response.Content);
                }
                else
                {
                    throw new Exception($"API call failed: {response.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"API request failed: {ex.Message}");
            }
        }

    }
}
