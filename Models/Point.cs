using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Models
{
    public class Point:ICloneable, IEquatable<Point>
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

        public bool Equals(Point other)
        {
            if(other == null) return false;
            return x == other.x && y == other.y;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as Point);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }
    }
}
