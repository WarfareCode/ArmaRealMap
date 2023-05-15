﻿using GameRealisticMap.Geometries;
using GameRealisticMap.Reporting;
using OsmSharp.Tags;

namespace GameRealisticMap.Nature
{
    internal abstract class PolygonBuilderBase
    {
        protected readonly IProgressSystem progress;

        public PolygonBuilderBase(IProgressSystem progress)
        {
            this.progress = progress;
        }

        protected abstract bool IsTargeted(TagsCollectionBase tags);

        protected abstract IEnumerable<TerrainPolygon> GetPriority(IBuildContext context);

        internal List<TerrainPolygon> GetPolygons(IBuildContext context, IEnumerable<TerrainPolygon> additionals)
        {
            var priority = GetPriority(context).ToList();

            var polygons = context.OsmSource.All
                .Where(s => s.Tags != null && IsTargeted(s.Tags))

                .ProgressStep(progress, "Interpret")
                .SelectMany(s => context.OsmSource.Interpret(s))
                .SelectMany(s => TerrainPolygon.FromGeometry(s, context.Area.LatLngToTerrainPoint))
                .Concat(additionals)

                .ProgressStep(progress, "Crop")
                .SelectMany(poly => poly.ClippedBy(context.Area.TerrainBounds))

                .RemoveOverlaps(progress, "Overlaps")

                .ProgressStep(progress, "Priority")
                .SelectMany(l => l.SubstractAll(priority))
                .ToList();

            return MergeIfRequired(polygons);
        }

        protected virtual List<TerrainPolygon> MergeIfRequired(List<TerrainPolygon> polygons)
        {
            using (var step = progress.CreateStepPercent("Merge"))
            {
                return TerrainPolygon.MergeAll(polygons, step);
            }
        }
    }
}
