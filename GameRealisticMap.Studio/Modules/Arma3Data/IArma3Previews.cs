﻿using System;
using GameRealisticMap.Arma3.TerrainBuilder;

namespace GameRealisticMap.Studio.Modules.Arma3Data
{
    internal interface IArma3Previews
    {
        Uri? GetPreview(ModelInfo modelInfo);
    }
}
