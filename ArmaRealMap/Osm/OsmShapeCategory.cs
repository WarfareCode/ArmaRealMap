﻿using ArmaRealMap.GroundTextureDetails;
using SixLabors.ImageSharp;

namespace ArmaRealMap.Osm
{
    class OsmShapeCategory
    {
        internal readonly TerrainMaterial TerrainMaterial;
        internal Color GroundTextureColorCode => TerrainMaterial.Color;
        internal readonly int GroundTexturePriority;
        internal readonly ObjectCategory? BuildingType;

        public OsmShapeCategory Parent { get; }

        public OsmShapeCategory(TerrainMaterial material, int priority, ObjectCategory? buildingCategory = null)
        {
            TerrainMaterial = material;
            GroundTexturePriority = priority;
            BuildingType = buildingCategory;
        }

        public OsmShapeCategory(OsmShapeCategory parent, ObjectCategory? buildingCategory)
        {
            TerrainMaterial = parent.TerrainMaterial;
            GroundTexturePriority = parent.GroundTexturePriority;
            BuildingType = buildingCategory;
            Parent = parent;
        }
        public OsmShapeCategory(OsmShapeCategory parent, int priority, ObjectCategory? buildingCategory)
        {
            TerrainMaterial = parent.TerrainMaterial;
            GroundTexturePriority = priority;
            BuildingType = buildingCategory;
            Parent = parent;
        }

        internal static readonly OsmShapeCategory Water = new OsmShapeCategory(TerrainMaterial.WetLand, 1);
        internal static readonly OsmShapeCategory Forest = new OsmShapeCategory(TerrainMaterial.Forest, 4);
        internal static readonly OsmShapeCategory WetLand = new OsmShapeCategory(TerrainMaterial.WetLand, 2);
        internal static readonly OsmShapeCategory Grass = new OsmShapeCategory(TerrainMaterial.GrassShort, 3);
        internal static readonly OsmShapeCategory FarmLand = new OsmShapeCategory(TerrainMaterial.FarmLand, 5);
        internal static readonly OsmShapeCategory Sand = new OsmShapeCategory(TerrainMaterial.Sand, 6);
        internal static readonly OsmShapeCategory Rocks = new OsmShapeCategory(TerrainMaterial.Rock, 7);
        internal static readonly OsmShapeCategory Concrete = new OsmShapeCategory(TerrainMaterial.Concrete, 9);
        internal static readonly OsmShapeCategory Dirt = new OsmShapeCategory(TerrainMaterial.Dirt, 8);
        internal static readonly OsmShapeCategory Military = new OsmShapeCategory(Grass, 10, ObjectCategory.Military);
        internal static readonly OsmShapeCategory Residential = new OsmShapeCategory(Grass, ObjectCategory.Residential);
        internal static readonly OsmShapeCategory Industrial = new OsmShapeCategory(Grass, ObjectCategory.Industrial);
        internal static readonly OsmShapeCategory Retail = new OsmShapeCategory(Grass, ObjectCategory.Retail);

        internal static readonly OsmShapeCategory[] BuildingCategorizers = new[] { Residential, Industrial, Retail, Military };

        internal static readonly OsmShapeCategory Building = new OsmShapeCategory(TerrainMaterial.Dirt, 0);
        internal static readonly OsmShapeCategory BuildingHistoricalFort = new OsmShapeCategory(Grass, ObjectCategory.HistoricalFort);
        internal static readonly OsmShapeCategory BuildingRetail = new OsmShapeCategory(Building, ObjectCategory.Retail);
        internal static readonly OsmShapeCategory BuildingChurch = new OsmShapeCategory(Building, ObjectCategory.Church);

        internal static readonly OsmShapeCategory Road = new OsmShapeCategory(TerrainMaterial.Dirt, 0);

        public bool IsBuilding
        {
            get
            {
                return this == Building || Parent == Building;
            }
        }
    }
}
