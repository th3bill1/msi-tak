namespace Tak.AI.Mcts;

/// <summary>RAVE (Rapid Action Value Estimation) / AMAF (All Moves As First) statistics</summary>
public class RaveData
{
    /// <summary>Number of RAVE visits for this action</summary>
    public int RaveVisits { get; set; } = 0;

    /// <summary>Number of RAVE wins for this action</summary>
    public double RaveWins { get; set; } = 0;

    /// <summary>Calculate RAVE value</summary>
    /// <returns>The average RAVE reward, or 0.5 before any visits.</returns>
    public double GetRaveValue() => RaveVisits > 0 ? RaveWins / RaveVisits : 0.5;

    /// <summary>Update RAVE statistics</summary>
    /// <param name="reward">The reward to add to the RAVE totals.</param>
    public void UpdateRave(double reward)
    {
        RaveVisits++;
        RaveWins += reward;
    }
}
