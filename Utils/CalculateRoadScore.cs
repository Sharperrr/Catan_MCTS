using System;
using System.Collections.Generic;
using Natak_Front_end.Models;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Utils
{
    public class CalculateRoadScore
    {
        private const double ROAD_WEIGHT = 1.1;
        private const double STRUCTURE_WEIGHT = 1.5;
        private const double OPP_STRUCTURE_WEIGHT = 3;
        public static double GetRoadScore(Road road, List<Road> existingRoads, List<Point> structures, List<Point> oppStructures)
        {
            double score = 100;
            /*
             * if p1 or p2 connected to a village or town, it's potentially a new road
             * score /= NEW_ROAD_WEIGHT
             * else if p1 or p2 is connected to another road, it's potentially continuing an existing road
             * for each road connected to the new road /existingroadweight, for each
             */
            foreach(Road eRoad in existingRoads)
            {
                if(eRoad.firstPoint.Equals(road.firstPoint) || eRoad.firstPoint.Equals(road.secondPoint) || eRoad.secondPoint.Equals(road.firstPoint) || eRoad.secondPoint.Equals(road.secondPoint))
                {

                }
            }
            return score;
        }
    }
}
