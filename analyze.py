"""
Analiza wyników turniejów z results/sweep/*.csv.
Generuje 6 wykresów do results/plots/.

Uruchomienie:
    python analyze.py
"""

from __future__ import annotations

import re
from pathlib import Path

import matplotlib.pyplot as plt
import numpy as np
import pandas as pd
import seaborn as sns

ROOT = Path(__file__).resolve().parent
SWEEP_DIR = ROOT / "results" / "sweep"
PLOTS_DIR = ROOT / "results" / "plots"
PLOTS_DIR.mkdir(parents=True, exist_ok=True)

# Filename patterns:
#   b{size}_{a}_vs_{b}.csv                  -> no iteration budget
#   b{size}_{a}_vs_{b}_iter{n}.csv          -> with iteration budget
PATTERN_WITH_ITER = re.compile(r"^b(\d+)_([a-z]+)_vs_([a-z]+)_iter(\d+)\.csv$")
PATTERN_NO_ITER = re.compile(r"^b(\d+)_([a-z]+)_vs_([a-z]+)\.csv$")

AGENT_ORDER = ["Random", "Heuristic", "UCT", "RAVE", "PW"]
AGENT_CANON = {
    "random": "Random",
    "heuristic": "Heuristic",
    "uct": "UCT",
    "rave": "RAVE",
    "pw": "PW",
}


def load_all() -> pd.DataFrame:
    """Wczytuje wszystkie CSV z sweepu do jednego DataFrame'a, dodając metadane z nazwy pliku."""
    rows = []
    for csv in sorted(SWEEP_DIR.glob("*.csv")):
        m = PATTERN_WITH_ITER.match(csv.name)
        iter_budget: int | None
        if m:
            board, a, b, iter_budget = int(m[1]), m[2], m[3], int(m[4])
        else:
            m = PATTERN_NO_ITER.match(csv.name)
            if not m:
                print(f"  ! pomijam (nieznana nazwa): {csv.name}")
                continue
            board, a, b = int(m[1]), m[2], m[3]
            iter_budget = None

        df = pd.read_csv(csv)
        df["FileBoard"] = board
        df["FileIter"] = iter_budget if iter_budget is not None else 0
        df["FileAgentA"] = AGENT_CANON[a]
        df["FileAgentB"] = AGENT_CANON[b]
        df["Matchup"] = f"{AGENT_CANON[a]} vs {AGENT_CANON[b]}"
        rows.append(df)

    if not rows:
        raise SystemExit("Brak CSV-ów w results/sweep/ — najpierw odpal ./run_sweep.sh")

    full = pd.concat(rows, ignore_index=True)
    print(f"Wczytano {len(full)} gier z {len(rows)} turniejów.")
    return full


def add_perspective_columns(df: pd.DataFrame) -> pd.DataFrame:
    """Dla każdej gry dodaje WinnerAgent (kto wygrał: A/B/Draw) z perspektywy nazwy pliku."""
    out = df.copy()
    out["WinnerAgent"] = "Draw"
    mask_white = (out["Winner"] == "White")
    mask_black = (out["Winner"] == "Black")

    # AgentWhite/AgentBlack mówią kto był jakim kolorem w danej grze;
    # przekładamy na "A" lub "B" wg etykiet z nazwy pliku.
    a_white = mask_white & (out["AgentWhite"] == out["FileAgentA"])
    a_black = mask_black & (out["AgentBlack"] == out["FileAgentA"])
    out.loc[a_white | a_black, "WinnerAgent"] = "A"

    b_white = mask_white & (out["AgentWhite"] == out["FileAgentB"])
    b_black = mask_black & (out["AgentBlack"] == out["FileAgentB"])
    out.loc[b_white | b_black, "WinnerAgent"] = "B"
    return out


