using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tak.Core;

namespace Tak.UI;

public partial class MainWindow
{
    private const int FixedBoardPixels = 500;

    private void CreateBoardUI(int size)
    {
        BoardGrid.Children.Clear();
        BoardGrid.Columns = size;
        BoardGrid.Rows = size;

        // Use a constant board pixel size; cells scale to fit the grid.
        BoardGrid.Width = FixedBoardPixels;
        BoardGrid.Height = FixedBoardPixels;
        boardButtons = new Button[size, size];

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                var button = new Button
                {
                    Content = string.Empty,
                    Background = Brushes.White,
                    BorderBrush = Brushes.SlateGray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(2),
                    Padding = new Thickness(4),
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = new Position(row, col)
                };

                button.Click += BoardButton_Click;
                BoardGrid.Children.Add(button);
                boardButtons[row, col] = button;
            }
        }
    }

    private void UpdateBoardDisplay()
    {
        if (CurrentState == null || boardButtons == null)
            return;

        var state = CurrentState;
        var legalMoves = IsLiveHumanTurn ? GameRules.GetLegalMoves(state).ToList() : new List<Move>();
        var lastMove = state.MoveHistory.LastOrDefault();
        var mode = GetMoveBuilderMode();
        var legalPlacementTargets = mode == MoveBuilderMode.Placement
            ? legalMoves.OfType<PlaceMove>().Where(move => move.PieceType == GetSelectedPlacementPieceType()).Select(move => move.Position).ToHashSet()
            : new HashSet<Position>();
        var legalSlideSources = mode == MoveBuilderMode.Slide
            ? legalMoves.OfType<SlideMove>().Select(move => move.From).Distinct().ToHashSet()
            : new HashSet<Position>();

        for (int row = 0; row < state.Config.BoardSize; row++)
        {
            for (int col = 0; col < state.Config.BoardSize; col++)
            {
                var position = new Position(row, col);
                var stack = state.Board.GetStack(position);
                var button = boardButtons[row, col];
                var isSelectedSource = selectedSlideSource.HasValue && selectedSlideSource.Value == position;
                var isLastMoveSquare = IsLastMoveSquare(lastMove, position);
                var isLegalSquare = mode == MoveBuilderMode.Placement
                    ? legalPlacementTargets.Contains(position)
                    : legalSlideSources.Contains(position);

                button.IsEnabled = IsLiveHumanTurn && isLegalSquare;
                button.Content = BuildStackContent(stack);
                button.Background = BuildSquareBrush(stack, isLegalSquare, isSelectedSource, isLastMoveSquare);
                button.BorderBrush = BuildSquareBorderBrush(stack, isLegalSquare, isSelectedSource, isLastMoveSquare);
                button.BorderThickness = new Thickness(isSelectedSource || isLastMoveSquare ? 3 : isLegalSquare ? 2 : 1);
                button.Foreground = BuildStackForeground(stack, isLegalSquare, isSelectedSource);
                button.ToolTip = BuildSquareToolTip(position, stack, isLegalSquare);
            }
        }

        LegalMovesText.Text = BuildLegalMoveSummary(state);
    }
}
