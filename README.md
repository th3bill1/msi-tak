# Tak

This project implements the board game Tak with a reusable game engine, AI opponents, a desktop UI, and a command-line tournament runner for comparing AI strategies.

## Solution Structure

```text
Tak.Core          Game engine and rules
Tak.AI            AI agents
Tak.UI            Avalonia desktop interface
Tak.Experiments   CLI tournament runner
Tak.Tests         Automated tests
```

## Requirements

- .NET 10 SDK

## What Is Implemented

- Board sizes 4x4, 5x5, and 6x6
- Flat stones, walls, and capstones
- Opening placement behavior
- Legal move generation and stack movement
- Road wins and flat wins
- Random, Heuristic, UCT, RAVE, and Progressive Widening agents
- Avalonia desktop UI
- CLI tournament output to CSV
- Automated tests

## Run The UI

```bash
dotnet run --project src/Tak.UI
```

The UI is an Avalonia desktop application for playing Tak against AI opponents.

## Run Tests

```bash
dotnet test
```

The test suite covers core rules, move generation, AI legality checks, and experiment runner coverage.

## Run Experiments

Quick default tournament:

```bash
dotnet run --project src/Tak.Experiments
```

The runner writes CSV output to `results/tournament.csv` by default and prints a summary to the console.

Example with explicit options:

```bash
dotnet run --project src/Tak.Experiments -- --games 100 --board 5 --white Heuristic --black Random --seed 123 --output results/heuristic_vs_random.csv
```

Supported agent names are case-insensitive:

- `random`
- `heuristic`
- `uct`
- `rave`
- `pw`

Supported options:

- `--games <n>` total games to play, alternating colors
- `--board` or `--board-size <n>` board size, one of 4, 5, or 6
- `--white` or `--agent-a <name>` white-side agent
- `--black` or `--agent-b <name>` black-side agent
- `--iterations <n>` iteration limit for search agents
- `--move-time-ms <n>` per-move time limit in milliseconds, use `0` for none
- `--seed <n>` base seed recorded per game
- `--exploration <n>` exploration constant used by UCT-style agents
- `--output <path>` CSV output path
- `--help` show usage text

## AI Agents

- Random chooses a legal move uniformly at random.
- Heuristic uses a simple board evaluation with immediate win and block checks.
- UCT is a Monte Carlo Tree Search-style agent.
- RAVE adds AMAF/RAVE statistics on top of MCTS search.
- Progressive Widening is an MCTS variant that limits branching early.

## Reproducibility

Use a fixed `--seed` to record the per-game seeds used by the tournament runner. This makes runs easier to compare, but full bit-for-bit determinism still depends on the selected agents and any wall-clock time limits.

## CSV Output

Each tournament row includes:

- game id
- run id
- timestamp
- board size
- white agent
- black agent
- winner
- result type
- move count
- duration
- average move time
- simulations per second
- seed
- white seed
- black seed
- iteration limit
- move time limit
- error text, if a game fails


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
