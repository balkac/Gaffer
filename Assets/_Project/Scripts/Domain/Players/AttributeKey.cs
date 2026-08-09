namespace Gaffer.Domain.Players
{
    /// <summary>
    /// One attribute a role leans on, as a display row: the attribute's identity plus a reader over an
    /// <see cref="Attributes"/> sheet, so a caller can show a role's key stats generically without a switch
    /// per attribute. Domain hands out the identity and a localization key; choosing the words is
    /// Presentation's job (NON-NEGOTIABLE #8, ARCHITECTURE §1).
    /// </summary>
    public readonly struct AttributeKey
    {
        public AttributeKey(PlayerAttribute attribute)
        {
            Attribute = attribute;
        }

        /// <summary>Which attribute this row shows — the identity every other layer keys off.</summary>
        public PlayerAttribute Attribute { get; }

        /// <summary>The localization key for the row's short label; the string table supplies the words.</summary>
        public string LabelKey => PlayerAttributes.GetLabelKey(Attribute);

        /// <summary>Reads this row's value off a sheet. <c>in</c> keeps the 32-byte struct from being copied.</summary>
        public byte Read(in Attributes attributes)
        {
            return attributes.ValueOf(Attribute);
        }
    }
}
