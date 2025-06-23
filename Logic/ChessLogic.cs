using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;

namespace ChessApp
{

    static class ChessLogic
    {
        public enum Piece { Pawn, Knight, Bishop, Rook, Queen, King, None};
        // bitboards for each piece type
        public static ulong WP, WN, WB, WR, WQ, WK;
        public static ulong BP, BN, BB, BR, BQ, BK;

        // occupancy masks
        public static ulong WhiteAll => WP | WN | WB | WR | WQ | WK;
        public static ulong BlackAll => BP | BN | BB | BR | BQ | BK;
        public static ulong Occupied => WhiteAll | BlackAll;

        // direction offsets in bits
        private const int Up = 8, Down = -8, Right = +1, Left = -1,
                          UR = Up + Right, UL = Up + Left,
                          DR = Down + Right, DL = Down + Left;

        // file masks to prevent wraparound
        private const ulong FileA = 0x0101010101010101UL;
        private const ulong FileH = 0x8080808080808080UL;
        private const ulong FileB = 0x0202020202020202UL;
        private const ulong FileG = 0x4040404040404040UL;


        //castling checks
        private static int castleData = 0; // 0b76543210; // 0: white king, 1: white right rook, 2: white left rook,
                                           // 3: black king, 4: black right rook, 5: black left rook.
                                           //0 is not moved, 1 is moved or eaten.

        private static ulong? enPassantSquare = null;

        //precomputed knight and king moves
        private static readonly ulong[] KnightMoves = new ulong[64];
        private static readonly ulong[] KingMoves = new ulong[64];

        // a tiny struct to hold all our bitboards
        public struct BoardState
        {
            public ulong WP, WN, WB, WR, WQ, WK;
            public ulong BP, BN, BB, BR, BQ, BK;
            public int castleData;
            public ulong? enPassantSquare;
        }

        public static BoardState SaveState() => new BoardState
        {
            WP = WP,
            WN = WN,
            WB = WB,
            WR = WR,
            WQ = WQ,
            WK = WK,
            BP = BP,
            BN = BN,
            BB = BB,
            BR = BR,
            BQ = BQ,
            BK = BK,
            castleData = castleData,
            enPassantSquare = enPassantSquare
        };

        public static void RestoreState(BoardState s)
        {
            WP = s.WP; WN = s.WN; WB = s.WB; WR = s.WR; WQ = s.WQ; WK = s.WK;
            BP = s.BP; BN = s.BN; BB = s.BB; BR = s.BR; BQ = s.BQ; BK = s.BK;
            castleData = s.castleData;
            enPassantSquare = s.enPassantSquare;
        }

        static ChessLogic()
        {
            precomputePieces();
        }

        /// <summary>
        /// Call once at startup to sync logic to the usual chess start.
        /// </summary>
        public static void InitializeBoard()
        {
            BP = 0x000000000000FF00UL;
            WP = 0x00FF000000000000UL;
            BR = 0x0000000000000081UL;
            WR = 0x8100000000000000UL;
            BN = 0x0000000000000042UL;
            WN = 0x4200000000000000UL;
            BB = 0x0000000000000024UL;
            WB = 0x2400000000000000UL;
            BQ = 0x0000000000000008UL;
            WQ = 0x0800000000000000UL;
            BK = 0x0000000000000010UL;
            WK = 0x1000000000000000UL;
        }

