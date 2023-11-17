﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BIS.Core.Config;
using BIS.Core.Streams;
using BIS.WRP;
using Caliburn.Micro;
using GameRealisticMap.Arma3;
using GameRealisticMap.Arma3.Assets;
using GameRealisticMap.Arma3.Edit;
using GameRealisticMap.Arma3.Edit.Imagery;
using GameRealisticMap.Arma3.GameEngine;
using GameRealisticMap.Arma3.GameEngine.Roads;
using GameRealisticMap.Arma3.GameLauncher;
using GameRealisticMap.Arma3.IO;
using GameRealisticMap.Arma3.TerrainBuilder;
using GameRealisticMap.ElevationModel;
using GameRealisticMap.Reporting;
using GameRealisticMap.Studio.Modules.Arma3Data;
using GameRealisticMap.Studio.Modules.Arma3Data.Services;
using GameRealisticMap.Studio.Modules.Explorer.ViewModels;
using GameRealisticMap.Studio.Modules.Reporting;
using GameRealisticMap.Studio.Toolkit;
using Gemini.Framework;
using Gemini.Framework.Services;
using HugeImages;
using HugeImages.Storage;
using Microsoft.Win32;
using SixLabors.ImageSharp.PixelFormats;

namespace GameRealisticMap.Studio.Modules.Arma3WorldEditor.ViewModels
{
    internal class Arma3WorldEditorViewModel : PersistedDocument, IExplorerRootTreeItem
    {
        private EditableWrp? _world;
        private GameConfigTextData? _configFile;
        private string _targetModDirectory = string.Empty;
        private int savedRevision;
        private List<RevisionHistoryEntry> _backups = new List<RevisionHistoryEntry>();
        private readonly IArma3DataModule arma3Data;
        private readonly IWindowManager windowManager;
        private readonly IArma3RecentHistory history;
        private readonly IArma3BackupService worldBackup;
        private ExistingImageryInfos? _imagery;
        private bool isRoadsDirty;
        private Arma3WorldMapViewModel? mapEditor;
        private EditableArma3Roads? roads;

        public Arma3WorldEditorViewModel(IArma3DataModule arma3Data, IWindowManager windowManager, IArma3RecentHistory history, IArma3BackupService worldBackup) 
        {
            this.arma3Data = arma3Data;
            this.windowManager = windowManager;
            this.history = history;
            this.worldBackup = worldBackup;
            OpenConfigFileCommand = new AsyncCommand(OpenConfigFile);
            OpenDirectoryCommand = new RelayCommand(_ => OpenDirectory());
        }

        private void OpenDirectory()
        {
            var pboPrefix = ConfigFile?.PboPrefix;
            if(!string.IsNullOrEmpty(pboPrefix))
            {
                ShellHelper.OpenUri(arma3Data.ProjectDrive.GetFullPath(pboPrefix));
            }
        }

        public IAsyncCommand OpenConfigFileCommand { get; }

        public RelayCommand OpenDirectoryCommand { get; }

        protected override async Task DoLoad(string filePath)
        {
            var worldName = Path.GetFileNameWithoutExtension(filePath);

            World = StreamHelper.Read<AnyWrp>(filePath).GetEditableWrp();

            ConfigFile = ReadGameConfig(filePath, worldName);

            savedRevision = ConfigFile?.Revision ?? 0;

            HistoryEntry = await history.GetEntryOrDefault(worldName);

            TargetModDirectory = HistoryEntry?.ModDirectory ?? Arma3MapConfig.GetAutomaticTargetModDirectory(worldName);

            Dependencies = await ReadDependencies(DependenciesFilePath(filePath));

            if (ConfigFile != null && WrpCompiler.SeemsGeneratedByUs(World, ConfigFile.PboPrefix))
            {
                Imagery = ExistingImageryInfos.TryCreate(arma3Data.ProjectDrive, ConfigFile.PboPrefix, SizeInMeters!.Value);
            }

            if (Roads != null)
            {
                LoadRoads();
            }

            UpdateBackupsList(filePath);

            NotifyOfPropertyChange(nameof(HistoryEntry));
            NotifyOfPropertyChange(nameof(TreeName));
        }

