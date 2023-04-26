﻿using GameRealisticMap.Buildings;
using GameRealisticMap.ElevationModel;
using GameRealisticMap.Geometries;
using GameRealisticMap.ManMade;
using GameRealisticMap.ManMade.Farmlands;
using GameRealisticMap.ManMade.Railways;
using GameRealisticMap.Nature.Forests;
using GameRealisticMap.Nature.RockAreas;
using GameRealisticMap.Nature.Scrubs;
using GameRealisticMap.Nature.Surfaces;
using GameRealisticMap.Reporting;
using GameRealisticMap.Roads;

namespace GameRealisticMap.Nature
{
    internal abstract class BasicRadialBuilder<TEdge,TSource> : IDataBuilder<TEdge>
        where TEdge : class, IBasicTerrainData
        where TSource : class, IBasicTerrainData
    {
        private readonly IProgressSystem progress;
        private readonly float width;

        public BasicRadialBuilder(IProgressSystem progress, float width)
        {
            this.progress = progress;
            this.width = width;
        }
        protected abstract TEdge CreateWrapper(List<TerrainPolygon> polygons);

        protected virtual IEnumerable<TerrainPolygon> GetPriority(IBuildContext context)
        {
            return context.GetData<BuildingsData>().Buildings.Select(b => b.Box.Polygon)
                .Concat(context.GetData<RoadsData>().Roads.Where(r => r.RoadType != RoadTypeId.Trail).SelectMany(r => r.ClearPolygons))
                .Concat(context.GetData<RailwaysData>().Railways.SelectMany(r => r.ClearPolygons))
                .Concat(context.GetData<ElevationWithLakesData>().Lakes.Select(l => l.TerrainPolygon))
                .Concat(context.GetData<ForestData>().Polygons)
                .Concat(context.GetData<ScrubData>().Polygons)
                .Concat(context.GetData<RocksData>().Polygons)
                .Concat(context.GetData<MeadowsData>().Polygons)
                .Concat(context.GetData<FarmlandsData>().Polygons)
                .Concat(context.GetData<CategoryAreaData>().Areas.SelectMany(a => a.PolyList));
        }

        public TEdge Build(IBuildContext context)
        {
            var forest = context.GetData<TSource>();
            var priority = GetPriority(context);

            var radial = forest.Polygons
                .ProgressStep(progress, "Crown")
                .SelectMany(e => e.OuterCrown(width))
                .SelectMany(poly => poly.ClippedBy(context.Area.TerrainBounds))

                .ProgressStep(progress, "Priority")
                .SelectMany(l => l.SubstractAll(priority))
                .ToList();

            using var step = progress.CreateStep("Merge", 1);

            var final = TerrainPolygon.MergeAll(radial);

            return CreateWrapper(final);
        }
    }
}
