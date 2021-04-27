﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using ArmaRealMap.Geometries;
using ArmaRealMap.Libraries;
using ArmaRealMap.TerrainBuilder;
using CoordinateSharp;
using NetTopologySuite.Geometries;
using OsmSharp;
using OsmSharp.Complete;
using OsmSharp.Db;
using OsmSharp.Db.Impl;
using OsmSharp.Geo;
using OsmSharp.Streams;
using OsmSharp.Tags;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SRTM;
using SRTM.Sources.NASA;

namespace ArmaRealMap
{
    class Program
    {
        private static readonly EagerLoad eagerUTM = new EagerLoad(false) { UTM_MGRS = true };

        static void Main(string[] args)
        {
            var config = JsonSerializer.Deserialize<Config>(File.ReadAllText("config.json"));

            Trace.Listeners.Clear();
            Trace.Listeners.Add(new TextWriterTraceListener(@"osm.log"));
            Trace.WriteLine("----------------------------------------------------------------------------------------------------");

            var olibs = new ObjectLibraries();
            olibs.Load(config);

            var size = config.GridSize;
            var cellSize = config.CellSize;

            var startPointMGRS = new MilitaryGridReferenceSystem(config.BottomLeft.GridZone, config.BottomLeft.D, config.BottomLeft.E, config.BottomLeft.N);

            var area = GetArea(startPointMGRS, size, cellSize);

            //BuildImage(area);

            //BuildElevationGrid(area);

            BuildLand(config, area, olibs);

            Trace.WriteLine("----------------------------------------------------------------------------------------------------");
            Trace.Flush();
        }

        private static void BuildLand(Config config,AreaInfos area, ObjectLibraries olibs)
        {
            var startPointUTM = area.StartPointUTM;

            var left = (float)Math.Min(area.SouthWest.Longitude.ToDouble(), area.NorthWest.Longitude.ToDouble());
            var top = (float)Math.Max(area.NorthEast.Latitude.ToDouble(), area.NorthWest.Latitude.ToDouble());
            var right = (float)Math.Max(area.SouthEast.Longitude.ToDouble(), area.NorthEast.Longitude.ToDouble());
            var bottom = (float)Math.Min(area.SouthEast.Latitude.ToDouble(), area.SouthWest.Latitude.ToDouble());


            var usedObjects = new HashSet<string>();

            using (var fileStream = File.OpenRead(config.OSM))
            {
                Console.WriteLine("Loading OSM data...");
                var source = new PBFOsmStreamSource(fileStream);

                var db = new SnapshotDb(new MemorySnapshotDb(source));

                Console.WriteLine("Filter OSM data...");
                var filtered = source.FilterBox(left, top, right, bottom, true);

                Console.WriteLine("Processing...");

                PlaceIsolatedTrees(area, olibs, usedObjects, filtered);

                var toRender = GetShapes(db, filtered);

                PlaceBuildings(area, olibs, usedObjects, toRender);

                //DrawShapes(area, startPointUTM, toRender);
            }


            var libs = olibs.TerrainBuilder.Libraries.Where(l => usedObjects.Any(o => l.Template.Any(t => t.Name==o))).Distinct().ToList();
            File.WriteAllLines("required_tml.txt", libs.Select(t => t.Name));
        }

