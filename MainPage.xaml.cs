using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using ChessApp.Logic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using static ChessApp.ChessLogic;

namespace ChessApp
{
    public partial class MainPage : ContentPage
    {
        const int BoardSize = 8;
        private readonly Image[,] _cells = new Image[BoardSize, BoardSize];
        private readonly Dictionary<string, ImageSource> _imageCache = new();
        private (int row, int col)? _selected;
        private List<(int row, int col)> _legal_moves;
        bool whiteTurn = true;
        bool BotPlay = false;
        int botDepth = 4;
        public MainPage()
        {
            InitializeComponent();
            BuildBoard();
            ChessLogic.InitializeBoard();
            SetupInitialPosition();
            {
                //ChessLogic.SetPositionFromFEN("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10 \r\n");
                //RefreshBoardFromLogic();
                testPrefit();
                //ChessLogic.PerftWithLogging(5, true, "perfit/prefit_log1.txt"); // Run Perft with logging for depth 5
            }

        }

        void BuildBoard()
        {
            for (int i = 0; i < BoardSize; i++)
            {
                ChessGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
                ChessGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            }

            for (int row = 0; row < BoardSize; row++)
                for (int col = 0; col < BoardSize; col++)
                {
                    var frame = new Frame
                    {
                        Padding = 0,
                        Margin = 0,
                        HasShadow = false,
                        BackgroundColor = ((row + col) % 2 == 0)
                            ? Colors.Beige
                            : Colors.SaddleBrown,
                        BorderColor = Colors.Transparent,
                        CornerRadius = 0
                    };

                    var img = new Image { Aspect = Aspect.AspectFit };
                    _cells[row, col] = img;

                    var tap = new TapGestureRecognizer();
                    int r = row, c = col;
                    // Changed the event handler to be async
                    tap.Tapped += (_, __) => OnCellTapped(r, c);
                    frame.GestureRecognizers.Add(tap);
                    frame.Content = img;

                    ChessGrid.Add(frame, col, row);
                }
        }

        void SetupInitialPosition()
        {
            string[,] start =
            {
                { "black_rook",   "black_knight", "black_bishop", "black_queen", "black_king",   "black_bishop", "black_knight", "black_rook" },
                { "black_pawn",   "black_pawn",   "black_pawn",   "black_pawn",  "black_pawn",   "black_pawn",   "black_pawn",   "black_pawn" },
                { null,           null,           null,           null,          null,           null,           null,           null },
                { null,           null,           null,           null,          null,           null,           null,           null },
                { null,           null,           null,           null,          null,           null,           null,           null },
                { null,           null,           null,           null,          null,           null,           null,           null },
                { "white_pawn",   "white_pawn",   "white_pawn",   "white_pawn",  "white_pawn",   "white_pawn",   "white_pawn",   "white_pawn" },
                { "white_rook",   "white_knight", "white_bishop", "white_queen", "white_king",   "white_bishop", "white_knight", "white_rook" },
            };

            for (int r = 0; r < BoardSize; r++)
                for (int c = 0; c < BoardSize; c++)
                    SetCellImage(r, c, start[r, c]);
        }

        void SetCellImage(int row, int col, string? pieceName)
        {
            var img = _cells[row, col];
            if (pieceName is null)
            {
                img.Source = null;
                return;
            }

            if (!_imageCache.TryGetValue(pieceName, out var src))
            {
                src = ImageSource.FromFile($"{pieceName}.png");
                _imageCache[pieceName] = src;
            }

            img.Source = src;
        }

