using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Models
{
    public class Port:ICloneable
    {
        public Point point { get; set; }
        public PortType type { get; set; }

        public object Clone()
        {
            return new Port
            {
                point = (Point)this.point.Clone(),
                type = this.type
            };
        }
    }
}
