using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end
{
    public enum PlayerColour
    {
        no_colour,
        Red,
        Blue,
        Orange,
        White
    }

    public enum GameState
    {
        Setup_village,
        Setup_road,
        Finished_setup,
        Before_roll,
        After_roll,
        Build_roads,
        Discard_resources,
        Move_thief,
        Steal_resource,
        Game_end
    }

    public enum ActionType
    {
        Build_a_village,
        Build_a_road,
        Build_a_town,
        Roll_the_dice,
        End_turn,
        Make_trade,
        Play_card,
        Discard_resources,
        Move_thief,
        Steal_resource,
        Buy_a_card
    }

    public enum ResourceType
    {
        None,
        Wood,
        Clay,
        Animal,
        Food,
        Metal
    }

    public enum HexType
    {
        None,
        Wood,
        Clay,
        Animal,
        Food,
        Metal
    }

    public enum GrowthCardType
    {
        Soldier,
        Roaming,
        Wealth,
        Gatherer,
        Victory_point
    }

    public enum PortType
    {
        Three_to_one,
        Wood,
        Clay,
        Animal,
        Food,
        Metal
    }
}
