using System;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Scoring
{
    /// <summary>Owns the run's banked score. ComboManager is the only writer.</summary>
    public sealed class ScoreManager : MonoBehaviour
    {
        public ScoreLedger Ledger { get; } = new ScoreLedger();
        public event Action<long> TotalChanged;

        public void Bank(ComboResult result, string label)
        {
            Ledger.Bank(result, label);
            TotalChanged?.Invoke(Ledger.Total);
        }

        public void RecordBail(long lost) => Ledger.RecordBail(lost);

        public void ResetRun()
        {
            Ledger.Reset();
            TotalChanged?.Invoke(0);
        }
    }
}
