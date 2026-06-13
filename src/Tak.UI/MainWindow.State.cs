using System.Globalization;
using System.Threading;
using Avalonia.Controls;
using Tak.AI;
using Tak.Core;

namespace Tak.UI;

/// <summary>State-management helpers for the main Tak board window.</summary>
public partial class MainWindow
{
    private void StartNewGame(bool startAutoPlay = false)
    {
        StopAiLoop();

        int boardSize = BoardSizeCombo.SelectedIndex switch
        {
            0 => 4,
            1 => 5,
            2 => 6,
            _ => 5
        };

        var whiteController = GetSelectedController(WhiteControllerCombo);
        var blackController = GetSelectedController(BlackControllerCombo);
        humanPlayer = whiteController == PlayerController.Human
            ? Player.White
            : blackController == PlayerController.Human
                ? Player.Black
                : Player.None;

        whiteAgent = CreateAgent(whiteController, seed: 42);
        blackAgent = CreateAgent(blackController, seed: 43);
        aiAgent = humanPlayer == Player.White ? blackAgent : whiteAgent;
        aiAutoPlayEnabled = startAutoPlay || (whiteController != PlayerController.Human && blackController != PlayerController.Human);
        aiAutoPlayPaused = false;
        aiStepRequested = false;

        var newGame = Utils.CreateNewGame(boardSize);
        stateTimeline.Clear();
        stateTimeline.Add(newGame);
        stateIndex = 0;
        selectedSlideSource = null;
        resultOverlayDismissed = false;

        CreateBoardUI(boardSize);
        RefreshUi();
    }

    private void StartAiVsAiGame()
    {
        if (GetSelectedController(WhiteControllerCombo) == PlayerController.Human)
            WhiteControllerCombo.SelectedIndex = 2;

        if (GetSelectedController(BlackControllerCombo) == PlayerController.Human)
            BlackControllerCombo.SelectedIndex = 4;

        StartNewGame(startAutoPlay: true);
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
                : IsHumanController(state.CurrentPlayer)
                    ? "Your turn. Legal squares are highlighted on the board."
                    : $"{GetPlayerDisplayName(state.CurrentPlayer)} is thinking.";
            BoardHintText.Text = isReviewing
                ? "Review mode is read-only. Return to live to continue the game."
                : IsHumanController(state.CurrentPlayer)
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
            MoveHistoryList.SelectedIndex = -1;
            suppressUiEvents = false;
            return;
        }

        suppressUiEvents = true;
        try
        {
            MoveHistoryList.Items.Clear();

            var historyState = stateTimeline.Count > 0 ? stateTimeline[^1] : gameState;
            var moveCount = Math.Min(historyState.MoveHistory.Count, Math.Max(0, stateTimeline.Count - 1));

            for (int index = 0; index < moveCount; index++)
            {
                MoveHistoryList.Items.Add(FormatHistoryEntry(stateTimeline[index], historyState.MoveHistory[index], index));
            }

            var desiredSelectedIndex = stateIndex > 0 && stateIndex <= moveCount ? stateIndex - 1 : -1;
            if (MoveHistoryList.SelectedIndex != desiredSelectedIndex)
            {
                suppressHistorySelectionEvents = true;
                try
                {
                    MoveHistoryList.SelectedIndex = desiredSelectedIndex;
                }
                finally
                {
                    suppressHistorySelectionEvents = false;
                }
            }
        }
        finally
        {
            suppressUiEvents = false;
        }
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
        if (aiAutoPlayEnabled)
        {
            await StartAiLoopAsync();
            return;
        }

        if (CurrentState == null || !IsLiveState || CurrentState.Result != null || IsHumanController(CurrentState.CurrentPlayer))
            return;

