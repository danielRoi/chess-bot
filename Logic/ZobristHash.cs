using System.Numerics;

namespace ChessApp
{
    static class ZobristHash
    {
        // Zobrist keys for pieces (12 piece types * 64 squares)
        private static readonly ulong[,] PieceKeys = new ulong[12, 64];

        // Additional keys for game state
        private static readonly ulong[] CastlingKeys = new ulong[16]; // 4 bits for castling rights
        private static readonly ulong[] EnPassantKeys = new ulong[8]; // 8 files for en passant
        private static readonly ulong BlackToMoveKey;

        // Position history for repetition detection
        private static readonly ulong[] PositionHistory = new ulong[1024];
        private static readonly int[] HalfmoveHistory = new int[1024];
        private static int historyIndex = 0;

        static ZobristHash()
        {
            var rng = new Random(12345); // Fixed seed for reproducibility

            // Initialize piece keys
            for (int piece = 0; piece < 12; piece++)
                for (int square = 0; square < 64; square++)
                    PieceKeys[piece, square] = NextULong(rng);

            // Initialize castling keys
            for (int i = 0; i < 16; i++)
                CastlingKeys[i] = NextULong(rng);

            // Initialize en passant keys
            for (int i = 0; i < 8; i++)
                EnPassantKeys[i] = NextULong(rng);

            BlackToMoveKey = NextULong(rng);
        }

        private static ulong NextULong(Random rng)
        {
            byte[] bytes = new byte[8];
            rng.NextBytes(bytes);
            return BitConverter.ToUInt64(bytes, 0);
        }

        public static ulong ComputeHash(bool whiteTurn, int castleData, ulong enPassantSquare)
        {
            ulong hash = 0;

            // Hash all pieces on board
            hash ^= HashPieces();

            // Hash side to move
            if (!whiteTurn) hash ^= BlackToMoveKey;

            // Hash castling rights
            hash ^= CastlingKeys[castleData & 0b00111111];

            // Hash en passant
            if (enPassantSquare != 0)
            {
                int file = BitOperations.TrailingZeroCount(enPassantSquare) % 8;
                hash ^= EnPassantKeys[file];
            }

            return hash;
        }

        private static ulong HashPieces()
        {
            ulong hash = 0;

            // White pieces
            hash ^= HashPieceBitboard(ChessLogic.WP, 0);  // White pawn
            hash ^= HashPieceBitboard(ChessLogic.WN, 1);  // White knight
            hash ^= HashPieceBitboard(ChessLogic.WB, 2);  // White bishop
            hash ^= HashPieceBitboard(ChessLogic.WR, 3);  // White rook
            hash ^= HashPieceBitboard(ChessLogic.WQ, 4);  // White queen
            hash ^= HashPieceBitboard(ChessLogic.WK, 5);  // White king

            // Black pieces
            hash ^= HashPieceBitboard(ChessLogic.BP, 6);  // Black pawn
            hash ^= HashPieceBitboard(ChessLogic.BN, 7);  // Black knight
            hash ^= HashPieceBitboard(ChessLogic.BB, 8);  // Black bishop
            hash ^= HashPieceBitboard(ChessLogic.BR, 9);  // Black rook
            hash ^= HashPieceBitboard(ChessLogic.BQ, 10); // Black queen
            hash ^= HashPieceBitboard(ChessLogic.BK, 11); // Black king

            return hash;
        }

        private static ulong HashPieceBitboard(ulong bitboard, int pieceType)
        {
            ulong hash = 0;
            ulong pieces = bitboard;

            while (pieces != 0)
            {
                int square = BitOperations.TrailingZeroCount(pieces);
                pieces &= pieces - 1;
                hash ^= PieceKeys[pieceType, square];
            }

            return hash;
        }

        public static void PushPosition(ulong hash, int halfmoveClock)
        {
            PositionHistory[historyIndex] = hash;
            HalfmoveHistory[historyIndex] = halfmoveClock;
            historyIndex++;
        }

        public static void PopPosition()
        {
            if (historyIndex > 0) historyIndex--;
        }

        public static bool IsThreefoldRepetition(ulong currentHash)
        {
            int count = 0;
            for (int i = 0; i < historyIndex; i++)
            {
                if (PositionHistory[i] == currentHash)
                {
                    count++;
                    if (count >= 2) return true; // Current position + 2 previous = 3 total
                }
            }
            return false;
        }

        public static bool IsFiftyMoveRule(int currentHalfmove)
        {
            return currentHalfmove >= 100; // 50 moves per side = 100 halfmoves
        }

        public static void ClearHistory()
        {
            historyIndex = 0;
        }

        public static int GetHistoryCount() => historyIndex;
    }
}