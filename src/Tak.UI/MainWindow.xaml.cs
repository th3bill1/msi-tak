using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tak.Core;
using Tak.AI;
using Tak.Experiments;

namespace Tak.UI;

public partial class MainWindow : Window
{
    private GameState? gameState;
    private Player humanPlayer = Player.White;
    private IAgent? aiAgent;
    private Button[,]? boardButtons;
    private Stack<GameState> stateHistory = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private void NewGameBtn_Click(object sender, RoutedEventArgs e)
    {
        int boardSize = BoardSizeCombo.SelectedIndex switch
        {
            0 => 4,
            1 => 5,
            2 => 6,
            _ => 5
        };

        humanPlayer = PlayerColorCombo.SelectedIndex == 0 ? Player.White : Player.Black;

        string opponentName = OpponentCombo.SelectedIndex switch
        {
            0 => "random",
            1 => "heuristic",
            2 => "uct",
            3 => "rave",
            4 => "pw",
            _ => "heuristic"
        };

        aiAgent = AgentFactory.CreateAgent(opponentName, seed: 42);

        gameState = Utils.CreateNewGame(boardSize);
        stateHistory.Clear();
        stateHistory.Push(gameState);

        CreateBoardUI(boardSize);
        UpdateBoardDisplay();
        UpdateStatus();
    }

    private void CreateBoardUI(int size)
    {
        BoardGrid.Children.Clear();
        BoardGrid.Columns = size;
        BoardGrid.Rows = size;
        BoardGrid.Width = BoardGrid.Height = Math.Min(500, 400 + size * 20);

        boardButtons = new Button[size, size];

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                var btn = new Button
                {
                    Content = "",
                    Background = Brushes.Beige,
                    Margin = new Thickness(1),
                    FontSize = 10,
                    Tag = new Position(r, c)
                };
                btn.Click += BoardButton_Click;
                BoardGrid.Children.Add(btn);
                boardButtons[r, c] = btn;
            }
        }
    }

    private void UpdateBoardDisplay()
    {
        if (gameState == null || boardButtons == null)
            return;

        int size = gameState.Config.BoardSize;
        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                var pos = new Position(r, c);
                var stack = gameState.Board.GetStack(pos);
                boardButtons[r, c].Content = FormatStackDisplay(stack);
                boardButtons[r, c].Background = Brushes.Beige;
            }
        }
    }

    private string FormatStackDisplay(Stack stack)
    {
        if (stack.IsEmpty) return "";
        var topPiece = stack.TopPiece;
        string type = topPiece.Type switch
        {
            PieceType.Flat => "F",
            PieceType.Wall => "W",
            PieceType.Capstone => "C",
            _ => "?"
        };
        string owner = topPiece.Owner == Player.White ? "W" : "B";
        return $"{owner}{type}" + (stack.Height > 1 ? $"\n×{stack.Height}" : "");
    }

    private void UpdateStatus()
    {
        if (gameState == null)
        {
            StatusText.Text = "No game in progress.";
            return;
        }

        if (gameState.Result != null)
        {
            StatusText.Text = $"Game Over! {gameState.Result}";
            CurrentPlayerText.Text = "-";
            return;
        }

        CurrentPlayerText.Text = gameState.CurrentPlayer.ToString();
        StatusText.Text = gameState.CurrentPlayer == humanPlayer ? "Your turn!" : "AI is thinking...";

        if (gameState.CurrentPlayer != humanPlayer)
        {
            // AI move
            if (aiAgent != null)
            {
                var move = aiAgent.ChooseMove(gameState, iterationLimit: 500);
                gameState = gameState.MakeMove(move);
                stateHistory.Push(gameState);
                UpdateBoardDisplay();
                UpdateStatus();
                UpdateMoveHistory();
            }
        }
    }

    private void UpdateMoveHistory()
    {
        if (gameState == null) return;

        MoveHistoryList.Items.Clear();
        foreach (var move in gameState.MoveHistory)
        {
            MoveHistoryList.Items.Add(Utils.FormatMove(move));
        }
    }

    private void BoardButton_Click(object sender, RoutedEventArgs e)
    {
        if (gameState == null || gameState.Result != null)
            return;

        if (gameState.CurrentPlayer != humanPlayer)
            return;

        if (sender is not Button btn || btn.Tag is not Position pos)
            return;

        try
        {
            string pieceType = PieceTypeCombo.SelectedIndex switch
            {
                0 => "Flat",
                1 => "Wall",
                2 => "Capstone",
                3 => "Move",
                _ => "Flat"
            };

            if (pieceType == "Move")
            {
                // For now, simple move logic - can be enhanced
                StatusText.Text = "Move functionality requires UI enhancement for direction/distribution selection.";
                return;
            }

            if (!Enum.TryParse<PieceType>(pieceType, out var pType))
                return;

            var move = new PlaceMove(pos, pType);
            gameState = gameState.MakeMove(move);
            stateHistory.Push(gameState);
            UpdateBoardDisplay();
            UpdateStatus();
            UpdateMoveHistory();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Invalid move: {ex.Message}";
        }
    }

    private void UndoBtn_Click(object sender, RoutedEventArgs e)
    {
        if (stateHistory.Count > 1)
        {
            stateHistory.Pop(); // Remove current
            gameState = stateHistory.Peek();
            UpdateBoardDisplay();
            UpdateStatus();
            UpdateMoveHistory();
        }
    }
}
