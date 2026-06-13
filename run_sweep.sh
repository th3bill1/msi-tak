#!/usr/bin/env bash
# Tournament sweep: runs all agent pairs across board sizes and iteration budgets.
# Idempotent — skips CSVs that already exist, so it's safe to re-run after Ctrl+C.

OUT_DIR="results/sweep"
mkdir -p "$OUT_DIR"

PROJECT="src/Tak.Experiments"
GAMES=20
SEED=42

# Pairs that don't use iteration budget (neither side is MCTS)
PAIRS_NO_ITER=(
  "random:heuristic"
)

# Pairs that use iteration budget (at least one side is MCTS)
PAIRS_WITH_ITER=(
  "random:uct"
  "random:rave"
  "random:pw"
  "heuristic:uct"
  "heuristic:rave"
  "heuristic:pw"
  "uct:rave"
  "uct:pw"
  "rave:pw"
)

ITERS=(1000 2000 5000)
BOARDS=(4 5 6)

# Pre-count total tournaments for progress display
TOTAL=$(( ${#BOARDS[@]} * (${#PAIRS_NO_ITER[@]} + ${#PAIRS_WITH_ITER[@]} * ${#ITERS[@]}) ))
DONE=0
RAN=0
SKIPPED=0
START=$(date +%s)

echo "============================================================"
echo "Tournament sweep started at $(date)"
echo "Total tournaments: $TOTAL"
echo "Games per tournament: $GAMES"
echo "Output dir: $OUT_DIR"
echo "============================================================"

run_one() {
  local board=$1
  local a=$2
  local b=$3
  local iter=$4   # may be empty
  local label
  local out

  if [ -n "$iter" ]; then
    out="$OUT_DIR/b${board}_${a}_vs_${b}_iter${iter}.csv"
    label="${a} vs ${b} on ${board}x${board}, iter=${iter}"
  else
    out="$OUT_DIR/b${board}_${a}_vs_${b}.csv"
    label="${a} vs ${b} on ${board}x${board}"
  fi

  DONE=$((DONE + 1))

  if [ -f "$out" ]; then
    SKIPPED=$((SKIPPED + 1))
    echo "[$DONE/$TOTAL] $(date +%H:%M:%S)  SKIP (exists): $out"
    return
  fi

  RAN=$((RAN + 1))
  local t0=$(date +%s)
  echo "[$DONE/$TOTAL] $(date +%H:%M:%S)  RUN: $label"

  local cmd=(dotnet run --project "$PROJECT" -c Release --
             --board-size "$board" --agent-a "$a" --agent-b "$b"
             --games "$GAMES" --seed "$SEED" --output "$out")
  if [ -n "$iter" ]; then
    cmd+=(--iterations "$iter")
  fi

  "${cmd[@]}" 2>&1 | grep -E "(SUMMARY|Agent A|Agent B|Win rate|Total duration)" || true

  local t1=$(date +%s)
  echo "         done in $((t1 - t0))s — output: $out"
}

# Order: smallest board first, then small iter to large iter — fastest results show first
for BOARD in "${BOARDS[@]}"; do
  echo
  echo ">>> Board ${BOARD}x${BOARD} <<<"

  # Non-MCTS pairs (one tournament each)
  for PAIR in "${PAIRS_NO_ITER[@]}"; do
    A=${PAIR%:*}
    B=${PAIR#*:}
    run_one "$BOARD" "$A" "$B" ""
  done

  # MCTS pairs across iteration budgets
  for ITER in "${ITERS[@]}"; do
    for PAIR in "${PAIRS_WITH_ITER[@]}"; do
      A=${PAIR%:*}
      B=${PAIR#*:}
      run_one "$BOARD" "$A" "$B" "$ITER"
    done
  done
done

END=$(date +%s)
TOTAL_SEC=$((END - START))
H=$((TOTAL_SEC / 3600))
M=$(((TOTAL_SEC % 3600) / 60))
S=$((TOTAL_SEC % 60))

echo
echo "============================================================"
echo "Sweep finished at $(date)"
echo "Ran: $RAN | Skipped (already done): $SKIPPED | Total: $DONE"
printf "Elapsed: %02d:%02d:%02d\n" "$H" "$M" "$S"
echo "============================================================"
