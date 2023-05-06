﻿using GameRealisticMap.Reporting;
using SixLabors.ImageSharp;

namespace GameRealisticMap.Arma3.IO
{
    public class ProjectDrive : IGameFileSystem, IGameFileSystemWriter
    {
        private readonly string mountPath;
        private readonly IGameFileSystem? autoUnpackFrom;
        private readonly List<string> imageToPaaPending = new ();
        private readonly List<KeyValuePair<string, string>> mountPoints = new ();

        public ProjectDrive(string mountPath = "P:", IGameFileSystem? autoUnpackFrom = null)
        {
            this.mountPath = mountPath;
            this.autoUnpackFrom = autoUnpackFrom;
        }

        public bool Exists(string path)
        {
            var fullPath = GetFullPath(path);

            return File.Exists(fullPath) || AutoUnpack(path, fullPath);
        }

        private string GetFullPath(string path)
        {
            if (path.StartsWith("\\", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }
            foreach(var item in mountPoints)
            {
                if (path.StartsWith(item.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return Path.Combine(item.Value, path.Substring(item.Key.Length));
                }
            }
            // Should call Arma3ToolsHelper.EnsureProjectDrive() if mountPath == "P:" on first call
            return Path.Combine(mountPath, path);
        }

        private bool AutoUnpack(string path, string fullPath)
        {
            if (path.StartsWith("temp/") || path.StartsWith("temp\\"))
            {
                return false;
            }
            using (var fallBack = autoUnpackFrom?.OpenFileIfExists(path))
            {
                if (fallBack != null)
                {
                    using (var target = File.Create(fullPath))
                    {
                        fallBack.CopyTo(target);
                    }
                    return true;
                }
            }
            return false;
        }

        public Stream? OpenFileIfExists(string path)
        {
            var fullPath = GetFullPath(path);
            if (File.Exists(fullPath) || AutoUnpack(path, fullPath))
            {
                return File.OpenRead(fullPath);
            }
            return null;
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(GetFullPath(path));
        }

        public void WritePngImage(string path, Image image)
        {
            var fullPath = GetFullPath(path);
            image.SaveAsPng(fullPath);
            imageToPaaPending.Add(fullPath);
        }

        public void WriteTextFile(string path, string text)
        {
            File.WriteAllText(GetFullPath(path), text);
        }

        public Stream Create(string path)
        {
            return File.Create(GetFullPath(path));
        }

        public async Task ProcessImageToPaa(IProgressSystem progress, int? maxDegreeOfParallelism = null)
        {
            await Arma3ToolsHelper.ImageToPAA(progress, imageToPaaPending, maxDegreeOfParallelism);
            imageToPaaPending.Clear();
        }

        public void AddMountPoint(string gamePath, string physicalPath)
        {
            if (!gamePath.EndsWith("\\", StringComparison.Ordinal))
            {
                gamePath = gamePath + "\\";
            }
            mountPoints.Add(new (gamePath, physicalPath));
            mountPoints.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        }
    }
}
