﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameRealisticMap.Buildings;
using GeoJSON.Text.Feature;

namespace GameRealisticMap.Osm
{
    internal class CategoryAreaData : ITerrainData
    {
        public CategoryAreaData(List<CategoryArea> areas)
        {
            Areas = areas;
        }

        public List<CategoryArea> Areas { get; }

        public IEnumerable<Feature> ToGeoJson()
        {
            return Enumerable.Empty<Feature>();
        }
    }
}