def winrate_table(df: pd.DataFrame) -> pd.DataFrame:
    """Agreguje: per (board, iter, AgentA, AgentB) -> win rate A, win rate B, remisy, liczba gier."""
    grouped = df.groupby(["FileBoard", "FileIter", "FileAgentA", "FileAgentB"])
    out = grouped["WinnerAgent"].value_counts().unstack(fill_value=0)
    for col in ("A", "B", "Draw"):
        if col not in out:
            out[col] = 0
    out["Total"] = out["A"] + out["B"] + out["Draw"]
    out["WinRateA"] = out["A"] / out["Total"]
    out["WinRateB"] = out["B"] / out["Total"]
    out["DrawRate"] = out["Draw"] / out["Total"]
    return out.reset_index()


# --- WYKRESY ---

def plot_heatmap_per_board(df: pd.DataFrame) -> None:
    """Heatmap win rate: rzędy=agent grający, kolumny=przeciwnik. Per rozmiar planszy."""
    boards = sorted(df["FileBoard"].unique())
    for board in boards:
        sub = df[df["FileBoard"] == board]
        # Dla par z iter, użyj największego budżetu (najmocniejszy MCTS)
        # Dla par bez iter, użyj jedynego dostępnego
        sub_max_iter = sub.loc[sub.groupby(["FileAgentA", "FileAgentB"])["FileIter"].idxmax()]

        # Macierz: rząd = agent obserwowany, kolumna = przeciwnik, wartość = win rate obserwowanego
        agents_present = sorted(set(sub_max_iter["FileAgentA"]) | set(sub_max_iter["FileAgentB"]),
                                key=lambda x: AGENT_ORDER.index(x) if x in AGENT_ORDER else 99)
        matrix = pd.DataFrame(np.nan, index=agents_present, columns=agents_present)

        for _, row in sub_max_iter.iterrows():
            a, b = row["FileAgentA"], row["FileAgentB"]
            matrix.loc[a, b] = row["WinRateA"]
            matrix.loc[b, a] = row["WinRateB"]

        plt.figure(figsize=(7, 5.5))
        sns.heatmap(matrix * 100, annot=True, fmt=".0f", cmap="RdYlGn",
                    vmin=0, vmax=100, cbar_kws={"label": "Win rate (%)"},
                    linewidths=0.5, linecolor="white")
        plt.title(f"Win rate macierz — plansza {board}×{board}\n(MCTS na najwyższym budżecie iteracji)")
        plt.xlabel("Przeciwnik")
        plt.ylabel("Agent")
        plt.tight_layout()
        plt.savefig(PLOTS_DIR / f"heatmap_winrate_board{board}.png", dpi=130)
        plt.close()
        print(f"  -> heatmap_winrate_board{board}.png")


