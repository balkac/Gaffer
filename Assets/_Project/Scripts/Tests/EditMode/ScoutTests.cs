using Gaffer.Application.Transfers;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class ScoutTests
    {
        private static Player Prospect(byte potential)
        {
            var attributes = new Attributes
            {
                Finishing = 62,
                Pace = 71,
                Technique = 58,
                Positioning = 66,
                Dribbling = 54,
            };
            return new Player(new PlayerId(9), "Test Prospect", "England", Position.Forward, 18, attributes, potential);
        }

        // Same sheet, a caller-chosen id and potential — the id is what salts the band's offset, so a
        // test about the offset needs to vary it.
        private static Player ProspectWithId(int id, byte potential)
        {
            var attributes = new Attributes
            {
                Finishing = 62,
                Pace = 71,
                Technique = 58,
                Positioning = 66,
                Dribbling = 54,
            };
            return new Player(new PlayerId(id), "Test Prospect", "England", Position.Forward, 18, attributes, potential);
        }

        [Test]
        public void Observe_FullAccuracy_IsExact()
        {
            Player player = Prospect(88);

            ScoutReport report = new Scout().Observe(player, 1.0);

            Assert.That(report.PotentialLow, Is.EqualTo(88));
            Assert.That(report.PotentialHigh, Is.EqualTo(88));
            foreach (AttributeEstimate estimate in report.KeyAttributes)
            {
                Assert.That(estimate.Low, Is.EqualTo(estimate.High), "A fully scouted attribute is a single value.");
            }
        }

        [Test]
        public void Observe_LowAccuracy_BracketsTheTruthStrictly_AndNeverCentresOnIt()
        {
            // `low <= truth <= high` is unfalsifiable: Band() ends with `if (low > truth) low = truth;`
            // and `if (high < truth) high = truth;`, so containment is clamped into existence no matter
            // how broken the offset is. The clamp can only ever produce a bound EQUAL to the truth, so
            // asserting a STRICT bracket is what actually tests the offset — it is the one outcome the
            // safety net cannot fake.
            //
            // And the property the doc actually sells is the opposite of centring: "the band is not
            // centred on the truth (a deterministic per-player offset keeps the midpoint from giving the
            // value away)". A mask whose midpoint is the truth hands the manager the number the fog
            // exists to hide, and the old assertion passed happily on exactly that mask. So: the midpoint
            // must miss, in both directions across the player population, and never by so much that the
            // truth escapes the band.
            var scout = new Scout();
            const int truth = 50;
            int offCentre = 0;
            int above = 0;
            int below = 0;

            for (int id = 1; id <= 60; id++)
            {
                ScoutReport report = scout.Observe(ProspectWithId(id, truth), 0.0);

                Assert.That(report.PotentialLow, Is.LessThan(truth),
                    $"player {id}: the low bound sits ON the truth, so the containment clamp fired");
                Assert.That(report.PotentialHigh, Is.GreaterThan(truth),
                    $"player {id}: the high bound sits ON the truth, so the containment clamp fired");

                double midpoint = (report.PotentialLow + report.PotentialHigh) / 2.0;
                if (System.Math.Abs(midpoint - truth) > 0.5)
                {
                    offCentre++;
                }

                if (midpoint > truth + 0.5)
                {
                    above++;
                }
                else if (midpoint < truth - 0.5)
                {
                    below++;
                }
            }

            Assert.That(offCentre, Is.GreaterThan(45),
                "The band is centred on the truth for most players — the midpoint gives the value away.");
            Assert.That(above, Is.GreaterThan(5), "The nudge only ever goes one way; that is a bias, not a mask.");
            Assert.That(below, Is.GreaterThan(5), "The nudge only ever goes one way; that is a bias, not a mask.");
        }

        [Test]
        public void Observe_KeyAttribute_CarriesItsLocalizationKeyAndBracketsItsValue()
        {
            // Finishing is the forward's first key attribute; its true value is 62. The label is the
            // attribute's localization key, never an English abbreviation — a scout report is core data
            // that reaches the player (NON-NEGOTIABLE #8).
            ScoutReport report = new Scout().Observe(Prospect(88), 0.0);

            AttributeEstimate finishing = report.KeyAttributes[0];
            Assert.That(finishing.LabelKey, Is.EqualTo("attr.finishing.abbrev"));
            Assert.That(finishing.Low, Is.LessThan(62), "clamped onto the truth instead of bracketing it");
            Assert.That(finishing.High, Is.GreaterThan(62), "clamped onto the truth instead of bracketing it");
        }

        [Test]
        public void Observe_HigherAccuracy_NarrowsTheBand()
        {
            Player player = Prospect(80);
            var scout = new Scout();

            ScoutReport vague = scout.Observe(player, 0.2);
            ScoutReport sharp = scout.Observe(player, 0.9);

            int vagueWidth = vague.PotentialHigh - vague.PotentialLow;
            int sharpWidth = sharp.PotentialHigh - sharp.PotentialLow;
            Assert.That(sharpWidth, Is.LessThan(vagueWidth));
        }

        [Test]
        public void Observe_WithOtherPlayersScoutedInBetween_ReturnsTheSameReportAndDoesNotAliasIt()
        {
            // Calling Observe twice in a row with the same arguments and asserting the answers match
            // tested nothing: Scout takes no IRandom and holds no state, so it was f(x) == f(x). The
            // contract that CAN break is the one its neighbour LineupSelector already breaks on purpose —
            // returning a buffer that the next call overwrites. Scout hands back a fresh list today, and
            // "a report is stable frame to frame" is only true while it does. So: scout other players in
            // between, then check both that the answer is unchanged AND that the first report was not
            // rewritten underneath its holder.
            var scout = new Scout();
            Player player = Prospect(75);

            ScoutReport first = scout.Observe(player, 0.4);
            int firstLow = first.PotentialLow;
            int firstHigh = first.PotentialHigh;
            var firstAttributeLows = new int[first.KeyAttributes.Count];
            for (int i = 0; i < first.KeyAttributes.Count; i++)
            {
                firstAttributeLows[i] = first.KeyAttributes[i].Low;
            }

            for (int id = 100; id < 120; id++)
            {
                scout.Observe(ProspectWithId(id, 60), 0.7);
            }

            ScoutReport again = scout.Observe(player, 0.4);

            Assert.That(again.PotentialLow, Is.EqualTo(firstLow));
            Assert.That(again.PotentialHigh, Is.EqualTo(firstHigh));
            Assert.That(first.PotentialLow, Is.EqualTo(firstLow), "the earlier report was rewritten by later ones");
            Assert.That(first.KeyAttributes, Is.Not.SameAs(again.KeyAttributes),
                "two reports share one estimate list, so holding a report is unsafe");
            for (int i = 0; i < firstAttributeLows.Length; i++)
            {
                Assert.That(first.KeyAttributes[i].Low, Is.EqualTo(firstAttributeLows[i]));
                Assert.That(again.KeyAttributes[i].Low, Is.EqualTo(firstAttributeLows[i]));
            }
        }

        [Test]
        public void Observe_TwoPlayersWithIdenticalSheets_GetDifferentBands()
        {
            // The offset is salted with the player id, which is what stops every report in a shortlist
            // from looking alike. Same attributes, same potential, same accuracy — different masks.
            var scout = new Scout();

            ScoutReport one = scout.Observe(ProspectWithId(4, 70), 0.3);
            ScoutReport other = scout.Observe(ProspectWithId(5, 70), 0.3);

            Assert.That(one.PotentialLow, Is.Not.EqualTo(other.PotentialLow).Or.Property("PotentialHigh").Not.EqualTo(other.PotentialHigh));
        }

        [Test]
        public void Observe_KeyAttributes_CoverTheRole()
        {
            Player player = Prospect(70);

            ScoutReport report = new Scout().Observe(player, 0.5);

            Assert.That(report.KeyAttributes.Count, Is.EqualTo(RoleKeyAttributes.For(Position.Forward).Count));
        }

        [Test]
        public void Observe_DefaultSettings_ReproduceTheHardcodedWidthsTheyReplaced()
        {
            // The mask's calibration used to be two literals inside Scout (22 / 12). Spelled out here,
            // not read from ScoutingSettings, so this is a real comparison and not a tautology.
            var theOldLiterals = new ScoutingSettings { PotentialMaxWidth = 22, AttributeMaxWidth = 12 };
            Player player = Prospect(66);

            ScoutReport shipped = new Scout().Observe(player, 0.0);
            ScoutReport old = new Scout(theOldLiterals).Observe(player, 0.0);

            Assert.That(shipped.PotentialLow, Is.EqualTo(old.PotentialLow));
            Assert.That(shipped.PotentialHigh, Is.EqualTo(old.PotentialHigh));
            for (int i = 0; i < shipped.KeyAttributes.Count; i++)
            {
                Assert.That(shipped.KeyAttributes[i].Low, Is.EqualTo(old.KeyAttributes[i].Low));
                Assert.That(shipped.KeyAttributes[i].High, Is.EqualTo(old.KeyAttributes[i].High));
            }
        }

        [Test]
        public void Observe_NarrowerSettings_ProduceANarrowerBand()
        {
            // Proves the settings are actually read rather than shadowed by the old constants.
            Player player = Prospect(66);

            ScoutReport wide = new Scout(ScoutingSettings.Default).Observe(player, 0.0);
            ScoutReport narrow = new Scout(new ScoutingSettings { PotentialMaxWidth = 4, AttributeMaxWidth = 2 })
                .Observe(player, 0.0);

            Assert.That(narrow.PotentialHigh - narrow.PotentialLow, Is.LessThan(wide.PotentialHigh - wide.PotentialLow));
        }
    }
}
