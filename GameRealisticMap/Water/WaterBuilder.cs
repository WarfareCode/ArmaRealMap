﻿using GameRealisticMap.Geometries;
using GameRealisticMap.Reporting;
using GameRealisticMap.Roads;
using GeoAPI.Geometries;
using OsmSharp.Tags;

namespace GameRealisticMap.Water
{
    internal class WaterBuilder : IDataBuilder<WaterData>
    {
        private readonly IProgressSystem progress;

        public WaterBuilder(IProgressSystem progress)
        {
            this.progress = progress;
        }

        private static bool IsWaterWay(TagsCollectionBase tags)
        {
            return ((tags.ContainsKey("waterway") && !tags.IsFalse("waterway")) || tags.GetValue("water") == "stream") 
                && !tags.ContainsKey("tunnel");
        }

        private static bool IsLake(TagsCollectionBase tags)
        {
            var water = tags.GetValue("water");
            if (!string.IsNullOrEmpty(water))
            {
                return water == "lake" || water == "pond";
            }
            return tags.GetValue("natural") == "water";
        }

        public WaterData Build(IBuildContext context)
        {
            var minimalArea = Math.Pow(5 * context.Area.GridCellSize, 2); // 5 x 5 nodes minimum
            var minimalOffsetArea = context.Area.GridCellSize * context.Area.GridCellSize;
            var embankmentMargin = 2.5f * context.Area.GridCellSize;

            var roadsData = context.GetData<RoadsData>();

            var embankmentsPolygons = roadsData.Roads
                .Where(r => r.SpecialSegment == RoadSpecialSegment.Embankment)
                .SelectMany(s => s.Path.ToTerrainPolygon(s.Width + embankmentMargin))
                .ToList();

            var lakesPolygons = context.OsmSource.All
                .Where(s => s.Tags != null && IsLake(s.Tags))

                .ProgressStep(progress, "Interpret")
                .SelectMany(s => context.OsmSource.Interpret(s))
                .SelectMany(s => TerrainPolygon.FromGeometry(s, context.Area.LatLngToTerrainPoint))

                .ProgressStep(progress, "Crop")
                .SelectMany(poly => poly.ClippedBy(context.Area.TerrainBounds))

                .ProgressStep(progress, "Embankment")
                .SelectMany(l => l.SubstractAll(embankmentsPolygons))
                .ToList();

            // Split in a specific builder ???

            var waterWaysPaths = context.OsmSource.All
                .Where(s => s.Tags != null && IsWaterWay(s.Tags)) // XXX: Compute width ? (from type)

                .ProgressStep(progress, "Interpret")
                .SelectMany(s => context.OsmSource.Interpret(s))
                .OfType<ILineString>() // XXX: Include IMultiLineString ?
                .Where(l => !l.IsClosed)
                .SelectMany(geometry => TerrainPath.FromGeometry(geometry, context.Area.LatLngToTerrainPoint))

                .ProgressStep(progress, "Crop")
                .SelectMany(path => path.ClippedBy(context.Area.TerrainBounds))

                .ProgressStep(progress, "Lakes")
                .SelectMany(p => p.SubstractAll(lakesPolygons)) 
                .Where(w => w.Length > 10f) // XXX: Merge ? (before the length filter)
                .ToList();

            return new WaterData(lakesPolygons, waterWaysPaths);
        }
    }
}
