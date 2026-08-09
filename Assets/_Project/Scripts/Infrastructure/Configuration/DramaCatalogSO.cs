using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Traits;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for the drama set a run plays with: a list of
    /// <see cref="DramaEventSO"/> assets mapped to the pure <see cref="DramaCatalog"/>.
    /// Config-as-override — empty (or no asset) means <see cref="DramaCatalog.Default"/>.
    /// <para>
    /// STRICTNESS POSTURE (ARCHITECTURE §11), stated rather than defaulted: this content SHIPS INSIDE
    /// THE BUILD, so the asset and the code that reads it are atomic — the schema version is a fact
    /// about the document and there is NO ACCEPTANCE POLICY to tune, because no asset of another
    /// version can arrive. <see cref="Load"/> is therefore strict, and it is strict about the thing
    /// that has no other detector: every trait slug an event points at (the two bias lists, the
    /// trigger's required trait, a choice's trait grant) must exist in the trait catalog the same run
    /// loads. A typo there costs the trait its mechanics and leaves the copy intact, which is exactly
    /// the flavor-text failure NON-NEGOTIABLE #7 rejects, and the tolerant lookup can never report it.
    /// The save path keeps the opposite posture on purpose; see <see cref="TraitCatalog"/>. When
    /// drama content one day downloads instead of shipping, this is the choice that has to invert.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Content/Drama Catalog", fileName = "DramaCatalog")]
    public sealed class DramaCatalogSO : ScriptableObject
    {
        [Tooltip("The drama events of this run. Empty = the built-in default catalog.")]
        [SerializeField] private List<DramaEventSO> events = new List<DramaEventSO>();

        /// <summary>Points the catalog at a set of event assets — used by the editor tooling when it
        /// materialises the built-in catalog.</summary>
        public void Author(List<DramaEventSO> assets)
        {
            events = new List<DramaEventSO>(assets);
        }

        /// <summary>
        /// The validating entry point — VALIDATE ON LOAD, per the posture above. Takes the SAME trait
        /// catalog the run will play with (from <see cref="TraitCatalogSO.Load"/>), because a dangling
        /// slug is only dangling relative to a specific trait set: authored drama on the built-in
        /// traits and authored drama on authored traits are different questions. Failure names the
        /// event and the trait id, so the fix is the message.
        /// </summary>
        public Result<DramaCatalog> Load(TraitCatalog traits)
        {
            if (traits == null)
            {
                return Result<DramaCatalog>.Failure(
                    $"{name}: cannot be loaded without the trait catalog its events reference.");
            }

            DramaCatalog catalog = Map();
            Result validation = catalog.ValidateAgainst(traits);
            return validation.IsFailure
                ? Result<DramaCatalog>.Failure($"{name}: {validation.Error}")
                : Result<DramaCatalog>.Success(catalog);
        }

        /// <summary>Assets to the pure catalog, no validation — an empty or all-null list is the
        /// config-as-override fallback to the built-in set (ARCHITECTURE §7).</summary>
        private DramaCatalog Map()
        {
            if (events == null || events.Count == 0)
            {
                return DramaCatalog.Default;
            }

            var mapped = new List<DramaEvent>(events.Count);
            foreach (DramaEventSO dramaEvent in events)
            {
                if (dramaEvent != null)
                {
                    mapped.Add(dramaEvent.ToEvent());
                }
            }

            return mapped.Count == 0 ? DramaCatalog.Default : new DramaCatalog(mapped);
        }
    }
}
