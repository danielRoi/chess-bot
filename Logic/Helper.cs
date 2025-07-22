using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessApp.Logic
{
    internal class Helper
    {
        public enum GameResult
        {
            WhiteWin,
            BlackWin,
            Draw
        }
        public static GameResult getGameResult()
        {
            return GameResult.Draw; // Placeholder for actual game result logic
        }
    }
}
