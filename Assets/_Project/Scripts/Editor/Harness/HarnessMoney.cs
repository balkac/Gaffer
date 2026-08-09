using Gaffer.Application.Transfers;
using Gaffer.Common;
using UnityEngine;

namespace Gaffer.Editor.Harness
{
    /// <summary>
    /// Money as a dev-tool window writes it. Same exemption, and the same reason, as
    /// <see cref="HarnessLabels"/>: these windows never ship, so the words live in one <c>internal</c>
    /// place instead of being scattered as ad-hoc formatting across four files.
    /// </summary>
    internal static class HarnessMoney
    {
        /// <summary>"€2.5M", "€12k", "€480", "-€1.2M" — short enough to sit on a button.</summary>
        internal static string Format(long value)
        {
            if (value < 0)
            {
                return "-" + Format(-value);
            }

            if (value >= 1_000_000)
            {
                return "€" + (value / 1_000_000.0).ToString("0.0") + "M";
            }

            if (value >= 1_000)
            {
                return "€" + (value / 1_000) + "k";
            }

            return "€" + value;
        }

        /// <summary>
        /// The same money with the sign always written: "+€12k", "-€340k", "€0". A drama preview lives or
        /// dies on which way the money goes, and <see cref="Format"/> writes a gain as a bare "€12k" — the
        /// exact vagueness the decision card was complained about for.
        /// </summary>
        internal static string Signed(long value)
        {
            return value > 0 ? "+" + Format(value) : Format(value);
        }
    }

    /// <summary>
    /// Whether a prospect fits the books right now, and which budget says no.
    ///
    /// <para><b>This does not decide anything.</b> <see cref="TransferService.Sign"/> owns the rule and
    /// still answers the click — the fee must fit the transfer cash <em>and</em> the wage must fit the
    /// weekly wage headroom, the CM 01/02 two-budget model. This mirrors the same two comparisons so a
    /// row can say <em>before</em> the click what the click would answer. Without it a manager sitting on
    /// plenty of cash reads "Sign €2.5M", clicks, and only then learns the wage bill was the problem —
    /// the defect the owner hit ("bazen çok cashim olmasına rağmen oyuncu alamıyorum").</para>
    /// </summary>
    internal readonly struct SigningVerdict
    {
        private SigningVerdict(long fee, long weeklyWage, long cashLeft, long wageRoomLeft)
        {
            Fee = fee;
            WeeklyWage = weeklyWage;
            CashLeft = cashLeft;
            WageRoomLeft = wageRoomLeft;
        }

        /// <summary>What signing him would cost, and what it would leave behind on each budget.</summary>
        internal static SigningVerdict For(long fee, long weeklyWage, Finances finances)
        {
            return new SigningVerdict(fee, weeklyWage, finances.Cash - fee, finances.WageHeadroom - weeklyWage);
        }

        internal long Fee { get; }

        internal long WeeklyWage { get; }

        /// <summary>Transfer cash after the fee. Negative is the shortfall.</summary>
        internal long CashLeft { get; }

        /// <summary>Weekly wage headroom after his wage. Negative is the shortfall.</summary>
        internal long WageRoomLeft { get; }

        // TransferService.Sign rejects on `fee > cash` and `wage > headroom`, which is exactly these two
        // being negative. Keep them in step if that rule ever moves.
        internal bool CashShort => CashLeft < 0;

        internal bool WageShort => WageRoomLeft < 0;

        internal bool Affordable => !CashShort && !WageShort;

        internal Color Tone => Affordable ? HarnessPalette.Win : HarnessPalette.Loss;

        /// <summary>Both numbers on the button — the fee you pay once and the wage you pay every week.</summary>
        internal string ActionLabel()
        {
            return "Sign " + HarnessMoney.Format(Fee) + "  ·  " + HarnessMoney.Format(WeeklyWage) + "/wk";
        }

        /// <summary>The row's one line on the books: which budget blocks him, or what he leaves you.</summary>
        internal string Sentence()
        {
            if (CashShort && WageShort)
            {
                return "Can't sign — " + HarnessMoney.Format(-CashLeft) + " short on cash and " +
                    HarnessMoney.Format(-WageRoomLeft) + "/wk short on wage room.";
            }

            if (CashShort)
            {
                return "Can't sign — cash: the fee is " + HarnessMoney.Format(Fee) + ", " +
                    HarnessMoney.Format(-CashLeft) + " more than you have.";
            }

            if (WageShort)
            {
                return "Can't sign — wage room: his " + HarnessMoney.Format(WeeklyWage) + "/wk needs " +
                    HarnessMoney.Format(-WageRoomLeft) + "/wk more free.";
            }

            return "Affordable — leaves " + HarnessMoney.Format(CashLeft) + " cash and " +
                HarnessMoney.Format(WageRoomLeft) + "/wk free.";
        }
    }