        private void UpdateBackupsList(string filePath)
        {
            Backups = new[] { new RevisionHistoryEntry(this) }.Concat(worldBackup.GetBackups(filePath).Select(b => new RevisionHistoryEntry(this, b))).ToList();
        }

        private static string DependenciesFilePath(string filePath)
        {
            return Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, "grma3-dependencies.json");
        }

        private static string ConfigFilePath(string filePath)
        {
            return Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, GameConfigTextData.FileName);
        }

        private GameConfigTextData? ReadGameConfig(string filePath, string worldName)
        {
            var configFile = ConfigFilePath(filePath);
            if (File.Exists(configFile))
            {
                return GameConfigTextData.ReadFromFile(configFile, worldName);
            }
            var recoverFile = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, GameConfigTextData.FileNameRecover);
            if (File.Exists(recoverFile))
            {
                return GameConfigTextData.ReadFromFile(recoverFile, worldName);
            }
            var binaryFile = Path.ChangeExtension(configFile, ".bin");
            if (File.Exists(binaryFile))
            {
                return GameConfigTextData.ReadFromContent(StreamHelper.Read<ParamFile>(binaryFile).ToString(), worldName);
            }
            return null;
        }



        private async Task<List<ModDependencyDefinition>> ReadDependencies(string dependenciesFile)
        {
            if (File.Exists(dependenciesFile))
            {
                try
                {
                    using var stream = File.OpenRead(dependenciesFile);
                    return await JsonSerializer.DeserializeAsync<List<ModDependencyDefinition>>(stream) ?? new List<ModDependencyDefinition>();
                }
                catch { 
                    // Ignore any error
                }
            }
            return DetectDependencies();
        }

        private List<ModDependencyDefinition> DetectDependencies()
        {
            if (World != null)
            {
                var usedFiles = World.Objects.Select(o => o.Model).Where(m => !string.IsNullOrEmpty(m)).Distinct().ToList();

                return IoC.Get<IArma3Dependencies>()
                    .ComputeModDependencies(usedFiles)
                    .ToList();
            }
            return new List<ModDependencyDefinition>();
        }

        public EditableWrp? World
        {
            get { return _world; }
            set { _world = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(Size)); }
        }

        public GameConfigTextData? ConfigFile
        {
            get { return _configFile; }
            set { _configFile = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(CanGenerateMod)); }
        }

        public string TargetModDirectory
        {
            get { return _targetModDirectory; }
            set { _targetModDirectory = value; NotifyOfPropertyChange(); }
        }

        public IArma3RecentEntry? HistoryEntry { get; private set; }

        public bool CanGenerateMod => !string.IsNullOrEmpty(_configFile?.PboPrefix);

        public float? CellSize
        {
            get
            {
                if (_world != null)
                {
                    return (_world.LandRangeX * _world.CellSize) / _world?.TerrainRangeX;
                }
                return null;
            }
        }

        public float? SizeInMeters
        {
            get
            {
                if (_world != null)
                {
                    return _world.LandRangeX * _world.CellSize;
                }
                return null;
            }
        }

        public string Size => $"{_world?.TerrainRangeX} × {CellSize} m ➜ {SizeInMeters} m";

        public IGameFileSystem GameFileSystem => arma3Data.ProjectDrive;

        public IModelInfoLibrary Library => arma3Data.Library;

        public List<ModDependencyDefinition> Dependencies { get; set; } = new List<ModDependencyDefinition>();

        public List<RevisionHistoryEntry> Backups
        {
            get { return _backups; }
            set { _backups = value; NotifyOfPropertyChange(); }
        }

        public bool IsImageryEditable => Imagery != null;

        public bool IsNotImageryEditable => Imagery == null;

        public ExistingImageryInfos? Imagery
        {
            get { return _imagery; }
            set { _imagery = value; NotifyOfPropertyChange(); NotifyOfPropertyChange(nameof(IsImageryEditable)); NotifyOfPropertyChange(nameof(IsNotImageryEditable)); }
        }

        public bool IsRoadsDirty 
        { 
            get { return isRoadsDirty; } 
            set { isRoadsDirty = value; if (value) { IsDirty = true; } } 
        } 

        public EditableArma3Roads? Roads 
        { 
            get { return roads; } 
            set { roads = value; mapEditor?.InvalidateRoads(); } 
        }

        public string TreeName => FileName;

        public string Icon => $"pack://application:,,,/GameRealisticMap.Studio;component/Resources/Icons/MapFile.png";

        public IEnumerable<IExplorerTreeItem> Children => Enumerable.Empty<IExplorerTreeItem>();

        protected override Task DoNew()
        {
            throw new NotSupportedException("You cannot create an empty world file.");
        }

        protected override async Task DoSave(string filePath)
        {
            if (World != null)
            {
                if (IsRoadsDirty && Roads != null && ConfigFile != null && !string.IsNullOrEmpty(ConfigFile.Roads))
                {
                    new RoadsSerializer(arma3Data.ProjectDrive).Serialize(
                        ConfigFile.Roads, Roads.Roads.Where(r => !r.IsRemoved), Roads.RoadTypeInfos);
                    ConfigFile.Revision++;
                    IsRoadsDirty = false;
                }
                if (IsDirty)
                {
                    worldBackup.CreateBackup(filePath, savedRevision, GetBackupFiles(filePath));
                    StreamHelper.Write(World, filePath);
                    UpdateBackupsList(filePath);
                }
                if (ConfigFile != null)
                {
                    ConfigFile.SaveIncrementalToFile(ConfigFilePath(filePath));
                    savedRevision = ConfigFile.Revision;
                }
                if (Dependencies != null)
                {
                    using var stream = File.Create(DependenciesFilePath(filePath));
                    await JsonSerializer.SerializeAsync(stream, Dependencies);
                }
            }
        }

        internal void LoadRoads()
        {
            if (ConfigFile != null && !string.IsNullOrEmpty(ConfigFile.Roads))
            {
                Roads = new RoadsDeserializer(arma3Data.ProjectDrive).Deserialize(ConfigFile.Roads);
            }
            else
            {
                Roads = null;
            }
            IsRoadsDirty = false;
        }

        private List<string> GetBackupFiles(string filePath)
        {
            var filesToBackup = new List<string>()
                    {
                        ConfigFilePath(filePath),
                        DependenciesFilePath(filePath)
                    };
            var roadBasePath = ConfigFile?.Roads;
            if (!string.IsNullOrEmpty(roadBasePath))
            {
                filesToBackup.AddRange(RoadsSerializer.GetFilenames(roadBasePath).Select(arma3Data.ProjectDrive.GetFullPath));
            }
            return filesToBackup;
        }

        public async Task GenerateMod()
        {
            if (ConfigFile == null || _world == null)
            {
                return;
            }

            if (IsDirty)
            {
                await Save();
            }

            var thoericFilepath = arma3Data.ProjectDrive.GetFullPath(ConfigFile.PboPrefix + "\\" + FileName);
            if (!PathUtility.IsSameFile(thoericFilepath, FilePath)) // works across subst and mklink
            {
                throw new ApplicationException($"File '{FilePath}' is not located in project drive, should be located in '{thoericFilepath}'.");
            }

            Arma3ToolsHelper.EnsureProjectDrive();

            _ = IoC.Get<IProgressTool>()
                .RunTask(Labels.GenerateModForArma3, DoGenerateMod);
        }

        private async Task DoGenerateMod(IProgressTaskUI task)
        {
            if (ConfigFile == null || _world == null)
            {
                return;
            }

            var wrpConfig = new SimpleWrpModConfig(ConfigFile.WorldName, ConfigFile.PboPrefix, TargetModDirectory);

            var generator = new SimpleWrpModGenerator(arma3Data.ProjectDrive, arma3Data.CreatePboCompilerFactory());

            await generator.GenerateMod(task, wrpConfig, _world);

            task.AddSuccessAction(() => ShellHelper.OpenUri(wrpConfig.TargetModDirectory), Labels.ViewInFileExplorer);
            //task.AddSuccessAction(() => ShellHelper.OpenUri("steam://run/107410"), Labels.OpenArma3Launcher, string.Format(Labels.OpenArma3LauncherWithGeneratedModHint, name));
            task.AddSuccessAction(() => Arma3Helper.Launch(Dependencies, wrpConfig.TargetModDirectory, wrpConfig.WorldName), Labels.LaunchArma3, Labels.LaunchArma3Hint);
            //await Arma3LauncherHelper.CreateLauncherPresetAsync(assets.Dependencies, a3config.TargetModDirectory, "GRM - " + name);

            await history.RegisterWorld(
                wrpConfig.WorldName,
                wrpConfig.PboPrefix,
                ConfigFile.Description,
                wrpConfig.TargetModDirectory);
        }

        public Task ImportEden()
        {
            return windowManager.ShowDialogAsync(new EdenImporterViewModel(this));
        }

        public Task ImportFile()
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Text File|*.txt";
            if (dialog.ShowDialog() == true)
            {
                return windowManager.ShowDialogAsync(new FileImporterViewModel(this, dialog.FileName));
            }
            return Task.CompletedTask;
        }

        internal void Apply(WrpEditBatch batch)
        {
            IoC.Get<IProgressTool>().RunTask("Import", task => {
                Apply(batch, task);
                return Task.CompletedTask;
            }, false);
        }

        internal void Apply(List<TerrainBuilderObject> list)
        {
            IoC.Get<IProgressTool>().RunTask("Import", async task =>
            {
                if (World == null)
                {
                    return;
                }
                await arma3Data.SaveLibraryCache();
                var size = World.TerrainRangeX;
                var grid = new ElevationGrid(size, CellSize!.Value);
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        grid[x, y] = World.Elevation[x + (y * size)];
                    }
                }
                var batch = new WrpEditBatch();
                batch.Add.AddRange(list
                    .ProgressStep(task, "ToWrpObject")
                    .Select(l => l.ToWrpObject(grid))
                    .Select(o => new WrpAddObject(o.Transform.Matrix, o.Model)));
                Apply(batch, task);
            }, false);
        }

        private void Apply(WrpEditBatch batch, IProgressTaskUI task)
        {
            if (World == null)
            {
                return;
            }
            var processor = new WrpEditProcessor(task);
            processor.Process(World, batch);
            if (ConfigFile != null)
            {
                if (Backups.Count > 0)
                {
                    ConfigFile.Revision = Math.Max(ConfigFile.Revision, Backups.Max(b => b.Revision)) + 1;
                }
                else
                {
                    ConfigFile.Revision++;
                }
            }
            // TODO: Update dependencies !
            IsDirty = true;
            ClearActive();
        }

        public async Task OpenConfigFile()
        {
            var file = HistoryEntry?.ConfigFile;
            if (!string.IsNullOrEmpty(file))
            {
                await EditorHelper.OpenWithEditor("Arma3MapConfigEditorProvider", file);
            }
        }

        public void ClearActive()
        {
            foreach(var entry in Backups)
            {
                entry.IsActive = false;
            }
        }

        public Task ExportSatMap()
        {
            if (_imagery != null)
            {
                var dialog = new SaveFileDialog();
                dialog.Filter = "PNG|*.png";
                dialog.FileName = Path.GetFileNameWithoutExtension(FileName) + "-satmap.png";
                if (dialog.ShowDialog() == true)
                {
                    var filename = dialog.FileName;
                    IoC.Get<IProgressTool>()
                        .RunTask(GameRealisticMap.Studio.Labels.ExportSatelliteImage, ui => DoExport(ui, filename, _imagery.GetSatMap(arma3Data.ProjectDrive)));
                }
            }
            return Task.CompletedTask;
        }

        public async Task ExportIdMap()
        {
            if (_imagery != null)
            {
                var dialog = new SaveFileDialog();
                dialog.Filter = "PNG|*.png";
                dialog.FileName = Path.GetFileNameWithoutExtension(FileName) + "-idmap.png";
                if (dialog.ShowDialog() == true)
                {
                    var filename = dialog.FileName;
                    var assets = await GetAssetsFromHistory() ?? await AskUserForAssets();
                    if (assets != null)
                    {
                        _ = IoC.Get<IProgressTool>()
                            .RunTask(GameRealisticMap.Studio.Labels.ExportTextureMaskImage, ui => DoExport(ui, filename, _imagery.GetIdMap(arma3Data.ProjectDrive, assets.Materials)));
                    }
                }
            }
        }

        private async Task<Arma3Assets?> AskUserForAssets()
        {
            var dialogAssets = new OpenFileDialog();
            dialogAssets.Title = "Please select assets";
            dialogAssets.Filter = Labels.Arma3AssetsConfiguration + "|*.grma3a";
            if (dialogAssets.ShowDialog() == true)
            {
                return await Arma3Assets.LoadFromFile(arma3Data.Library, dialogAssets.FileName, true);
            }
            return null;
        }

        internal async Task<Arma3Assets?> GetAssetsFromHistory()
        {
            try
            {
                var configFile = HistoryEntry?.ConfigFile;
                if (!string.IsNullOrEmpty(configFile) && File.Exists(configFile))
                {
                    using var configStream = File.OpenRead(configFile);
                    var assetConfigFile = (await JsonSerializer.DeserializeAsync<Arma3MapConfigJson>(configStream))?.AssetConfigFile;
                    if (!string.IsNullOrEmpty(assetConfigFile))
                    {
                        return await Arma3Assets.LoadFromFile(arma3Data.Library, assetConfigFile, true);
                    }
                }
            }
            catch(Exception ex)
            {
                // TODO: LOG !
            }
            return null;
        }

        public async Task DoExport(IProgressTaskUI ui, string filename, HugeImage<Rgb24> himage)
        {
            using (var task = ui.CreateStep("Write", 1))
            {
                using (himage)
                {
                    await himage.SaveUniqueAsync(filename);
                }
            }
            ui.AddSuccessAction(() => ShellHelper.OpenUri(filename), GameRealisticMap.Studio.Labels.OpenImage);
            ui.AddSuccessAction(() => ShellHelper.OpenUri(Path.GetDirectoryName(filename)!), GameRealisticMap.Studio.Labels.OpenFolder);
        }

        public Task ImportSatMap()
        {
            if (_imagery != null)
            {
                var dialog = new OpenFileDialog();
                dialog.Filter = "PNG|*.png";
                if (dialog.ShowDialog() == true)
                {
                    var filename = dialog.FileName;
                    IoC.Get<IProgressTool>()
                        .RunTask(GameRealisticMap.Studio.Labels.ImportSatelliteImage, ui => DoImport(ui, () => new ImageryImporter(arma3Data.ProjectDrive, ui).UpdateSatMap(_imagery, filename)));
                }
            }
            return Task.CompletedTask;
        }

        private async Task DoImport(IProgressTaskUI ui, Func<Task> action)
        {
            await action();

            if (CanGenerateMod)
            {
                ui.AddSuccessAction(() => _ = GenerateMod(), Labels.GenerateModForArma3);
            }
        }

        public async Task ImportIdMap()
        {
            if (_imagery != null)
            {
                var dialog = new OpenFileDialog();
                dialog.Filter = "PNG|*.png";
                if (dialog.ShowDialog() == true)
                {
                    var filename = dialog.FileName;
                    var assets = await GetAssetsFromHistory() ?? await AskUserForAssets();
                    if (assets != null)
                    {
                        _ = IoC.Get<IProgressTool>()
                            .RunTask(GameRealisticMap.Studio.Labels.ImportTextureMaskImage, ui => DoImport(ui, () => new ImageryImporter(arma3Data.ProjectDrive, assets.Materials, ui).UpdateIdMap(_imagery, filename)));
                    }
                }
            }
        }

        public Task EditAdvanced()
        {
            if (mapEditor == null)
            {
                mapEditor = new Arma3WorldMapViewModel(this, arma3Data);
            }
            return IoC.Get<IShell>().OpenDocumentAsync(mapEditor);
        }

    }
}
