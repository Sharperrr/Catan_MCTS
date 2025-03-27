using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Agents
{
    public class RulesBasedAgent : IAgent
    {
        public RulesBasedAgent() { }
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
