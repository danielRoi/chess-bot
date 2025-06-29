using System.Collections.Generic;
//all the code here and the numbers based on this code https://github.com/SebLague/Chess-Coding-Adventure/tree/Chess-V2-UCI/Chess-Coding-Adventure/src/Core/Move%20Generation/Magics
namespace ChessApp
{
    // Helper structures and utilities
    public struct Coord
    {
        public int fileIndex;
        public int rankIndex;

        public Coord(int fileIndex, int rankIndex)
        {
            this.fileIndex = fileIndex;
            this.rankIndex = rankIndex;
        }

        public Coord(int squareIndex)
        {
            fileIndex = squareIndex % 8;
            rankIndex = squareIndex / 8;
        }

        public int SquareIndex => rankIndex * 8 + fileIndex;

        public bool IsValidSquare()
        {
            return fileIndex >= 0 && fileIndex < 8 && rankIndex >= 0 && rankIndex < 8;
        }

        public static Coord operator +(Coord a, Coord b)
        {
            return new Coord(a.fileIndex + b.fileIndex, a.rankIndex + b.rankIndex);
        }

        public static Coord operator *(Coord coord, int multiplier)
        {
            return new Coord(coord.fileIndex * multiplier, coord.rankIndex * multiplier);
        }
    }

    public static class BoardHelper
    {
        public static readonly Coord[] RookDirections = {
            new Coord(0, 1),   // North
            new Coord(0, -1),  // South
            new Coord(1, 0),   // East
            new Coord(-1, 0)   // West
        };

        public static readonly Coord[] BishopDirections = {
            new Coord(1, 1),   // Northeast
            new Coord(1, -1),  // Southeast
            new Coord(-1, 1),  // Northwest
            new Coord(-1, -1)  // Southwest
        };
    }

    public static class BitBoardUtility
    {
        public static void SetSquare(ref ulong bitboard, int squareIndex)
        {
            bitboard |= 1UL << squareIndex;
        }

        public static bool ContainsSquare(ulong bitboard, int squareIndex)
        {
            return ((bitboard >> squareIndex) & 1) != 0;
        }
    }

    // Precomputed magic numbers and shifts
    public static class PrecomputedMagics
    {
        public static readonly int[] RookShifts = { 52, 52, 52, 52, 52, 52, 52, 52, 53, 53, 53, 54, 53, 53, 54, 53, 53, 54, 54, 54, 53, 53, 54, 53, 53, 54, 53, 53, 54, 54, 54, 53, 52, 54, 53, 53, 53, 53, 54, 53, 52, 53, 54, 54, 53, 53, 54, 53, 53, 54, 54, 54, 53, 53, 54, 53, 52, 53, 53, 53, 53, 53, 53, 52 };
        public static readonly int[] BishopShifts = { 58, 60, 59, 59, 59, 59, 60, 58, 60, 59, 59, 59, 59, 59, 59, 60, 59, 59, 57, 57, 57, 57, 59, 59, 59, 59, 57, 55, 55, 57, 59, 59, 59, 59, 57, 55, 55, 57, 59, 59, 59, 59, 57, 57, 57, 57, 59, 59, 60, 60, 59, 59, 59, 59, 60, 60, 58, 60, 59, 59, 59, 59, 59, 58 };

