using Gaffer.Infrastructure.Configuration;
using UnityEngine;

namespace Gaffer.Composition
{
    /// <summary>
    /// The composition root's boot step: it applies the runtime frame budget once, at startup, from a
    /// <see cref="PerformanceSettingsSO"/> so the numbers are tuned without touching code (PERFORMANCE §3).
    /// <para>
    /// It constructs nothing else on purpose. ARCHITECTURE §6 warns that a bootstrapper which grows an
    /// event handler "has stopped being a composition root and become a controller with a misleading
    /// name" — the object graph for a run belongs to the run session, and the matchday flow arrives with
    /// Presentation (Faz 7). Keep this file about process-wide startup facts only.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [Tooltip("Frame budget asset. Leave empty to boot on the PerformanceSettingsSO defaults — the values still get set explicitly, which is the point of the boot step.")]
        [SerializeField] private PerformanceSettingsSO _performance;

        private void Awake()
        {
            ApplyFrameBudget();
        }

        /// <summary>
        /// Sets the frame budget explicitly. Note the fully-qualified <c>UnityEngine.Application</c>: this
        /// project has its own <c>Gaffer.Application</c> layer, so the bare name binds to the namespace
        /// here and would not compile.
        /// </summary>
        private void ApplyFrameBudget()
        {
            // The SO is optional, but the SET is not (§3) — a missing asset falls back to the same shipped
            // constants rather than leaving the platform default of -1 in place, which on mobile means a
            // fixed 30 fps.
            int frameRate = _performance != null
                ? _performance.TargetFrameRate
                : PerformanceSettingsSO.DefaultTargetFrameRate;
            int vSync = _performance != null
                ? _performance.VSyncCount
                : PerformanceSettingsSO.DefaultVSyncCount;

            // Order matters on desktop/editor: vSyncCount != 0 makes targetFrameRate ineffective there, so
            // vsync is set first and the rate second. On mobile vSyncCount is ignored entirely.
            QualitySettings.vSyncCount = vSync;
            UnityEngine.Application.targetFrameRate = frameRate;
        }
    }
}
