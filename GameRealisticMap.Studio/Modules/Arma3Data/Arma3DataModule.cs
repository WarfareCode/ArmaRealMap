﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameRealisticMap.Arma3;
using GameRealisticMap.Arma3.IO;
using GameRealisticMap.Arma3.TerrainBuilder;
using Gemini.Framework;

namespace GameRealisticMap.Studio.Modules.Arma3Data
{
    [Export(typeof(Arma3DataModule))] // TODO: an interface would be nicer
    [Export(typeof(IModule))]
    [Export(typeof(IArma3Previews))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class Arma3DataModule : ModuleBase, IArma3Previews
    {
        private List<string>? previewsInProject;

        public ModelInfoLibrary Library { get; private set; }

        public ProjectDrive ProjectDrive { get; private set; }

        public string PreviewCachePath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "GameRealisticMap", 
            "Arma3",
            "Previews");

        public override void Initialize()
        {
            ProjectDrive = new ProjectDrive(Arma3ToolsHelper.GetProjectDrivePath(), new PboFileSystem());

            Library = new ModelInfoLibrary(ProjectDrive);
        }

        public IEnumerable<ModelInfo> Import(IEnumerable<string> paths)
        {
            try
            {
                return paths.Select(ProjectDrive.GetGamePath).Select(p => Library.ResolveByPath(p)).ToList();
            }
            catch
            {
                return new List<ModelInfo>();
            }
        }

        public Uri? GetPreview(ModelInfo modelInfo)
        {
            var cacheJpeg = Path.Combine(PreviewCachePath, Path.ChangeExtension(modelInfo.Path, ".jpg"));
            var cachePng = Path.Combine(PreviewCachePath, Path.ChangeExtension(modelInfo.Path, ".png"));
            if (File.Exists(cacheJpeg))
            {
                return new Uri(cacheJpeg);
            }
            if (File.Exists(cachePng))
            {
                return new Uri(cachePng);
            }
            var editorPreview = LocateGameEditorPreview(modelInfo);
            if (!string.IsNullOrEmpty(editorPreview))
            {
                return CacheGameEditorPreview(cacheJpeg, editorPreview);
            }
            return null;
        }

        private Uri CacheGameEditorPreview(string cacheJpeg, string editorPreview)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cacheJpeg)!);
            using (var target = File.Create(cacheJpeg))
            {
                using (var source = ProjectDrive.OpenFileIfExists(editorPreview)!)
                {
                    source.CopyTo(target);
                }
            }
            return new Uri(cacheJpeg);
        }

        private string? LocateGameEditorPreview(ModelInfo modelInfo)
        {
            if (previewsInProject == null)
            {
                lock (this)
                {
                    if (previewsInProject == null)
                    {
                        previewsInProject = ProjectDrive.FindAll($"land_*.jpg").ToList();
                    }
                }
            }
            var previewName = $"land_{Path.GetFileNameWithoutExtension(modelInfo.Path)}.jpg";
            var editorPreview = previewsInProject.FirstOrDefault(p => p.EndsWith(previewName, StringComparison.OrdinalIgnoreCase));
            return editorPreview;
        }
    }
}
