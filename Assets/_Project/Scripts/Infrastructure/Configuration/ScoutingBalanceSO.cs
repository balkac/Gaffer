using System;
using Gaffer.Application.Transfers;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for the scouting mask's calibration (NON-NEGOTIABLE #3): how wide a
    /// completely unscouted band is, for the hidden-potential estimate and for each key attribute. These
    /// two numbers are the discovery fantasy's dial — widen them and a gem is indistinguishable from an
    /// ordinary teenager until you have watched him, narrow them and the punt stops being a punt (TDD §5).
    /// Tune it in the Inspector and <see cref="ToSettings"/> maps to the pure
    /// <see cref="ScoutingSettings"/>. Config-as-override — no asset means
    /// <see cref="ScoutingSettings.Default"/>. Defaults mirror it.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Balance/Scouting", fileName = "ScoutingBalance")]
    public sealed class ScoutingBalanceSO : ScriptableObject
    {
        [Tooltip("Half-width of a fully unscouted (accuracy 0) hidden-potential band, in rating points. Wider = a gem hides longer.")]
        [Range(0, 50)] [SerializeField] private int potentialMaxWidth = 22;

        [Tooltip("Half-width of a fully unscouted (accuracy 0) attribute band, in rating points.")]
        [Range(0, 50)] [SerializeField] private int attributeMaxWidth = 12;

        private void OnValidate()
        {
            ClampToValidRanges();
        }

        public ScoutingSettings ToSettings()
        {
            ClampToValidRanges();
            return new ScoutingSettings(
                potentialMaxWidth: potentialMaxWidth,
                attributeMaxWidth: attributeMaxWidth);
        }

        /// <summary>
        /// Bounds the fields the way <c>[Range]</c> already does in the Inspector, but from the mapper too
        /// (UNITY.md §8): the attribute constrains what a human can drag, not an asset serialised before a
        /// band was tightened, and a negative half-width would invert <c>Scout</c>'s band so the estimate
        /// no longer brackets the truth — the one invariant the mask has. Both bands are far wider than
        /// the calibrated values they hold.
        /// </summary>
        private void ClampToValidRanges()
        {
            potentialMaxWidth = Math.Clamp(potentialMaxWidth, 0, 50);
            attributeMaxWidth = Math.Clamp(attributeMaxWidth, 0, 50);
        }
    }
}