def plot_mcts_vs_heuristic_scaling(df: pd.DataFrame) -> None:
    """Słupkowy: jak win rate MCTS vs Heurystyka zmienia się z liczbą iteracji."""
    # Wybieramy pary gdzie Heuristic gra przeciw MCTS
    mcts_agents = ["UCT", "RAVE", "PW"]
    boards = sorted(df["FileBoard"].unique())

    fig, axes = plt.subplots(1, len(boards), figsize=(5 * len(boards), 4.5), sharey=True)
    if len(boards) == 1:
        axes = [axes]

    for ax, board in zip(axes, boards):
        sub = df[(df["FileBoard"] == board) & (df["FileIter"] > 0)]
        # Bierzemy gry gdzie jeden z agentów to Heuristic, a drugi to MCTS
        keep = sub[((sub["FileAgentA"] == "Heuristic") & sub["FileAgentB"].isin(mcts_agents)) |
                   ((sub["FileAgentB"] == "Heuristic") & sub["FileAgentA"].isin(mcts_agents))].copy()
        # Win rate MCTS-a (nie Heuristic-a)
        def mcts_winrate(row):
            return row["WinRateB"] if row["FileAgentA"] == "Heuristic" else row["WinRateA"]
        def mcts_name(row):
            return row["FileAgentB"] if row["FileAgentA"] == "Heuristic" else row["FileAgentA"]
        keep["MctsWinRate"] = keep.apply(mcts_winrate, axis=1)
        keep["MctsAgent"] = keep.apply(mcts_name, axis=1)

        pivot = keep.pivot_table(index="FileIter", columns="MctsAgent", values="MctsWinRate")
        if not pivot.empty:
            pivot.plot(kind="bar", ax=ax, color={"UCT": "#3b82f6", "RAVE": "#10b981", "PW": "#f59e0b"})
        ax.axhline(0.5, color="grey", linestyle="--", linewidth=0.8, label="50% (parytet)")
        ax.set_title(f"Plansza {board}×{board}")
        ax.set_xlabel("Liczba iteracji MCTS")
        ax.set_ylabel("Win rate MCTS vs Heurystyka")
        ax.set_ylim(0, 1)
        ax.set_xticklabels([str(int(x)) for x in pivot.index], rotation=0)
        ax.grid(axis="y", linestyle=":", alpha=0.6)
        ax.legend(loc="upper left", fontsize=8)

    fig.suptitle("Skalowanie MCTS vs Heurystyka z budżetem iteracji", fontsize=13)
    plt.tight_layout()
    plt.savefig(PLOTS_DIR / "mcts_vs_heuristic_scaling.png", dpi=130)
    plt.close()
    print("  -> mcts_vs_heuristic_scaling.png")


def plot_move_time_vs_iterations(df: pd.DataFrame) -> None:
    """Liniowy: średni czas ruchu MCTS w zależności od liczby iteracji (per agent, per rozmiar planszy)."""
    mcts_agents = ["UCT", "RAVE", "PW"]
    sub = df[df["FileIter"] > 0].copy()

    # Z każdej gry wyciągamy średni czas ruchu agenta MCTS
    records = []
    for _, row in sub.iterrows():
        for agent_col, color_col in (("FileAgentA", "AgentWhite"), ("FileAgentB", "AgentBlack")):
            agent = row[agent_col]
            if agent not in mcts_agents:
                continue
            # AverageMoveTimeMs to średnia z wszystkich ruchów w grze, miesza graczy.
            # Lepsze przybliżenie: DurationMs / Moves, też miesza, ale w tym samym CSV.
            # Zostawiam AverageMoveTimeMs jako proxy.
            records.append({"Agent": agent, "Board": row["FileBoard"], "Iter": row["FileIter"],
                            "AvgMoveMs": row["AverageMoveTimeMs"]})
    md = pd.DataFrame(records)
    if md.empty:
        print("  (brak danych dla move_time_vs_iterations)")
        return

    agg = md.groupby(["Agent", "Board", "Iter"])["AvgMoveMs"].mean().reset_index()

    boards = sorted(agg["Board"].unique())
    fig, axes = plt.subplots(1, len(boards), figsize=(5 * len(boards), 4.5), sharey=False)
    if len(boards) == 1:
        axes = [axes]

    for ax, board in zip(axes, boards):
        sub_b = agg[agg["Board"] == board]
        for agent in mcts_agents:
            sub_a = sub_b[sub_b["Agent"] == agent].sort_values("Iter")
            if sub_a.empty:
                continue
            ax.plot(sub_a["Iter"], sub_a["AvgMoveMs"], marker="o", label=agent, linewidth=2)
        ax.set_title(f"Plansza {board}×{board}")
        ax.set_xlabel("Liczba iteracji MCTS")
        ax.set_ylabel("Średni czas ruchu (ms)")
        ax.grid(linestyle=":", alpha=0.6)
        ax.legend()

    fig.suptitle("Koszt obliczeniowy MCTS: czas ruchu vs liczba iteracji", fontsize=13)
    plt.tight_layout()
    plt.savefig(PLOTS_DIR / "move_time_vs_iterations.png", dpi=130)
    plt.close()
    print("  -> move_time_vs_iterations.png")


