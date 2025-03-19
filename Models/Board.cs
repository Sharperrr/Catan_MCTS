using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Models
{
    public class Board:ICloneable
    {
        public List<Hex> hexes { get; set; }
        public List<Road> roads { get; set; }
        public List<Building> villages { get; set; }
        public List<Building> towns { get; set; }
        public List<Port> ports { get; set; }

        public object Clone()
        {
            return new Board
            {
                hexes = new List<Hex>(this.hexes.Select(h => (Hex)h.Clone()).ToList()),
                roads = new List<Road>(this.roads.Select(r => (Road)r.Clone()).ToList()),
                villages = new List<Building>(this.villages.Select(v => (Building)v.Clone()).ToList()),
                towns = new List<Building>(this.towns.Select(t => (Building)t.Clone()).ToList()),
                ports = new List<Port>(this.ports.Select(p => (Port)p.Clone()).ToList())
            };
        }
    }
}
