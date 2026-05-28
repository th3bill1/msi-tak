using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tak.Core;

namespace Tak.UI;

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
        SlideMove slideMove => slideMove.From == position || slideMove.To == position,
        _ => false
    };

    private static string BuildLegalMoveSummary(GameState state)
    {
        var legalMoves = GameRules.GetLegalMoves(state).ToList();
        var placementCount = legalMoves.OfType<PlaceMove>().Count();
        var slideCount = legalMoves.OfType<SlideMove>().Count();
        return $"{placementCount} placement moves, {slideCount} slide moves";
    }

    private static UIElement BuildStackContent(Stack stack)
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

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        panel.Children.Add(new TextBlock
        {
            Text = stack.Owner == Player.White ? "W" : "B",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = stack.Owner == Player.White ? Brushes.SlateGray : Brushes.White
        });

        panel.Children.Add(new TextBlock
        {
            Text = stack.TopPiece.Type switch
            {
                PieceType.Flat => "Flat",
                PieceType.Wall => "Wall",
                PieceType.Capstone => "Cap",
                _ => "?"
            },
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = stack.Owner == Player.White ? Brushes.SlateGray : Brushes.White
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"H{stack.Height}",
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = stack.Owner == Player.White ? Brushes.SlateGray : Brushes.White
        });

        return panel;
    }

    private static Brush BuildSquareBrush(Stack stack, bool legal, bool selected, bool lastMove)
    {
        if (selected)
            return new SolidColorBrush(Color.FromRgb(253, 230, 138));

        if (lastMove)
            return new SolidColorBrush(Color.FromRgb(254, 249, 195));

        if (stack.IsEmpty)
            return legal ? new SolidColorBrush(Color.FromRgb(220, 252, 231)) : new SolidColorBrush(Color.FromRgb(241, 245, 249));

        if (stack.Owner == Player.White)
            return new SolidColorBrush(Color.FromRgb(248, 250, 252));

        return new SolidColorBrush(Color.FromRgb(30, 41, 59));
    }

    private static Brush BuildSquareBorderBrush(Stack stack, bool legal, bool selected, bool lastMove)
    {
        if (selected)
            return new SolidColorBrush(Color.FromRgb(202, 138, 4));

        if (lastMove)
            return new SolidColorBrush(Color.FromRgb(234, 179, 8));

        if (legal)
            return new SolidColorBrush(Color.FromRgb(34, 197, 94));

        return stack.IsEmpty ? new SolidColorBrush(Color.FromRgb(203, 213, 225)) : new SolidColorBrush(Color.FromRgb(71, 85, 105));
    }

    private static Brush BuildStackForeground(Stack stack, bool legal, bool selected)
    {
        if (selected)
            return Brushes.Black;

        if (stack.IsEmpty)
            return legal ? new SolidColorBrush(Color.FromRgb(22, 101, 52)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));

        return stack.Owner == Player.White ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : Brushes.White;
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
