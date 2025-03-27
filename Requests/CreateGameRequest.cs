using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Requests
{
    public class CreateGameRequest
    {
        public int playerCount {  get; set; }
        public int seed { get; set; }
    }
}
