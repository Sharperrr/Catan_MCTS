using Natak_Front_end.Controllers;
using Natak_Front_end.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Agents
{
    internal class MCTSAgent : IAgent
    {
        public MCTSAgent() { }

        public Task PlaySetupTurn(GameController gameController, string gameId, PlayerColour playerColour)
        {
            return Task.CompletedTask;
        }

        public Task PlayTurn(GameController gameController, string gameId, PlayerColour playerColour, Models.Point thiefLocation)
        {
            return Task.CompletedTask;
        }

    }
}
