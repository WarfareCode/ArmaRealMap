﻿using System.Numerics;
using GeoJSON.Text.Feature;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GameRealisticMap.Satellite
{
    public class RawSatelliteImageData
    {
        public RawSatelliteImageData(Image<Rgb24> image)
        {
            Image = image;
        }

        public Image<Rgb24> Image { get; }
    }
}
