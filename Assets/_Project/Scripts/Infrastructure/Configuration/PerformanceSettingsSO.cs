using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for the runtime frame budget (PERFORMANCE §3). Tune it in the
    /// Inspector; <see cref="GameBootstrapper"/> applies it once at boot. Config-as-override — no asset
    /// means the fallback constants below, which mirror the shipped values.
    /// <para>
    /// This asset exists because §3's boot step is <b>not optional on mobile</b>: Unity's default
    /// <c>targetFrameRate</c> of -1 means the platform default, which on Android/iOS is a fixed 30 fps
    /// "to conserve battery power, independent of the native refresh rate", and mobile platforms ignore
    /// <c>vSyncCount</c> entirely. A build that skips the explicit set ships rock-steady 30 fps however
    /// light the frame is — which for a menu-driven manager game is the whole frame rate lost to a
    /// default nobody chose.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Balance/Performance", fileName = "PerformanceSettings")]
    public sealed class PerformanceSettingsSO : ScriptableObject
    {
        /// <summary>The rate applied when no asset is wired. Mirrors <see cref="targetFrameRate"/>.</summary>
        public const int DefaultTargetFrameRate = 60;

        /// <summary>The vsync count applied when no asset is wired. Mirrors <see cref="vSyncCount"/>.</summary>
        public const int DefaultVSyncCount = 0;

        /// <summary>
        /// The per-frame CPU budget in milliseconds, written down so systems can be held to it (§3).
        /// Not applied to the engine — it is the number a profiler session is judged against.
        /// </summary>
        public const double DefaultFrameBudgetMilliseconds = 11.0;

        [Tooltip("Frames per second to request at boot. The effective rate rounds DOWN to a divisor of the display refresh; iOS ProMotion (120 Hz) additionally needs the Xcode/Info.plist opt-in, so 60 is the ceiling without it.")]
        [Range(30, 120)] [SerializeField] private int targetFrameRate = DefaultTargetFrameRate;

        [Tooltip("QualitySettings.vSyncCount. Mobile platforms IGNORE this entirely — it is here for editor and desktop runs, where 0 lets targetFrameRate do the pacing.")]
        [Range(0, 4)] [SerializeField] private int vSyncCount = DefaultVSyncCount;

        [Tooltip("CPU budget per frame in ms, for profiling targets only — nothing reads it at runtime. 60 fps is 16.7 ms, but Unity's mobile guidance budgets ~65% of the frame and leaves the rest as thermal headroom.")]
        [Range(1.0f, 33.0f)] [SerializeField] private float frameBudgetMilliseconds = (float)DefaultFrameBudgetMilliseconds;

        /// <summary>Frames per second requested at boot.</summary>
        public int TargetFrameRate => targetFrameRate;

        /// <summary>The vsync count requested at boot. Ignored by mobile platforms.</summary>
        public int VSyncCount => vSyncCount;

        /// <summary>The per-frame CPU budget systems are held to, in milliseconds.</summary>
        public float FrameBudgetMilliseconds => frameBudgetMilliseconds;
    }
}
