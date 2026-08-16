using System;
using System.Collections.Generic;
using Gaffer.Presentation.Squad;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// ART_STYLE §4.1's brightness ramp, pinned. It reads as a styling detail and is not: the palette has
    /// ONE accent and no second bright colour, so brightness is the only thing carrying "how good is this
    /// number" — and it only carries it while every screen bands a number the same way. An attribute cell,
    /// a role rating and a scout row that disagreed would teach the eye to stop trusting it.
    /// </summary>
    public sealed class AbilityBandTests
    {
        [Test]
        public void Of_EachThreshold_LandsInTheBandTheStyleGuideNames()
        {
            // The boundaries, exactly — an off-by-one here is invisible on screen and wrong everywhere.
            Assert.That(AbilityBands.Of(85.0), Is.EqualTo(AbilityBand.Elite), "85 is the accent step");
            Assert.That(AbilityBands.Of(84.9), Is.EqualTo(AbilityBand.Strong));
            Assert.That(AbilityBands.Of(70.0), Is.EqualTo(AbilityBand.Strong));
            Assert.That(AbilityBands.Of(69.9), Is.EqualTo(AbilityBand.Fair));
            Assert.That(AbilityBands.Of(55.0), Is.EqualTo(AbilityBand.Fair));
            Assert.That(AbilityBands.Of(54.9), Is.EqualTo(AbilityBand.Weak));
            Assert.That(AbilityBands.Of(40.0), Is.EqualTo(AbilityBand.Weak));
            Assert.That(AbilityBands.Of(39.9), Is.EqualTo(AbilityBand.Negligible));
        }

        [Test]
        public void Of_RisesMonotonicallyAcrossTheWholeScale()
        {
            // Whatever the thresholds are tuned to, a better number may never read as a quieter one.
            AbilityBand previous = AbilityBands.Of(0.0);
            for (int value = 1; value <= 100; value++)
            {
                AbilityBand band = AbilityBands.Of(value);
                Assert.That((int)band, Is.GreaterThanOrEqualTo((int)previous), $"value {value}");
                previous = band;
            }
        }

        [Test]
        public void Of_ValuesOutsideTheScale_StillLandSomewhere()
        {
            // A rating is 0-100 by construction, but a band is asked about numbers from several places and
            // must never have an answer it cannot give.
            Assert.That(AbilityBands.Of(-5.0), Is.EqualTo(AbilityBand.Negligible));
            Assert.That(AbilityBands.Of(140.0), Is.EqualTo(AbilityBand.Elite));
            Assert.That(AbilityBands.Of(double.NaN), Is.EqualTo(AbilityBand.Negligible),
                "NaN compares false against every threshold, so it must fall through to the quietest band "
                + "rather than to whichever comparison happens to be written first (CONVENTIONS §6).");
        }

        [Test]
        public void ClassOf_EveryBand_HasItsOwnStylesheetClass()
        {
            // A band added tomorrow that fell through the switch would draw an unstyled cell — visible
            // only as "that one looks wrong", which is the hardest kind of bug to be told about.
            var seen = new HashSet<string>();
            foreach (AbilityBand band in (AbilityBand[])Enum.GetValues(typeof(AbilityBand)))
            {
                string styleClass = AbilityBands.ClassOf(band);
                Assert.That(styleClass, Does.StartWith("value--"), band.ToString());
                Assert.That(seen.Add(styleClass), Is.True, $"'{styleClass}' is used by more than one band.");
            }
        }

        [Test]
        public void ClassOf_CarriesNoColour()
        {
            // The palette lives in the stylesheet. A class name that named a colour would put it in two
            // places and let a second bright accent in through the back door (ART_STYLE §3).
            foreach (AbilityBand band in (AbilityBand[])Enum.GetValues(typeof(AbilityBand)))
            {
                string styleClass = AbilityBands.ClassOf(band);
                Assert.That(styleClass, Does.Not.Contain("#"), band.ToString());
                Assert.That(styleClass, Does.Not.Contain("accent"),
                    $"{band} names the accent in its class; the stylesheet decides which band burns it.");
            }
        }
    }
}
