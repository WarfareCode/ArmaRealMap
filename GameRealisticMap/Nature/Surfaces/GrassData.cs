﻿using System.Text.Json.Serialization;
using GameRealisticMap.Geometries;
using GeoJSON.Text.Feature;

namespace GameRealisticMap.Nature.Surfaces
{
    public class GrassData : IBasicTerrainData
    {
        [JsonConstructor]
        public GrassData(List<TerrainPolygon> polygons)
        {
            Polygons = polygons;
        }

        public List<TerrainPolygon> Polygons { get; }

        public IEnumerable<Feature> ToGeoJson()
        {
            var properties = new Dictionary<string, object>() {
                {"type", "grass" }
            };
            return Polygons.Select(b => new Feature(b.ToGeoJson(), properties));
        }
    }
}
