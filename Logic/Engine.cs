using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static ChessApp.ChessLogic;

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
            Span<int> moves = stackalloc int[256];
            int count = ChessLogic.GetAllAvailableMoves(whiteTurn, moves);

            for(int i = 0; i < count; i++)
            {
                var savedState = ChessLogic.SaveState();
                int move = moves[i];
                // Decode and make the move
                int fromSq = (move >> 24) & 0x7F;
                int toSq = (move >> 17) & 0x7F;
                int promo = (move >> 12) & 0x7;
                ChessLogic.MovePiece(fromSq / 8, fromSq % 8, toSq / 8, toSq % 8, promo == 1 ? Piece.Queen :
                                                                                 promo == 2 ? Piece.Rook :
                                                                                 promo == 3 ? Piece.Bishop :
                                                                                 promo == 4 ? Piece.Knight : Piece.None);

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
                return Evaluate(whiteTurn);
            }
            Span<int> moves = stackalloc int[256];
            int count = ChessLogic.GetAllAvailableMoves(whiteTurn, moves);
            if (count == 0)
            {
                // Check for checkmate or stalemate
                return ChessLogic.IsInCheck(whiteTurn) ? (whiteTurn ? -KingValue - depth : KingValue + depth) : 0;
            }

            if (whiteTurn)
            {
                int maxEval = int.MinValue;
                for(int i = 0; i< count; i++)
                {
                    int move = moves[i];
                    var savedState = ChessLogic.SaveState();
                    int fromSq = (move >> 24) & 0x7F;
                    int toSq = (move >> 17) & 0x7F;
                    int promo = (move >> 12) & 0x7;
                    ChessLogic.MovePiece(fromSq / 8, fromSq % 8, toSq / 8, toSq % 8, promo == 1 ? Piece.Queen :
                                                                                     promo == 2 ? Piece.Rook :
                                                                                     promo == 3 ? Piece.Bishop :
                                                                                     promo == 4 ? Piece.Knight : Piece.None);

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
                    int promo = (move >> 12) & 0x7;
                    ChessLogic.MovePiece(fromSq / 8, fromSq % 8, toSq / 8, toSq % 8, promo == 1 ? Piece.Queen :
                                                                                     promo == 2 ? Piece.Rook :
                                                                                     promo == 3 ? Piece.Bishop :
                                                                                     promo == 4 ? Piece.Knight : Piece.None);

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
        public static int Evaluate(bool whiteTurn)
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

            int endGameScore = evaluateEndGame(whiteTurn);
            return score + endGameScore;
        }


        private static int evaluateEndGame(bool whiteTurn)
        {
            if (!IsEndgame()) return 0;

            int whiteKingSquare = BitOperations.TrailingZeroCount(ChessLogic.WK);
            int blackKingSquare = BitOperations.TrailingZeroCount(ChessLogic.BK);

            int whiteKingX = whiteKingSquare % 8;
            int whiteKingY = whiteKingSquare / 8;

            int blackKingX = blackKingSquare % 8;
            int blackKingY = blackKingSquare / 8;

            // 1. Push black king toward corner (distance to closest corner)
            int minCornerDistance = 0;
            if (!whiteTurn)
            {
                int corner1 = whiteKingX + whiteKingY;
                int corner2 = whiteKingX + (7 - whiteKingY);
                int corner3 = (7 - whiteKingX) + whiteKingY;
                int corner4 = (7 - whiteKingX) + (7 - whiteKingY);
                minCornerDistance = Math.Min(Math.Min(corner1, corner2), Math.Min(corner3, corner4));
            }
            else
            {
                int corner1 = blackKingX + blackKingY;
                int corner2 = blackKingX + (7 - blackKingY);
                int corner3 = (7 - blackKingX) + blackKingY;
                int corner4 = (7 - blackKingX) + (7 - blackKingY);
                minCornerDistance = Math.Min(Math.Min(corner1, corner2), Math.Min(corner3, corner4));
            }
            int cornerScore = (14 - minCornerDistance) * 5; // Max value when in a corner

            // 2. Bring white king close to black king
            int dx = Math.Abs(whiteKingX - blackKingX);
            int dy = Math.Abs(whiteKingY - blackKingY);
            int distanceScore = (14 - (dx + dy)) * 3;

            int res = (cornerScore + distanceScore) * 3;
            return whiteTurn ? res : -res; // Return positive for white's perspective, negative for black
        }

        private static bool IsEndgame()
        {
            int totalNonPawnMaterial = BitOperations.PopCount(ChessLogic.WQ | ChessLogic.BQ) * QueenValue;
            totalNonPawnMaterial += BitOperations.PopCount(ChessLogic.WR | ChessLogic.BR) * RookValue;
            totalNonPawnMaterial += BitOperations.PopCount(ChessLogic.WB | ChessLogic.BB) * BishopValue;
            totalNonPawnMaterial += BitOperations.PopCount(ChessLogic.WN | ChessLogic.BN) * KnightValue;

            return totalNonPawnMaterial <= 1300;
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
