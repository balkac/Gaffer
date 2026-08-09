using System;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Pins the numeric value of every enum member that crosses a persistence boundary — a drama
    /// <c>.asset</c> (Unity serializes an enum as its ordinal, with no by-name option) or a save file.
    /// Reordering or renumbering any of these silently rewrites content and player data that already exists,
    /// with no error at the moment it happens and no possible after-the-fact migration (UNITY.md §7,
    /// CONVENTIONS §6). There is nothing to detect that at runtime, so it is detected here instead: this file
    /// is the reason such a change fails loudly, and names the member that moved.
    /// <para>
    /// If a test here fails, the fix is almost never to update the expected number. Put the member back where
    /// it was and APPEND the new one — a value that has shipped belongs to the assets and saves that carry
    /// it, and is never reused.
    /// </para>
    /// </summary>
    public sealed class PersistedEnumValueTests
    {
        [Test]
        public void PlayerRole_MemberValues_AreThePinnedSaveContract()
        {
            // Saves up to schema v4 stored these numbers directly; the v4 -> v5 migration can only read them
            // because they are still exactly these numbers.
            Assert.That((int)PlayerRole.Goalkeeper, Is.EqualTo(0));
            Assert.That((int)PlayerRole.RightBack, Is.EqualTo(1));
            Assert.That((int)PlayerRole.CentreBack, Is.EqualTo(2));
            Assert.That((int)PlayerRole.LeftBack, Is.EqualTo(3));
            Assert.That((int)PlayerRole.DefensiveMidfield, Is.EqualTo(4));
            Assert.That((int)PlayerRole.CentralMidfield, Is.EqualTo(5));
            Assert.That((int)PlayerRole.AttackingMidfield, Is.EqualTo(6));
            Assert.That((int)PlayerRole.RightMidfield, Is.EqualTo(7));
            Assert.That((int)PlayerRole.LeftMidfield, Is.EqualTo(8));
            Assert.That((int)PlayerRole.RightWing, Is.EqualTo(9));
            Assert.That((int)PlayerRole.LeftWing, Is.EqualTo(10));
            Assert.That((int)PlayerRole.Striker, Is.EqualTo(11));

            AssertNoMemberWasAddedUnpinned(typeof(PlayerRole), 12);
        }

        [Test]
        public void Position_MemberValues_ArePinned()
        {
            Assert.That((int)Position.Goalkeeper, Is.EqualTo(0));
            Assert.That((int)Position.Defender, Is.EqualTo(1));
            Assert.That((int)Position.Midfielder, Is.EqualTo(2));
            Assert.That((int)Position.Forward, Is.EqualTo(3));

            AssertNoMemberWasAddedUnpinned(typeof(Position), 4);
        }

        /// <summary>
        /// The four tactical axes cross the save boundary from schema v6, BY NAME
        /// (<c>PersistedEnum</c>) — so what a save contains is <c>"VeryAttacking"</c>, and the numbers
        /// below are not themselves the wire format. They are pinned anyway, for two reasons that are
        /// specific rather than ceremonial: the SCALES the sim reads are arithmetic ON the ordinals
        /// (<c>MentalityScale</c> is <c>(int)Mentality - (int)Balanced</c>), so a reordered member silently
        /// re-balances every match rather than merely re-labelling a dropdown; and the count check is what
        /// catches a member appended without a thought for the saves that will meet it. If a member is ever
        /// added, APPEND it — the neutral value must keep its place in the middle for the scales to hold.
        /// </summary>
        [Test]
        public void TacticalAxes_MemberValues_ArePinned()
        {
            Assert.That((int)Mentality.VeryDefensive, Is.EqualTo(0));
            Assert.That((int)Mentality.Defensive, Is.EqualTo(1));
            Assert.That((int)Mentality.Balanced, Is.EqualTo(2));
            Assert.That((int)Mentality.Attacking, Is.EqualTo(3));
            Assert.That((int)Mentality.VeryAttacking, Is.EqualTo(4));
            AssertNoMemberWasAddedUnpinned(typeof(Mentality), 5);

            Assert.That((int)Tempo.Patient, Is.EqualTo(0));
            Assert.That((int)Tempo.Standard, Is.EqualTo(1));
            Assert.That((int)Tempo.Intense, Is.EqualTo(2));
            AssertNoMemberWasAddedUnpinned(typeof(Tempo), 3);

            Assert.That((int)Pressing.Contain, Is.EqualTo(0));
            Assert.That((int)Pressing.Standard, Is.EqualTo(1));
            Assert.That((int)Pressing.Press, Is.EqualTo(2));
            AssertNoMemberWasAddedUnpinned(typeof(Pressing), 3);

            Assert.That((int)Approach.Possession, Is.EqualTo(0));
            Assert.That((int)Approach.Balanced, Is.EqualTo(1));
            Assert.That((int)Approach.Counter, Is.EqualTo(2));
            AssertNoMemberWasAddedUnpinned(typeof(Approach), 3);
        }

        /// <summary>
        /// The names themselves, which from v6 ARE the wire format for these four. Spelled out as literals
        /// so that renaming a member fails here — at the commit that renames it — instead of on a player's
        /// device, where the axis would quietly read back as the neutral value.
        /// </summary>
        [Test]
        public void TacticalAxes_MemberNames_AreTheSaveContract()
        {
            Assert.That(Names(typeof(Mentality)), Is.EqualTo(
                new[] { "VeryDefensive", "Defensive", "Balanced", "Attacking", "VeryAttacking" }));
            Assert.That(Names(typeof(Tempo)), Is.EqualTo(new[] { "Patient", "Standard", "Intense" }));
            Assert.That(Names(typeof(Pressing)), Is.EqualTo(new[] { "Contain", "Standard", "Press" }));
            Assert.That(Names(typeof(Approach)), Is.EqualTo(new[] { "Possession", "Balanced", "Counter" }));
        }

        /// <summary>
        /// And <c>PlayerRole</c>'s names, for the same reason twice over: they are the save contract for a
        /// player's role (v5) AND, from v6, for every slot of a saved formation.
        /// </summary>
        [Test]
        public void PlayerRole_MemberNames_AreTheSaveContract()
        {
            Assert.That(Names(typeof(PlayerRole)), Is.EqualTo(new[]
            {
                "Goalkeeper", "RightBack", "CentreBack", "LeftBack", "DefensiveMidfield", "CentralMidfield",
                "AttackingMidfield", "RightMidfield", "LeftMidfield", "RightWing", "LeftWing", "Striker",
            }));
        }

        private static string[] Names(Type enumType)
        {
            Array values = Enum.GetValues(enumType);
            var names = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                names[i] = values.GetValue(i).ToString();
            }

            return names;
        }

        [Test]
        public void DramaEffectKind_MemberValues_AreTheAuthoredAssetContract()
        {
            // DramaEventSO.EffectDef.kind is serialized into every drama .asset as one of these numbers.
            Assert.That((int)DramaEffectKind.SubjectMorale, Is.EqualTo(0));
            Assert.That((int)DramaEffectKind.TeamMorale, Is.EqualTo(1));
            Assert.That((int)DramaEffectKind.Cash, Is.EqualTo(2));
            Assert.That((int)DramaEffectKind.CashFraction, Is.EqualTo(3));
            Assert.That((int)DramaEffectKind.SubjectWageFine, Is.EqualTo(4));
            Assert.That((int)DramaEffectKind.SellSubject, Is.EqualTo(5));
            Assert.That((int)DramaEffectKind.GrantTraitToSuccessor, Is.EqualTo(6));

            AssertNoMemberWasAddedUnpinned(typeof(DramaEffectKind), 7);
        }

        [Test]
        public void DramaCategory_MemberValues_AreTheAuthoredAssetContract()
        {
            Assert.That((int)DramaCategory.Personal, Is.EqualTo(0));
            Assert.That((int)DramaCategory.Institutional, Is.EqualTo(1));
            Assert.That((int)DramaCategory.FansMedia, Is.EqualTo(2));
            Assert.That((int)DramaCategory.Relationship, Is.EqualTo(3));
            Assert.That((int)DramaCategory.Rivalry, Is.EqualTo(4));

            AssertNoMemberWasAddedUnpinned(typeof(DramaCategory), 5);
        }

        [Test]
        public void MatchStakes_FlagValues_ArePinned()
        {
            // Flags: TraitSO stores these as separate bools rather than the enum, so no asset carries the
            // number today — but a flag value is a bit position, and reusing one is the same silent rewrite.
            Assert.That((int)MatchStakes.None, Is.EqualTo(0));
            Assert.That((int)MatchStakes.Derby, Is.EqualTo(1));
            Assert.That((int)MatchStakes.Rivalry, Is.EqualTo(2));
            Assert.That((int)MatchStakes.Final, Is.EqualTo(4));
            Assert.That((int)MatchStakes.TitleDecider, Is.EqualTo(8));
            Assert.That((int)MatchStakes.RelegationSixPointer, Is.EqualTo(16));
            Assert.That((int)MatchStakes.BigCrowd, Is.EqualTo(32));

            AssertNoMemberWasAddedUnpinned(typeof(MatchStakes), 7);
        }

        /// <summary>The count check is what makes the pinning complete: without it a member appended (or
        /// inserted, or deleted) after the last assertion would sail through untested, which is exactly the
        /// edit these tests exist to catch.</summary>
        private static void AssertNoMemberWasAddedUnpinned(Type enumType, int pinnedMemberCount)
        {
            Assert.That(
                Enum.GetValues(enumType).Length,
                Is.EqualTo(pinnedMemberCount),
                enumType.Name + " gained or lost a member. Pin the new one's value in this test too — and if a "
                + "member moved rather than being appended, put it back: its old number is already in shipped "
                + "assets and saves.");
        }
    }
}