    /// <summary>
    /// What moving money between the two budgets would leave, written out for a button and the line under
    /// it.
    ///
    /// <para><b>This mirrors nothing.</b> Unlike <see cref="SigningVerdict"/>, which re-does the two
    /// comparisons <see cref="TransferService.Sign"/> makes, this is handed the core's own answer —
    /// <c>RunSession.PreviewWageBudgetShift</c> runs the very method the click runs, without committing —
    /// so the preview cannot drift from the rule, and a refusal is quoted in the same words the click
    /// would answer with. All that is left here is the wording, which is why it lives with the rest of
    /// the dev-tool exemption.</para>
    /// </summary>
    internal readonly struct BudgetShiftVerdict
    {
        private readonly Finances _before;
        private readonly Result<Finances> _preview;

        private BudgetShiftVerdict(long weeklyDelta, Finances before, Result<Finances> preview)
        {
            WeeklyDelta = weeklyDelta;
            _before = before;
            _preview = preview;
        }

        /// <summary>
        /// A shift of <paramref name="weeklyDelta"/> €/wk — positive buys ceiling with cash, negative
        /// gives ceiling up for cash — judged by <paramref name="preview"/>, the core's own verdict on it.
        /// </summary>
        internal static BudgetShiftVerdict For(long weeklyDelta, Finances before, Result<Finances> preview)
        {
            return new BudgetShiftVerdict(weeklyDelta, before, preview);
        }

        /// <summary>How much weekly ceiling would move, signed the way it moves the ceiling.</summary>
        internal long WeeklyDelta { get; }

        internal bool Allowed => _preview.IsSuccess;

        /// <summary>The cash the shift moves, from the core's answer. Zero when it is refused.</summary>
        internal long CashDelta => Allowed ? _preview.Value.Cash - _before.Cash : 0L;

        internal Color Tone => Allowed ? HarnessPalette.Muted : HarnessPalette.Loss;

        /// <summary>
        /// Both halves of the trade on the button: the ceiling that moves and the cash it is worth. A
        /// refused shift has no price worth quoting, so the button says only what was asked for and the
        /// line underneath says why it cannot happen.
        /// </summary>
        internal string ActionLabel()
        {
            long weekly = WeeklyDelta < 0 ? -WeeklyDelta : WeeklyDelta;
            string what = WeeklyDelta < 0
                ? "Free " + HarnessMoney.Format(weekly) + "/wk"
                : "Buy " + HarnessMoney.Format(weekly) + "/wk";

            return Allowed ? what + "  ·  " + HarnessMoney.Signed(CashDelta) : what;
        }

        /// <summary>The books after the trade — or the core's refusal, word for word.</summary>
        internal string Sentence()
        {
            if (!Allowed)
            {
                return _preview.Error;
            }

            Finances after = _preview.Value;
            return "Leaves " + HarnessMoney.Format(after.Cash) + " cash and a " +
                HarnessMoney.Format(after.WeeklyWageBudget) + "/wk ceiling — " +
                HarnessMoney.Format(after.WageHeadroom) + "/wk free for wages.";
        }
    }
}
