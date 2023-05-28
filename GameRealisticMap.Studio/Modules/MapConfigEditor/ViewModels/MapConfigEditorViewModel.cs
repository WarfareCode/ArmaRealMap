﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Caliburn.Micro;
using GameRealisticMap.Arma3;
using GameRealisticMap.Arma3.Assets;
using GameRealisticMap.Arma3.IO;
using GameRealisticMap.Arma3.TerrainBuilder;
using GameRealisticMap.ManMade.Roads.Libraries;
using GameRealisticMap.Osm;
using GameRealisticMap.Preview;
using GameRealisticMap.Reporting;
using GameRealisticMap.Studio.Modules.Arma3Data;
using GameRealisticMap.Studio.Modules.AssetConfigEditor;
using GameRealisticMap.Studio.Modules.AssetConfigEditor.ViewModels;
using GameRealisticMap.Studio.Modules.Explorer.ViewModels;
using GameRealisticMap.Studio.Modules.Reporting;
using GameRealisticMap.Studio.Toolkit;
using Gemini.Framework;
using Gemini.Framework.Services;
using GeoJSON.Text.Feature;
using MapControl;
using Microsoft.Win32;

namespace GameRealisticMap.Studio.Modules.MapConfigEditor.ViewModels
{
    internal class MapConfigEditorViewModel : PersistedDocument2, IExplorerRootTreeItem
    {
        private readonly IShell _shell;
        private readonly IArma3DataModule _arma3DataModule;

        public MapConfigEditorViewModel(IShell shell, IArma3DataModule arma3DataModule)
        {
            _shell = shell;
            _arma3DataModule = arma3DataModule;
        }

        public Arma3MapConfigJson Config { get; set; } = new Arma3MapConfigJson();

        public int[] GridSizes { get; } = new int[] { 256, 512, 1024, 2048, 4096, 8192 };
        
        public string Center
        {
            get { return Config.Center ?? string.Empty ; }
            set
            {
                Config.Center = value;
                if (!string.IsNullOrEmpty(value))
                {
                    Config.SouthWest = null;
                }
                NotifyOfPropertyChange(nameof(SouthWest));
                NotifyOfPropertyChange(nameof(Center));
                NotifyOfPropertyChange(nameof(Locations));
                IsDirty = true;
            }
        }

        public string SouthWest
        {
            get { return Config.SouthWest ?? string.Empty; }
            set
            {
                Config.SouthWest = value;
                if (!string.IsNullOrEmpty(value))
                {
                    Config.Center = null;
                }
                NotifyOfPropertyChange(nameof(SouthWest));
                NotifyOfPropertyChange(nameof(Center));
                NotifyOfPropertyChange(nameof(Locations));
                IsDirty = true;
            }
        }

        public float GridCellSize
        {
            get { return Config.GridCellSize; }
            set
            {
                Config.GridCellSize = value;
                NotifyOfPropertyChange(nameof(MapSize));
                NotifyOfPropertyChange(nameof(GridCellSize));
                NotifyOfPropertyChange(nameof(Locations));
                IsDirty = true;
            }
        }

        public int GridSize 
        { 
            get { return Config.GridSize; }
            set 
            {
                Config.GridSize = value;
                NotifyOfPropertyChange(nameof(MapSize));
                NotifyOfPropertyChange(nameof(GridSize));
                NotifyOfPropertyChange(nameof(Locations));
                IsDirty = true;
            }
        }

        public float MapSize 
        { 
            get { return Config.GridSize * Config.GridCellSize; }
            set
            {
                Config.GridSize = GridSizes.Max();
                foreach (var candidate in GridSizes)
                {
                    var cellsize = value / candidate;
                    if (cellsize > 2 && cellsize < 8)
                    {
                        Config.GridSize = candidate;
                        break;
                    }
                }
                Config.GridCellSize = value / Config.GridSize;
                NotifyOfPropertyChange(nameof(MapSize));
                NotifyOfPropertyChange(nameof(GridSize));
                NotifyOfPropertyChange(nameof(GridCellSize));
                NotifyOfPropertyChange(nameof(Locations));
                IsDirty = true;
            }
        }

        public IEnumerable<Location> Locations
        {
            get 
            { 
                if (!string.IsNullOrEmpty(Config.SouthWest) || !string.IsNullOrEmpty(Config.Center))
                {
                    var area = Config.ToArma3MapConfig().TerrainArea;
                    return area.TerrainBounds.Shell.Select(area.TerrainPointToLatLng).Select(l => new Location(l.Y, l.X));
                }
                return new List<Location>(); 
            }
        }

        public string AssetConfigFile
        {
            get { return Config.AssetConfigFile ?? string.Empty; }
            set
            {
                Config.AssetConfigFile = value;
                NotifyOfPropertyChange();
                IsDirty = true;
            }
        }

        public string TreeName => DisplayName;
        public string Icon => $"pack://application:,,,/GameRealisticMap.Studio;component/Resources/Icons/MapConfig.png";

        public override string DisplayName { get => base.DisplayName; set { base.DisplayName = value; NotifyOfPropertyChange(nameof(TreeName)); } }

        public IEnumerable<IExplorerTreeItem> Children => Enumerable.Empty<IExplorerTreeItem>();