        /// <summary>
        /// Main entry: returns a list of all target (row,col) from the given square.
        /// </summary>
        public static List<(int row, int col)> GetValidMoves(int row, int col, bool whiteTurn)
        {
            //save them for the function, they wont change
            ulong whiteAll = WP | WN | WB | WR | WQ | WK;
            ulong blackAll = BP | BN | BB | BR | BQ | BK;
            ulong occupied = whiteAll | blackAll;

            int sq = row * 8 + col;
            ulong srcLoc = 1UL << sq;
            ulong pseuduMoves = 0UL;
            if (whiteTurn && (srcLoc & blackAll) != 0) return new List<(int, int)>();
            if (!whiteTurn && (srcLoc & whiteAll) != 0) return new List<(int, int)>();

            // determine which piece sits here
            if ((WP & srcLoc) != 0) pseuduMoves = PawnMoves(srcLoc, true);
            else if ((BP & srcLoc) != 0) pseuduMoves = PawnMoves(srcLoc, false);
            else if ((WN & srcLoc) != 0) pseuduMoves = KnightAttacks(srcLoc);
            else if ((BN & srcLoc) != 0) pseuduMoves = KnightAttacks(srcLoc);
            else if ((WB & srcLoc) != 0) pseuduMoves = BishopAttacks(srcLoc);
            else if ((BB & srcLoc) != 0) pseuduMoves = BishopAttacks(srcLoc);
            else if ((WR & srcLoc) != 0) pseuduMoves = RookAttacks(srcLoc);
            else if ((BR & srcLoc) != 0) pseuduMoves = RookAttacks(srcLoc);
            else if ((WQ & srcLoc) != 0) pseuduMoves = RookAttacks(srcLoc) | BishopAttacks(srcLoc); // queen is rook and bishop combined
            else if ((BQ & srcLoc) != 0) pseuduMoves = RookAttacks(srcLoc) | BishopAttacks(srcLoc);
            else if ((WK & srcLoc) != 0) pseuduMoves = KingAttacks(srcLoc) | KingCastles(srcLoc);
            else if ((BK & srcLoc) != 0) pseuduMoves = KingAttacks(srcLoc) | KingCastles(srcLoc);
            else return new List<(int, int)>();  // empty if no piece

            ulong friends = whiteTurn ? whiteAll : blackAll;
            pseuduMoves &= ~friends;

            // build list of pseudo‑legal target squares
            var moves = new List<(int, int)>();
            var saved = SaveState();

            while (pseuduMoves != 0)
            {
                int toSq = BitOperations.TrailingZeroCount(pseuduMoves);
                pseuduMoves &= pseuduMoves - 1;
                int tr = toSq / 8, tc = toSq % 8; // target row and column
                MovePiece(row, col, tr, tc);
                if (!IsInCheck(whiteTurn))
                    moves.Add((tr, tc));
                RestoreState(saved);

            }
            return moves;
        }

