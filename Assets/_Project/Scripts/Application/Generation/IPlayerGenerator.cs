using Gaffer.Common;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Produces a player deterministically from a context and the injected rng (TDD §5). Players are
    /// always generated, never hand-authored — the story comes from the generator plus the memory
    /// layer, not from scripted characters.
    /// </summary>
    public interface IPlayerGenerator
    {
        Player Generate(PlayerId id, GenerationContext context, IRandom rng);
    }
}