        private static void PlaceIsolatedTrees(AreaInfos area, ObjectLibraries olibs, HashSet<string> usedObjects, OsmStreamSource filtered)
        {
            var treeModels = olibs.TerrainBuilder.Libraries.FirstOrDefault(l => l.Name == "enoch_veg_tree");

            var result = new StringBuilder();

            var trees = filtered.Where(o => o.Type == OsmGeoType.Node && Get(o.Tags, "natural") == "tree").ToList();
            foreach (var tree in trees)
            {
                var pos = ToTerrainBuilderPoint(area.StartPointUTM, (Node)tree);  
                if (area.IsInside(pos))
                {
                    var random = new Random((int)Math.Truncate(pos.X + pos.Y));
                    var obj = treeModels.Template[random.Next(0, treeModels.Template.Count)];
                    result.AppendFormat(CultureInfo.InvariantCulture, @"""{0}"";{1:0.000};{2:0.000};{3:0.000};0.0;0.0;1;0.0;",
                    obj.Name,
                    pos.X,
                    pos.Y,
                    random.NextDouble() * 360.0
                    );
                    usedObjects.Add(obj.Name);
                    result.AppendLine();
                }
            }
            File.WriteAllText("trees.txt", result.ToString());
        }

        private static void PlaceBuildings(AreaInfos area, ObjectLibraries olibs, HashSet<string> usedObjects, List<CategorizedGeometry> toRender)
        {
            var result = new StringBuilder();
            var buildings = toRender.Count(b => b.Category.IsBuilding);
            var metas = toRender.Where(b => Category.BuildingCategorizers.Contains(b.Category)).ToList();
            var done = 0;
            foreach (var building in toRender.Where(b => b.Category.IsBuilding))
            {
                if (done % 100 == 0)
                {
                    Console.WriteLine($"Placing ... {Math.Round(done * 100.0 / buildings, 2)}% done");
                }

                if (building.BuildingCategory == null)
                {
                    building.BuildingCategory = metas.Where(m => m.Geometry.Contains(building.Geometry)).FirstOrDefault()?.BuildingCategory ?? BuildingCategory.Residential;
                }

                var points = ToTerrainBuilderPoints(area.StartPointUTM, building.Geometry.Coordinates).ToArray();


                done++;

                if (points.Any(p => !area.IsInside(p)))
                {
                    continue;
                }

                //var box = BoundingBox.Compute(ToPixelsPoints(startPointUTM, area.Height, building.Geometry.Coordinates).ToArray());
                var box = BoundingBox.Compute(points);

                var candidates = olibs.Libraries
                    .Where(l => l.Category == building.BuildingCategory)
                    .SelectMany(l => l.Objects.Where(o => o.Fits(box, 0.75f, 1.15f)))
                    .ToList()
                    .OrderByDescending(c => c.Surface)
                    .Take(5)
                    .ToList();

                if (candidates.Count > 0)
                {
                    var random = new Random((int)Math.Truncate(box.Center.X + box.Center.Y));
                    var obj = candidates[random.Next(0, candidates.Count)];

                    var delta = obj.RotateToFit(box, 0.75f, 1.15f);
                    if (delta == 0.0f)
                    {
                        result.AppendFormat(CultureInfo.InvariantCulture, @"""{0}"";{1:0.000};{2:0.000};{3:0.000};0.0;0.0;1;0.0;",
                            obj.Name,
                            box.Center.X + obj.CX,
                            box.Center.Y + obj.CY,
                            -box.Angle + delta
                            );
                        result.AppendLine();
                        usedObjects.Add(obj.Name);
                    }
                }
                else
                {
                    Trace.WriteLine($"Nothing fits {building.BuildingCategory} {box.Width} x {box.Height}");
                }
            }
            File.WriteAllText("buildings.txt", result.ToString());
        }

        private static List<CategorizedGeometry> GetShapes(SnapshotDb db, OsmStreamSource filtered)
        {
            var toRender = new List<CategorizedGeometry>();

            var interpret = new DefaultFeatureInterpreter2();
            var list = filtered.Where(osmGeo =>
            (osmGeo.Type == OsmSharp.OsmGeoType.Way || osmGeo.Type == OsmSharp.OsmGeoType.Relation)
            && osmGeo.Tags != null).ToList();

            foreach (OsmGeo osmGeo in list)
            {
                var category = GetCategory(osmGeo.Tags, interpret);
                if (category != null)
                {
                    var complete = osmGeo.CreateComplete(db);
                    var count = 0;
                    foreach (var feature in interpret.Interpret(complete))
                    {
                        toRender.Add(new CategorizedGeometry(category, osmGeo, feature.Geometry));
                        count++;
                    }
                    if (count == 0)
                    {

                    }
                }
            }

            return toRender;
        }

