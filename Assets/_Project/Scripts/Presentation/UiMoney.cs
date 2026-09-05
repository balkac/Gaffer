using System.Globalization;
using Gaffer.Common.Localization;

namespace Gaffer.Presentation
{
    /// <summary>
    /// Money as a screen writes it: "€1.2M", "€850k", "€900". Three steps and one decimal, because a fee
    /// is compared against a budget at a glance and a manager does not read seven digits to do that.
    ///
    /// <para>The decimal mark follows the LOCALE — a Turkish screen writes "€1,2M" — which is why the
    /// formatter takes one and why it lives here rather than in the core: how a number is written is
    /// Presentation's business, exactly as which word to use is (NON-NEGOTIABLE #8). Framework-free so
    /// <c>dotnet test</c> can hold both spellings.</para>
    /// </summary>
    public static class UiMoney
    {
        public static string Format(long value, string locale)
        {
            if (value < 0)
            {
                return "-" + Format(-value, locale);
            }

            CultureInfo culture = CultureFor(locale);
            if (value >= 1_000_000)
            {
                return "€" + (value / 1_000_000.0).ToString("0.0", culture) + "M";
            }

            if (value >= 1_000)
            {
                return "€" + (value / 1_000).ToString(culture) + "k";
            }

            return "€" + value.ToString(culture);
        }

        /// <summary>Money in the locale these words are bound to.</summary>
        public static string Money(this LocalizedStrings text, long value)
        {
            return Format(value, text.Locale);
        }

        // A locale the runtime does not know falls back to the invariant spelling rather than throwing:
        // a wrong decimal mark is a blemish, a fee that will not draw is a screen that lost its numbers.
        private static CultureInfo CultureFor(string locale)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return CultureInfo.InvariantCulture;
            }

            try
            {
                return CultureInfo.GetCultureInfo(locale);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
