using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Models
{
    public class Building:ICloneable
    {
        public PlayerColour playerColour { get; set; }
        public Point point { get; set; }

        public object Clone()
        {
            return new Building
            {
                playerColour = this.playerColour,
                point = (Point)this.point.Clone()
            };
        }
    }
}