        /// <summary>
        /// Update bitboards when the user moves a piece (no legality checks).
        /// returns:
        /// 0 is no castle, 1 is castle right, 2 is castle left, 3 is en passant capture, 4 is promotion
        /// </summary>
        public static int MovePiece(int fr, int fc, int tr, int tc, Piece? promoteTo = Piece.None)
        {
            int fSq = fr * 8 + fc;
            int tSq = tr * 8 + tc;
            ulong fromBB = 1UL << fSq;
            ulong toBB = 1UL << tSq;


            //castle data update
            if ((fromBB & WK) != 0) castleData |= 0b00000001; // white king moved
            else if ((fromBB & BK) != 0) castleData |= 0b00001000; // black king moved
            else if ((fromBB & WR) != 0 && fc == 7) castleData |= 0b00000010; // white right rook moved
            else if ((fromBB & WR) != 0 && fc == 0) castleData |= 0b00000100; // white left rook moved
            else if ((fromBB & BR) != 0 && fc == 7) castleData |= 0b00010000; // black right rook moved
            else if ((fromBB & BR) != 0 && fc == 0) castleData |= 0b00100000; // black left rook moved


            //check for castling
            if ((fromBB & WK) != 0)
            {
                if (fromBB << 2 == toBB)
                {
                    WK = toBB;
                    WR &= ~(toBB << 1);
                    WR |= (toBB >> 1);
                    return 1; //castle right
                }
                if (fromBB >> 2 == toBB)
                {
                    WK = toBB;
                    WR &= ~(toBB >> 2);
                    WR |= (toBB << 1);
                    return 2; //castle left
                }
            }
            else if ((fromBB & BK) != 0)
            {
                if (fromBB << 2 == toBB)
                {
                    BK = toBB;
                    BR &= ~(toBB << 1);
                    BR |= (toBB >> 1);
                    return 1; //castle right
                }
                if (fromBB >> 2 == toBB)
                {
                    BK = toBB;
                    BR &= ~(toBB >> 2);
                    BR |= (toBB << 1);
                    return 2; //castle left
                }
            }

            //check for en passant capture
            if (enPassantSquare.HasValue && toBB == enPassantSquare)
            {
                // en passant capture
                if ((fromBB & WP) != 0) // white pawn captures
                {
                    WP &= ~fromBB;
                    BP &= ~enPassantSquare.Value << 8; // remove black pawn
                    WP |= toBB;
                    return 3;

                }
                else if ((fromBB & BP) != 0) // black pawn captures
                {
                    BP &= ~fromBB;
                    BP |= toBB;
                    WP &= ~enPassantSquare.Value >> 8; // remove white pawn
                    return 3;

                }
            }


            //if the move is pawn advancement, save for en passant
            if ((fromBB & WP) != 0 && (toBB << 16) == fromBB)
            {
                // white pawn moved two squares forward
                enPassantSquare = toBB << 8;
            }
            else if ((fromBB & BP) != 0 && (toBB >> 16) == fromBB)
            {
                // black pawn moved two squares forward
                enPassantSquare = toBB >> 8;
            }
            else
            {
                enPassantSquare = null; // reset en passant if not a pawn double push
            }

            // remove any captured piece
            BP &= ~toBB; BN &= ~toBB; BB &= ~toBB; BR &= ~toBB; BQ &= ~toBB; BK &= ~toBB;
            WP &= ~toBB; WN &= ~toBB; WB &= ~toBB; WR &= ~toBB; WQ &= ~toBB; WK &= ~toBB;

            // find which color & piece, clear from‐square, set to‐square
            if ((WP & fromBB) != 0) { WP &= ~fromBB; WP |= toBB; }
            else if ((BP & fromBB) != 0) { BP &= ~fromBB; BP |= toBB; }
            else if ((WN & fromBB) != 0) { WN &= ~fromBB; WN |= toBB; }
            else if ((BN & fromBB) != 0) { BN &= ~fromBB; BN |= toBB; }
            else if ((WB & fromBB) != 0) { WB &= ~fromBB; WB |= toBB; }
            else if ((BB & fromBB) != 0) { BB &= ~fromBB; BB |= toBB; }
            else if ((WR & fromBB) != 0) { WR &= ~fromBB; WR |= toBB; }
            else if ((BR & fromBB) != 0) { BR &= ~fromBB; BR |= toBB; }
            else if ((WQ & fromBB) != 0) { WQ &= ~fromBB; WQ |= toBB; }
            else if ((BQ & fromBB) != 0) { BQ &= ~fromBB; BQ |= toBB; }
            else if ((WK & fromBB) != 0) { WK &= ~fromBB; WK |= toBB; }
            else if ((BK & fromBB) != 0) { BK &= ~fromBB; BK |= toBB; }

            //check if the move was a promotion
            if ((WP & toBB) != 0 && (toBB & 0x00000000000000FFUL) != 0)
            {
                WP &= ~toBB;
                if(promoteTo == Piece.Queen) WQ |= toBB;
                if(promoteTo == Piece.Knight) WN |= toBB;
                if(promoteTo == Piece.Rook) WR |= toBB;
                if(promoteTo == Piece.Bishop) WB |= toBB;
                return 4;
            }
            else if ((BP & toBB) != 0 && (toBB & 0xFF00000000000000UL) != 0)
            {
                BP &= ~toBB;
                if (promoteTo == Piece.Queen) BQ |= toBB;
                if (promoteTo == Piece.Knight) BN |= toBB;
                if (promoteTo == Piece.Rook) BR |= toBB;
                if (promoteTo == Piece.Bishop) BB |= toBB;
                return 4;
            }
            return 0;
        }

        //precompute knight and king moves
        private static ulong computeKnightAttack(ulong k)
        {
            ulong moves = 0UL;
            moves |= ((k >> 15) & ~FileA); // top left
            moves |= ((k >> 17) & ~FileH); // top right

            moves |= (k << 15) & ~FileH; // down left
            moves |= (k << 17) & ~FileA; // down right

            moves |= (k << 6) & ~FileH & ~FileG; // middle top left
            moves |= (k << 10) & ~FileA & ~FileB; // middle top right

            moves |= (k >> 6) & ~FileB & ~FileA; // middle down left
            moves |= (k >> 10) & ~FileG & ~FileH; // middle down right
            return moves;
        }
        private static ulong computeKingAttack(ulong k)
        {
            ulong attacks = 0UL;
            ulong notFileA = ~FileA;
            ulong notFileH = ~FileH;

            // vertical and horizontal
            attacks |= (k << 8);                    // up
            attacks |= (k >> 8);                    // down
            attacks |= (k << 1) & notFileA;         // right
            attacks |= (k >> 1) & notFileH;         // left

            // diagonals
            attacks |= (k << 9) & notFileA;         // up-right
            attacks |= (k << 7) & notFileH;         // up-left
            attacks |= (k >> 7) & notFileA;         // down-right
            attacks |= (k >> 9) & notFileH;         // down-left

            return attacks;
        }

