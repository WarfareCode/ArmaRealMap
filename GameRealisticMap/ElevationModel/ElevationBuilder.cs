﻿using System.Diagnostics;
using System.Numerics;
using GameRealisticMap.ElevationModel.Constrained;
using GameRealisticMap.Geometries;
using GameRealisticMap.Nature.Lakes;
using GameRealisticMap.Nature.WaterWays;
using GameRealisticMap.Reporting;
using GameRealisticMap.Roads;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace GameRealisticMap.ElevationModel
{
    internal class ElevationBuilder : IDataBuilder<ElevationData>
    {
        private readonly IProgressSystem progress;

        public ElevationBuilder(IProgressSystem progress)
        {
            this.progress = progress;
        }

        public ElevationData Build(IBuildContext context)
        {
            var raw = context.GetData<RawElevationData>();
            var roadsData = context.GetData<RoadsData>();
            var waterData = context.GetData<WaterWaysData>();
            var lakesData = context.GetData<LakesData>();

            var lakes = DigLakes(raw.RawElevation, lakesData, context.Area); // Split in a specific builder ???

            // Forced elevation by config

            // Forced elevation for airstrip

            var constraintGrid = new ElevationConstraintGrid(context.Area, raw.RawElevation, progress);

            ProcessRoads(constraintGrid, roadsData);

            ProcessWaterWays(constraintGrid, waterData);

            ProtectLakes(constraintGrid, lakes, context.Area);

            constraintGrid.SolveAndApplyOnGrid();

            return new ElevationData(constraintGrid.Grid, lakes);
        }

        private List<LakeWithElevation> DigLakes(ElevationGrid rawElevation, LakesData lakesData, ITerrainArea area)
        {
            using var report = progress.CreateStep("DigLakes", lakesData.Polygons.Count);

            var minimalArea = Math.Pow(5 * area.GridCellSize, 2); // 5 x 5 nodes minimum
            var minimalOffsetArea = area.GridCellSize * area.GridCellSize;
            var lakes = new List<LakeWithElevation>();
            var cellSize = new Vector2(area.GridCellSize, area.GridCellSize);
            foreach (var g in lakesData.Polygons)
            {
                if (g.Area < minimalArea)
                {
                    continue; // too small
                }
                var offsetArea = g.Offset(area.GridCellSize * -2f).Sum(a => a.Area);
                if (offsetArea < minimalOffsetArea)
                {
                    Trace.WriteLine($"Lake {g}");
                    Trace.WriteLine($"is in fact too small, {offsetArea} offseted -- {Math.Round(g.Area)} area");
                    continue; // too small
                }
                var oldBorderElevation = g.Shell.Min(p => rawElevation.ElevationAt(p));
                var lake = new LakeWithElevation(g, GeometryHelper.PointsOnPath(g.Shell).Min(p => rawElevation.ElevationAt(p)));
                var lakeElevation = rawElevation.PrepareToMutate(g.MinPoint - cellSize, g.MaxPoint + cellSize, lake.BorderElevation - 2.5f, lake.BorderElevation);
                lakeElevation.Image.Mutate(d =>
                {
                    PolygonDrawHelper.DrawPolygon(d, g, new SolidBrush(Color.FromRgba(255, 255, 255, 128)), lakeElevation.ToPixels);
                    foreach (var scaled in g.Offset(-10))
                    {
                        PolygonDrawHelper.DrawPolygon(d, scaled, new SolidBrush(Color.FromRgba(128, 128, 128, 192)), lakeElevation.ToPixels);
                    }
                    foreach (var scaled in g.Offset(-20))
                    {
                        PolygonDrawHelper.DrawPolygon(d, scaled, new SolidBrush(Color.FromRgba(0, 0, 0, 255)), lakeElevation.ToPixels);
                    }
                });
                lakeElevation.Apply();
                report.ReportOneDone();
                lakes.Add(lake);
            }
            return lakes;
        }

        private void ProcessRoads(ElevationConstraintGrid constraintGrid, RoadsData roadsData)
        {
            var roads = roadsData.Roads.Where(r => r.RoadType <= RoadTypeId.TwoLanesConcreteRoad).ToList();

            using var report = progress.CreateStep("Roads", roads.Count);

            foreach (var road in roads)
            {
                if (road.SpecialSegment == RoadSpecialSegment.Bridge)
                {
                    ProcessRoadBridge(road, constraintGrid);
                }
                else if (road.SpecialSegment == RoadSpecialSegment.Embankment)
                {
                    ProcessRoadEmbankment(constraintGrid, road);
                }
                else
                {
                    ProcessNormalRoad(constraintGrid, road);
                }
                report.ReportOneDone();
            }
        }

        private void ProcessWaterWays(ElevationConstraintGrid constraintGrid, WaterWaysData waterData)
        {
            var waterWaysPaths = waterData.WaterWaysPaths.Where(w => w.Length > 10f).ToList();
            using var report = progress.CreateStep("Waterways", waterWaysPaths.Count);
            foreach (var waterWay in waterWaysPaths)
            {
                var points = GeometryHelper.PointsOnPath(waterWay.Points, 2).Select(constraintGrid.NodeSoft).ToList();
                foreach (var segment in points.Take(points.Count - 1).Zip(points.Skip(1)))
                {
                    if (segment.First != segment.Second)
                    {
                        segment.Second.MustBeLowerThan(segment.First);
                        segment.First.WantedInitialRelativeElevation = -1f;
                        segment.First.LowerLimitRelativeElevation = -4f;
                    }
                }
                report.ReportOneDone();
            }
        }

        private void ProtectLakes(ElevationConstraintGrid constraintGrid, List<LakeWithElevation> lakes, ITerrainArea area)
        {
            using var report = progress.CreateStep("LakeLimit", lakes.Count);
            foreach (var lake in lakes)
            {
                foreach (var extended in lake.TerrainPolygon.Offset(2 * area.GridCellSize))
                {
                    foreach (var node in constraintGrid.Search(extended.MinPoint.Vector, extended.MaxPoint.Vector).Where(p => extended.Contains(p.Point)))
                    {
                        node.SetNotBelow(lake.BorderElevation);
                        node.IsProtected = true;
                    }
                }
                report.ReportOneDone();
            }
        }

        private void ProcessRoadEmbankment(ElevationConstraintGrid constraintGrid, Road road)
        {
            // pin start/stop, imposed smoothing as SRTM precision is too low for this kind of elevation detail
            var start = constraintGrid.NodeHard(road.Path.FirstPoint).PinToInitial();
            var stop = constraintGrid.NodeHard(road.Path.LastPoint).PinToInitial();
            var lengthFromStart = 0f;
            var points = GeometryHelper.PointsOnPath(road.Path.Points, 2).Select(constraintGrid.NodeHard).ToList();
            var totalLength = points.Take(points.Count - 1).Zip(points.Skip(1)).Sum(segment => (segment.Second.Point.Vector - segment.First.Point.Vector).Length());
            var smooth = constraintGrid.CreateSmoothSegment(start, road.Width * 4f);
            foreach (var segment in points.Take(points.Count - 1).Zip(points.Skip(1)))
            {
                if (segment.First != segment.Second)
                {
                    var delta = segment.Second.Point.Vector - segment.First.Point.Vector;
                    constraintGrid.AddFlatSegmentHard(segment.First, delta, road.Width);
                    if (segment.First.Elevation == null)
                    {
                        var elevation = start.Elevation.Value + ((stop.Elevation.Value - start.Elevation.Value) * (lengthFromStart / totalLength));
                        segment.First.SetElevation(elevation);
                    }
                    lengthFromStart += delta.Length();
                    smooth.Add(lengthFromStart, segment.Second);
                }
            }
        }
        private void ProcessNormalRoad(ElevationConstraintGrid constraintGrid, Road road)
        {
            var lengthFromStart = 0f;
            var points = GeometryHelper.PointsOnPath(road.Path.Points, 2).Select(constraintGrid.NodeHard).ToList();
            var smooth = constraintGrid.CreateSmoothSegment(constraintGrid.NodeHard(road.Path.FirstPoint), road.Width * 4f);
            foreach (var segment in points.Take(points.Count - 1).Zip(points.Skip(1)))
            {
                if (segment.First != segment.Second)
                {
                    var delta = segment.Second.Point.Vector - segment.First.Point.Vector;
                    constraintGrid.AddFlatSegmentHard(segment.First, delta, road.Width);
                    lengthFromStart += delta.Length();
                    smooth.Add(lengthFromStart, segment.Second);
                }
            }
        }

        private void ProcessRoadBridge(Road road, ElevationConstraintGrid constraintGrid)
        {
            constraintGrid.NodeHard(road.Path.FirstPoint).PinToInitial();
            constraintGrid.NodeHard(road.Path.LastPoint).PinToInitial();
        }

    }
}
