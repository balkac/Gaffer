using UnityEngine;

namespace Gaffer.Editor.Harness
{
    /// <summary>The ART_STYLE broadcast tokens as editor colours — hard-coded hex lives only here.</summary>
    internal static class HarnessPalette
    {
        public static readonly Color Pitch = Hex(0x0C1B1A);
        public static readonly Color PitchRaised = Hex(0x122624);
        public static readonly Color PitchLine = Hex(0x1E3733);
        public static readonly Color Chalk = Hex(0xEAF2EE);
        public static readonly Color Muted = Hex(0x7C938C);
        public static readonly Color Accent = Hex(0xFF2E7E);
        public static readonly Color Win = Hex(0x2FD48A);
        public static readonly Color Loss = Hex(0xFF5A4D);
        public static readonly Color Draw = Hex(0xE7B84B);

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }
    }
}
