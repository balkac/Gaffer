using System;
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
