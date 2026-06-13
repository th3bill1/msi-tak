using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tak.Core;

namespace Tak.UI;

/// <summary>Formatting and visual helper methods for the main Tak board window.</summary>
public partial class MainWindow
{
    private static string FormatHistoryEntry(GameState stateBeforeMove, Move move, int moveIndex)
    {
        var mover = stateBeforeMove.CurrentPlayer;
        return $"{moveIndex + 1}. {mover}: {FormatMove(move)}";
    }

    private static string FormatMove(Move move) => move switch
    {
        PlaceMove placeMove => $"Place {placeMove.PieceType} at {placeMove.Position}",
        SlideMove slideMove => $"Slide {slideMove.From} -> {slideMove.To} carrying {slideMove.PiecesCarried} [{DistributionToText(slideMove.Distribution)}]",
        _ => "Unknown move"
    };

    private static string DistributionToText(int[] distribution) => string.Join(",", distribution);

    private static bool DistributionEquals(int[] left, int[] right)
    {
        if (left.Length != right.Length)
            return false;

        for (int index = 0; index < left.Length; index++)
        {
            if (left[index] != right[index])
                return false;
        }

        return true;
    }

    private static bool AreEquivalentMoves(Move left, Move right) => (left, right) switch
    {
        (PlaceMove leftPlace, PlaceMove rightPlace) => leftPlace.Position == rightPlace.Position && leftPlace.PieceType == rightPlace.PieceType,
        (SlideMove leftSlide, SlideMove rightSlide) => leftSlide.From == rightSlide.From && leftSlide.To == rightSlide.To && leftSlide.Direction == rightSlide.Direction && DistributionEquals(leftSlide.Distribution, rightSlide.Distribution),
        _ => false
    };

    private static bool IsLastMoveSquare(Move? move, Position position) => move switch
    {
        PlaceMove placeMove => placeMove.Position == position,
        SlideMove slideMove => IsSlidePathSquare(slideMove, position),
        _ => false
    };

    private static bool IsSlidePathSquare(SlideMove move, Position position)
    {
        if (move.From == position)
            return true;

        var current = move.From;
        for (int step = 0; step < move.Distribution.Length; step++)
        {
            current = current.Offset(move.Direction);
            if (current == position)
                return true;
        }

        return false;
    }

    private static string BuildLegalMoveSummary(GameState state)
    {
        var legalMoves = GameRules.GetLegalMoves(state).ToList();
        var placementCount = legalMoves.OfType<PlaceMove>().Count();
        var slideCount = legalMoves.OfType<SlideMove>().Count();
        return $"{placementCount} placement moves, {slideCount} slide moves";
    }

