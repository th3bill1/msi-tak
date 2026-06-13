using System.Globalization;
using Avalonia.Controls;
using Tak.Core;

namespace Tak.UI;

public partial class MainWindow
{
    private void UpdateMoveBuilderMode()
    {
        if (CurrentState == null)
        {
            suppressUiEvents = true;
            PieceTypeCombo.Items.Clear();
            SlideDirectionCombo.Items.Clear();
            SlideCarryCombo.Items.Clear();
            SlideDistributionCombo.Items.Clear();
            suppressUiEvents = false;
            SubmitMoveBtn.IsEnabled = false;
            ClearSelectionBtn.IsEnabled = false;
            MoveModeCombo.IsEnabled = false;
            PieceTypeCombo.IsEnabled = false;
            SlideDirectionCombo.IsEnabled = false;
            SlideCarryCombo.IsEnabled = false;
            SlideDistributionCombo.IsEnabled = false;
            return;
        }

        var state = CurrentState;
        var isInteractive = IsLiveHumanTurn;
        var legalMoves = isInteractive ? GameRules.GetLegalMoves(state).ToList() : new List<Move>();
        var placementTypes = legalMoves.OfType<PlaceMove>().Select(move => move.PieceType).Distinct().ToList();

        suppressUiEvents = true;
        MoveModeCombo.IsEnabled = isInteractive;

        if (MoveModeCombo.SelectedIndex < 0)
            MoveModeCombo.SelectedIndex = 0;

        if (GetMoveBuilderMode() == MoveBuilderMode.Placement)
        {
            SetComboItems(PieceTypeCombo, placementTypes.Cast<object>(), PieceTypeCombo.SelectedItem);
            PieceTypeCombo.IsEnabled = isInteractive && PieceTypeCombo.Items.Count > 0;
            SlideSourceText.Text = selectedSlideSource.HasValue ? selectedSlideSource.Value.ToString() : "None selected";
            SlideDirectionCombo.Items.Clear();
            SlideCarryCombo.Items.Clear();
            SlideDistributionCombo.Items.Clear();
            SlideDirectionCombo.IsEnabled = false;
            SlideCarryCombo.IsEnabled = false;
            SlideDistributionCombo.IsEnabled = false;
            SubmitMoveBtn.IsEnabled = false;
        }
        else
        {
            PieceTypeCombo.IsEnabled = false;
            SetComboItems(PieceTypeCombo, placementTypes.Cast<object>(), PieceTypeCombo.SelectedItem);
            UpdateSlideSelectors(legalMoves);
        }

        ClearSelectionBtn.IsEnabled = isInteractive && GetMoveBuilderMode() == MoveBuilderMode.Slide;
        suppressUiEvents = false;

        UpdateBoardDisplay();
    }

    private void UpdateSlideSelectors(IReadOnlyCollection<Move> legalMoves)
    {
        if (CurrentState == null)
            return;

        if (!IsLiveHumanTurn)
        {
            SlideSourceText.Text = selectedSlideSource.HasValue ? selectedSlideSource.Value.ToString() : "None selected";
            SlideDirectionCombo.Items.Clear();
            SlideCarryCombo.Items.Clear();
            SlideDistributionCombo.Items.Clear();
            SubmitMoveBtn.IsEnabled = false;
            SlideDirectionCombo.IsEnabled = false;
            SlideCarryCombo.IsEnabled = false;
            SlideDistributionCombo.IsEnabled = false;
            return;
        }

        var legalSlideMoves = legalMoves.OfType<SlideMove>().ToList();
        var legalSources = legalSlideMoves.Select(move => move.From).Distinct().ToList();

        if (selectedSlideSource.HasValue && !legalSources.Contains(selectedSlideSource.Value))
            selectedSlideSource = null;

        SlideSourceText.Text = selectedSlideSource.HasValue ? selectedSlideSource.Value.ToString() : "None selected";

        if (!selectedSlideSource.HasValue)
        {
            SetComboItems(SlideDirectionCombo, Array.Empty<object>());
            SetComboItems(SlideCarryCombo, Array.Empty<object>());
            SetComboItems(SlideDistributionCombo, Array.Empty<object>());
            SlideDirectionCombo.IsEnabled = false;
            SlideCarryCombo.IsEnabled = false;
            SlideDistributionCombo.IsEnabled = false;
            SubmitMoveBtn.IsEnabled = false;
            return;
        }

        var sourceMoves = legalSlideMoves.Where(move => move.From == selectedSlideSource.Value).ToList();
        var directions = sourceMoves.Select(move => move.Direction).Distinct().ToList();
        SetComboItems(SlideDirectionCombo, directions.Cast<object>(), SlideDirectionCombo.SelectedItem);

        if (SlideDirectionCombo.SelectedItem is not Direction selectedDirection)
            selectedDirection = directions.FirstOrDefault();

        var carryCounts = sourceMoves.Where(move => move.Direction == selectedDirection).Select(move => move.PiecesCarried).Distinct().OrderBy(value => value).ToList();
        SetComboItems(SlideCarryCombo, carryCounts.Cast<object>(), SlideCarryCombo.SelectedItem);

        if (SlideCarryCombo.SelectedItem is not int selectedCarryCount)
            selectedCarryCount = carryCounts.FirstOrDefault();

        var distributions = sourceMoves
            .Where(move => move.Direction == selectedDirection && move.PiecesCarried == selectedCarryCount)
            .Select(move => DistributionToText(move.Distribution))
            .Distinct()
            .ToList();

        SetComboItems(SlideDistributionCombo, distributions.Cast<object>(), SlideDistributionCombo.SelectedItem);

        SlideDirectionCombo.IsEnabled = directions.Count > 0;
        SlideCarryCombo.IsEnabled = carryCounts.Count > 0;
        SlideDistributionCombo.IsEnabled = distributions.Count > 0;
        SubmitMoveBtn.IsEnabled = TryBuildSelectedSlideMove(CurrentState) != null;
    }

