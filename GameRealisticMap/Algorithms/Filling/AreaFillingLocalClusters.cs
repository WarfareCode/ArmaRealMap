﻿using System.Numerics;
using GameRealisticMap.Geometries;
using GameRealisticMap.Algorithms.Definitions;

namespace GameRealisticMap.Algorithms.Filling
{
    internal sealed class AreaFillingLocalClusters<TModelInfo> : AreaFillingClustersBase<TModelInfo>
    {
        private readonly IClusterCollectionDefinition<TModelInfo> definition;
        private SimpleSpacialIndex<IClusterDefinition<TModelInfo>>? map;

        public AreaFillingLocalClusters(AreaDefinition fillarea, IClusterCollectionDefinition<TModelInfo> definition) 
            : base(fillarea, definition, definition.Clusters)
        {
            this.definition = definition;
        }

        private static SimpleSpacialIndex<IClusterDefinition<TModelInfo>> CreateMap(double density, AreaDefinition fillarea, IClusterCollectionDefinition<TModelInfo> definition)
        {
            var size = fillarea.MaxPoint.Vector - fillarea.MinPoint.Vector;
            var clusterCount = Math.Max(density * size.X * size.Y, 100);
            var map = new SimpleSpacialIndex<IClusterDefinition<TModelInfo>>(fillarea.MinPoint.Vector, fillarea.MaxPoint.Vector);
            foreach (var cluster in definition.Clusters)
            {
                var count = clusterCount * cluster.Probability;
                for (int i = 0; i < count; ++i)
                {
                    map.Insert(fillarea.GetRandomPoint().Vector, cluster);
                }
            }

            return map;
        }

        protected override IReadOnlyList<IClusterDefinition<TModelInfo>> Search(Vector2 start, Vector2 end)
        {
            if ( map == null)
            {
                map = CreateMap(Density, area, definition);
            }
            return map.Search(start, end);
        }

        public override void Cleanup()
        {
            map = null;
            base.Cleanup();
        }

    }
}
