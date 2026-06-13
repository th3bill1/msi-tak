# Tak MCTS - Monte Carlo Tree Search for the Game of Tak

## Project Description

A complete implementation of the board game **Tak** with multiple AI players based on Monte Carlo Tree Search (MCTS) algorithms. This is a university AI project demonstrating various MCTS enhancements and comparison with heuristic and random agents.

## Game Rules Summary

**Tak** is an abstract strategy board game played on an N×N grid (typically 4×4, 5×5, or 6×6).

### Pieces
- **Flat stone**: Counts toward road victory, can be covered by other pieces
- **Wall/Standing stone**: Blocks movement, doesn't count toward road, cannot be covered by flat stones
- **Capstone**: Counts toward road, cannot be covered, can flatten walls

### Game Flow
1. Players alternate turns placing and moving pieces
2. First move: Opponent places a flat stone (opening rule)
3. Subsequent moves: Place piece from reserve or move a controlled stack
4. Movement is orthogonal, limited to board edges

### Win Conditions
1. **Road Win**: First player to connect opposite board edges with flat stones/capstone (walls don't count)
2. **Flat Win**: If board fills or reserve exhausts, player with more controlled flat stones wins
3. **Tak-Tin Rule**: If both roads form after one move, current player wins

## Solution Structure

```
TakMcts/
├── TakMcts.sln                          # Solution file
├── README.md                            # This file
├── .gitignore                          # Git ignore rules
├── src/
│   ├── Tak.Core/                       # Game rules and models
│   │   ├── Tak.Core.csproj
│   │   ├── Enums.cs                    # Player, PieceType, Direction
│   │   ├── Models.cs                   # Position, Piece, Move types
│   │   ├── Board.cs                    # Board state management
│   │   ├── GameState.cs                # Complete game state
│   │   ├── GameConfig.cs               # Configuration (board size, piece counts)
│   │   ├── GameRules.cs                # Game logic and win conditions
│   │   ├── MoveGenerator.cs            # Legal move generation
│   │   └── Utils.cs                    # Utility functions
│   │
│   ├── Tak.AI/                         # AI agents and MCTS implementations
│   │   ├── Tak.AI.csproj
│   │   ├── IAgent.cs                   # Common agent interface
│   │   ├── Agents/
│   │   │   ├── RandomAgent.cs
│   │   │   ├── HeuristicAgent.cs
│   │   │   ├── UctAgent.cs             # Standard UCT/MCTS
│   │   │   ├── RaveAgent.cs            # UCT with RAVE
│   │   │   └── ProgressiveWideningAgent.cs  # UCT with Progressive Widening
│   │   └── Mcts/
│   │       ├── MctsNode.cs             # MCTS tree node
│   │       ├── MctsTree.cs             # MCTS tree manager
│   │       └── RaveData.cs             # RAVE/AMAF statistics
│   │
│   ├── Tak.Experiments/                # Tournament runner
│   │   ├── Tak.Experiments.csproj
│   │   ├── Program.cs                  # CLI entry point
│   │   ├── Tournament.cs               # Tournament logic
│   │   ├── CsvWriter.cs                # CSV output
│   │   └── AgentFactory.cs             # Create agents from parameters
│   │
│   └── Tak.UI/                         # WPF application
│       ├── Tak.UI.csproj
│       ├── App.xaml, App.xaml.cs       # WPF application
│       ├── MainWindow.xaml, MainWindow.xaml.cs  # Main UI
│       ├── GameViewModel.cs            # UI logic
│       └── Converters.cs               # WPF value converters
│
├── tests/
│   └── Tak.Tests/
│       ├── Tak.Tests.csproj
│       ├── GameRulesTests.cs           # Game logic tests
│       ├── MoveGenerationTests.cs      # Move generation tests
│       ├── AgentTests.cs               # Agent tests
│       └── IntegrationTests.cs         # End-to-end tests
│
├── results/                            # Experiment output directory
│   └── .gitkeep                        # Keep directory tracked
│
└── docs/
    └── (Optional) Additional documentation
```

## Requirements

- .NET 10 SDK (or later)
- Windows (for WPF UI; Core and AI are cross-platform)
- Visual Studio 2022+ or VS Code with C# Dev Kit (recommended)

## Getting Started

### Restore Dependencies

```bash
dotnet restore
```

### Build Solution

```bash
dotnet build
```

### Run All Tests

```bash
dotnet test
```

## Running the Applications

### Run WPF UI (Play Against AI)

```bash
dotnet run --project src/Tak.UI
```

**UI Features:**
- Choose board size (4×4, 5×5, 6×6)
- Choose opponent AI (Random, Heuristic, UCT, RAVE, Progressive Widening)
- Choose player color
- Place pieces and move stacks
- View board state and move history
- AI responds automatically or on button click

### Run Experiments/Tournament

Basic tournament:
```bash
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 `
  --agent-a uct `
  --agent-b heuristic `
  --games 100 `
  --iterations 1000 `
  --seed 123 `
  --output results/uct_vs_heuristic.csv
```

**Supported Agents:** `random`, `heuristic`, `uct`, `rave`, `pw` (progressive widening)

**Supported Parameters:**
- `--board-size` 4|5|6 (default: 5)
- `--agent-a` (first player)
- `--agent-b` (second player)
- `--games` number of games (default: 100)
- `--iterations` maximum MCTS iterations per move (default: 1000)
- `--move-time-ms` maximum wall-clock time per move in milliseconds; use `0` for no time cap (default: 1000)
- `--seed` random seed for reproducibility (default: random)
- `--exploration` UCT exploration constant C (default: 1.414)
- `--output` CSV file path (default: results/tournament.csv)

## Example Tournaments

### Random vs Heuristic
```bash
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a random --agent-b heuristic `
  --games 100 --iterations 500 --output results/random_vs_heuristic.csv
```

### UCT vs Heuristic
```bash
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a uct --agent-b heuristic `
  --games 50 --iterations 1000 --output results/uct_vs_heuristic.csv
```

### RAVE vs UCT
```bash
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a rave --agent-b uct `
  --games 50 --iterations 1000 --output results/rave_vs_uct.csv
```

### Progressive Widening vs UCT
```bash
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a pw --agent-b uct `
  --games 50 --iterations 1000 --output results/pw_vs_uct.csv
```

### RAVE vs Progressive Widening
```bash
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a rave --agent-b pw `
  --games 50 --iterations 1000 --output results/rave_vs_pw.csv
```

## Testing Different Board Sizes

### 4×4 Board
```bash
dotnet run --project src/Tak.Experiments -- `
  --board-size 4 --agent-a uct --agent-b heuristic `
  --games 50 --iterations 500 --output results/4x4_uct_vs_heuristic.csv
```

### 5×5 Board
```bash
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a uct --agent-b heuristic `
  --games 50 --iterations 1000 --output results/5x5_uct_vs_heuristic.csv
```

### 6×6 Board
```bash
dotnet run --project src/Tak.Experiments -- `
  --board-size 6 --agent-a uct --agent-b heuristic `
  --games 20 --iterations 500 --output results/6x6_uct_vs_heuristic.csv
```

## Testing Different Iteration Budgets

```bash
# 200 iterations
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a uct --agent-b heuristic `
  --games 50 --iterations 200 --output results/uct_200iter.csv

# 500 iterations
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a uct --agent-b heuristic `
  --games 50 --iterations 500 --output results/uct_500iter.csv

# 1000 iterations
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a uct --agent-b heuristic `
  --games 50 --iterations 1000 --output results/uct_1000iter.csv

# 5000 iterations
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a uct --agent-b heuristic `
  --games 20 --iterations 5000 --output results/uct_5000iter.csv
```

## Reproducibility

Use the `--seed` parameter to reproduce exact game sequences:

```bash
# This will produce identical results on re-run
dotnet run --project src/Tak.Experiments -- `
  --board-size 5 --agent-a uct --agent-b heuristic `
  --games 50 --iterations 1000 --seed 12345 `
  --output results/reproducible.csv
```

## CSV Output Format

Tournament results are saved to CSV with the following columns:

| Column | Description |
|--------|-------------|
| GameIndex | Game number in tournament (1-based) |
| BoardSize | Board size (4, 5, or 6) |
| AgentWhite | AI agent playing white |
| AgentBlack | AI agent playing black |
| Winner | `White`, `Black`, or `Draw` |
| ResultType | `Road`, `Flat`, or `Draw` |
| Moves | Number of moves played |
| DurationMs | Total game time in milliseconds |
| AverageMoveTimeMs | Average move time per turn |
| SimulationsPerSecond | (For MCTS agents) simulations/second throughput |
| Seed | Random seed used |
| IterationLimit | MCTS iteration limit |

## Implemented Agents

### RandomAgent
Selects a uniformly random legal move from all available moves. Baseline for comparison.

### HeuristicAgent
Employs greedy heuristics:
1. Immediate winning move if available
2. Block opponent's immediate road win if necessary
3. Extend own road connection
4. Increase controlled flat stone count
5. Random fallback for tie-breaking

### UctAgent (Standard MCTS)
Monte Carlo Tree Search with Upper Confidence bounds applied to Trees (UCT):

**Formula:** $UCT = \frac{w_i}{n_i} + C \sqrt{\frac{\ln N}{n_i}}$

Where:
- $w_i$ = wins for node $i$
- $n_i$ = visits to node $i$
- $N$ = parent visits
- $C$ = exploration constant (default 1.414 ≈ √2)

**Phases:**
1. **Selection**: Traverse tree using UCT formula until reaching unexpanded node
2. **Expansion**: Add one new child for an unvisited action
3. **Simulation**: Random rollout from new node to terminal state
4. **Backpropagation**: Update statistics back to root

### RaveAgent (UCT + RAVE/AMAF)
Extends UCT with Rapid Action Value Estimation (RAVE):

- Tracks all-moves-as-first statistics (AMAF) in addition to true UCT stats
- Blends RAVE and UCT values using beta weighting
- Beta formula: $\beta = \frac{r}{r + m + r \cdot m \cdot \alpha}$
- Useful early in search when RAVE is more informative

### ProgressiveWideningAgent (UCT + PW)
Extends UCT with progressive widening to handle large branching factors:

- Limits child expansion using: $k(n) = \lfloor C_{pw} \cdot n^\alpha \rfloor$
- Default: $C_{pw} = 0.5, \alpha = 0.5$
- Early in search: fewer children; as search deepens, more children available
- Reduces computational overhead for large action spaces

## Project Completion TODO

The core implementation is in place, but these items should be finished before considering the project complete:

### Tak.UI
- Support every legal move type in the UI, including all placement variants and full stack-slide input.
- Add direction and drop-distribution controls for slide moves.
- Show legal moves directly on the board and prevent illegal clicks before move submission.
- Display reserves, captured/covered stacks, current player, and game result more clearly.
- Improve board rendering for stacks so height, owner, and piece type are easier to read.
- Add undo/redo, restart, and move-review controls that work across human and AI turns.
- Make end-of-game flow explicit with a clear result screen and replay/reset option.

### Enhanced UI and Visualization
- Replace the current minimal layout with a more polished and responsive design.
- Add stronger visual feedback for selected squares, legal destinations, and last move.
- Add a move log with richer formatting and stack-state details.
- Add experiment result visualization in the UI or a companion viewer: win-rate charts, move-time charts, and agent comparison plots.
- Provide filters for board size, agent type, seed, and iteration budget when viewing results.

### Tak.Experiments
- Add a progress bar for tournament execution, including per-game and per-move progress where practical.
- Print clearer live status updates for the active game, current matchup, and elapsed time.
- Add richer CSV/JSON summaries for downstream analysis.
- Generate aggregated summaries for win rate, average move time, and simulations per second.
- Add automatic result validation so malformed tournament output is detected early.

### Agents
- Verify that every agent always returns a legal move in all supported board states.
- Add broader seeded reproducibility tests for head-to-head matchups.
- Improve heuristic evaluation with stronger Tak-specific signals such as road potential, stack mobility, reserve pressure, center control, and capstone tactics.
- Tune MCTS rollout policy so simulations are less random and more Tak-aware.
- Revisit RAVE and progressive widening parameters for better default behavior.
- Add optional stronger agents or search enhancements such as transposition tables, opening books, and parallel search.

### Rules and Validation
- Expand test coverage for Tak rule edge cases, especially stack movement, wall flattening, capstone behavior, and road detection.
- Verify the opening rule and all special win conditions on every supported board size.
- Add integration coverage for full-length games between every agent pair.
- Validate that board full, reserve exhaustion, and no-legal-move conditions resolve correctly.

### Documentation and Polish
- Update README examples once UI and experiment output formats are finalized.
- Add screenshots or short GIFs of the UI and experiment reports.
- Document supported command-line options and sample workflows more clearly.
- Add troubleshooting notes for build, run, and tournament execution issues.
- Clean up warnings and keep the solution build/test output as close to warning-free as practical.