def plot_result_type_breakdown(df: pd.DataFrame) -> None:
    """Słupkowy: jakim typem wygranych kończą się gry per matchup (Road vs Flat vs Draw)."""
    # Tylko dla najwyższego budżetu iter per matchup (najmocniejsze gry)
    idx = df.groupby(["FileBoard", "FileAgentA", "FileAgentB"])["FileIter"].idxmax()
    sub = df.loc[df.index.isin(idx) if False else df.index, :]  # użyjemy wszystkich gier z najmocniejszym iter
    max_iter_per = df.groupby(["FileBoard", "FileAgentA", "FileAgentB"])["FileIter"].max().reset_index()
    sub = df.merge(max_iter_per, on=["FileBoard", "FileAgentA", "FileAgentB", "FileIter"])

    counts = sub.groupby(["Matchup", "ResultType"]).size().unstack(fill_value=0)
    counts = counts.div(counts.sum(axis=1), axis=0)  # normalizacja do %

    plt.figure(figsize=(11, 0.4 * len(counts) + 2))
    counts.plot(kind="barh", stacked=True, ax=plt.gca(),
                color={"Road": "#3b82f6", "Flat": "#f59e0b", "Draw": "#9ca3af"})
    plt.title("Rozkład typu zakończenia gry (najwyższy budżet iteracji)")
    plt.xlabel("Udział")
    plt.ylabel("")
    plt.xlim(0, 1)
    plt.legend(loc="lower right")
    plt.tight_layout()
    plt.savefig(PLOTS_DIR / "result_type_breakdown.png", dpi=130)
    plt.close()
    print("  -> result_type_breakdown.png")


def plot_game_length_histogram(df: pd.DataFrame) -> None:
    """Histogram długości gier per rozmiar planszy."""
    boards = sorted(df["FileBoard"].unique())
    fig, axes = plt.subplots(1, len(boards), figsize=(5 * len(boards), 4), sharey=False)
    if len(boards) == 1:
        axes = [axes]

    for ax, board in zip(axes, boards):
        sub = df[df["FileBoard"] == board]
        ax.hist(sub["Moves"], bins=20, color="#3b82f6", edgecolor="white", alpha=0.85)
        ax.axvline(sub["Moves"].mean(), color="red", linestyle="--",
                   label=f"średnia: {sub['Moves'].mean():.1f}")
        ax.set_title(f"Plansza {board}×{board}")
        ax.set_xlabel("Liczba ruchów w grze")
        ax.set_ylabel("Liczba gier")
        ax.legend()
        ax.grid(linestyle=":", alpha=0.6)

    fig.suptitle("Rozkład długości gier", fontsize=13)
    plt.tight_layout()
    plt.savefig(PLOTS_DIR / "game_length_histogram.png", dpi=130)
    plt.close()
    print("  -> game_length_histogram.png")


def plot_summary_table(table: pd.DataFrame) -> None:
    """Zapisuje też tabelę podsumowującą jako CSV (do raportu)."""
    out_csv = PLOTS_DIR / "summary_table.csv"
    table.to_csv(out_csv, index=False)
    print(f"  -> summary_table.csv ({len(table)} wierszy)")


def main() -> None:
    sns.set_style("whitegrid")
    print(f"Wczytywanie CSV z {SWEEP_DIR} ...")
    df = load_all()
    df = add_perspective_columns(df)

    print("\nAgregacja win rate ...")
    table = winrate_table(df)

    print("\nGeneruję wykresy do results/plots/:")
    plot_summary_table(table)
    plot_heatmap_per_board(table)
    plot_mcts_vs_heuristic_scaling(table)
    plot_move_time_vs_iterations(df)
    plot_result_type_breakdown(df)
    plot_game_length_histogram(df)

    print("\nGotowe.")


if __name__ == "__main__":
    main()
