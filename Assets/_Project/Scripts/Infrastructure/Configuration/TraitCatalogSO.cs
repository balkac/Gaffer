using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Domain.Traits;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for the trait set a run plays with: a list of <see cref="TraitSO"/>
    /// assets mapped to the pure <see cref="TraitCatalog"/>. Config-as-override — an empty list (or no
    /// asset at all) means <see cref="TraitCatalog.Default"/>, so the built-in calibrated set is always
    /// the floor and authored content replaces it wholesale (ARCHITECTURE §7).
    /// <para>
    /// STRICTNESS POSTURE (ARCHITECTURE §11), stated rather than defaulted: this catalog SHIPS INSIDE
    /// THE BUILD. Asset and reader are therefore atomic, the schema version is a fact about the
    /// document and NOT an acceptance policy — there is no range of versions to accept, because there
    /// is no way for an asset of another version to arrive. So <see cref="Load"/> is strict: a hole in
    /// the authored content fails the load with a named id, loudly enough for CI, instead of resolving
    /// to null at some later lookup and quietly costing the trait its mechanics (NON-NEGOTIABLE #7).
    /// The opposite posture is deliberately kept on the save path, where data outlives the build; see
    /// <see cref="TraitCatalog"/> for the pair written out in full. This is the setting that inverts
    /// the day content is downloaded rather than shipped.
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Content/Trait Catalog", fileName = "TraitCatalog")]
    public sealed class TraitCatalogSO : ScriptableObject
    {
        [Tooltip("The traits of this run. Empty = the built-in default catalog.")]
        [SerializeField] private List<TraitSO> traits = new List<TraitSO>();

        /// <summary>Points the catalog at a set of trait assets — used by the editor tooling when it
        /// materialises the built-in catalog.</summary>
        public void Author(List<TraitSO> assets)
        {
            traits = new List<TraitSO>(assets);
        }

        /// <summary>
        /// The validating entry point — VALIDATE ON LOAD, per the posture above. Maps the assets, then
        /// checks the result as shipped content: a missing slug or a duplicate id fails here, naming
        /// the offender, so the mistake surfaces at the composition root where a load can still be
        /// refused. Empty (or an all-null list) still falls back to <see cref="TraitCatalog.Default"/>
        /// — "author nothing" is a valid answer, unlike "author it wrong".
        /// </summary>
        public Result<TraitCatalog> Load()
        {
            TraitCatalog catalog = Map();
            Result validation = catalog.Validate();
            return validation.IsFailure
                ? Result<TraitCatalog>.Failure($"{name}: {validation.Error}")
                : Result<TraitCatalog>.Success(catalog);
        }

        /// <summary>Assets to the pure catalog, no validation — an empty or all-null list is the
        /// config-as-override fallback to the built-in set (ARCHITECTURE §7), because "author nothing"
        /// is a valid answer; "author it wrong" is what <see cref="Load"/> refuses.</summary>
        private TraitCatalog Map()
        {
            if (traits == null || traits.Count == 0)
            {
                return TraitCatalog.Default;
            }

            var mapped = new List<Trait>(traits.Count);
            foreach (TraitSO trait in traits)
            {
                if (trait != null)
                {
                    mapped.Add(trait.ToTrait());
                }
            }

            return mapped.Count == 0 ? TraitCatalog.Default : new TraitCatalog(mapped);
        }
    }
}
