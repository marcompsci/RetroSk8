using System;

namespace RetroSk8.Core
{
    /// <summary>Run-level score: banked total, best combo, counts. One instance per run.</summary>
    public sealed class ScoreLedger
    {
        public long Total { get; private set; }
        public long BestCombo { get; private set; }
        public string BestComboLabel { get; private set; } = string.Empty;
        public int CombosBanked { get; private set; }
        public int Bails { get; private set; }
        public long PointsLostToBails { get; private set; }

        public void Bank(ComboResult result, string label = null)
        {
            if (result.Points <= 0) return;
            Total += result.Points;
            CombosBanked++;
            if (result.Points > BestCombo)
            {
                BestCombo = result.Points;
                BestComboLabel = label ?? string.Empty;
            }
        }

        public void RecordBail(long lostPoints)
        {
            Bails++;
            PointsLostToBails += Math.Max(0, lostPoints);
        }

        public int TapeTokensFor(ScoringConfig config)
        {
            if (config == null || config.pointsPerTapeToken <= 0) return 0;
            return (int)(Total / config.pointsPerTapeToken);
        }

        public void Reset()
        {
            Total = 0;
            BestCombo = 0;
            BestComboLabel = string.Empty;
            CombosBanked = 0;
            Bails = 0;
            PointsLostToBails = 0;
        }
    }
}