        // Method signature changed to `async void` to support `await`
        async void OnCellTapped(int row, int col)
        {
            if (_selected == null)
            {
                // First click: Select piece and highlight legal moves
                if (_cells[row, col].Source == null) return;

                _selected = (row, col);
                _legal_moves = ChessLogic.GetValidMoves(row, col, whiteTurn);

                if (_legal_moves.Count == 0)
                {
                    _selected = null;
                    return;
                }

                foreach (var (tr, tc) in _legal_moves)
                    HighlightSquare(tr, tc, true, true);

                HighlightSquare(row, col, true, false);
            }
            else
            {
                // Second click: Perform move

                clearAllHighlights();

                if (!_legal_moves.Contains((row, col)))
                {
                    _selected = null;
                    return;
                }

                var (sr, sc) = _selected.Value;

                // --- Improved Promotion Logic ---
                var (_, piece) = ChessLogic.WhatPieceIsIt(sr, sc);
                bool isPromotion = (piece == ChessLogic.Piece.Pawn) && (row == 0 || row == 7);
                string promotedPieceName = null;
                ChessLogic.Piece promotedPiece = ChessLogic.Piece.None;
                if (isPromotion)
                {
                    // `await` the selection instead of blocking with `.Result`
                    int selectedIndex = await ShowImageSelectionDialogAsync();

                    // Handle cancellation from the dialog
                    if (selectedIndex < 0)
                    {
                        _selected = null;
                        return;
                    }

                    ChessLogic.Piece[] promotionPieces = { ChessLogic.Piece.Queen, ChessLogic.Piece.Rook, ChessLogic.Piece.Bishop, ChessLogic.Piece.Knight };
                    promotedPiece = promotionPieces[selectedIndex];
                }
                movePiece(sr, sc, row, col, promotedPiece);

                //check for checkmate or stalemate
                if (ChessLogic.getAllAvailableMoves(whiteTurn).Count == 0)
                {
                    if (ChessLogic.IsInCheck(whiteTurn))
                    {
                        await DisplayAlert("Game Over", whiteTurn ? "Black wins!" : "White wins!", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Game Over", "Stalemate!", "OK");
                    }
                    return; // Exit the method after displaying the alert
                }
                showEvaluation();
                if (BotPlay)
                {
                    await playBotAsync();
                }

                //check for checkmate or stalemate
                if (ChessLogic.getAllAvailableMoves(whiteTurn).Count == 0)
                {
                    if (ChessLogic.IsInCheck(whiteTurn))
                    {
                        await DisplayAlert("Game Over", whiteTurn ? "Black wins!" : "White wins!", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Game Over", "Stalemate!", "OK");
                    }
                    return; // Exit the method after displaying the alert
                }
                showEvaluation();
            }
        }

        void HighlightSquare(int row, int col, bool on, bool isTarget)
        {
            var frame = (Frame)ChessGrid.Children[row * BoardSize + col];
            if (on)
            {
                if (isTarget)
                {
                    frame.BackgroundColor = Colors.Red; // Highlight for legal moves
                }
                else
                {
                    frame.BorderColor = Colors.Gold; // Highlight for selected piece
                }
            }
            else
            {
                // Reset to default colors
                frame.BackgroundColor = ((row + col) % 2 == 0) ? Colors.Beige : Colors.SaddleBrown;
                frame.BorderColor = Colors.Transparent;
            }
        }

        private async Task<int> ShowImageSelectionDialogAsync()
        {
            var selectionPage = new ImageSelectionPage(whiteTurn);
            await Navigation.PushModalAsync(selectionPage);
            return await selectionPage.GetSelectionAsync();
        }

        private void orderInFenNotation(object sender, EventArgs e)
        {
            string inputText = UserInput.Text.Trim();
            whiteTurn = true;
            _selected = null;
            clearAllHighlights(); // Clear any existing highlights before setting a new position
            if (!string.IsNullOrEmpty(inputText))
            {
                ChessLogic.SetPositionFromFEN(inputText);
                RefreshBoardFromLogic();
            }
        }


        async private void copyPosition(object sender, EventArgs e)
        {
            await Clipboard.SetTextAsync(ChessLogic.GetFENFromCurrentPosition(whiteTurn));
        }
        private void showEvaluation()
        {
            double score = Engine.Evaluate(whiteTurn);
            EvalLabel.Text = $"Eval: {(score >= 0 ? "+" : "")}{score/1000:0.00}";
        }

        void RefreshBoardFromLogic()
        {
            for (int r = 0; r < BoardSize; r++)
            {
                for (int c = 0; c < BoardSize; c++)
                {
                    var (isWhite, piece) = ChessLogic.WhatPieceIsIt(r, c);
                    string? name = null;
                    switch (piece)
                    {
                        case ChessLogic.Piece.Pawn: name = "pawn"; break;
                        case ChessLogic.Piece.Knight: name = "knight"; break;
                        case ChessLogic.Piece.Bishop: name = "bishop"; break;
                        case ChessLogic.Piece.Rook: name = "rook"; break;
                        case ChessLogic.Piece.Queen: name = "queen"; break;
                        case ChessLogic.Piece.King: name = "king"; break;
                    }
                    if (name != null)
                        name = (isWhite ? "white_" : "black_") + name;

                    SetCellImage(r, c, name);
                }
            }
        }


        void clearAllHighlights()
        {
            for (int r = 0; r < BoardSize; r++)
            {
                for (int c = 0; c < BoardSize; c++)
                {
                    HighlightSquare(r, c, false, false); // Clears border
                    HighlightSquare(r, c, false, true);  // Clears background
                }
            }
        }
        void testPrefit()
        {
            string filePath = "perfit/perft_results.txt";
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                long[] results = new long[9];
                long totalNodes = 0;
                TimeSpan totalTime = TimeSpan.Zero;

                for (int depth = 1; depth <= 6; depth++)
                {
                    Console.WriteLine($"Running Perft({depth})...");
                    Stopwatch sw = Stopwatch.StartNew();
                    long nodes = ChessLogic.Perfit(depth, true);
                    sw.Stop();

                    results[depth - 1] = nodes;
                    totalNodes += nodes;
                    totalTime += sw.Elapsed;

                    string result = $"Perft({depth}) = {nodes} in {sw.Elapsed.TotalSeconds:F2} seconds";
                    Console.WriteLine(result);
                    writer.WriteLine(result);
                }

                string totalLine = $"Total Nodes: {totalNodes}, Total Time: {totalTime.TotalSeconds:F2} seconds";
                Console.WriteLine(totalLine);
                writer.WriteLine(totalLine);
            }

            Console.WriteLine("Results saved to perft_results.txt");
        }

        void movePiece(int sr, int sc, int row, int col, ChessLogic.Piece promotedPiece)
        {
            int res = ChessLogic.MovePiece(sr, sc, row, col, promotedPiece);

            Dictionary<ChessLogic.Piece, string> pieceToString = new Dictionary<ChessLogic.Piece, string>();
            pieceToString.Add(ChessLogic.Piece.Queen, "queen");
            pieceToString.Add(ChessLogic.Piece.Rook, "rook");
            pieceToString.Add(ChessLogic.Piece.Bishop, "bishop");
            pieceToString.Add(ChessLogic.Piece.Knight, "knight");

            // --- Corrected UI Update Logic ---
            if (promotedPiece != ChessLogic.Piece.None)
            {
                SetCellImage(sr, sc, null);          // Clear the pawn
                SetCellImage(row, col, $"{(whiteTurn ? "white" : "black")}_{pieceToString[promotedPiece]}"); // Set the new promoted piece
            }
            else
            {
                // Handle standard moves, castling, and en passant
                var src = _cells[sr, sc].Source;
                if (res == 1) // Castle right
                {
                    var rookSource = _cells[row, col + 1].Source;
                    SetCellImage(sr, sc, null);
                    SetCellImage(row, col + 1, null);
                    _cells[row, col].Source = src;
                    _cells[row, col - 1].Source = rookSource;
                }
                else if (res == 2) // Castle left
                {
                    var rookSource = _cells[row, col - 2].Source;
                    SetCellImage(sr, sc, null);
                    SetCellImage(row, col - 2, null);
                    _cells[row, col].Source = src;
                    _cells[row, col + 1].Source = rookSource;
                }
                else if (res == 3) // En passant
                {
                    SetCellImage(sr, sc, null);
                    _cells[row, col].Source = src;
                    SetCellImage(sr, col, null); // Clear the captured pawn
                }
                else // Normal move
                {
                    SetCellImage(sr, sc, null);
                    _cells[row, col].Source = src;
                }
            }

            whiteTurn = !whiteTurn;
            _selected = null;
        }
        async Task playBotAsync()
        {
            // Run engine calculation on background thread
            var botMove = await Task.Run(() => Engine.FindBestMove(botDepth, whiteTurn));

            // Extract move details
            int from = (botMove >> 24) & 0x7F;
            int to = (botMove >> 17) & 0x7F;
            int promo = (botMove >> 12) & 0x7;

            int fr = from / 8, fc = from % 8;
            int tr = to / 8, tc = to % 8;

            // Apply the move on UI thread
            movePiece(fr, fc, tr, tc,
                promo == 1 ? Piece.Queen :
                promo == 2 ? Piece.Rook :
                promo == 3 ? Piece.Bishop :
                promo == 4 ? Piece.Knight :
                Piece.None);
        }
    }
}