using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Midi;

namespace ChessApp.Logic
{
    internal class Engine
    {

        // --- Piece Value Constants ---
        #region PieceValues
        private const int PawnValue = 100;
        private const int KnightValue = 320;
        private const int BishopValue = 330;
        private const int RookValue = 500;
        private const int QueenValue = 900;
        private const int KingValue = 20000;
        #endregion

        // --- Piece-Square Tables (PST) ---
        // These tables assign a score bonus/penalty based on a piece's position.
        // Scores are from white's perspective. Black's scores are derived by flipping the board.
        #region PieceSquareTables
        private static readonly int[] PawnPST = {
             0,  0,  0,  0,  0,  0,  0,  0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
             5,  5, 10, 25, 25, 10,  5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5, -5,-10,  0,  0,-10, -5,  5,
             5, 10, 10,-20,-20, 10, 10,  5,
             0,  0,  0,  0,  0,  0,  0,  0
        };

        private static readonly int[] KnightPST = {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50
        };

        private static readonly int[] BishopPST = {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -20,-10,-10,-10,-10,-10,-10,-20
        };

        private static readonly int[] RookPST = {
              0,  0,  0,  0,  0,  0,  0,  0,
              5, 10, 10, 10, 10, 10, 10,  5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
             -5,  0,  0,  0,  0,  0,  0, -5,
              0,  0,  0,  5,  5,  0,  0,  0
        };

        private static readonly int[] QueenPST = {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5,  5,  5,  5,  0,-10,
             -5,  0,  5,  5,  5,  5,  0, -5,
              0,  0,  5,  5,  5,  5,  0, -5,
            -10,  5,  5,  5,  5,  5,  0,-10,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20
        };
        private static readonly int[] KingPST = {

        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -30, -40, -40, -50, -50, -40, -40, -30,
        -20, -30, -30, -40, -40, -30, -30, -20,
        -10, -20, -20, -20, -20, -20, -20, -10,
         20,  20,   0,   0,   0,   0,  20,  20,
         20,  30,  10,   0,   0,  10,  30,  20
        };
        #endregion

        public static int FindBestMove(int depth, bool whiteTurn)
        {
            int bestMove = 0;
            int bestScore = whiteTurn ? int.MinValue : int.MaxValue;
            var moves = ChessLogic.getAllAvailableMoves(whiteTurn);

            foreach (var move in moves)
            {
                var savedState = ChessLogic.SaveState();

                // Decode and make the move
                int fromSq = (move >> 24) & 0x7F;
                int toSq = (move >> 17) & 0x7F;
                int promo = (move >> 15) & 0x3;
                ChessLogic.MovePiece(fromSq / 8, fromSq % 8, toSq / 8, toSq % 8, (ChessLogic.Piece)promo);

                // Start the search
                int score = minimax(depth - 1, int.MinValue, int.MaxValue, !whiteTurn);

                ChessLogic.RestoreState(savedState);

                if (whiteTurn)
                {
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMove = move;
                    }
                }
                else
                {
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestMove = move;
                    }
                }
            }
            return bestMove;
        }

        /// <summary>
        /// The core recursive search function implementing the Minimax algorithm with Alpha-Beta pruning.
        /// </summary>
        private static int minimax(int depth, int alpha, int beta, bool whiteTurn)
        {
            if (depth == 0)
            {
                // When search depth is reached, evaluate the board position
                return Evaluate();
            }

            var moves = ChessLogic.getAllAvailableMoves(whiteTurn);
            if (moves.Count == 0)
            {
                // Check for checkmate or stalemate
                return ChessLogic.IsInCheck(whiteTurn) ? (whiteTurn ? -KingValue - depth : KingValue + depth) : 0;
            }

            if (whiteTurn)
            {
                int maxEval = int.MinValue;
                foreach (var move in moves)
                {
                    var savedState = ChessLogic.SaveState();
                    int fromSq = (move >> 24) & 0x7F;
                    int toSq = (move >> 17) & 0x7F;
                    int promo = (move >> 15) & 0x3;
                    ChessLogic.MovePiece(fromSq / 8, fromSq % 8, toSq / 8, toSq % 8, (ChessLogic.Piece)promo);

                    int eval = minimax(depth - 1, alpha, beta, false);
                    ChessLogic.RestoreState(savedState);

                    maxEval = Math.Max(maxEval, eval);
                    alpha = Math.Max(alpha, eval);
                    if (beta <= alpha) // Pruning
                        break;
                }
                return maxEval;
            }
            else // Black's turn
            {
                int minEval = int.MaxValue;
                foreach (var move in moves)
                {
                    var savedState = ChessLogic.SaveState();
                    int fromSq = (move >> 24) & 0x7F;
                    int toSq = (move >> 17) & 0x7F;
                    int promo = (move >> 15) & 0x3;
                    ChessLogic.MovePiece(fromSq / 8, fromSq % 8, toSq / 8, toSq % 8, (ChessLogic.Piece)promo);

                    int eval = minimax(depth - 1, alpha, beta, true);
                    ChessLogic.RestoreState(savedState);

                    minEval = Math.Min(minEval, eval);
                    beta = Math.Min(beta, eval);
                    if (beta <= alpha) // Pruning
                        break;
                }
                return minEval;
            }
        }


        /// <summary>
        /// Evaluates the current board position and returns a score.
        /// Positive score favors white, negative favors black.
        /// NOTE: This requires access to the bitboards in ChessLogic. They must be made public or accessible via a public method.
        /// </summary>
        private static int Evaluate()
        {
            int score = 0;
            
            score += CalculateScoreForPiece(ChessLogic.WP, PawnValue, PawnPST, true);
            score += CalculateScoreForPiece(ChessLogic.WN, KnightValue, KnightPST, true);
            score += CalculateScoreForPiece(ChessLogic.WB, BishopValue, BishopPST, true);
            score += CalculateScoreForPiece(ChessLogic.WR, RookValue, RookPST, true);
            score += CalculateScoreForPiece(ChessLogic.WQ, QueenValue, QueenPST, true);
            score += CalculateScoreForPiece(ChessLogic.WK, KingValue, KingPST, true);

            score -= CalculateScoreForPiece(ChessLogic.BP, PawnValue, PawnPST, false);
            score -= CalculateScoreForPiece(ChessLogic.BN, KnightValue, KnightPST, false);
            score -= CalculateScoreForPiece(ChessLogic.BB, BishopValue, BishopPST, false);
            score -= CalculateScoreForPiece(ChessLogic.BR, RookValue, RookPST, false);
            score -= CalculateScoreForPiece(ChessLogic.BQ, QueenValue, QueenPST, false);
            score -= CalculateScoreForPiece(ChessLogic.BK, KingValue, KingPST, false);
            
            return score;
        }

        /// <summary>
        /// Helper function to calculate the score for a single piece type.
        /// </summary>
        private static int CalculateScoreForPiece(ulong bitboard, int value, int[] pst, bool isWhite)
        {
            int score = 0;
            ulong tempBoard = bitboard;
            while (tempBoard != 0)
            {
                int square = BitOperations.TrailingZeroCount(tempBoard);
                tempBoard &= tempBoard - 1; // Clear the least significant bit

                score += value;
                // For black pieces, the PST index is flipped vertically
                int pstIndex = isWhite ? square : 63 - square;
                score += pst[pstIndex];
            }
            return score;
        }


    }
}
