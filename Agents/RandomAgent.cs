using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Agents
{
    public class RandomAgent : IAgent
    {
        public RandomAgent() { }
        public Task PlayTurn()
        {
            return Task.CompletedTask;
        }

        public Task PlaySetupTurn()
        {
            return Task.CompletedTask;
        }
    }
}