        private static void precomputePieces()
        {
            for (int i = 0; i < 64; i++)
            {
                ulong k = 1UL << i;
                KnightMoves[i] = computeKnightAttack(k);
                KingMoves[i] = computeKingAttack(k);
            }
        }
        
        // —— attack generators ——

        private static ulong PawnMoves(ulong p, bool white)
        {
            ulong moves = 0UL, occ = Occupied;
            if (white)
            {
                // single pushes
                ulong one = (p >> 8) & ~occ;
                moves |= one;
                // capture
                moves |= ((p >> 7) & ~FileA & BlackAll)
                      | ((p >> 9) & ~FileH & BlackAll);
                moves |= (0x000000FF00000000UL & (p >> 16)) & ~occ & ~(occ >> 8); // double push

                //en passant capture
                if (enPassantSquare.HasValue && (((enPassantSquare.Value << 7 == p) && (p & ~FileH) != 0)|| (((enPassantSquare.Value << 9 == p) && (p & ~FileA) != 0))))
                {
                    // en passant capture
                    moves |= (enPassantSquare.Value);
                }
            }
            else
            {
                ulong one = (p << 8) & ~occ;
                moves |= one;

                moves |= ((p << 7) & ~FileH & WhiteAll)
                      | ((p << 9) & ~FileA & WhiteAll);
                moves |= (0x00000000FF000000UL & (p << 16)) & ~occ & ~(occ << 8); // double push

                //en passant capture
                if (enPassantSquare.HasValue && ((enPassantSquare.Value >> 7 == p && (p & ~FileA) != 0) || (enPassantSquare.Value >> 9 == p && (p & ~FileH) != 0)))
                {
                    // en passant capture
                    moves |= (enPassantSquare.Value);
                }
            }
            return moves;
        }

        private static ulong KnightAttacks(ulong k) => KnightMoves[BitOperations.TrailingZeroCount(k)];

        private static ulong KingAttacks(ulong k) => KingMoves[BitOperations.TrailingZeroCount(k)];

        private static ulong KingCastles(ulong k)
        {
            ulong moves = 0UL;
            if ((k & WK) != 0)
            {
                if ((~(castleData << 1) & ~castleData & 0b00000010) != 0 && !isPieceUnderAttack(k, true) && !isPieceUnderAttack(k << 1, true) && ((k << 1 & Occupied) == 0)) moves |= (k << 2); // white king can castle right
                if ((~(castleData << 2) & ~castleData & 0b00000100) != 0 && !isPieceUnderAttack(k, true) && !isPieceUnderAttack(k >> 1, true) && ((k >> 1 & Occupied) == 0) && ((k >> 3 & Occupied) == 0)) moves |= (k >> 2); // white king can castle left
            }
            else
            {
                if ((~(castleData << 1) & ~castleData & 0b00010000) != 0 && !isPieceUnderAttack(k, false) && !isPieceUnderAttack(k << 1, false) && ((k << 1 & Occupied) == 0)) moves |= (k << 2); // white king can castle right
                if ((~(castleData << 2) & ~castleData & 0b00100000) != 0 && !isPieceUnderAttack(k, false) && !isPieceUnderAttack(k >> 1, false) && ((k >> 1 & Occupied) == 0) && ((k >> 3 & Occupied) == 0)) moves |= (k >> 2); // white king can castle left
            }
            return moves;
        }

        private static ulong RookAttacks(ulong r)
            => Slide(r, Up) | Slide(r, Down)
                         | Slide(r, Right) | Slide(r, Left);

        private static ulong BishopAttacks(ulong b)
            => Slide(b, UR) | Slide(b, DL) | Slide(b, UL) | Slide(b, DR);

        private static ulong Slide(ulong bb, int dir)
        {
            ulong attacks = 0UL;
            while (true)
            {
                if ((bb & FileH) != 0 && ((dir + 16) % 8) == 1) break;
                if ((bb & FileA) != 0 && (dir + 16) % 8 == 7) break;

                if (dir > 0) bb <<= dir;
                else bb >>= -dir;
                attacks |= bb;

                if (bb == 0) break;
                if ((bb & Occupied) != 0) break;
            }
            return attacks;
        }

