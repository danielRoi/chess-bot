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

        // Game state
        bool whiteTurn = true;
        bool whiteIsBot = false;
        bool blackIsBot = false;
        int botDepth = 4;
        bool boardFlipped = false;
        bool autoSize = true;
        double currentBoardSize = 400;

        // Move tracking
        int moveCount = 1;
        DateTime gameStartTime;

        public MainPage()
        {
            InitializeComponent();
            InitializeGame();
        }

        private void InitializeGame()
        {
            // Initialize UI controls
            WhitePlayerPicker.SelectedIndex = 0; // Human
            BlackPlayerPicker.SelectedIndex = 0; // Human
            BotDepthStepper.Value = botDepth;
            BotDepthLabel.Text = botDepth.ToString();
            PerftDepthLabel.Text = "6";
            BoardSizeSlider.Value = currentBoardSize;
            BoardSizeLabel.Text = currentBoardSize.ToString("0");
            AutoSizeSwitch.IsToggled = autoSize;

            // Initialize game
            gameStartTime = DateTime.Now;
            BuildBoard();
            ChessLogic.InitializeBoard();
            SetupInitialPosition();
            UpdateGameStatus();

            // Start timer for game time display
            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                UpdateTimeDisplay();
                return true; // Continue timer
            });
        }

        void BuildBoard()
        {
            // Clear existing definitions
            ChessGrid.RowDefinitions.Clear();
            ChessGrid.ColumnDefinitions.Clear();
            ChessGrid.Children.Clear();

            // Set board size
            if (autoSize)
            {
                ChessGrid.HeightRequest = Math.Min(Width * 0.6, Height * 0.8);
                ChessGrid.WidthRequest = ChessGrid.HeightRequest;
            }
            else
            {
                ChessGrid.HeightRequest = currentBoardSize;
                ChessGrid.WidthRequest = currentBoardSize;
            }

            // Create grid definitions
            for (int i = 0; i < BoardSize; i++)
            {
                ChessGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
                ChessGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            }

            // Create cells
            for (int row = 0; row < BoardSize; row++)
            {
                for (int col = 0; col < BoardSize; col++)
                {
                    int displayRow = boardFlipped ? (7 - row) : row;
                    int displayCol = boardFlipped ? (7 - col) : col;

                    var frame = new Frame
                    {
                        Padding = 0,
                        Margin = 0,
                        HasShadow = false,
                        BackgroundColor = ((displayRow + displayCol) % 2 == 0)
                            ? Colors.Beige
                            : Colors.SaddleBrown,
                        BorderColor = Colors.Transparent,
                        CornerRadius = 0
                    };

                    var img = new Image { Aspect = Aspect.AspectFit };
                    _cells[row, col] = img;

                    var tap = new TapGestureRecognizer();
                    int r = row, c = col;
                    tap.Tapped += (_, __) => OnCellTapped(r, c);
                    frame.GestureRecognizers.Add(tap);
                    frame.Content = img;

                    ChessGrid.Add(frame, displayCol, displayRow);
                }
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

        async void OnCellTapped(int row, int col)
        {
            // Don't allow moves if it's bot's turn
            if ((whiteTurn && whiteIsBot) || (!whiteTurn && blackIsBot))
                return;

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

                // Handle promotion
                var (_, piece) = ChessLogic.WhatPieceIsIt(sr, sc);
                bool isPromotion = (piece == ChessLogic.Piece.Pawn) && (row == 0 || row == 7);
                ChessLogic.Piece promotedPiece = ChessLogic.Piece.None;

                if (isPromotion)
                {
                    int selectedIndex = await ShowImageSelectionDialogAsync();
                    if (selectedIndex < 0)
                    {
                        _selected = null;
                        return;
                    }
                    ChessLogic.Piece[] promotionPieces = { ChessLogic.Piece.Queen, ChessLogic.Piece.Rook, ChessLogic.Piece.Bishop, ChessLogic.Piece.Knight };
                    promotedPiece = promotionPieces[selectedIndex];
                }

                movePiece(sr, sc, row, col, promotedPiece);
                await CheckGameEnd();
            }
        }

        private async Task CheckGameEnd()
        {
            if (ChessLogic.isGameOver(whiteTurn))
            {
                if (ChessLogic.IsInCheck(whiteTurn))
                {
                    await DisplayAlert("Game Over", whiteTurn ? "Black wins!" : "White wins!", "OK");
                }
                else
                {
                    await DisplayAlert("Game Over", "Stalemate!", "OK");
                }
                return;
            }

            showEvaluation();

            // Check if bot should play
            if ((whiteTurn && whiteIsBot) || (!whiteTurn && blackIsBot))
            {
                await playBotAsync();
            }
        }

        void HighlightSquare(int row, int col, bool on, bool isTarget)
        {
            int displayRow = boardFlipped ? (7 - row) : row;
            int displayCol = boardFlipped ? (7 - col) : col;

            var frame = (Frame)ChessGrid.Children[displayRow * BoardSize + displayCol];
            if (on)
            {
                if (isTarget)
                {
                    frame.BackgroundColor = Colors.Red;
                }
                else
                {
                    frame.BorderColor = Colors.Gold;
                }
            }
            else
            {
                frame.BackgroundColor = ((displayRow + displayCol) % 2 == 0) ? Colors.Beige : Colors.SaddleBrown;
                frame.BorderColor = Colors.Transparent;
            }
        }

        void clearAllHighlights()
        {
            for (int r = 0; r < BoardSize; r++)
            {
                for (int c = 0; c < BoardSize; c++)
                {
                    HighlightSquare(r, c, false, false);
                    HighlightSquare(r, c, false, true);
                }
            }
        }

        void movePiece(int sr, int sc, int row, int col, ChessLogic.Piece promotedPiece)
        {
            int res = ChessLogic.MovePiece(sr, sc, row, col, promotedPiece);

            Dictionary<ChessLogic.Piece, string> pieceToString = new Dictionary<ChessLogic.Piece, string>
            {
                { ChessLogic.Piece.Queen, "queen" },
                { ChessLogic.Piece.Rook, "rook" },
                { ChessLogic.Piece.Bishop, "bishop" },
                { ChessLogic.Piece.Knight, "knight" }
            };

            // Update UI
            if (promotedPiece != ChessLogic.Piece.None)
            {
                SetCellImage(sr, sc, null);
                SetCellImage(row, col, $"{(whiteTurn ? "white" : "black")}_{pieceToString[promotedPiece]}");
            }
            else
            {
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
                    SetCellImage(sr, col, null);
                }
                else // Normal move
                {
                    SetCellImage(sr, sc, null);
                    _cells[row, col].Source = src;
                }
            }

            whiteTurn = !whiteTurn;
            _selected = null;

            // Update move count
            if (whiteTurn) moveCount++;

            UpdateGameStatus();
        }

        async Task playBotAsync()
        {
            StatusLabel.Text = "Bot is thinking...";

            var botMove = await Task.Run(() => Engine.FindBestMove(botDepth, whiteTurn));

            int from = (botMove >> 24) & 0x7F;
            int to = (botMove >> 17) & 0x7F;
            int promo = (botMove >> 12) & 0x7;

            int fr = from / 8, fc = from % 8;
            int tr = to / 8, tc = to % 8;

            movePiece(fr, fc, tr, tc,
                promo == 1 ? Piece.Queen :
                promo == 2 ? Piece.Rook :
                promo == 3 ? Piece.Bishop :
                promo == 4 ? Piece.Knight :
                Piece.None);

            StatusLabel.Text = "Ready to play";
            CheckGameEnd();
        }

        private void UpdateGameStatus()
        {
            TurnLabel.Text = whiteTurn ? "White to move" : "Black to move";
            MoveCountLabel.Text = $"Move: {moveCount}";
            showEvaluation();
        }

        private void UpdateTimeDisplay()
        {
            var elapsed = DateTime.Now - gameStartTime;
            TimeLabel.Text = $"Time: {elapsed.ToString(@"mm\:ss")}";
        }

        private void showEvaluation()
        {
            double score = Engine.Evaluate(whiteTurn);
            EvalLabel.Text = $"Eval: {(score >= 0 ? "+" : "")}{score / 1000:0.00}";
        }

        // Event Handlers
        private void OnPageSizeChanged(object sender, EventArgs e)
        {
            if (autoSize)
            {
                BuildBoard();
            }
        }

        private void OnPlayerTypeChanged(object sender, EventArgs e)
        {
            whiteIsBot = WhitePlayerPicker.SelectedIndex == 1;
            blackIsBot = BlackPlayerPicker.SelectedIndex == 1;

            // If it's currently bot's turn, make the move
            if ((whiteTurn && whiteIsBot) || (!whiteTurn && blackIsBot))
            {
                Task.Run(async () => await playBotAsync());
            }
        }

        private void OnBotDepthChanged(object sender, ValueChangedEventArgs e)
        {
            botDepth = (int)e.NewValue;
            BotDepthLabel.Text = botDepth.ToString();
        }

        private void OnBoardSizeChanged(object sender, ValueChangedEventArgs e)
        {
            currentBoardSize = e.NewValue;
            BoardSizeLabel.Text = currentBoardSize.ToString("0");
            if (!autoSize)
            {
                BuildBoard();
            }
        }

        private void OnAutoSizeToggled(object sender, ToggledEventArgs e)
        {
            autoSize = e.Value;
            BoardSizeSlider.IsEnabled = !autoSize;
            BuildBoard();
        }

        private void OnPerftDepthChanged(object sender, ValueChangedEventArgs e)
        {
            PerftDepthLabel.Text = ((int)e.NewValue).ToString();
        }

        private async void StartPerftTest(object sender, EventArgs e)
        {
            int depth = (int)PerftDepthStepper.Value;
            PerftStatusLabel.Text = "Running...";

            await Task.Run(() => testPrefit(depth));

            PerftStatusLabel.Text = "Complete!";
        }

        private async void StartNewGame(object sender, EventArgs e)
        {
            ChessLogic.InitializeBoard();
            SetupInitialPosition();
            whiteTurn = true;
            moveCount = 1;
            gameStartTime = DateTime.Now;
            _selected = null;
            clearAllHighlights();
            UpdateGameStatus();
            StatusLabel.Text = "New game started";
            if ((whiteTurn && whiteIsBot) || (!whiteTurn && blackIsBot))
            {
                await playBotAsync();
            }
        }

        private void FlipBoard(object sender, EventArgs e)
        {
            boardFlipped = !boardFlipped;
            BuildBoard();
            RefreshBoardFromLogic();
        }

        private void UndoMove(object sender, EventArgs e)
        {
            // This would require implementing move history in ChessLogic
            StatusLabel.Text = "Undo not yet implemented";
        }

        private async void ResetToStartPosition(object sender, EventArgs e)
        {
            UserInput.Text = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            orderInFenNotation(sender, e);
        }

        // Existing methods (kept from original code)
        private async Task<int> ShowImageSelectionDialogAsync()
        {
            var selectionPage = new ImageSelectionPage(whiteTurn);
            await Navigation.PushModalAsync(selectionPage);
            return await selectionPage.GetSelectionAsync();
        }

        private async void orderInFenNotation(object sender, EventArgs e)
        {
            string inputText = UserInput.Text.Trim();
            if (string.IsNullOrEmpty(inputText)) return;

            string[] fenParts = inputText.Split(" ");
            if (fenParts.Length > 1)
            {
                whiteTurn = fenParts[1] == "w";
            }

            _selected = null;
            clearAllHighlights();

            ChessLogic.SetPositionFromFEN(inputText);
            RefreshBoardFromLogic();
            UpdateGameStatus();

            // Check if bot should play after position change
            //if ((whiteTurn && whiteIsBot) || (!whiteTurn && blackIsBot))
            //{
            //    await playBotAsync();
            //}
        }

        async private void copyPosition(object sender, EventArgs e)
        {
            await Clipboard.SetTextAsync(ChessLogic.GetFENFromCurrentPosition(whiteTurn));
            StatusLabel.Text = "Position copied to clipboard";
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

        void testPrefit(int maxDepth = 6)
        {
            string filePath = "perfit/perft_results.txt";
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                long[] results = new long[9];
                long totalNodes = 0;
                TimeSpan totalTime = TimeSpan.Zero;

                for (int depth = 1; depth <= maxDepth; depth++)
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
    }
}