        public static readonly ulong[] RookMagics = { 468374916371625120, 18428729537625841661, 2531023729696186408, 6093370314119450896, 13830552789156493815, 16134110446239088507, 12677615322350354425, 5404321144167858432, 2111097758984580, 18428720740584907710, 17293734603602787839, 4938760079889530922, 7699325603589095390, 9078693890218258431, 578149610753690728, 9496543503900033792, 1155209038552629657, 9224076274589515780, 1835781998207181184, 509120063316431138, 16634043024132535807, 18446673631917146111, 9623686630121410312, 4648737361302392899, 738591182849868645, 1732936432546219272, 2400543327507449856, 5188164365601475096, 10414575345181196316, 1162492212166789136, 9396848738060210946, 622413200109881612, 7998357718131801918, 7719627227008073923, 16181433497662382080, 18441958655457754079, 1267153596645440, 18446726464209379263, 1214021438038606600, 4650128814733526084, 9656144899867951104, 18444421868610287615, 3695311799139303489, 10597006226145476632, 18436046904206950398, 18446726472933277663, 3458977943764860944, 39125045590687766, 9227453435446560384, 6476955465732358656, 1270314852531077632, 2882448553461416064, 11547238928203796481, 1856618300822323264, 2573991788166144, 4936544992551831040, 13690941749405253631, 15852669863439351807, 18302628748190527413, 12682135449552027479, 13830554446930287982, 18302628782487371519, 7924083509981736956, 4734295326018586370 };
        public static readonly ulong[] BishopMagics = { 16509839532542417919, 14391803910955204223, 1848771770702627364, 347925068195328958, 5189277761285652493, 3750937732777063343, 18429848470517967340, 17870072066711748607, 16715520087474960373, 2459353627279607168, 7061705824611107232, 8089129053103260512, 7414579821471224013, 9520647030890121554, 17142940634164625405, 9187037984654475102, 4933695867036173873, 3035992416931960321, 15052160563071165696, 5876081268917084809, 1153484746652717320, 6365855841584713735, 2463646859659644933, 1453259901463176960, 9808859429721908488, 2829141021535244552, 576619101540319252, 5804014844877275314, 4774660099383771136, 328785038479458864, 2360590652863023124, 569550314443282, 17563974527758635567, 11698101887533589556, 5764964460729992192, 6953579832080335136, 1318441160687747328, 8090717009753444376, 16751172641200572929, 5558033503209157252, 17100156536247493656, 7899286223048400564, 4845135427956654145, 2368485888099072, 2399033289953272320, 6976678428284034058, 3134241565013966284, 8661609558376259840, 17275805361393991679, 15391050065516657151, 11529206229534274423, 9876416274250600448, 16432792402597134585, 11975705497012863580, 11457135419348969979, 9763749252098620046, 16960553411078512574, 15563877356819111679, 14994736884583272463, 9441297368950544394, 14537646123432199168, 9888547162215157388, 18140215579194907366, 18374682062228545019 };
    }

    // Magic helper functions
    public static class MagicHelper
    {
        public static ulong[] CreateAllBlockerBitboards(ulong movementMask)
        {
            // Create a list of the indices of the bits that are set in the movement mask
            List<int> moveSquareIndices = new();
            for (int i = 0; i < 64; i++)
            {
                if (((movementMask >> i) & 1) == 1)
                {
                    moveSquareIndices.Add(i);
                }
            }

            // Calculate total number of different bitboards (one for each possible arrangement of pieces)
            int numPatterns = 1 << moveSquareIndices.Count; // 2^n
            ulong[] blockerBitboards = new ulong[numPatterns];

            // Create all bitboards
            for (int patternIndex = 0; patternIndex < numPatterns; patternIndex++)
            {
                for (int bitIndex = 0; bitIndex < moveSquareIndices.Count; bitIndex++)
                {
                    int bit = (patternIndex >> bitIndex) & 1;
                    blockerBitboards[patternIndex] |= (ulong)bit << moveSquareIndices[bitIndex];
                }
            }

            return blockerBitboards;
        }

        public static ulong CreateMovementMask(int squareIndex, bool ortho)
        {
            ulong mask = 0;
            Coord[] directions = ortho ? BoardHelper.RookDirections : BoardHelper.BishopDirections;
            Coord startCoord = new Coord(squareIndex);

            foreach (Coord dir in directions)
            {
                for (int dst = 1; dst < 8; dst++)
                {
                    Coord coord = startCoord + dir * dst;
                    Coord nextCoord = startCoord + dir * (dst + 1);

                    if (nextCoord.IsValidSquare())
                    {
                        BitBoardUtility.SetSquare(ref mask, coord.SquareIndex);
                    }
                    else { break; }
                }
            }
            return mask;
        }