        private static bool isPieceUnderAttack(ulong p, bool isWhite)
        {
            ulong attackers = 0UL;
            attackers |= (PawnMoves(p, isWhite) & (isWhite ? BP : WP));
            attackers |= (KnightAttacks(p) & (isWhite ? BN : WN));
            attackers |= (BishopAttacks(p) & ((isWhite ? BB : WB) | (isWhite ? BQ : WQ)));
            attackers |= (RookAttacks(p) & ((isWhite ? BR : WR) | (isWhite ? BQ : WQ)));
            attackers |= (KingAttacks(p) & (isWhite ? BK : WK));
            return attackers != 0;
        }

        public static bool IsInCheck(bool whiteTurn)
        {
            ulong kingBB = whiteTurn ? WK : BK;
            if (kingBB == 0UL) return true;
            return isPieceUnderAttack(kingBB, whiteTurn);
        }

        public static (bool, Piece) WhatPieceIsIt(int row, int col)
        {
            int loc = row * 8 + col;
            ulong srcLoc = 1UL << loc;
            if ((WP & srcLoc) != 0) return (true, Piece.Pawn);
            else if ((BP & srcLoc) != 0) return (false, Piece.Pawn);
            else if ((WN & srcLoc) != 0) return (true, Piece.Knight);
            else if ((BN & srcLoc) != 0) return (false, Piece.Knight);
            else if ((WB & srcLoc) != 0) return (true, Piece.Bishop);
            else if ((BB & srcLoc) != 0) return (false, Piece.Bishop);
            else if ((WR & srcLoc) != 0) return (true, Piece.Rook);
            else if ((BR & srcLoc) != 0) return (false, Piece.Rook);
            else if ((WQ & srcLoc) != 0) return (true, Piece.Queen); 
            else if ((BQ & srcLoc) != 0) return (false, Piece.Queen);
            else if ((WK & srcLoc) != 0) return (true, Piece.King);
            else if ((BK & srcLoc) != 0) return (false, Piece.King);
            return (false, Piece.None);//deafult
        }
        public static void SetPositionFromFEN(string fen)
        {
            WP = WN = WB = WR = WQ = WK = 0;
            BP = BN = BB = BR = BQ = BK = 0;
            castleData = 0;
            enPassantSquare = null;

            var parts = fen.Split(' ');
            var rows = parts[0].Split('/');
            for (int r = 0; r < 8; r++)
            {
                int c = 0;
                foreach (char ch in rows[r])
                {
                    if (char.IsDigit(ch))
                    {
                        c += ch - '0';
                    }
                    else
                    {
                        ulong bit = 1UL << ((r) * 8 + c);
                        switch (ch)
                        {
                            case 'P': WP |= bit; break;
                            case 'N': WN |= bit; break;
                            case 'B': WB |= bit; break;
                            case 'R': WR |= bit; break;
                            case 'Q': WQ |= bit; break;
                            case 'K': WK |= bit; break;
                            case 'p': BP |= bit; break;
                            case 'n': BN |= bit; break;
                            case 'b': BB |= bit; break;
                            case 'r': BR |= bit; break;
                            case 'q': BQ |= bit; break;
                            case 'k': BK |= bit; break;
                        }
                        c++;
                    }
                }
            }

            if (parts.Length > 2 && parts[2] != "-")
            {
                int file = parts[2][0] - 'a';
                int rank = parts[2][1] - '1';
                enPassantSquare = 1UL << (rank * 8 + file);
            }

            if (parts.Length > 1)
            {
                string castling = parts[1];
                if (!castling.Contains("K")) castleData |= 0b00000010;
                if (!castling.Contains("Q")) castleData |= 0b00000100;
                if (!castling.Contains("k")) castleData |= 0b00010000;
                if (!castling.Contains("q")) castleData |= 0b00100000;
            }
        }

