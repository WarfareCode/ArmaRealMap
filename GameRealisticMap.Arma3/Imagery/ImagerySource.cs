﻿using GameRealisticMap.Arma3.Assets;
using GameRealisticMap.Arma3.GameEngine;
using GameRealisticMap.Arma3.IO;
using GameRealisticMap.Reporting;
using HugeImages;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GameRealisticMap.Arma3.Imagery
{
    internal class ImagerySource : IImagerySource
    {
        private readonly IdMapRender idMapRender;
        private readonly SatMapRender satMapRender;
        private readonly IArma3MapConfig config;
        private readonly IContext context;

        public ImagerySource(TerrainMaterialLibrary materialLibrary, IProgressSystem progress, IGameFileSystem gameFileSystem, IArma3MapConfig config, IContext context)
        {
            idMapRender = new IdMapRender(materialLibrary, progress);
            satMapRender = new SatMapRender(materialLibrary, progress, gameFileSystem);
            this.config = config;
            this.context = context;
        }

        public HugeImage<Rgba32> CreateIdMap()
        {
            return idMapRender.Render(config, context);
        }

        public Image CreatePictureMap()
        {
            return satMapRender.RenderPictureMap(config, context, 2048);
        }

        public HugeImage<Rgba32> CreateSatMap()
        {
            return satMapRender.Render(config, context);
        }

        public Image CreateSatOut()
        {
            return satMapRender.RenderSatOut(config, context, config.TileSize / 2);
        }
    }
}