        await ExecuteSingleAiTurnAsync(CancellationToken.None);
    }

    private async System.Threading.Tasks.Task StartAiLoopAsync()
    {
        if (aiAutoLoopRunning)
            return;

        aiLoopCancellation?.Cancel();
        aiLoopCancellation?.Dispose();
        aiLoopCancellation = new CancellationTokenSource();
        var token = aiLoopCancellation.Token;
        aiAutoLoopRunning = true;

        try
        {
            while (!token.IsCancellationRequested && aiAutoPlayEnabled && CurrentState != null && IsLiveState && CurrentState.Result == null)
            {
                if (aiAutoPlayPaused && !aiStepRequested)
                {
                    await System.Threading.Tasks.Task.Delay(100, token);
                    continue;
                }

                if (IsHumanController(CurrentState.CurrentPlayer))
                {
                    aiAutoPlayEnabled = false;
                    break;
                }

                aiStepRequested = false;
                await ExecuteSingleAiTurnAsync(token);

                if (CurrentState?.Result != null)
                {
                    aiAutoPlayEnabled = false;
                    break;
                }

                if (!aiStepRequested)
                    await System.Threading.Tasks.Task.Delay(GetAiMoveDelay(), token);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when restarting or pausing through cancellation.
        }
        finally
        {
            aiAutoLoopRunning = false;
            UpdateControlsState();
        }
    }

    private async System.Threading.Tasks.Task ExecuteSingleAiTurnAsync(CancellationToken cancellationToken)
    {
        if (aiTurnInProgress || CurrentState == null || !IsLiveState || CurrentState.Result != null)
            return;

        var stateBeforeMove = CurrentState;
        var agent = GetAgentForPlayer(stateBeforeMove.CurrentPlayer);
        if (agent == null)
            return;

        aiTurnInProgress = true;
        UpdateControlsState();
        try
        {
            HeaderStatusText.Text = $"{agent.Name} ({stateBeforeMove.CurrentPlayer}) is thinking...";
            var move = await System.Threading.Tasks.Task.Run(() => agent.ChooseMove(stateBeforeMove.Clone(), DefaultAiTimeLimit, DefaultAiIterationLimit), cancellationToken);

            if (cancellationToken.IsCancellationRequested || CurrentState != stateBeforeMove || !IsLiveState || CurrentState.Result != null)
                return;

            var legalMoves = GameRules.GetLegalMoves(CurrentState).ToList();
            var matchedMove = legalMoves.FirstOrDefault(legalMove => AreEquivalentMoves(legalMove, move));
            if (matchedMove == null)
            {
                StopAiLoop();
                StatusTextMessage($"{agent.Name} returned an illegal move: {FormatMove(move)}");
                return;
            }

            ExecuteMove(matchedMove);
            HeaderStatusText.Text = $"{agent.Name} played {FormatMove(matchedMove)}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            StopAiLoop();
            StatusTextMessage($"{agent.Name} failed: {ex.Message}");
        }
        finally
        {
            aiTurnInProgress = false;
            UpdateControlsState();
        }
    }

    private void UpdateControlsState()
    {
        var interactive = IsLiveHumanTurn && !aiTurnInProgress && !aiAutoPlayEnabled;
        var hasGame = CurrentState != null;
        var aiVsAiConfigured = GetSelectedController(WhiteControllerCombo) != PlayerController.Human && GetSelectedController(BlackControllerCombo) != PlayerController.Human;
        var canControlAi = hasGame && CurrentState?.Result == null && !IsLiveHumanTurn;

        BoardSizeCombo.IsEnabled = !aiTurnInProgress && !aiAutoPlayEnabled;
        WhiteControllerCombo.IsEnabled = !aiTurnInProgress && !aiAutoPlayEnabled;
        BlackControllerCombo.IsEnabled = !aiTurnInProgress && !aiAutoPlayEnabled;
        AiSpeedCombo.IsEnabled = true;
        NewGameBtn.IsEnabled = !aiTurnInProgress;
        StartAiVsAiBtn.IsEnabled = !aiTurnInProgress;
        PauseAiBtn.IsEnabled = aiAutoPlayEnabled && !aiAutoPlayPaused;
        ResumeAiBtn.IsEnabled = canControlAi && aiAutoPlayPaused;
        StepAiBtn.IsEnabled = canControlAi && !aiTurnInProgress && (!aiAutoPlayEnabled || aiAutoPlayPaused || aiVsAiConfigured);
        MoveModeCombo.IsEnabled = interactive;
        PieceTypeCombo.IsEnabled = interactive && GetMoveBuilderMode() == MoveBuilderMode.Placement;
        ClearSelectionBtn.IsEnabled = interactive && GetMoveBuilderMode() == MoveBuilderMode.Slide;
        UndoBtn.IsEnabled = stateIndex > 0 && !aiTurnInProgress && (!aiAutoPlayEnabled || aiAutoPlayPaused);
        RedoBtn.IsEnabled = stateIndex < stateTimeline.Count - 1 && !aiTurnInProgress && (!aiAutoPlayEnabled || aiAutoPlayPaused);
        LiveBtn.IsEnabled = stateTimeline.Count > 0;
        RestartBtn.IsEnabled = true;
        SubmitMoveBtn.IsEnabled = interactive && GetMoveBuilderMode() == MoveBuilderMode.Slide && TryBuildSelectedSlideMove(CurrentState!) != null;
    }

    private void StatusTextMessage(string message)
    {
        HeaderStatusText.Text = message;
        BoardHintText.Text = message;
    }

    private bool IsHumanController(Player player)
    {
        return player switch
        {
            Player.White => GetSelectedController(WhiteControllerCombo) == PlayerController.Human,
            Player.Black => GetSelectedController(BlackControllerCombo) == PlayerController.Human,
            _ => false
        };
    }

    private IAgent? GetAgentForPlayer(Player player)
    {
        return player switch
        {
            Player.White => whiteAgent,
            Player.Black => blackAgent,
            _ => null
        };
    }

    private string GetPlayerDisplayName(Player player)
    {
        var agent = GetAgentForPlayer(player);
        return agent == null ? player.ToString() : $"{agent.Name} ({player})";
    }

    private static IAgent? CreateAgent(PlayerController controller, int? seed = null, double explorationConstant = 1.414)
    {
        return controller switch
        {
            PlayerController.Human => null,
            PlayerController.Random => new RandomAgent(seed),
            PlayerController.Heuristic => new HeuristicAgent(seed),
            PlayerController.Uct => new UctAgent(explorationConstant, seed),
            PlayerController.Rave => new RaveAgent(explorationConstant, seed),
            PlayerController.ProgressiveWidening => new ProgressiveWideningAgent(explorationConstant, seed: seed),
            _ => null
        };
    }

    private static PlayerController GetSelectedController(ComboBox comboBox)
    {
        return comboBox.SelectedIndex switch
        {
            0 => PlayerController.Human,
            1 => PlayerController.Random,
            2 => PlayerController.Heuristic,
            3 => PlayerController.Uct,
            4 => PlayerController.Rave,
            5 => PlayerController.ProgressiveWidening,
            _ => PlayerController.Human
        };
    }

    private TimeSpan GetAiMoveDelay()
    {
        return AiSpeedCombo.SelectedIndex switch
        {
            0 => TimeSpan.FromMilliseconds(1000),
            2 => TimeSpan.FromMilliseconds(150),
            _ => TimeSpan.FromMilliseconds(500)
        };
    }

    private void StopAiLoop()
    {
        aiAutoPlayEnabled = false;
        aiAutoPlayPaused = false;
        aiStepRequested = false;
        aiLoopCancellation?.Cancel();
        aiLoopCancellation?.Dispose();
        aiLoopCancellation = null;
    }
}