    private static Control BuildStackContent(Stack stack, Position position, bool legal, bool selected)
    {
        if (stack.IsEmpty)
        {
            return new TextBlock
            {
                Text = string.Empty,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        // Visualize pieces as fixed-size rectangles 10x2 px stacked vertically, placed to the left of the text
        var pieces = stack.GetPieces().ToList();

        // Container for stripes: fixed width to the left of the text
        var stripesPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 10,
            Margin = new Thickness(0)
        };

        // Render from top (top piece first) stacked vertically
        var topFirst = pieces.TakeLast(pieces.Count).ToList();
        topFirst.Reverse();

        foreach (var piece in topFirst)
        {
            var rect = new Border
            {
                Width = 10,
                Height = 2,
                Background = piece.Owner == Player.White ? Brushes.White : Brushes.Black,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.5),
                Margin = new Thickness(0, 1, 0, 1)
            };

            stripesPanel.Children.Add(rect);
        }

        // Textual info placed to the right of the stripes (no background)
        var overlay = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0)
        };

        overlay.Children.Add(new TextBlock
        {
            Text = stack.TopPiece.Owner == Player.White ? "W" : "B",
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = BuildStackForeground(stack, position, legal, selected)
        });

        overlay.Children.Add(new TextBlock
        {
            Text = stack.TopPiece.Type switch
            {
                PieceType.Flat => "Flat",
                PieceType.Wall => "Wall",
                PieceType.Capstone => "Cap",
                _ => "?"
            },
            FontSize = 10,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = BuildStackForeground(stack, position, legal, selected)
        });

        overlay.Children.Add(new TextBlock
        {
            Text = $"H{stack.Height}",
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = BuildStackForeground(stack, position, legal, selected)
        });

        var parent = new Grid();
        parent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        parent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(stripesPanel, 0);
        Grid.SetColumn(overlay, 1);
        parent.Children.Add(stripesPanel);
        parent.Children.Add(overlay);
        return parent;
    }

    private static IBrush BuildSquareBrush(Stack stack, Position position, bool legal, bool selected, bool lastMove)
    {
        // Chessboard base colors
        var light = Color.FromRgb(240, 217, 181);
        var dark = Color.FromRgb(181, 136, 99);
        bool darkSquare = (position.Row + position.Col) % 2 == 1;

        if (selected)
            return new SolidColorBrush(Color.FromRgb(253, 230, 138));

        // base tile color
        var baseColor = darkSquare ? dark : light;

        // if legal and empty, slightly tint the base color greenish
        if (stack.IsEmpty && legal)
        {
            // blend base with a pale green
            var green = Color.FromRgb(220, 252, 231);
            return new SolidColorBrush(Color.FromRgb(
                (byte)((baseColor.R + green.R) / 2),
                (byte)((baseColor.G + green.G) / 2),
                (byte)((baseColor.B + green.B) / 2)));
        }

        return new SolidColorBrush(baseColor);
    }

    private static IBrush BuildSquareBorderBrush(Stack stack, Position position, bool legal, bool selected, bool lastMove)
    {
        if (selected)
            return new SolidColorBrush(Color.FromRgb(202, 138, 4));

        if (lastMove)
            return new SolidColorBrush(Color.FromRgb(234, 179, 8));

        if (legal)
            return new SolidColorBrush(Color.FromRgb(34, 197, 94));

        return stack.IsEmpty ? new SolidColorBrush(Color.FromRgb(203, 213, 225)) : new SolidColorBrush(Color.FromRgb(71, 85, 105));
    }

    private static IBrush BuildStackForeground(Stack stack, Position position, bool legal, bool selected)
    {
        // If the top piece is Black, text should be black; otherwise text should be white.
        if (!stack.IsEmpty && stack.TopPiece.Owner == Player.Black)
            return Brushes.Black;

        return Brushes.White;
    }

    private static string BuildSquareToolTip(Position position, Stack stack, bool legal)
    {
        if (stack.IsEmpty)
            return legal ? $"{position}: legal placement target" : $"{position}: empty square";

        var pieces = string.Join(", ", stack.GetPieces().Select(piece => $"{(piece.Owner == Player.White ? "W" : "B")}{PieceTypeAbbrev(piece.Type)}"));
        return $"{position}: {stack.Owner} controls this stack\nHeight: {stack.Height}\nPieces: {pieces}";
    }

    private static string PieceTypeAbbrev(PieceType pieceType) => pieceType switch
    {
        PieceType.Flat => "F",
        PieceType.Wall => "S",
        PieceType.Capstone => "C",
        _ => "?"
    };

    private static string FormatStackCoverage(GameState state, Player player)
    {
        var controlledStacks = 0;
        var coveredPieces = 0;

        foreach (var (_, stack) in state.Board.GetNonEmptySquares())
        {
            var pieces = stack.GetPieces();
            if (stack.Owner == player)
                controlledStacks++;

            coveredPieces += pieces.Take(Math.Max(0, pieces.Count - 1)).Count(piece => piece.Owner == player);
        }

        return $"Controlled stacks: {controlledStacks} | Covered pieces: {coveredPieces}";
    }
}
