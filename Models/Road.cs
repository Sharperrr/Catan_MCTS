using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Models
{
    public class Road:ICloneable
    {
        public PlayerColour playerColour { get; set; }
        public Point firstPoint { get; set; }
        public Point secondPoint { get; set; }

        public object Clone()
        {
            return new Road
            {
                playerColour = this.playerColour,
                firstPoint = (Point)this.firstPoint.Clone(),
                secondPoint = (Point)this.secondPoint.Clone()
            };
        }
    }
}
