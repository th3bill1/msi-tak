using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Tak.AI;
using Tak.Core;

namespace Tak.UI;

public partial class MainWindow : Window
{
    private enum MoveBuilderMode
    {
        Placement,
        Slide
    }

    private enum PlayerController
    {
        Human,
        Random,
        Heuristic,
        Uct,
        Rave,
        ProgressiveWidening
    }

    private const int DefaultAiIterationLimit = 500;
    private static readonly TimeSpan DefaultAiTimeLimit = TimeSpan.FromMilliseconds(750);

    private GameState? gameState;
    private Player humanPlayer = Player.White;
    private IAgent? aiAgent;
    private IAgent? whiteAgent;
    private IAgent? blackAgent;
    private Button[,]? boardButtons;
    private readonly List<GameState> stateTimeline = new();
    private int stateIndex = -1;
    private bool suppressUiEvents;
    private bool suppressHistorySelectionEvents;
    private bool aiTurnInProgress;
    private bool aiAutoPlayEnabled;
    private bool aiAutoPlayPaused;
    private bool aiAutoLoopRunning;
    private bool aiStepRequested;
    private bool resultOverlayDismissed;
    private Position? selectedSlideSource;
    private CancellationTokenSource? aiLoopCancellation;

    /// <summary>Initializes the main window and prepares the UI state.</summary>
    public MainWindow()
    {
        suppressUiEvents = true;
        InitializeComponent();
        suppressUiEvents = false;
        UpdateMoveBuilderMode();
        UpdateStaticUi();
    }

    private GameState? CurrentState => stateIndex >= 0 && stateIndex < stateTimeline.Count ? stateTimeline[stateIndex] : null;

    private bool IsLiveState => CurrentState != null && stateIndex == stateTimeline.Count - 1;

    private bool IsLiveHumanTurn => CurrentState != null && IsLiveState && CurrentState.Result == null && IsHumanController(CurrentState.CurrentPlayer);

    private void NewGameBtn_Click(object? sender, RoutedEventArgs e)
    {
        StartNewGame();
        _ = MaybePlayAiTurnAsync();
    }

    private void StartAiVsAiBtn_Click(object? sender, RoutedEventArgs e)
    {
        StartAiVsAiGame();
        _ = MaybePlayAiTurnAsync();
    }

    private void PauseAiBtn_Click(object? sender, RoutedEventArgs e)
    {
        aiAutoPlayPaused = true;
        HeaderStatusText.Text = "AI playback paused.";
        UpdateControlsState();
    }

    private void ResumeAiBtn_Click(object? sender, RoutedEventArgs e)
    {
        aiAutoPlayEnabled = true;
        aiAutoPlayPaused = false;
        HeaderStatusText.Text = "AI playback resumed.";
        UpdateControlsState();
        _ = MaybePlayAiTurnAsync();
    }

    private void StepAiBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (CurrentState == null || CurrentState.Result != null || IsLiveHumanTurn)
            return;

        aiAutoPlayEnabled = false;
        aiAutoPlayPaused = true;
        aiStepRequested = true;
        _ = ExecuteSingleAiTurnAsync(System.Threading.CancellationToken.None);
    }

    private void ReplayBtn_Click(object? sender, RoutedEventArgs e)
    {
        StartNewGame();
        _ = MaybePlayAiTurnAsync();
    }

    private void RestartBtn_Click(object? sender, RoutedEventArgs e)
    {
        StartNewGame();
        _ = MaybePlayAiTurnAsync();
    }

    private void ResumeBtn_Click(object? sender, RoutedEventArgs e)
    {
        resultOverlayDismissed = true;
        JumpToLiveState();
        _ = MaybePlayAiTurnAsync();
    }

    private void LiveBtn_Click(object? sender, RoutedEventArgs e)
    {
        JumpToLiveState();
        _ = MaybePlayAiTurnAsync();
    }

    private void UndoBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (aiTurnInProgress || (aiAutoPlayEnabled && !aiAutoPlayPaused))
            return;

        if (stateIndex <= 0)
            return;

        stateIndex--;
        selectedSlideSource = null;
        RefreshUi();
    }

    private void RedoBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (aiTurnInProgress || (aiAutoPlayEnabled && !aiAutoPlayPaused))
            return;

        if (stateIndex < stateTimeline.Count - 1)
        {
            stateIndex++;
            selectedSlideSource = null;
            RefreshUi();
        }
    }

    private void MoveHistoryList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (suppressUiEvents || suppressHistorySelectionEvents)
            return;

        if (aiTurnInProgress || (aiAutoPlayEnabled && !aiAutoPlayPaused))
        {
            RefreshUi();
            return;
        }

        if (MoveHistoryList.SelectedIndex < 0)
            return;

        var targetStateIndex = MoveHistoryList.SelectedIndex + 1;
        if (targetStateIndex < 0 || targetStateIndex >= stateTimeline.Count)
            return;

        stateIndex = targetStateIndex;
        selectedSlideSource = null;
        RefreshUi();
    }

    private void MoveModeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (suppressUiEvents)
            return;

        if (GetMoveBuilderMode() == MoveBuilderMode.Placement)
            selectedSlideSource = null;

        UpdateMoveBuilderMode();
        UpdateBoardDisplay();
        UpdateStatusPanel();
    }

    private void PieceTypeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (suppressUiEvents)
            return;

        UpdateBoardDisplay();
        UpdateStatusPanel();
    }

    private void SlideSelection_Changed(object? sender, SelectionChangedEventArgs e)
    {
        if (suppressUiEvents)
            return;

        UpdateBoardDisplay();
        UpdateStatusPanel();
    }

    private void ClearSelectionBtn_Click(object? sender, RoutedEventArgs e)
    {
        selectedSlideSource = null;
        RefreshUi();
    }

    private async void SubmitMoveBtn_Click(object? sender, RoutedEventArgs e)
    {
        if (!IsLiveHumanTurn || CurrentState == null)
            return;

        if (GetMoveBuilderMode() != MoveBuilderMode.Slide)
            return;

        var move = TryBuildSelectedSlideMove(CurrentState);
        if (move == null)
        {
            StatusTextMessage("Select a legal source, direction, carry count, and drop pattern.");
            return;
        }

        ExecuteMove(move);
        await MaybePlayAiTurnAsync();
    }

    private async void BoardButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!IsLiveHumanTurn || CurrentState == null || sender is not Button button || button.Tag is not Position position)
            return;

        var legalMoves = GameRules.GetLegalMoves(CurrentState).ToList();

        if (GetMoveBuilderMode() == MoveBuilderMode.Placement)
        {
            var pieceType = GetSelectedPlacementPieceType();
            var move = legalMoves.OfType<PlaceMove>().FirstOrDefault(place => place.Position == position && place.PieceType == pieceType);
            if (move == null)
            {
                StatusTextMessage("That placement is not currently legal.");
                return;
            }

            ExecuteMove(move);
            await MaybePlayAiTurnAsync();
            return;
        }

        var legalSources = legalMoves.OfType<SlideMove>().Select(move => move.From).ToHashSet();
        if (!legalSources.Contains(position))
        {
            StatusTextMessage("That stack cannot slide right now.");
            return;
        }

        selectedSlideSource = selectedSlideSource.HasValue && selectedSlideSource.Value == position ? null : position;
        RefreshUi();
    }
}
