using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Natak_Front_end.Core;

namespace Natak_Front_end.Requests
{
    public class DiscardRequest
    {
        public Dictionary<ResourceType, int> resources { get; set; }
    }
}