        public static ulong LegalMoveBitboardFromBlockers(int startSquare, ulong blockerBitboard, bool ortho)
        {
            ulong bitboard = 0;

            Coord[] directions = ortho ? BoardHelper.RookDirections : BoardHelper.BishopDirections;
            Coord startCoord = new Coord(startSquare);

            foreach (Coord dir in directions)
            {
                for (int dst = 1; dst < 8; dst++)
                {
                    Coord coord = startCoord + dir * dst;

                    if (coord.IsValidSquare())
                    {
                        BitBoardUtility.SetSquare(ref bitboard, coord.SquareIndex);
                        if (BitBoardUtility.ContainsSquare(blockerBitboard, coord.SquareIndex))
                        {
                            break;
                        }
                    }
                    else { break; }
                }
            }

            return bitboard;
        }
    }

    // Main Magic class with exposed attack tables
    public static class Magic
    {
        // Rook and bishop mask bitboards for each origin square.
        // A mask is simply the legal moves available to the piece from the origin square
        // (on an empty board), except that the moves stop 1 square before the edge of the board.
        public static readonly ulong[] RookMask;
        public static readonly ulong[] BishopMask;

        // Exposed attack tables - these are what you requested
        public static readonly ulong[][] rookAttacks;
        public static readonly ulong[][] bishopAttacks;

        // Alternative names for compatibility with original code
        public static readonly ulong[][] RookAttacks;
        public static readonly ulong[][] BishopAttacks;

        public static ulong GetSliderAttacks(int square, ulong blockers, bool ortho)
        {
            return ortho ? GetRookAttacks(square, blockers) : GetBishopAttacks(square, blockers);
        }

        public static ulong GetRookAttacks(int square, ulong blockers)
        {
            ulong key = ((blockers & RookMask[square]) * PrecomputedMagics.RookMagics[square]) >> PrecomputedMagics.RookShifts[square];
            return RookAttacks[square][key];
        }

        public static ulong GetBishopAttacks(int square, ulong blockers)
        {
            ulong key = ((blockers & BishopMask[square]) * PrecomputedMagics.BishopMagics[square]) >> PrecomputedMagics.BishopShifts[square];
            return BishopAttacks[square][key];
        }

        static Magic()
        {
            RookMask = new ulong[64];
            BishopMask = new ulong[64];

            for (int squareIndex = 0; squareIndex < 64; squareIndex++)
            {
                RookMask[squareIndex] = MagicHelper.CreateMovementMask(squareIndex, true);
                BishopMask[squareIndex] = MagicHelper.CreateMovementMask(squareIndex, false);
            }

            RookAttacks = new ulong[64][];
            BishopAttacks = new ulong[64][];

            for (int i = 0; i < 64; i++)
            {
                RookAttacks[i] = CreateTable(i, true, PrecomputedMagics.RookMagics[i], PrecomputedMagics.RookShifts[i]);
                BishopAttacks[i] = CreateTable(i, false, PrecomputedMagics.BishopMagics[i], PrecomputedMagics.BishopShifts[i]);
            }

            // Set the exposed arrays to reference the same data
            rookAttacks = RookAttacks;
            bishopAttacks = BishopAttacks;

            ulong[] CreateTable(int square, bool rook, ulong magic, int leftShift)
            {
                int numBits = 64 - leftShift;
                int lookupSize = 1 << numBits;
                ulong[] table = new ulong[lookupSize];

                ulong movementMask = MagicHelper.CreateMovementMask(square, rook);
                ulong[] blockerPatterns = MagicHelper.CreateAllBlockerBitboards(movementMask);

                foreach (ulong pattern in blockerPatterns)
                {
                    ulong index = (pattern * magic) >> leftShift;
                    ulong moves = MagicHelper.LegalMoveBitboardFromBlockers(square, pattern, rook);
                    table[index] = moves;
                }

                return table;
            }
        }
    }
}