        public static (bool whiteToMove, string fen) GetFENFromCurrentPosition()
        {
            string[] boardRows = new string[8];
            for (int r = 0; r < 8; r++)
            {
                string row = "";
                int empty = 0;
                for (int c = 0; c < 8; c++)
                {
                    int idx = (7 - r) * 8 + c;
                    ulong mask = 1UL << idx;
                    char? piece = null;

                    if ((WP & mask) != 0) piece = 'P';
                    else if ((BP & mask) != 0) piece = 'p';
                    else if ((WN & mask) != 0) piece = 'N';
                    else if ((BN & mask) != 0) piece = 'n';
                    else if ((WB & mask) != 0) piece = 'B';
                    else if ((BB & mask) != 0) piece = 'b';
                    else if ((WR & mask) != 0) piece = 'R';
                    else if ((BR & mask) != 0) piece = 'r';
                    else if ((WQ & mask) != 0) piece = 'Q';
                    else if ((BQ & mask) != 0) piece = 'q';
                    else if ((WK & mask) != 0) piece = 'K';
                    else if ((BK & mask) != 0) piece = 'k';

                    if (piece == null)
                        empty++;
                    else
                    {
                        if (empty > 0) { row += empty.ToString(); empty = 0; }
                        row += piece;
                    }
                }
                if (empty > 0) row += empty.ToString();
                boardRows[r] = row;
            }

            string castling = "";
            if ((castleData & 0b00000010) == 0) castling += "K";
            if ((castleData & 0b00000100) == 0) castling += "Q";
            if ((castleData & 0b00010000) == 0) castling += "k";
            if ((castleData & 0b00100000) == 0) castling += "q";
            if (castling == "") castling = "-";

            string ep = "-";
            if (enPassantSquare.HasValue)
            {
                int epIdx = BitOperations.TrailingZeroCount(enPassantSquare.Value);
                ep = $"{(char)('a' + epIdx % 8)}{1 + epIdx / 8}";
            }

            return (true, $"{string.Join("/", boardRows)} {castling} {ep}"); // You can add halfmove/fullmove counters later
        }
        
        /// <summary>
        /// Generates all legal moves for the current board position, encoded as 32-bit integers.
        /// Each move is encoded with the following bit layout:
        /// 
        /// Bit layout (from most to least significant):
        /// ┌────────┬────────────┬────────────┬────────────┐
        /// │ Bit 31 │ Bits 30–24 │ Bits 23–17 │ Bits 16–15 │
        /// └────────┴────────────┴────────────┴────────────┘
        /// │ Color  │ From Square│ To Square  │ Promotion   │
        /// │ 0=White│ (0–63)     │ (0–63)     │ 00=None     │
        /// │ 1=Black│            │            │ 01=Queen    │
        /// │        │            │            │ 10=Rook     │
        /// │        │            │            │ 11=Bishop   │
        /// └────────┴────────────┴────────────┴────────────┘
        /// </summary>
        /// <param name="whiteTurn">Indicates whether it is white's turn</param>
        /// <returns>A list of legal moves, each represented as a 32-bit int</returns>
        public static List<int> getAllAvailableMoves(bool whiteTurn)
        {
            var allMoves = new List<int>();

            if (whiteTurn)
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((WhiteAll & (1UL << i)) == 0) continue; // skip empty squares
                    ulong p = 1UL << i;

                    int row = i / 8;
                    int col = i % 8;
                    List<(int r, int c)> legalTargets = GetValidMoves(row, col, true);

                    foreach (var (tr, tc) in legalTargets)
                    {
                        int from = row * 8 + col;
                        int to = tr * 8 + tc;

                        // Start encoding
                        int colorBit = 0;
                        int move = (colorBit << 31)             // bit 31
                                 | ((from & 0x7F) << 24)         // bits 30–24
                                 | ((to & 0x7F) << 17);          // bits 23–17

                        // Handle promotion
                        if ((p & WP) != 0 && (tr == 0 || tr == 7))
                        {
                            // Add four promotion options: 01 = Q, 10 = R, 11 = B, 00 = N
                            allMoves.Add(move | (0b01 << 15)); // Queen
                            allMoves.Add(move | (0b10 << 15)); // Rook
                            allMoves.Add(move | (0b11 << 15)); // Bishop
                            allMoves.Add(move | (0b00 << 15)); // Knight (or none)
                        }
                        else
                        {
                            allMoves.Add(move); // No promotion
                        }
                    }
                }

