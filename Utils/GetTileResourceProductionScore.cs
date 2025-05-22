using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Utils
{
    public class GetTileResourceProductionScore
    {
        public static int GetYieldScore(int rollNumber)
        {
            if (rollNumber == 8 || rollNumber == 6)
            {
                return 5;
            }
            else if (rollNumber == 9 || rollNumber == 5)
            {
                return 4;
            }
            else if (rollNumber == 10 || rollNumber == 4)
            {
                return 3;
            }
            else if (rollNumber == 11 || rollNumber == 3)
            {
                return 2;
            }
            else if (rollNumber == 12 || rollNumber == 2)
            {
                return 1;
            }
            else
            {
                return 0;
            }
        }
    }
}