        protected override async Task DoLoad(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            Config = await JsonSerializer.DeserializeAsync<Arma3MapConfigJson>(stream) ?? new Arma3MapConfigJson();
            NotifyOfPropertyChange(nameof(Config));
            NotifyOfPropertyChange(nameof(SouthWest));
            NotifyOfPropertyChange(nameof(Center));
            NotifyOfPropertyChange(nameof(MapSize));
            NotifyOfPropertyChange(nameof(GridCellSize));
            NotifyOfPropertyChange(nameof(Locations));
            NotifyOfPropertyChange(nameof(AssetConfigFile));
        }

        protected override Task DoNew()
        {
            Config = new Arma3MapConfigJson();
            NotifyOfPropertyChange(nameof(Config));
            return Task.CompletedTask;
        }

        protected override async Task DoSave(string filePath)
        {
            using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, Config);
        }

        public Task GeneratePreview(bool ignoreElevation = false)
        {
            var progress = IoC.Get<IProgressTool>();
            if (!progress.IsRunning)
            {
                _shell.ShowTool(progress);
                _ = Task.Run(() => DoGeneratePreview(progress, ignoreElevation));
            }
            return Task.CompletedTask;
        }

        public Task GeneratePreviewFast() => GeneratePreview(true);

        public Task GeneratePreviewNormal() => GeneratePreview(false);

        private async Task DoGeneratePreview(IProgressTool progressTool, bool ignoreElevation)
        {
            using var task = progressTool.StartTask("Generate Preview");
            try
            {
                var a3config = Config.ToArma3MapConfig();
                var render = new PreviewRender(a3config.TerrainArea, a3config.Imagery);
                var target = Path.Combine(Path.GetTempPath(), "grm-preview.html");
                await render.RenderHtml(task, target, ignoreElevation);
                task.DisplayResult = () => Process.Start(new ProcessStartInfo() { UseShellExecute = true, FileName = target });
            }
            catch(Exception ex)
            {
                task.Failed(ex);
            }
        }

        public Task ChooseAssetConfig()
        {
            var provider = IoC.Get<AssetConfigEditorProvider>();
            var dialog = new OpenFileDialog();
            dialog.Filter = string.Join("|", provider.FileTypes.Select(x => x.Name + "|*" + x.FileExtension));
            if (dialog.ShowDialog() == true)
            {
                AssetConfigFile = dialog.FileName;
            }
            return Task.CompletedTask;
        }

        public async Task EditAssetConfig()
        {
            var file = AssetConfigFile;
            if (!string.IsNullOrEmpty(file))
            {
                var doc = GetAssetConfigEditor(file);
                if (doc == null)
                {
                    var fullpath = GetAssetFullPath(file);
                    if (fullpath != null && File.Exists(fullpath))
                    {
                        var provider = IoC.Get<AssetConfigEditorProvider>();
                        doc = (AssetConfigEditorViewModel)provider.Create();
                        await provider.Open(doc, fullpath);
                    }
                }
                await _shell.OpenDocumentAsync(doc);
            }
        }

        public string? GetAssetFullPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }
            if (!IsNew)
            {
                var relPath = Path.Combine(Path.GetDirectoryName(FilePath)!, path);
                if (File.Exists(relPath))
                {
                    return Path.GetFullPath(relPath);
                }
            }
            return null;
        }

        public AssetConfigEditorViewModel? GetAssetConfigEditor(string file)
        {
            var fullpath = GetAssetFullPath(file);
            var editors = _shell.Documents.OfType<AssetConfigEditorViewModel>();
            if (fullpath != null)
            {
                return editors.FirstOrDefault(d => !d.IsNew && string.Equals(d.FilePath, fullpath, StringComparison.OrdinalIgnoreCase));
            }
            return editors.FirstOrDefault(d => d.IsNew && string.Equals(d.FileName, file, StringComparison.OrdinalIgnoreCase));
        }

        public Task CreateAssetConfig()
        {
            return Task.CompletedTask;
        }

        public Task GenerateMap()
        {
            var progress = IoC.Get<IProgressTool>();
            if (!progress.IsRunning)
            {
                _shell.ShowTool(progress);
                _ = Task.Run(() => DoGenerateMap(progress));
            }
            return Task.CompletedTask;
        }

        private async Task DoGenerateMap(IProgressTool progressTool)
        {
            using var task = progressTool.StartTask("Generate Map");
            try
            {
                var projectDrive = _arma3DataModule.ProjectDrive;
                var library = _arma3DataModule.Library;

                var a3config = Config.ToArma3MapConfig();

                Arma3Assets assets = await GetAssets(library, a3config);

                var generator = new Arma3MapGenerator(assets, projectDrive);

                await generator.GenerateWrp(task, a3config);

                task.DisplayResult = null;
            }
            catch (Exception ex)
            {
                task.Failed(ex);
            }
        }

        private async Task<Arma3Assets> GetAssets(ModelInfoLibrary library, Arma3MapConfig a3config)
        {
            var doc = GetAssetConfigEditor(a3config.AssetConfigFile);
            if ( doc != null)
            {
                return doc.ToJson();
            }

            var file = GetAssetFullPath(a3config.AssetConfigFile);
            if (string.IsNullOrEmpty(file))
            {
                throw new ApplicationException("Asset config file is missing.");
            }

            return await Arma3Assets.LoadFromFile(library, file);
        }
    }
}