        private static void DrawShapes(AreaInfos area, UniversalTransverseMercator startPointUTM, List<CategorizedGeometry> toRender)
        {
            var shapes = toRender.Count(b => !b.Category.IsBuilding);

            using (var img = new Image<Rgb24>(area.Size * area.CellSize, area.Size * area.CellSize, Color.LightGreen))
            {
                var done = 0;
                foreach (var item in toRender.Where(b => !b.Category.IsBuilding).OrderByDescending(e => e.Category.GroundTexturePriority))
                {
                    if (done % 100 == 0)
                    {
                        Console.WriteLine($"Drawing ... {Math.Round(done * 100.0 / shapes, 2)}% done");
                    }
                    DrawGeometry(startPointUTM, img, new SolidBrush(item.Category.GroundTextureColorCode), item.Geometry);
                    done++;
                }
                img.Save("osm.png");
            }
        }

        private static string Get(TagsCollectionBase tags, string key)
        {
            string value;
            if (tags != null && tags.TryGetValue(key, out value))
            {
                return value;
            }
            return null;
        }

        private static void DrawGeometry(UniversalTransverseMercator startPointUTM, Image<Rgb24> img, IBrush solidBrush, Geometry geometry)
        {
            if (geometry.OgcGeometryType == OgcGeometryType.MultiPolygon)
            {
                foreach (var geo in ((GeometryCollection)geometry).Geometries)
                {
                    DrawGeometry(startPointUTM, img, solidBrush, geo);
                }
            }
            else if (geometry.OgcGeometryType == OgcGeometryType.Polygon)
            {
                var poly = (Polygon)geometry;
                // TODO : holes
                var points = ToPixelsPoints(startPointUTM, img, poly.Shell.Coordinates).ToArray();
                try
                {
                    img.Mutate(p => p.FillPolygon(solidBrush, points));
                }
                catch
                {

                }
            }
            else if (geometry.OgcGeometryType == OgcGeometryType.LineString)
            {
                var line = (LineString)geometry;
                var points = ToPixelsPoints(startPointUTM, img, line.Coordinates).ToArray();
                try
                {
                    if (line.IsClosed)
                    {
                        img.Mutate(p => p.FillPolygon(solidBrush, points));
                    }
                    else
                    {
                        img.Mutate(p => p.DrawLines(solidBrush, 6.0f, points));
                    }
                }
                catch
                {

                }
            }
            else
            {
                Console.WriteLine(geometry.OgcGeometryType);
            }
        }



        private static Category GetCategory(TagsCollectionBase tags, FeatureInterpreter interpreter)
        {
            if ( tags.ContainsKey("water") || (tags.ContainsKey("waterway") && !tags.IsFalse("waterway")))
            {
                return Category.Water;
            }
            if (tags.ContainsKey("building") && !tags.IsFalse("building"))
            {
                switch(Get(tags, "building"))
                {
                    case "church": 
                        return Category.BuildingChurch;
                }
                if (Get(tags, "historic") == "fort")
                {
                    return Category.BuildingHistoricalFort;
                }
                if (tags.ContainsKey("brand"))
                {
                    return Category.BuildingRetail;
                }
                return Category.Building;
            }

            if (Get(tags, "type") == "boundary")
            {
                return null;
            }

            switch (Get(tags, "surface"))
            {
                case "grass": return Category.Grass;
                case "sand": return Category.Sand;
                case "concrete": return Category.Concrete;
            }

            switch (Get(tags, "landuse"))
            { 
                case "forest": return Category.Forest;
                case "grass": return Category.Grass;
                case "farmland": return Category.FarmLand;
                case "farmyard": return Category.FarmLand;
                case "vineyard": return Category.FarmLand;
                case "orchard": return Category.FarmLand;
                case "meadow": return Category.FarmLand;
                case "industrial": return Category.Industrial;
                case "residential": return Category.Residential;
                case "cemetery": return Category.Concrete;
                case "railway": return Category.Concrete;
                case "retail": return Category.Retail;

                case "basin": return Category.Water;
                case "reservoir": return Category.Water;
                case "allotments": return Category.Grass;
                case "military": return Category.Military;
            }

            switch (Get(tags, "natural"))
            { 
                case "wood": return Category.Forest;
                case "water": return Category.Water;
                case "grass": return Category.Grass;
                case "heath": return Category.Grass;
                case "grassland": return Category.Grass;
                case "scrub": return Category.Grass;
                case "wetland": return Category.WetLand;
                case "tree_row": return Category.Forest;
                case "scree": return Category.Sand;
                case "sand": return Category.Sand;
                case "beach": return Category.Sand;
            }


            if (interpreter.IsPotentiallyArea(tags))
            {
                tags.RemoveKey("source");
                tags.RemoveKey("name");
                tags.RemoveKey("alt_name");
                Trace.WriteLine(tags);
                //Console.WriteLine(tags);
            }
            return null;
        }

