using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Agents
{
    public interface IAgent
    {
        Task PlayTurn();

        Task PlaySetupTurn();
    }
}
