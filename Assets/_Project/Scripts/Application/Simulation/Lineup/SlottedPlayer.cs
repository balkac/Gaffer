using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// One line of a team sheet: a player, the slot he stands in, and the role that slot asks for.
    ///
    /// <para><b>Why the three travel together.</b> An eleven used to be passed around as a bare list of
    /// players "in slot order", with the formation's slot roles kept somewhere else and matched up by
    /// index. That holds exactly as long as every slot is filled — the first gap shifts every index after
    /// it, and the eleven silently starts being charged for the wrong positions. A pairing that cannot come
    /// apart is not a convenience here, it is the correctness argument.</para>
    ///
    /// <para>A struct, not a class: the sheet is rebuilt whenever the roster changes and read every match,
    /// and eleven of these are 16 bytes each plus a reference, not eleven allocations (PERFORMANCE §8).</para>
    /// </summary>
    public readonly struct SlottedPlayer
    {
        public SlottedPlayer(int slot, PlayerRole role, Player player)
        {
            Slot = slot;
            Role = role;
            Player = player;
        }

        /// <summary>Which slot on the team sheet — the index the manager placed him at.</summary>
        public int Slot { get; }

        /// <summary>The role that slot asks for, which is not necessarily the player's own.</summary>
        public PlayerRole Role { get; }

        /// <summary>The player standing there.</summary>
        public Player Player { get; }

        /// <summary>How badly he is out of position here (<see cref="PlayerRoles.FitFor"/>).</summary>
        public PositionalFit Fit => PlayerRoles.FitFor(Player.Role, Role);
    }
}
