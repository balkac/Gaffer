using System.Collections.Generic;
using Gaffer.Domain.Drama;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for the drama set a run plays with: a list of
    /// <see cref="DramaEventSO"/> assets mapped to the pure <see cref="DramaCatalog"/>.
    /// Config-as-override — empty (or no asset) means <see cref="DramaCatalog.Default"/>.
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

        public DramaCatalog ToCatalog()
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
