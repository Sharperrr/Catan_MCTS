using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Models
{
    public class Hex:ICloneable
    {
        public Point point { get; set; }
        public ResourceType resource { get; set; }
        public int rollNumber { get; set; }
        
        public object Clone()
        {
            return new Hex
            {
                point = (Point)this.point.Clone(),
                resource = this.resource,
                rollNumber = this.rollNumber
            };
        }
    }
}
