﻿using GameRealisticMap.Preview;
using GameRealisticMap.Reporting;

namespace GameRealisticMap.CommandLine
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var progress = new ConsoleProgressSystem();

            var area = TerrainAreaUTM.CreateFromCenter("43.805011792296725, -1.4100638139572768", 3.25f, 256);
            var render = new PreviewRender(area, new ImageryOptions());
            await render.RenderHtml(progress, Path.GetFullPath("preview2.html"));

            area = TerrainAreaUTM.CreateFromCenter("46.71532290005527, -2.3412981298116162", 4.5f, 2048);
            render = new PreviewRender(area, new ImageryOptions());
            await render.RenderHtml(progress, Path.GetFullPath("preview.html"));

            area = TerrainAreaUTM.CreateFromSouthWest("47.6856, 6.8270", 2.5f, 1024);
            render = new PreviewRender(area, new ImageryOptions());
            await render.RenderHtml(progress, Path.GetFullPath("preview3.html"));
        }

    }
}