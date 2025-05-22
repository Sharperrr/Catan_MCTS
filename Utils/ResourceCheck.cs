using Natak_Front_end.Controllers;
using Natak_Front_end.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using ResourceType = Natak_Front_end.Core.ResourceType;

namespace Natak_Front_end.Utils
{
    public class ResourceCheck
    {
        public static bool Village(Dictionary<ResourceType, int> resources)
        {
            if (resources.ContainsKey(ResourceType.Wood) && resources.ContainsKey(ResourceType.Clay) && resources.ContainsKey(ResourceType.Food) && resources.ContainsKey(ResourceType.Animal))
            {
                if (resources[ResourceType.Wood] >= 1 && resources[ResourceType.Clay] >= 1 && resources[ResourceType.Food] >= 1 && resources[ResourceType.Animal] >= 1)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public static bool Town(Dictionary<ResourceType, int> resources)
        {
            if (resources.ContainsKey(ResourceType.Metal) && resources.ContainsKey(ResourceType.Food))
            {
                if (resources[ResourceType.Metal] >= 3 && resources[ResourceType.Food] >= 2)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public static bool Road(Dictionary<ResourceType, int> resources)
        {
            if (resources.ContainsKey(ResourceType.Wood) && resources.ContainsKey(ResourceType.Clay))
            {
                if (resources[ResourceType.Wood] >= 1 && resources[ResourceType.Clay] >= 1)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
