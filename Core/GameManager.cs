using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Natak_Front_end.Core
{
    public class GameManager
    {
        // Singleton instance
        private static GameManager _instance;
        public static GameManager Instance => _instance ??= new GameManager();

        // Properties for managing game state
        public string GameId { get; set; }

        // Private constructor to enforce singleton pattern
        private GameManager() { }
    }
}