    private MoveBuilderMode GetMoveBuilderMode() => MoveModeCombo.SelectedIndex == 1 ? MoveBuilderMode.Slide : MoveBuilderMode.Placement;

    private PieceType GetSelectedPlacementPieceType()
    {
        if (PieceTypeCombo.SelectedItem is PieceType pieceType)
            return pieceType;

        return PieceType.Flat;
    }

    private SlideMove? TryBuildSelectedSlideMove(GameState state)
    {
        if (selectedSlideSource == null)
            return null;

        if (SlideDirectionCombo.SelectedItem is not Direction direction)
            return null;

        if (SlideCarryCombo.SelectedItem is not int piecesCarried)
            return null;

        var distribution = ParseDistributionSelection();
        if (distribution == null)
            return null;

        return GameRules.GetLegalMoves(state)
            .OfType<SlideMove>()
            .FirstOrDefault(move =>
                move.From == selectedSlideSource.Value &&
                move.Direction == direction &&
                move.PiecesCarried == piecesCarried &&
                DistributionEquals(move.Distribution, distribution));
    }

    private int[]? ParseDistributionSelection()
    {
        if (SlideDistributionCombo.SelectedItem is not string text || string.IsNullOrWhiteSpace(text))
            return null;

        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<int>(parts.Length);

        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 1)
                return null;

            values.Add(value);
        }

        return values.ToArray();
    }

    private void UpdateStatusPanel()
    {
        if (gameState == null)
            return;

        UpdateControlsState();
        UpdateMoveBuilderSummary();
    }

    private void UpdateMoveBuilderSummary()
    {
        if (gameState == null)
            return;

        if (!IsLiveHumanTurn)
        {
            return;
        }

        if (GetMoveBuilderMode() == MoveBuilderMode.Placement)
        {
            var pieceType = GetSelectedPlacementPieceType();
            var legalCount = GameRules.GetLegalMoves(gameState).OfType<PlaceMove>().Count(move => move.PieceType == pieceType);
            HelpText.Text = $"{pieceType} placements: {legalCount} legal destinations are highlighted on the board.";
            return;
        }

        var selectedMove = TryBuildSelectedSlideMove(gameState);
        if (selectedMove != null)
        {
            HelpText.Text = $"Slide from {selectedMove.From} to {selectedMove.To} carrying {selectedMove.PiecesCarried} pieces with [{DistributionToText(selectedMove.Distribution)}].";
        }
        else if (selectedSlideSource.HasValue)
        {
            HelpText.Text = $"Selected source {selectedSlideSource.Value}. Choose a direction, carry count, and drop pattern.";
        }
        else
        {
            HelpText.Text = "Choose a source stack to begin building a slide move.";
        }
    }

    private void SetComboItems(ComboBox comboBox, IEnumerable<object> items, object? selectedItem = null)
    {
        comboBox.Items.Clear();

        foreach (var item in items)
        {
            comboBox.Items.Add(item);
        }

        if (comboBox.Items.Count == 0)
        {
            comboBox.SelectedIndex = -1;
            comboBox.IsEnabled = false;
            return;
        }

        comboBox.IsEnabled = true;
        if (selectedItem != null && comboBox.Items.Contains(selectedItem))
        {
            comboBox.SelectedItem = selectedItem;
        }
        else
        {
            comboBox.SelectedIndex = 0;
        }
    }
}
