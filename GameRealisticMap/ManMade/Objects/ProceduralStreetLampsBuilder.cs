﻿using System.Numerics;
using GameRealisticMap.Geometries;
using GameRealisticMap.ManMade.Railways;
using GameRealisticMap.ManMade.Roads;
using GameRealisticMap.Reporting;

namespace GameRealisticMap.ManMade.Objects
{
    internal class ProceduralStreetLampsBuilder : IDataBuilder<ProceduralStreetLampsData>
    {
        internal static RoadTypeId[] RoadsWithLamps = new[] {
            RoadTypeId.TwoLanesPrimaryRoad,
            RoadTypeId.TwoLanesSecondaryRoad,
            RoadTypeId.TwoLanesConcreteRoad,
            RoadTypeId.SingleLaneConcreteRoad,
        };

        private readonly IProgressSystem progress;

        public ProceduralStreetLampsBuilder(IProgressSystem progress)
        {
            this.progress = progress;
        }

        public ProceduralStreetLampsData Build(IBuildContext context)
        {
            // Build Index of non-procedural elements
            var index = new SimpleSpacialIndex<IOrientedObject>(Vector2.Zero, new Vector2(context.Area.SizeInMeters));
            var nonProcedural = context.GetData<OrientedObjectData>().Objects.Where(o => o.TypeId == ObjectTypeId.StreetLamp).ToList();
            foreach(var o in nonProcedural)
            {
                index.Insert(o.Point.Vector, o);
            }

            var allRoads = context.GetData<RoadsData>().Roads;
            var roadsWithLamps = allRoads.Where(r => r.RoadTypeInfos.HasStreetLamp).OrderBy(r => r.RoadType).ToList();
            var lamps = new List<ProceduralStreetLamp>();

            var mask = allRoads.SelectMany(r => r.ClearPolygons).ToList();

            mask.AddRange(context.GetData<RailwaysData>().Railways.SelectMany(r => r.ClearPolygons));

            foreach (var road in roadsWithLamps.ProgressStep(progress, "Roads"))
            {
                var spacing = road.RoadTypeInfos.DistanceBetweenStreetLamps;
                var marginDistance = spacing / 2;
                var margin = new Vector2(marginDistance);

                var paths = road.Path.ToTerrainPolygon(road.ClearWidth + 0.1f)
                    .SelectMany(p => p.Holes.Concat(new[] { p.Shell }))
                    .Select(p => new TerrainPath(p))
                    .SelectMany(p => p.SubstractAll(mask))
                    .Where(p => p.Length > spacing)
                    .ToList();

                foreach(var path in paths)
                {
                    foreach (var point in GeometryHelper.PointsOnPathRegular(path.Points, spacing))
                    {
                        if (!index.Search(point.Vector - margin, point.Vector + margin).Any(o => (o.Point.Vector - point.Vector).Length() < marginDistance))
                        { 
                            var angle = GeometryHelper.GetFacing(point, new[] { road.Path }, spacing) ?? OrientedObjectBuilder.GetRandomAngle(point);
                            var lamp = new ProceduralStreetLamp(point, angle, road);
                            lamps.Add(lamp);
                            index.Insert(point.Vector, lamp);
                        }
                    }
                }
            }

            return new ProceduralStreetLampsData(lamps);
        }
    }
}
