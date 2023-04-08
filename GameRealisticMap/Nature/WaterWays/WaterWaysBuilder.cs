﻿using GameRealisticMap.Geometries;
using GameRealisticMap.Nature.Lakes;
using GameRealisticMap.Reporting;
using GameRealisticMap.Roads;
using GeoAPI.Geometries;
using OsmSharp;
using OsmSharp.Tags;

namespace GameRealisticMap.Nature.WaterWays
{
    internal class WaterWaysBuilder : IDataBuilder<WaterWaysData>
    {
        private readonly IProgressSystem progress;

        public WaterWaysBuilder(IProgressSystem progress)
        {
            this.progress = progress;
        }

        private static WaterWayId? GetWaterwayPathTypeId(TagsCollectionBase tags)
        {
            if (tags.TryGetValue("waterway", out var waterway))
            {
                if (tags.ContainsKey("tunnel"))
                {
                    switch (waterway)
                    {
                        case "river":
                            return WaterWayId.RiverTunnel;

                        case "stream":
                            return WaterWayId.StreamTunnel;
                    }
                }
                else
                {
                    switch (waterway)
                    {
                        case "river":
                            return WaterWayId.River;

                        case "stream":
                            return WaterWayId.Stream;
                    }
                }
            }
            return null;
        }

        private static bool IsWaterwaySurface(TagsCollectionBase tags)
        {
            if (tags.TryGetValue("water", out var water))
            {
                switch (water)
                {
                    case "river":
                    case "stream":
                        return true;
                }
            }
            return false;
        }

        public WaterWaysData Build(IBuildContext context)
        {
            var lakesPolygons = context.GetData<LakesData>().Polygons;

            var waterwayNodes = context.OsmSource.All
                .Where(s => s.Tags != null && GetWaterwayPathTypeId(s.Tags) != null)
                .ToList();

            var waterwaysPaths = GetPaths(context, lakesPolygons, waterwayNodes);

            var polygons = GetSurface(context, lakesPolygons, waterwaysPaths);

            return new WaterWaysData(waterwaysPaths, polygons);
        }

        private List<TerrainPolygon> GetSurface(IBuildContext context, List<TerrainPolygon> lakesPolygons, List<WaterWay> waterwaysPaths)
        {
            var priority = lakesPolygons
                .Concat(context.GetData<RoadsData>().Roads.Where(r => r.RoadType != RoadTypeId.Trail && r.SpecialSegment != RoadSpecialSegment.Bridge).SelectMany(r => r.ClearPolygons))
                .ToList();

            var builder = new PolygonBuilder(progress, IsWaterwaySurface, priority);

            var surfaceOfWays = waterwaysPaths.Where(w => !w.IsTunnel).SelectMany(w => w.Polygons);

            return TerrainPolygon.MergeAll(builder.GetPolygons(context, surfaceOfWays));
        }

        private List<WaterWay> GetPaths(IBuildContext context, List<TerrainPolygon> lakesPolygons, List<OsmGeo> waterwayNodes)
        {
            var waterwaysPaths = new List<WaterWay>();
            using (var report = progress.CreateStep("Paths", waterwayNodes.Count))
            {
                foreach (var way in waterwayNodes)
                {
                    var kind = GetWaterwayPathTypeId(way.Tags);
                    if (kind != null)
                    {
                        foreach (var segment in context.OsmSource.Interpret(way)
                                                        .OfType<ILineString>()
                                                        .Where(l => !l.IsClosed)
                                                        .SelectMany(geometry => TerrainPath.FromGeometry(geometry, context.Area.LatLngToTerrainPoint))
                                                        .SelectMany(path => path.ClippedBy(context.Area.TerrainBounds))
                                                        .SelectMany(p => p.SubstractAll(lakesPolygons)))
                        {
                            waterwaysPaths.Add(new WaterWay(segment, kind.Value));
                        }
                    }
                    report.ReportOneDone();
                }
            }
            return waterwaysPaths;
        }
    }
}
