﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using ArmaRealMap.Core.ObjectLibraries;
using GameRealisticMap.Geometries;
using ArmaRealMap.Libraries;
using ArmaRealMap.Osm;

namespace ArmaRealMap.TerrainData.DefaultAreas
{
    class DefaultAreasBuilder
    {
        public static void PrepareDefaultAreas(MapData data, List<OsmShape> shapes, ObjectLibraries olibs)
        {
            var filler = olibs.GetSingleLibrary(ObjectCategory.RandomVegetation);
            if (filler != null && filler.Objects.Count > 0)
            {
                var allPolys = shapes.Where(s => !s.Category.IsBuilding).SelectMany(s => s.TerrainPolygons).ToList();

                /*
                var area = TerrainPolygon.FromRectangle(data.MapInfos.P1, data.MapInfos.P2);
                

                var report = new ProgressReport("DefaultArea", allPolys.Count);
                var polygons = area.SubstractAll(allPolys, report.ReportOneDone).ToList();
                report.TaskDone();*/

                // Make chunks to make it easier for fill algorithm
                var list = new List<TerrainPolygon>();
                var step = data.MapInfos.Width / 4;

                var report = new ProgressReport($"DefaultArea", 16);

                for (int x = 0; x < data.MapInfos.Width; x += step)
                {
                    for (int y = 0; y < data.MapInfos.Height; y += step)
                    {
                        var p1 = data.MapInfos.P1 + new Vector2(x, y);
                        var p2 = p1 + new Vector2(step, step);
                        var clip = TerrainPolygon.FromRectangle(p1, p2);
                        //list.AddRange(polygons.SelectMany(p => p.ClippedBy(clip)));

                        var filter = allPolys.Where(o => GeometryHelper.EnveloppeIntersects(clip, o)).ToList();
                        list.AddRange(clip.SubstractAll(filter));
                        report.ReportOneDone();
                    }
                }
                report.TaskDone();


                var objects = new FillShapeWithObjectsBasic(data, ObjectCategory.RandomVegetation, olibs).Fill(list, "defaultfill.txt");

                Forests.NatureBuilder.RemoveOnBuildingsAndRoads(data, objects);

                objects.WriteFile(Path.Combine(data.Config.Target.Objects, "defaultfill.rel.txt"));
            }
        }

    }
}
