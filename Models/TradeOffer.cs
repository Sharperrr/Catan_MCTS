using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Models
{
    public class TradeOffer:ICloneable
    {
        public bool isActive { get; set; }
        public Dictionary<ResourceType, int> offer { get; set; }
        public Dictionary<ResourceType, int> request { get; set; }
        public List<PlayerColour> rejectedBy { get; set; }

        public object Clone()
        {
            return new TradeOffer
            {
                isActive = this.isActive,
                offer = new Dictionary<ResourceType, int>(this.offer),
                request = new Dictionary<ResourceType, int>(this.request),
                rejectedBy = new List<PlayerColour>(this.rejectedBy)
            };
        }
    }
}
