using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Models
{
    public class DetailedPlayer:ICloneable
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

        public object Clone()
        {
            return new DetailedPlayer
            {
                victoryPoints = this.victoryPoints,
                resourceCards = new Dictionary<ResourceType, int>(this.resourceCards),
                playableGrowthCards = new Dictionary<GrowthCardType, int>(this.playableGrowthCards),
                onHoldGrowthCards = new Dictionary<GrowthCardType, int>(this.onHoldGrowthCards),
                colour = this.colour,
                soldiersPlayed = this.soldiersPlayed,
                visibleVictoryPoints = this.visibleVictoryPoints,
                totalResourceCards = this.totalResourceCards,
                totalGrowthCards = this.totalGrowthCards,
                hasLargestArmy = this.hasLargestArmy,
                hasLongestRoad = this.hasLongestRoad,
                cardsToDiscard = this.cardsToDiscard,
                remainingVillages = this.remainingVillages,
                remainingTowns = this.remainingTowns,
                remainingRoads = this.remainingRoads,
                embargoedPlayerColours = this.embargoedPlayerColours
            };
        }
    }
}
