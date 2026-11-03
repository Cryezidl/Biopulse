using System.Collections.Generic;
using UnityEngine;

namespace Biopulse.Gameplay
{
    public sealed class GrabTarget : MonoBehaviour
    {
        internal static readonly List<GrabTarget> Active = new List<GrabTarget>();
        [SerializeField] private string stableId;
        public string StableId { get => stableId; set => stableId = value; }
        private void OnValidate() { if (string.IsNullOrEmpty(stableId)) stableId = System.Guid.NewGuid().ToString("N"); }
        private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
        private void OnDisable() => Active.Remove(this);
    }
}
