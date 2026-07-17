using System.Collections.Generic;
using Gaffer.Domain.Traits;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for the trait set a run plays with: a list of <see cref="TraitSO"/>
    /// assets mapped to the pure <see cref="TraitCatalog"/>. Config-as-override — an empty list (or no
    /// asset at all) means <see cref="TraitCatalog.Default"/>, so the built-in calibrated set is always
    /// the floor and authored content replaces it wholesale.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Content/Trait Catalog", fileName = "TraitCatalog")]
    public sealed class TraitCatalogSO : ScriptableObject
    {
        [Tooltip("The traits of this run. Empty = the built-in default catalog.")]
        [SerializeField] private List<TraitSO> traits = new List<TraitSO>();

        public TraitCatalog ToCatalog()
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