                return allMoves;
            }
            else
            {
                for (int i = 0; i < 64; i++)
                {
                    if ((BlackAll & (1UL << i)) == 0) continue; // skip empty squares
                    ulong p = 1UL << i;

                    int row = i / 8;
                    int col = i % 8;
                    List<(int r, int c)> legalTargets = GetValidMoves(row, col, false);

                    foreach (var (tr, tc) in legalTargets)
                    {
                        int from = row * 8 + col;
                        int to = tr * 8 + tc;

                        // Start encoding
                        int colorBit = 1;
                        int move = (colorBit << 31)             // bit 31
                                 | ((from & 0x7F) << 24)         // bits 30–24
                                 | ((to & 0x7F) << 17);          // bits 23–17

                        // Handle promotion
                        if ((p & BP) != 0 && (tr == 0 || tr == 7))
                        {
                            // Add four promotion options: 01 = Q, 10 = R, 11 = B, 00 = N
                            allMoves.Add(move | (0b01 << 15)); // Queen
                            allMoves.Add(move | (0b10 << 15)); // Rook
                            allMoves.Add(move | (0b11 << 15)); // Bishop
                            allMoves.Add(move | (0b00 << 15)); // Knight (or none)
                        }
                        else
                        {
                            allMoves.Add(move); // No promotion
                        }
                    }
                }

                return allMoves;
            }
        }

        /// <summary>
        /// Performs perft (performance test) to count all leaf nodes up to a given depth.
        /// Useful for verifying move generation and detecting bugs like incorrect castling/en-passant logic.
        /// </summary>
        /// <param name="depth">Depth to search (number of plies)</param>
        /// <param name="whiteTurn">True if current side to move is White</param>
        /// <returns>Total number of leaf positions at exactly this depth</returns>
        public static long Perfit(int depth, bool whiteTurn)
        {
            if (depth == 0)
                return 1; // Reached leaf node

            long nodes = 0;
            var moves = getAllAvailableMoves(whiteTurn);

            foreach (int move in moves)
            {
                // Decode the move
                bool color = ((move >> 31) & 1) == 1;
                int fromSq = (move >> 24) & 0x7F;
                int toSq = (move >> 17) & 0x7F;
                int promo = (move >> 15) & 0x3;

                int fr = fromSq / 8, fc = fromSq % 8;
                int tr = toSq / 8, tc = toSq % 8;
                var lastSavedState = SaveState(); // Save current state before making the move
                // Execute move (updates board state)
                MovePiece(fr, fc, tr, tc,
                    promo == 1 ? Piece.Queen :
                    promo == 2 ? Piece.Rook :
                    promo == 3 ? Piece.Bishop :
                    Piece.Knight);

                // Recurse
                nodes += Perfit(depth - 1, !whiteTurn);

                // Undo move
                RestoreState(lastSavedState);
            }

            return nodes;
        }


        public static void PerftWithLogging(int depth, bool whiteTurn, string outputPath)
        {
            using StreamWriter writer = new StreamWriter(outputPath);
            var state = SaveState();
            PerftRecursive(depth, whiteTurn, "", writer);
            RestoreState(state);
        }

        /// <summary>
        /// Internal recursive perft that logs full move sequences to the writer.
        /// </summary>
        private static void PerftRecursive(int depth, bool whiteTurn, string moveHistory, StreamWriter writer)
        {
            if (depth == 0)
            {
                writer.WriteLine(moveHistory);
                return;
            }

            var moves = getAllAvailableMoves(whiteTurn);
            var saved = SaveState();

            foreach (int move in moves)
            {
                // Decode move info
                int from = (move >> 24) & 0x7F;
                int to = (move >> 17) & 0x7F;
                int promo = (move >> 15) & 0x3;

                int fr = from / 8, fc = from % 8;
                int tr = to / 8, tc = to % 8;

                // Convert to algebraic notation (e.g., e2e4)
                string moveStr = SquareToAlgebraic(from) + SquareToAlgebraic(to);
                if (promo != 0)
                {
                    moveStr += promo switch
                    {
                        1 => "q",
                        2 => "r",
                        3 => "b",
                        _ => ""
                    };
                }

                MovePiece(fr, fc, tr, tc,
                    promo == 1 ? Piece.Queen :
                    promo == 2 ? Piece.Rook :
                    promo == 3 ? Piece.Bishop :
                    Piece.Knight);

                PerftRecursive(depth - 1, !whiteTurn, moveHistory + moveStr + " ", writer);

                RestoreState(saved);
            }
        }

        /// <summary>
        /// Converts a square index (0–63) to algebraic notation (e.g., 0 → a1, 63 → h8).
        /// </summary>
        private static string SquareToAlgebraic(int square)
        {
            int file = square % 8;
            int rank = 7 - (square / 8);
            return $"{(char)('a' + file)}{1 + rank}";
        }
    }
}
