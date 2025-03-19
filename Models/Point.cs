using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Models
{
    public class Point:ICloneable
    {
        public int x { get; set; }
        public int y { get; set; }

        public object Clone()
        {
            return new Point
            {
                x = this.x,
                y = this.y
            };
        }
    }
}
