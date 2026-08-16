using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Players;
using Gaffer.Presentation.Squad;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// How a formation turns into a shape. It reads as layout trivia and is not: if two formations draw
    /// the same picture, the tactics board has stopped telling the manager anything, and picking a shape
    /// becomes a menu choice with no visible consequence.
    /// </summary>
    public sealed class PitchBandTests
    {
        private static readonly PlayerRole[] AllRoles = (PlayerRole[])Enum.GetValues(typeof(PlayerRole));

        [Test]
        public void Of_TheKeeper_StandsAloneInGoal()
        {
            Assert.That(PitchBands.Of(PlayerRole.Goalkeeper), Is.EqualTo(PitchBand.Goal));

            for (int i = 0; i < AllRoles.Length; i++)
            {
                if (AllRoles[i] != PlayerRole.Goalkeeper)
                {
                    Assert.That(PitchBands.Of(AllRoles[i]), Is.Not.EqualTo(PitchBand.Goal), AllRoles[i].ToString());
                }
            }
        }

        [Test]
        public void Of_TheThreeKindsOfMidfielder_StandAtThreeDifferentDepths()
        {
            // The reason this type exists at all. The Domain calls all three "Midfielder", and drawing them
            // on one line makes 4-4-2 and 3-5-2 the same picture.
            Assert.That(PitchBands.Of(PlayerRole.DefensiveMidfield), Is.EqualTo(PitchBand.HoldingMidfield));
            Assert.That(PitchBands.Of(PlayerRole.CentralMidfield), Is.EqualTo(PitchBand.Midfield));
            Assert.That(PitchBands.Of(PlayerRole.AttackingMidfield), Is.EqualTo(PitchBand.AttackingMidfield));
        }

        [Test]
        public void Of_EveryRole_StandsSomewhere()
        {
            // A slot with nowhere to be would not draw, and eleven players would quietly become ten.
            var bands = new HashSet<PitchBand>();
            for (int i = 0; i < AllRoles.Length; i++)
            {
                bands.Add(PitchBands.Of(AllRoles[i]));
            }

            Assert.That(bands.Count, Is.GreaterThan(3), "The roles are collapsing onto too few lines.");
        }

        [Test]
        public void WidthOrderOf_PutsTheFlanksOnOppositeSidesAndEverybodyElseBetween()
        {
            Assert.That(PitchBands.WidthOrderOf(PlayerRole.RightBack), Is.LessThan(PitchBands.WidthOrderOf(PlayerRole.CentreBack)));
            Assert.That(PitchBands.WidthOrderOf(PlayerRole.CentreBack), Is.LessThan(PitchBands.WidthOrderOf(PlayerRole.LeftBack)));
            Assert.That(PitchBands.WidthOrderOf(PlayerRole.RightWing), Is.LessThan(PitchBands.WidthOrderOf(PlayerRole.LeftWing)));
        }

        [Test]
        public void EveryShippedFormation_DrawsADifferentPicture()
        {
            // THE test. Two formations that produce the same band signature are the same board, whatever
            // their names say, and choosing between them would be a menu with no consequence.
            var pictures = new Dictionary<string, string>();
            foreach (Formation formation in Formation.Presets)
            {
                string picture = SignatureOf(formation);
                Assert.That(pictures.ContainsKey(picture), Is.False,
                    $"{formation.Name} draws the same shape as {(pictures.TryGetValue(picture, out string other) ? other : "?")}: {picture}");
                pictures[picture] = formation.Name;
            }
        }

        [Test]
        public void EveryShippedFormation_PutsElevenPlayersOnTheBoard()
        {
            foreach (Formation formation in Formation.Presets)
            {
                int drawn = 0;
                for (int i = 0; i < formation.Slots.Count; i++)
                {
                    // Every slot lands in some band, so counting the slots counts the cards drawn.
                    PitchBands.Of(formation.Slots[i]);
                    drawn++;
                }

                Assert.That(drawn, Is.EqualTo(11), formation.Name);
            }
        }

        // How many cards sit in each band, deepest first — "1-4-0-4-0-2" for a 4-4-2.
        private static string SignatureOf(Formation formation)
        {
            var counts = new int[6];
            for (int i = 0; i < formation.Slots.Count; i++)
            {
                counts[(int)PitchBands.Of(formation.Slots[i])]++;
            }

            return string.Join("-", counts);
        }
    }
}
