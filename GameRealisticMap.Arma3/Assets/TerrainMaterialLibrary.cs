﻿using System.Linq;
using System.Text.Json.Serialization;
using GameRealisticMap.Arma3.GameEngine.Materials;
using SixLabors.ImageSharp.PixelFormats;

namespace GameRealisticMap.Arma3.Assets
{
    public class TerrainMaterialLibrary
    {
        private readonly Dictionary<Rgb24, TerrainMaterial> indexByColor = new Dictionary<Rgb24, TerrainMaterial>();
        private readonly Dictionary<TerrainMaterialUsage, TerrainMaterial> indexByUsage = new Dictionary<TerrainMaterialUsage, TerrainMaterial>();
        private readonly List<SurfaceConfig> surfaces = new List<SurfaceConfig>();

        public const double DefaultTextureSizeInMeters = 4;

        public TerrainMaterialLibrary()
        {
            var none = new TerrainMaterial("", "", new Rgb24(), null);
            indexByColor[none.Id] = none;
            foreach(var id in Enum.GetValues<TerrainMaterialUsage>())
            {
                indexByUsage[id] = none;
            }
        }

        [JsonConstructor]
        public TerrainMaterialLibrary(List<TerrainMaterialDefinition> definitions, double textureSizeInMeters = DefaultTextureSizeInMeters)
        {
            foreach (var definition in definitions)
            {
                indexByColor.Add(definition.Material.Id, definition.Material);

                foreach (var usage in definition.Usages)
                {
                    // tolerate duplicates
                    indexByUsage[usage] = definition.Material;
                }
            }
            surfaces.AddRange(definitions.Select(s => s.Surface).Where(s => s != null)!);
            TextureSizeInMeters = textureSizeInMeters;
        }

        public double TextureSizeInMeters { get; } = DefaultTextureSizeInMeters;

        public TerrainMaterial GetMaterialById(Rgb24 id)
        {
            return indexByColor[id];
        }

        public TerrainMaterial GetMaterialByUsage(TerrainMaterialUsage usage)
        {
            if (indexByUsage.TryGetValue(usage, out var material))
            {
                return material;
            }
            if (usage == TerrainMaterialUsage.Default)
            {
                return indexByColor.Values.First();
            }
            if (usage == TerrainMaterialUsage.ScreeSurface)
            {
                return GetMaterialByUsage(TerrainMaterialUsage.RockGround);
            }
            return GetMaterialByUsage(TerrainMaterialUsage.Default);
        }

        public List<TerrainMaterialDefinition> Definitions => indexByColor.Values
            .Select(m => new TerrainMaterialDefinition(m, GetUsage(m), GetSurface(m)))
            .ToList();

        private TerrainMaterialUsage[] GetUsage(TerrainMaterial m)
        {
            return indexByUsage.Where(p => p.Value == m).Select(p => p.Key).ToArray();
        }

        private SurfaceConfig? GetSurface(TerrainMaterial m)
        {
            var name = Path.GetFileNameWithoutExtension(m.ColorTexture);
            return surfaces.FirstOrDefault(s => s.Match(name));
        }

        [JsonIgnore]
        public IEnumerable<SurfaceConfig> Surfaces => surfaces;
    }
}