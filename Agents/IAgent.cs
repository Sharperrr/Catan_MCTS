using Natak_Front_end.Controllers;
using Natak_Front_end.Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Agents
{
    public interface IAgent
    {
        Task PlaySetupTurn(GameController gameController, string gameId, PlayerColour playerColour);
        Task PlayTurn(GameController gameController, string gameId, PlayerColour playerColour, Models.Point thiefLocation);

    }
}
