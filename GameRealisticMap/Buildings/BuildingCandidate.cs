﻿using GameRealisticMap.Geometries;
using GeoAPI.Geometries;

namespace GameRealisticMap.Buildings
{
    internal class BuildingCandidate
    {
        public BuildingCandidate(TerrainPolygon polygon, BuildingTypeId? category)
        {
            Polygons = new List<TerrainPolygon>() { polygon };
            Box = BoundingBox.Compute(polygon.Shell.ToArray());
            Category = category;
        }

        public List<TerrainPolygon> Polygons { get; }

        public BoundingBox Box { get; set; }

        public IPolygon Poly => Box.Poly;

        public TerrainPolygon Polygon => Box.Polygon;

        public BuildingTypeId? Category { get; set; }

        public void Add(BuildingCandidate other)
        {
            Polygons.AddRange(other.Polygons);
            Box = Box.Add(other.Box);
            Category = Category ?? other.Category;
        }

        internal Building ToBuilding()
        {
            if (Category == null)
            {
                throw new InvalidOperationException();
            }
            return new Building(Box, Category.Value, Polygons);
        }
    }
}