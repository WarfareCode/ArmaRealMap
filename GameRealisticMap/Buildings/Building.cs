﻿using GameRealisticMap.Geometries;

namespace GameRealisticMap.Buildings
{
    public class Building : ITerrainGeometry
    {
        public Building(BoundingBox box, BuildingTypeId value, List<TerrainPolygon> polygons)
        {
            Box = box;
            Value = value;
            Polygons = polygons;
        }

        public BoundingBox Box { get; }

        public BuildingTypeId Value { get; }

        public List<TerrainPolygon> Polygons { get; }

        public TerrainPoint MinPoint => Box.MinPoint;

        public TerrainPoint MaxPoint => Box.MaxPoint;
    }
}
