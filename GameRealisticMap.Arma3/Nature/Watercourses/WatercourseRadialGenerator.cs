﻿using GameRealisticMap.Arma3.Assets;
using GameRealisticMap.Nature.Watercourses;
using GameRealisticMap.Reporting;

namespace GameRealisticMap.Arma3.Nature.Watercourses
{
    internal class WatercourseRadialGenerator : ClusteredGeneratorBase<WatercourseRadialData>
    {
        public WatercourseRadialGenerator(IProgressSystem progress, IArma3RegionAssets assets)
            : base(progress, assets)
        {
        }

        protected override ClusterCollectionId Id => ClusterCollectionId.WatercourseRadial;
    }
}
