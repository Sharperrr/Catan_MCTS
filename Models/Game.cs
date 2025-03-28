using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Natak_Front_end.Core;

namespace Natak_Front_end.Models
{
    public class Game:ICloneable
    {
        public string id { get; set; }
        public int playerCount { get; set; }
        public PlayerColour currentPlayerColour { get; set; }
        public GameState gameState { get; set; }
        public List<ActionType>? actions { get; set; }
        public DetailedPlayer player { get; set; }
        public List<Player> players { get; set; }
        public Board board { get; set; }
        public PlayerColour? winner { get; set; }
        public List<int> lastRoll { get; set; }
        public PlayerColour? largestArmyPlayer { get; set; }
        public PlayerColour? longestRoadPlayer { get; set; }
        public Dictionary<ResourceType, int> remainingResourceCards { get; set; }
        public int remainingGrowthCards { get; set; }
        public TradeOffer tradeOffer { get; set; }

        public object Clone()
        {
            return new Game
            {
                id = this.id,
                playerCount = this.playerCount,
                currentPlayerColour = this.currentPlayerColour,
                gameState = this.gameState,
                actions = new List<ActionType>(this.actions),
                player = (DetailedPlayer)this.player.Clone(),
                players = new List<Player>(this.players.Select(p => (Player)p.Clone()).ToList()),
                board = (Board)this.board.Clone(),
                winner = this.winner,
                lastRoll = new List<int>(this.lastRoll),
                largestArmyPlayer = this.largestArmyPlayer,
                longestRoadPlayer = this.longestRoadPlayer,
                remainingResourceCards = new Dictionary<ResourceType, int>(this.remainingResourceCards),
                remainingGrowthCards = this.remainingGrowthCards,
                tradeOffer = (TradeOffer)this.tradeOffer.Clone()
            };
        }
    }
}
