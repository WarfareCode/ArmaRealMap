﻿using GameRealisticMap.Buildings;
using GameRealisticMap.Geometries;
using GameRealisticMap.Reporting;
using GameRealisticMap.Roads;

namespace GameRealisticMap.Nature.Forests
{
    internal class ForestEdgeBuilder : IDataBuilder<ForestEdgeData>
    {
        private readonly IProgressSystem progress;

        public ForestEdgeBuilder(IProgressSystem progress)
        {
            this.progress = progress;
        }

        public ForestEdgeData Build(IBuildContext context)
        {
            var roads = context.GetData<RoadsData>().Roads;
            var forests = context.GetData<ForestData>().Polygons;

            // Trails are ignored by BasicBuilderBase, but prevents forest edge effect
            // Buildings are not surrounded with bushed
            var priority = roads
                .Where(r => r.RoadType == RoadTypeId.Trail)
                .SelectMany(r => r.Path.ToTerrainPolygon(r.Width + 1))
                .Concat(context.GetData<BuildingsData>().Buildings.SelectMany(b => b.Box.Polygon.Offset(2.5f)))
                .ToList();

            using (var step = progress.CreateStep("Merge", 1))
            {
                forests = TerrainPolygon.MergeAll(forests); // XXX: Merge in BasicBuilderBase to rely only on clusters ?
            }

            // Ignore reallys smalls "forests", as it might have been used to map some isolated trees
            forests = forests.Where(f => f.Area > 200).ToList();

            var edges = 
                forests.ProgressStep(progress, "Edges")
                    .SelectMany(f => f.InnerCrown(2)) // 2m width offset

                    .ProgressStep(progress, "Priority")
                    .SelectMany(l => l.SubstractAll(priority))

                    .ToList();

            return new ForestEdgeData(edges, forests);
        }
    }
}
