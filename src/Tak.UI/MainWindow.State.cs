using System.Globalization;
using Avalonia.Controls;
using Tak.AI;
using Tak.Core;

namespace Tak.UI;

public partial class MainWindow
{
    private void StartNewGame()
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

        aiAgent = CreateAgent(opponentName, seed: 42);

        var newGame = Utils.CreateNewGame(boardSize);
        stateTimeline.Clear();
        stateTimeline.Add(newGame);
        stateIndex = 0;
        selectedSlideSource = null;
        resultOverlayDismissed = false;

        CreateBoardUI(boardSize);
        RefreshUi();
    }

    private void JumpToLiveState()
    {
        if (stateTimeline.Count == 0)
            return;

        stateIndex = stateTimeline.Count - 1;
        selectedSlideSource = null;
        RefreshUi();
    }

    private void RefreshUi()
    {
        gameState = CurrentState;
        UpdateStaticUi();
        UpdateMoveBuilderMode();
        UpdateBoardDisplay();
        UpdateStatusPanel();
        UpdateHistoryPanel();
        UpdateResultOverlay();
    }

    private void UpdateStaticUi()
    {
        if (gameState == null)
        {
            HeaderTurnText.Text = "No game in progress";
            HeaderStatusText.Text = "Start a new game to begin.";
            BoardHintText.Text = "Click Start New Game to create a match.";
            LegalMovesText.Text = "-";
            ResultSummaryText.Text = "-";
            ResultDetailText.Text = "-";
            CurrentPlayerText.Text = "-";
            LiveStateText.Text = "-";
            MoveCountText.Text = "0";
            WhiteReserveText.Text = "-";
            WhiteCoverageText.Text = "-";
            BlackReserveText.Text = "-";
            BlackCoverageText.Text = "-";
            LastMoveText.Text = "-";
            ResultOverlay.IsVisible = false;
            return;
        }

        var state = gameState;
        var isReviewing = !IsLiveState;

        CurrentPlayerText.Text = state.Result != null ? "-" : state.CurrentPlayer.ToString();
        LiveStateText.Text = state.Result != null
            ? "Complete"
            : isReviewing
                ? $"Review {stateIndex + 1}/{stateTimeline.Count}"
                : "Live";
        MoveCountText.Text = state.MoveHistory.Count.ToString(CultureInfo.InvariantCulture);
        WhiteReserveText.Text = $"Flat / wall pieces: {state.FlatStoneReserve[Player.White]}";
        WhiteCoverageText.Text = FormatStackCoverage(state, Player.White);
        BlackReserveText.Text = $"Flat / wall pieces: {state.FlatStoneReserve[Player.Black]}";
        BlackCoverageText.Text = FormatStackCoverage(state, Player.Black);
        if (state.MoveHistory.Count == 0)
        {
            LastMoveText.Text = "-";
        }
        else if (stateIndex > 0 && stateIndex - 1 < stateTimeline.Count)
        {
            LastMoveText.Text = $"{stateTimeline[stateIndex - 1].CurrentPlayer}: {FormatMove(state.MoveHistory[^1])}";
        }
        else
        {
            LastMoveText.Text = FormatMove(state.MoveHistory[^1]);
        }

        if (state.Result == null)
        {
            HeaderTurnText.Text = isReviewing
                ? $"Reviewing move {stateIndex + 1} of {stateTimeline.Count}"
                : $"{state.CurrentPlayer} to move";
            HeaderStatusText.Text = isReviewing
                ? "Use undo, redo, or the history list to inspect previous turns."
                : state.CurrentPlayer == humanPlayer
                    ? "Your turn. Legal squares are highlighted on the board."
                    : $"{aiAgent?.Name ?? "AI"} is thinking.";
            BoardHintText.Text = isReviewing
                ? "Review mode is read-only. Return to live to continue the game."
                : state.CurrentPlayer == humanPlayer
                    ? GetMoveBuilderMode() == MoveBuilderMode.Placement
                        ? "Click a highlighted square to submit the selected placement."
                        : "Select a source stack, then choose direction, carry count, and drop pattern."
                    : "Waiting for the AI to move.";
        }

        if (state.Result != null)
        {
            HeaderTurnText.Text = "Game complete";
            HeaderStatusText.Text = state.Result.ToString();
            BoardHintText.Text = "Use replay to start over, or return to the board to inspect the final position.";
            ResultSummaryText.Text = state.Result.ToString();
            ResultDetailText.Text = $"Winner: {state.Result.Winner} | Move count: {state.Result.MoveCount} | Type: {state.Result.Type}";
        }
    }

    private void UpdateHistoryPanel()
    {
        if (gameState == null)
        {
            suppressUiEvents = true;
            MoveHistoryList.Items.Clear();
            suppressUiEvents = false;
            MoveHistoryList.SelectedIndex = -1;
            return;
        }

        suppressUiEvents = true;
        MoveHistoryList.Items.Clear();

        for (int index = 0; index < gameState.MoveHistory.Count; index++)
        {
            MoveHistoryList.Items.Add(FormatHistoryEntry(stateTimeline[index], gameState.MoveHistory[index], index));
        }

        MoveHistoryList.SelectedIndex = stateIndex <= 0 ? -1 : stateIndex - 1;
        suppressUiEvents = false;
    }

    private void UpdateResultOverlay()
    {
        if (gameState?.Result == null || !IsLiveState || resultOverlayDismissed)
        {
            ResultOverlay.IsVisible = false;
            return;
        }

        ResultOverlay.IsVisible = true;
        ResultText.Text = gameState.Result.ToString();
        ResultDetailText.Text = $"Winner: {gameState.Result.Winner} | Move count: {gameState.Result.MoveCount} | Type: {gameState.Result.Type}";
    }

    private void ExecuteMove(Move move)
    {
        if (CurrentState == null || !IsLiveState)
            return;

        var legalMoves = GameRules.GetLegalMoves(CurrentState).ToList();
        var matchedMove = legalMoves.FirstOrDefault(legalMove => AreEquivalentMoves(legalMove, move));
        if (matchedMove == null)
        {
            StatusTextMessage("That move is not legal in the current position.");
            return;
        }

        if (stateIndex < stateTimeline.Count - 1)
            stateTimeline.RemoveRange(stateIndex + 1, stateTimeline.Count - stateIndex - 1);

        var nextState = CurrentState.MakeMove(matchedMove);
        stateTimeline.Add(nextState);
        stateIndex = stateTimeline.Count - 1;
        selectedSlideSource = null;
        resultOverlayDismissed = false;
        gameState = nextState;
        RefreshUi();
    }

    private async System.Threading.Tasks.Task MaybePlayAiTurnAsync()
    {
        if (aiTurnInProgress || CurrentState == null || aiAgent == null || !IsLiveState || CurrentState.Result != null || CurrentState.CurrentPlayer == humanPlayer)
            return;

        aiTurnInProgress = true;
        try
        {
            HeaderStatusText.Text = $"{aiAgent.Name} is thinking...";
            var move = await System.Threading.Tasks.Task.Run(() => aiAgent.ChooseMove(CurrentState.Clone(), iterationLimit: DefaultAiIterationLimit));

            if (CurrentState == null || !IsLiveState || CurrentState.Result != null || CurrentState.CurrentPlayer == humanPlayer)
                return;

            ExecuteMove(move);
        }
        finally
        {
            aiTurnInProgress = false;
        }
    }

    private void UpdateControlsState()
    {
        var interactive = IsLiveHumanTurn;
        MoveModeCombo.IsEnabled = interactive;
        PieceTypeCombo.IsEnabled = interactive && GetMoveBuilderMode() == MoveBuilderMode.Placement;
        ClearSelectionBtn.IsEnabled = interactive && GetMoveBuilderMode() == MoveBuilderMode.Slide;
        UndoBtn.IsEnabled = stateIndex > 0;
        RedoBtn.IsEnabled = stateIndex < stateTimeline.Count - 1;
        LiveBtn.IsEnabled = stateTimeline.Count > 0;
        RestartBtn.IsEnabled = true;
        SubmitMoveBtn.IsEnabled = interactive && GetMoveBuilderMode() == MoveBuilderMode.Slide && TryBuildSelectedSlideMove(CurrentState!) != null;
    }

    private void StatusTextMessage(string message)
    {
        HeaderStatusText.Text = message;
        BoardHintText.Text = message;
    }

    private static IAgent CreateAgent(string agentName, int? seed = null, double explorationConstant = 1.414)
    {
        return agentName.ToLowerInvariant() switch
        {
            "random" => new RandomAgent(seed),
            "heuristic" => new HeuristicAgent(seed),
            "uct" => new UctAgent(explorationConstant, seed),
            "rave" => new RaveAgent(explorationConstant, seed),
            "pw" => new ProgressiveWideningAgent(explorationConstant, seed: seed),
            _ => throw new ArgumentException($"Unknown agent: {agentName}")
        };
    }
}
