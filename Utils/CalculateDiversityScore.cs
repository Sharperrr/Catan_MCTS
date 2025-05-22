using Natak_Front_end.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Utils
{
    public class CalculateDiversityScore
    {
        private const double ROAD_WEIGHT = 1.3; // wood and clay weight
        private const double VILLAGE_WEIGHT = 1.2; // food and animal weight
        private const double TOWN_WEIGHT = 1.2; // metal weight

        private const double DIVERSITY_WEIGHT = 1.5;
        private const double NEW_RESOURCE_WEIGHT = 1.5;

        public static double GetDiversityScore(List<ResourceType> resources, HashSet<ResourceType> existingResources)
        {
            int resourceCount = resources.Count;
            double score = resourceCount;
            foreach (ResourceType resource in resources)
            {
                double resourceWeight = (resource == ResourceType.Wood || resource == ResourceType.Clay) ? ROAD_WEIGHT
                    : (resource == ResourceType.Animal || resource == ResourceType.Food) ? VILLAGE_WEIGHT
                    : (resource == ResourceType.Metal) ? TOWN_WEIGHT
                    : 1;

                score *= resourceWeight;

                if(!existingResources.Contains(resource) && resource != ResourceType.None)
                {
                    score *= NEW_RESOURCE_WEIGHT;
                }
            }

            return score * DIVERSITY_WEIGHT;
        }
    }
}
