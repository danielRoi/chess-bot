using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ChessApp
{
    // Helper structures and utilities
    public readonly struct Coord
    {
        public readonly int fileIndex;
        public readonly int rankIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Coord(int fileIndex, int rankIndex)
        {
            this.fileIndex = fileIndex;
            this.rankIndex = rankIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Coord(int squareIndex)
        {
            fileIndex = squareIndex & 7; // Faster than % 8
            rankIndex = squareIndex >> 3; // Faster than / 8
        }

        public int SquareIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (rankIndex << 3) + fileIndex; // Faster than * 8
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValidSquare() => (uint)fileIndex < 8 && (uint)rankIndex < 8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Coord operator +(Coord a, Coord b) => new Coord(a.fileIndex + b.fileIndex, a.rankIndex + b.rankIndex);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Coord operator *(Coord coord, int multiplier) => new Coord(coord.fileIndex * multiplier, coord.rankIndex * multiplier);
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSquare(ref ulong bitboard, int squareIndex)
        {
            bitboard |= 1UL << squareIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    // Optimized magic helper functions
    public static class MagicHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong[] CreateAllBlockerBitboards(ulong movementMask)
        {
            // Use bit manipulation to find set bits more efficiently
            int bitCount = System.Numerics.BitOperations.PopCount(movementMask);
            int[] moveSquareIndices = new int[bitCount];

            int index = 0;
            ulong mask = movementMask;
            while (mask != 0)
            {
                int bitIndex = System.Numerics.BitOperations.TrailingZeroCount(mask);
                moveSquareIndices[index++] = bitIndex;
                mask &= mask - 1; // Clear the lowest set bit
            }

            int numPatterns = 1 << bitCount;
            ulong[] blockerBitboards = new ulong[numPatterns];

            // Generate all possible blocker patterns
            for (int patternIndex = 0; patternIndex < numPatterns; patternIndex++)
            {
                ulong pattern = 0;
                for (int bitIndex = 0; bitIndex < bitCount; bitIndex++)
                {
                    if ((patternIndex & (1 << bitIndex)) != 0)
                    {
                        pattern |= 1UL << moveSquareIndices[bitIndex];
                    }
                }
                blockerBitboards[patternIndex] = pattern;
            }

            return blockerBitboards;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong CreateMovementMask(int squareIndex, bool ortho)
        {
            ulong mask = 0;
            ReadOnlySpan<Coord> directions = ortho ?
                new ReadOnlySpan<Coord>(BoardHelper.RookDirections) :
                new ReadOnlySpan<Coord>(BoardHelper.BishopDirections);

            int file = squareIndex & 7;
            int rank = squareIndex >> 3;

            // Unroll the direction loop for better performance
            if (ortho)
            {
                // North
                for (int r = rank + 1; r < 7; r++) mask |= 1UL << (r * 8 + file);
                // South  
                for (int r = rank - 1; r > 0; r--) mask |= 1UL << (r * 8 + file);
                // East
                for (int f = file + 1; f < 7; f++) mask |= 1UL << (rank * 8 + f);
                // West
                for (int f = file - 1; f > 0; f--) mask |= 1UL << (rank * 8 + f);
            }
            else
            {
                // Northeast
                for (int d = 1; file + d < 7 && rank + d < 7; d++) mask |= 1UL << ((rank + d) * 8 + (file + d));
                // Southeast
                for (int d = 1; file + d < 7 && rank - d > 0; d++) mask |= 1UL << ((rank - d) * 8 + (file + d));
                // Northwest
                for (int d = 1; file - d > 0 && rank + d < 7; d++) mask |= 1UL << ((rank + d) * 8 + (file - d));
                // Southwest
                for (int d = 1; file - d > 0 && rank - d > 0; d++) mask |= 1UL << ((rank - d) * 8 + (file - d));
            }

            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong LegalMoveBitboardFromBlockers(int startSquare, ulong blockerBitboard, bool ortho)
        {
            ulong bitboard = 0;
            int file = startSquare & 7;
            int rank = startSquare >> 3;

            // Unrolled loops for better performance
            if (ortho)
            {
                // North
                for (int r = rank + 1; r < 8; r++)
                {
                    int square = r * 8 + file;
                    bitboard |= 1UL << square;
                    if ((blockerBitboard & (1UL << square)) != 0) break;
                }
                // South
                for (int r = rank - 1; r >= 0; r--)
                {
                    int square = r * 8 + file;
                    bitboard |= 1UL << square;
                    if ((blockerBitboard & (1UL << square)) != 0) break;
                }
                // East
                for (int f = file + 1; f < 8; f++)
                {
                    int square = rank * 8 + f;
                    bitboard |= 1UL << square;
                    if ((blockerBitboard & (1UL << square)) != 0) break;
                }
                // West
                for (int f = file - 1; f >= 0; f--)
                {
                    int square = rank * 8 + f;
                    bitboard |= 1UL << square;
                    if ((blockerBitboard & (1UL << square)) != 0) break;
                }
            }
            else
            {
                // Northeast
                for (int d = 1; file + d < 8 && rank + d < 8; d++)
                {
                    int square = (rank + d) * 8 + (file + d);
                    bitboard |= 1UL << square;
                    if ((blockerBitboard & (1UL << square)) != 0) break;
                }
                // Southeast
                for (int d = 1; file + d < 8 && rank - d >= 0; d++)
                {
                    int square = (rank - d) * 8 + (file + d);
                    bitboard |= 1UL << square;
                    if ((blockerBitboard & (1UL << square)) != 0) break;
                }
                // Northwest
                for (int d = 1; file - d >= 0 && rank + d < 8; d++)
                {
                    int square = (rank + d) * 8 + (file - d);
                    bitboard |= 1UL << square;
                    if ((blockerBitboard & (1UL << square)) != 0) break;
                }
                // Southwest
                for (int d = 1; file - d >= 0 && rank - d >= 0; d++)
                {
                    int square = (rank - d) * 8 + (file - d);
                    bitboard |= 1UL << square;
                    if ((blockerBitboard & (1UL << square)) != 0) break;
                }
            }

            return bitboard;
        }
    }

    // Main Magic class with exposed attack tables
    public static class Magic
    {
        // Static readonly arrays for better cache locality
        public static readonly ulong[] RookMask = new ulong[64];
        public static readonly ulong[] BishopMask = new ulong[64];

        // Flattened arrays for better cache performance
        public static readonly ulong[] RookAttacksFlat;
        public static readonly ulong[] BishopAttacksFlat;

        // Offset arrays to index into flattened arrays
        public static readonly int[] RookOffsets = new int[64];
        public static readonly int[] BishopOffsets = new int[64];

        // Traditional 2D arrays for compatibility
        public static readonly ulong[][] RookAttacks = new ulong[64][];
        public static readonly ulong[][] BishopAttacks = new ulong[64][];

        // Alternative names for compatibility
        public static readonly ulong[][] rookAttacks;
        public static readonly ulong[][] bishopAttacks;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetSliderAttacks(int square, ulong blockers, bool ortho)
        {
            return ortho ? GetRookAttacks(square, blockers) : GetBishopAttacks(square, blockers);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetRookAttacks(int square, ulong blockers)
        {
            ulong key = ((blockers & RookMask[square]) * PrecomputedMagics.RookMagics[square]) >> PrecomputedMagics.RookShifts[square];
            return RookAttacksFlat[(ulong)RookOffsets[square] + key];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetBishopAttacks(int square, ulong blockers)
        {
            ulong key = ((blockers & BishopMask[square]) * PrecomputedMagics.BishopMagics[square]) >> PrecomputedMagics.BishopShifts[square];
            return BishopAttacksFlat[(ulong)BishopOffsets[square] + key];
        }

        static Magic()
        {
            // Initialize masks
            for (int squareIndex = 0; squareIndex < 64; squareIndex++)
            {
                RookMask[squareIndex] = MagicHelper.CreateMovementMask(squareIndex, true);
                BishopMask[squareIndex] = MagicHelper.CreateMovementMask(squareIndex, false);
            }

            // Calculate total sizes for flattened arrays
            int rookTotalSize = 0;
            int bishopTotalSize = 0;

            for (int i = 0; i < 64; i++)
            {
                RookOffsets[i] = rookTotalSize;
                BishopOffsets[i] = bishopTotalSize;

                int rookBits = 64 - PrecomputedMagics.RookShifts[i];
                int bishopBits = 64 - PrecomputedMagics.BishopShifts[i];

                rookTotalSize += 1 << rookBits;
                bishopTotalSize += 1 << bishopBits;
            }

            // Allocate flattened arrays
            RookAttacksFlat = new ulong[rookTotalSize];
            BishopAttacksFlat = new ulong[bishopTotalSize];

            // Initialize tables
            for (int i = 0; i < 64; i++)
            {
                RookAttacks[i] = CreateTable(i, true, PrecomputedMagics.RookMagics[i], PrecomputedMagics.RookShifts[i], RookAttacksFlat, RookOffsets[i]);
                BishopAttacks[i] = CreateTable(i, false, PrecomputedMagics.BishopMagics[i], PrecomputedMagics.BishopShifts[i], BishopAttacksFlat, BishopOffsets[i]);
            }

            // Set the exposed arrays to reference the same data
            rookAttacks = RookAttacks;
            bishopAttacks = BishopAttacks;
        }

        private static ulong[] CreateTable(int square, bool rook, ulong magic, int leftShift, ulong[] flatArray, int offset)
        {
            int numBits = 64 - leftShift;
            int lookupSize = 1 << numBits;

            // Create a view into the flattened array
            var tableSpan = new Span<ulong>(flatArray, offset, lookupSize);
            ulong[] table = new ulong[lookupSize];

            ulong movementMask = MagicHelper.CreateMovementMask(square, rook);
            ulong[] blockerPatterns = MagicHelper.CreateAllBlockerBitboards(movementMask);

            foreach (ulong pattern in blockerPatterns)
            {
                ulong index = (pattern * magic) >> leftShift;
                ulong moves = MagicHelper.LegalMoveBitboardFromBlockers(square, pattern, rook);
                tableSpan[(int)index] = moves;
                table[index] = moves;
            }

            return table;
        }
    }
}