        private static IEnumerable<PointF> ToPixelsPoints(UniversalTransverseMercator startPointUTM, Image<Rgb24> img, IEnumerable<NetTopologySuite.Geometries.Coordinate> nodes)
        {
            return ToPixelsPoints(startPointUTM, img.Height, nodes);
        }

        private static IEnumerable<PointF> ToPixelsPoints(UniversalTransverseMercator startPointUTM, float height, IEnumerable<NetTopologySuite.Geometries.Coordinate> nodes)
        {
            return nodes
                .Select(n => new CoordinateSharp.Coordinate(n.Y, n.X, eagerUTM).UTM)
                .Select(u => new PointF(
                    (float)(u.Easting - startPointUTM.Easting),
                    (float)height - (float)(u.Northing - startPointUTM.Northing)
                ));
        }


        private static IEnumerable<PointF> ToTerrainBuilderPoints(UniversalTransverseMercator startPointUTM, IEnumerable<NetTopologySuite.Geometries.Coordinate> nodes)
        {
            return nodes
                .Select(n => new CoordinateSharp.Coordinate(n.Y, n.X, eagerUTM).UTM)
                .Select(u => new PointF(
                    (float)u.Easting,
                    (float)u.Northing
                ));
        }
        private static PointF ToTerrainBuilderPoint(UniversalTransverseMercator startPointUTM, Node node)
        {
            var coord = new CoordinateSharp.Coordinate(node.Latitude.Value, node.Longitude.Value, eagerUTM).UTM;

            return new PointF(
                    (float)coord.Easting,
                    (float)coord.Northing
                );
        }

        private static AreaInfos GetArea(MilitaryGridReferenceSystem startPointMGRS, int size, int cellSize)
        {
            var southWest = MilitaryGridReferenceSystem.MGRStoLatLong(startPointMGRS);

            var startPointUTM = new UniversalTransverseMercator(
                southWest.UTM.LatZone,
                southWest.UTM.LongZone,
                Math.Round(southWest.UTM.Easting),
                Math.Round(southWest.UTM.Northing));

            var southEast = UniversalTransverseMercator.ConvertUTMtoLatLong(new UniversalTransverseMercator(
                southWest.UTM.LatZone,
                southWest.UTM.LongZone,
                Math.Round(southWest.UTM.Easting) + (size * cellSize),
                Math.Round(southWest.UTM.Northing)));

            var northEast = UniversalTransverseMercator.ConvertUTMtoLatLong(new UniversalTransverseMercator(
                southWest.UTM.LatZone,
                southWest.UTM.LongZone,
                Math.Round(southWest.UTM.Easting) + (size * cellSize),
                Math.Round(southWest.UTM.Northing) + (size * cellSize)));

            var northWest = UniversalTransverseMercator.ConvertUTMtoLatLong(new UniversalTransverseMercator(
                southWest.UTM.LatZone,
                southWest.UTM.LongZone,
                Math.Round(southWest.UTM.Easting),
                Math.Round(southWest.UTM.Northing) + (size * cellSize)));

            return new AreaInfos
            {
                StartPointMGRS = startPointMGRS,
                StartPointUTM = startPointUTM,
                SouthWest = southWest,
                NorthEast = northEast,
                NorthWest = northWest,
                SouthEast = southEast,
                CellSize = cellSize,
                Size = size
            };
        }
    }
}
