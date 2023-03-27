﻿using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using GameRealisticMap.Geometries;
using CoordinateSharp;
using MapToolkit;
using MapToolkit.Databases;
using MapToolkit.DataCells;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ArmaRealMap.ElevationModel
{
    public class ElevationGrid
    {
        public readonly MapInfos area;
        public readonly float[,] elevationGrid;

        private readonly Vector2 cellSize;
        //private readonly Vector2 cellDelta;

        public ElevationGrid(MapInfos areaInfos)
        {
            area = areaInfos;
            elevationGrid = new float[area.Size, area.Size];
            cellSize = new Vector2(area.CellSize);
            //cellDelta = new Vector2(0.5f); // Elevation is at the center of the cell
        }
        public ElevationGrid(ElevationGrid other)
        {
            area = other.area;
            elevationGrid = (float[,])other.elevationGrid.Clone();
            cellSize = other.cellSize;
            //cellDelta = other.cellDelta;
        }

        public void LoadFromSRTM(SRTMConfig configSRTM)
        {
            var db = new DemDatabase(new DemHttpStorage(configSRTM.CacheLocation, new Uri("https://dem.pmad.net/SRTM1/")));

            var startPointUTM = area.StartPointUTM;
            var eager = new EagerLoad(false);

            var done = 0;
            double delta = 1d / 3600d;

            var points = new[] { area.SouthWest, area.NorthEast, area.NorthWest, area.SouthEast };

            var view = db.CreateView<ushort>(
                new Coordinates(points.Min(p => p.Latitude.ToDouble()) - 0.001, points.Min(p => p.Longitude.ToDouble()) - 0.001),
                new Coordinates(points.Max(p => p.Latitude.ToDouble()) + 0.001, points.Max(p => p.Longitude.ToDouble()) + 0.001))
                .GetAwaiter()
                .GetResult()
                .ToDataCell();

            var report = new ProgressReport("LoadFromSRTM", area.Size);
            Parallel.For(0, area.Size, y =>
            {
                for (int x = 0; x < area.Size; x++)
                {
                    var latLong = area.TerrainToLatLong(x * area.CellSize, y * area.CellSize);
                    var elevation = GetElevationBilinear(view, latLong.Latitude.ToDouble(), latLong.Longitude.ToDouble());
                    if (area.CellSize > 30) // Smooth cells larger than SRTM resolution
                    {
                        elevation = (new[] { elevation,
                            GetElevationBilinear(view, latLong.Latitude.ToDouble() - delta, latLong.Longitude.ToDouble() - delta),
                            GetElevationBilinear(view, latLong.Latitude.ToDouble() - delta, latLong.Longitude.ToDouble() + delta),
                            GetElevationBilinear(view, latLong.Latitude.ToDouble() + delta, latLong.Longitude.ToDouble() - delta),
                            GetElevationBilinear(view, latLong.Latitude.ToDouble() + delta, latLong.Longitude.ToDouble() + delta)
                        }).Average();
                    }
                    elevationGrid[x, y] = (float)elevation;
                }
                report.ReportItemsDone(Interlocked.Increment(ref done));
            });

            report.TaskDone();
        }

        private double GetElevationBilinear(DemDataCellBase<ushort> view, double lat, double lon)
        {
            return view.GetLocalElevation(new Coordinates(lat, lon), DefaultInterpolation.Instance);
        }

        internal float ElevationAround(TerrainPoint p)
        {
            return ElevationAround(p, cellSize.X / 2);
        }

        internal float ElevationAround(TerrainPoint p, float radius)
        {
            return 
                (ElevationAt(p) + 
                ElevationAt(p + new Vector2(-radius, -radius)) +
                ElevationAt(p + new Vector2(radius, -radius)) +
                ElevationAt(p + new Vector2(-radius, radius)) +
                ElevationAt(p + new Vector2(radius, radius))) / 5f;
        }

        public void LoadFromAsc(string path)
        {
            var report = new ProgressReport("LoadFromAsc", area.Size);
            using (var reader = File.OpenText(path))
            {
                for(int i = 0;i < 6; ++i)
                {
                    reader.ReadLine();
                }
                string line;
                var y = area.Size - 1;
                while ((line = reader.ReadLine()) != null)
                {
                    int x = 0;
                    foreach(var item in line.Split(' ').Take(area.Size))
                    {
                        var elevation = double.Parse(item, CultureInfo.InvariantCulture);
                        elevationGrid[x, y] = (float)elevation;
                        x++;
                    }
                    report.ReportItemsDone(area.Size - y);
                    y--;
                }
            }
            report.TaskDone();
        }
        public void LoadFromBin(string path)
        {
            var report = new ProgressReport("LoadFromBin", area.Size);
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                var size = reader.ReadInt32();
                if (size != area.Size)
                {
                    throw new IOException("File size does not match");
                }
                for (int y = 0; y < area.Size; y++)
                {
                    for (int x = 0; x < area.Size; x++)
                    {
                        elevationGrid[x, y] = reader.ReadSingle();
                    }
                    report.ReportItemsDone(y);
                }
            }
            report.TaskDone();
        }

        public void SaveToBin(string path)
        {
            var report = new ProgressReport("SaveToBin", area.Size);
            using (var writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write)))
            {
                writer.Write(area.Size);
                for (int y = 0; y < area.Size; y++)
                {
                    for (int x = 0; x < area.Size; x++)
                    {
                        writer.Write(elevationGrid[x, y]);
                    }
                    report.ReportItemsDone(y);
                }
            }
            report.TaskDone();
        }

        public void SaveToAsc(string path)
        {
            var report = new ProgressReport("SaveToAsc", area.Size);

            using (var writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write)))
            {
                SaveToAsc(report, writer);
            }
            report.TaskDone();
        }

        public void SaveToAsc(ProgressReport report, TextWriter writer)
        {
            writer.WriteLine($"ncols         {area.Size}");
            writer.WriteLine($"nrows         {area.Size}");
            writer.WriteLine($"xllcorner     200000");
            writer.WriteLine($"yllcorner     0");
            writer.WriteLine($"cellsize      {area.CellSize}");
            writer.WriteLine($"NODATA_value  -9999");
            for (int y = 0; y < area.Size; y++)
            {
                report?.ReportItemsDone(y);
                for (int x = 0; x < area.Size; x++)
                {
                    writer.Write(elevationGrid[x, area.Size - y - 1].ToString("0.00", CultureInfo.InvariantCulture));
                    writer.Write(" ");
                }
                writer.WriteLine();
            }
        }

        public void SaveToObj(string path, int w = -1)
        {
            if ( w == -1 )
            {
                w = area.Size;
            }
            var report = new ProgressReport("SaveToObj", area.Size * 3 - 1);
            using (var writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write)))
            {
                var min = 4000f;
                for (int y = 0; y < w; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        min = MathF.Min(elevationGrid[x, y], min);
                    }
                    report.ReportOneDone();
                }

                for (int y = 0; y < w; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        writer.WriteLine(FormattableString.Invariant($"v {x * area.CellSize:0.00} {elevationGrid[x, y] - min:0.00} {y * area.CellSize:0.00}"));
                    }
                    report.ReportOneDone();
                }
                int index = 0;
                for (int y = 0; y < w - 1; y++)
                {
                    for (int x = 0; x < w - 1; x++)
                    {
                        writer.WriteLine(FormattableString.Invariant($"f {index + 1} {index + 2} {index + w + 1}"));
                        writer.WriteLine(FormattableString.Invariant($"f {index + 2} {index + w + 1} {index + w + 2}"));
                        index++;
                    }
                    index++; // for the missing X
                    report.ReportOneDone();
                }
            }
            report.TaskDone();
        }



        public void SavePreview(string path)
        {
            var min = 4000d;
            var max = -1000d;

            var report = new ProgressReport("ElevationPreview", area.Size * 2);

            for (int y = 0; y < area.Size; y++)
            {
                report.ReportItemsDone(y);
                for (int x = 0; x < area.Size; x++)
                {
                    max = Math.Max(elevationGrid[x, y], max);
                    min = Math.Min(elevationGrid[x, y], min);
                }
            }

            var legend = new[]
            {
                new { E = min, Color = Color.LightBlue.ToPixel<Rgb24>().ToScaledVector4() },
                new { E = min + (max - min) * 0.10, Color = Color.DarkGreen.ToPixel<Rgb24>().ToScaledVector4() },
                new { E = min + (max - min) * 0.15, Color = Color.Green.ToPixel<Rgb24>().ToScaledVector4() },
                new { E = min + (max - min) * 0.40, Color = Color.Yellow.ToPixel<Rgb24>().ToScaledVector4() },
                new { E = min + (max - min) * 0.70, Color = Color.Red.ToPixel<Rgb24>().ToScaledVector4() },
                new { E = max, Color = Color.Maroon.ToPixel<Rgb24>().ToScaledVector4() }
            };
            using (var img = new Image<Rgb24>(area.Size, area.Size))
            {
                for (int y = 0; y < area.Size; y++)
                {
                    report.ReportItemsDone(area.Size + y);
                    for (int x = 0; x < area.Size; x++)
                    {
                        var elevation = elevationGrid[x, y];
                        var before = legend.Where(e => e.E <= elevation).Last();
                        var after = legend.FirstOrDefault(e => e.E > elevation) ?? legend.Last();
                        var scale = (float)((elevation - before.E) / (after.E - before.E));
                        Rgb24 rgb = new Rgb24();
                        rgb.FromScaledVector4(Vector4.Lerp(before.Color, after.Color, scale));
                        img[x, area.Size - y - 1] = rgb;
                    }
                }
                img.Save(path);
                report.TaskDone();
            }
        }

        private float ElevationAtCell(int x, int y)
        {
            return elevationGrid[
                Math.Min(Math.Max(0, x), area.Size - 1),
                Math.Min(Math.Max(0, y), area.Size - 1)];
        }


        public float ElevationAtGrid(Vector2 gridPos)
        {
            var x = (int)MathF.Floor(gridPos.X);
            var y = (int)MathF.Floor(gridPos.Y);
            var xIn = gridPos.X - x;
            var yIn = gridPos.Y - y;
            var z10 = ElevationAtCell(x + 1, y);
            var z01 = ElevationAtCell(x, y + 1);
            if (xIn <= 1 - yIn)
            {
                var z00 = ElevationAtCell(x, y);
                var d1000 = z10 - z00;
                var d0100 = z01 - z00;
                return z00 + d0100 * yIn + d1000 * xIn;
            }
            var z11 = ElevationAtCell(x + 1, y + 1);
            var d1011 = z10 - z11;
            var d0111 = z01 - z11;
            return z10 + d0111 - d0111 * xIn - d1011 * yIn;
        }

        public float ElevationAt(TerrainPoint point)
        {
            return ElevationAtGrid(ToGrid(point));
            //var pos = ToGrid(point);
            //var x1 = (int)Math.Floor(pos.X);
            //var y1 = (int)Math.Floor(pos.Y);
            //var x2 = (int)Math.Ceiling(pos.X);
            //var y2 = (int)Math.Ceiling(pos.Y);
            //return Blerp(
            //    ElevationAtCell(x1, y1),
            //    ElevationAtCell(x2, y1),
            //    ElevationAtCell(x1, y2),
            //    ElevationAtCell(x2, y2),
            //    x2 - pos.X,
            //    y2 - pos.Y);
        }
        /*
        private float Lerp(float start, float end, float delta)
        {
            return start + (end - start) * delta;
        }

        private float Blerp(float val00, float val10, float val01, float val11, float deltaX, float deltaY)
        {
            return Lerp(Lerp(val11, val01, deltaX), Lerp(val10, val00, deltaX), deltaY);
        }
        */
        public Vector2 ToGrid(TerrainPoint point)
        {
            return ((point.Vector - area.P1.Vector) / cellSize);
        }

        public TerrainPoint ToTerrain(int x, int y)
        {
            return ToTerrain(new Vector2(x, y));
        }

        public TerrainPoint ToTerrain(Vector2 grid)
        {
            return new TerrainPoint(((grid) * cellSize)  + area.P1.Vector);
        }

        public ElevationGridArea PrepareToMutate(TerrainPoint min, TerrainPoint max, float minElevation, float maxElevation)
        {
            var posMin = ToGrid(min);
            var posMax = ToGrid(max);
            var x1 = (int)Math.Floor(posMin.X);
            var y1 = (int)Math.Floor(posMin.Y);
            var x2 = (int)Math.Ceiling(posMax.X);
            var y2 = (int)Math.Ceiling(posMax.Y);
            var delta = maxElevation - minElevation;
            return new ElevationGridArea(this, x1, y1, x2 - x1 + 1, y2 - y1 + 1, minElevation, delta);
        }

        public void Apply (int startX, int startY, Image<Rgba64> data, float minElevation, float elevationDelta)
        {
            for(int x = 0; x < data.Width; ++x)
            {
                for (int y = 0; y < data.Height; ++y)
                {
                    if (x + startX >= 0 && y + startY >= 0 && x + startX < area.Size && y + startY < area.Size)
                    {
                        var pixel = data[x, y];
                        if (pixel.A != ushort.MinValue)
                        {
                            var pixelElevation = minElevation + (elevationDelta * pixel.B / (float)ushort.MaxValue);
                            if (pixel.A == ushort.MaxValue)
                            {
                                elevationGrid[x + startX, y + startY] = pixelElevation;
                            }
                            else
                            {
                                var existingElevation = elevationGrid[x + startX, y + startY];
                                elevationGrid[x + startX, y + startY] = existingElevation + ((pixelElevation - existingElevation) * pixel.A / (float)ushort.MaxValue);
                            }
                        }
                    }
                }
            }

        }

        public string DumpAsc()
        {
            var str = new StringWriter();
            SaveToAsc(null, str);
            return str.ToString();
        }
    }
}
