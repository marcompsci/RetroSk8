using System.Collections.Generic;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Data
{
    /// <summary>A park's Spot Contract: three original goals completed inside one two-minute run.</summary>
    [CreateAssetMenu(menuName = "Retro Sk8/Spot Contract", fileName = "Contract_")]
    public sealed class ContractDefinition : ScriptableObject
    {
        public string locationId = "harbor_plaza";
        public string displayName = "Harbor Plaza Contract";
        public List<GoalDefinition> goals = new List<GoalDefinition>();
    }
}
