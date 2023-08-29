﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BIS.Core.Streams;
using BIS.WRP;
using Caliburn.Micro;
using GameRealisticMap.Arma3;
using GameRealisticMap.Arma3.Assets;
using GameRealisticMap.Arma3.Edit;
using GameRealisticMap.Arma3.GameEngine;
using GameRealisticMap.Arma3.GameLauncher;
using GameRealisticMap.Arma3.IO;
using GameRealisticMap.Arma3.TerrainBuilder;
using GameRealisticMap.ElevationModel;
using GameRealisticMap.Reporting;
using GameRealisticMap.Studio.Modules.Arma3Data;
using GameRealisticMap.Studio.Modules.Arma3Data.Services;
using GameRealisticMap.Studio.Modules.Reporting;
using GameRealisticMap.Studio.Toolkit;
using Gemini.Framework;
using Microsoft.Win32;

namespace GameRealisticMap.Studio.Modules.Arma3WorldEditor.ViewModels
{
    internal class Arma3WorldEditorViewModel : PersistedDocument
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

            var configFile = ConfigFilePath(filePath);
            if (File.Exists(configFile))
            {
                ConfigFile = GameConfigTextData.ReadFromFile(configFile, worldName);
            }
            else
            {
                ConfigFile = null;
            }
            savedRevision = ConfigFile?.Revision ?? 0;

            HistoryEntry = await history.GetEntryOrDefault(worldName);

            TargetModDirectory = HistoryEntry?.ModDirectory ?? Arma3MapConfig.GetAutomaticTargetModDirectory(worldName);

            Dependencies = await ReadDependencies(DependenciesFilePath(filePath));

            UpdateBackupsList(filePath);

            NotifyOfPropertyChange(nameof(HistoryEntry));
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

        protected override Task DoNew()
        {
            throw new NotSupportedException("You cannot create an empty world file.");
        }

        protected override async Task DoSave(string filePath)
        {
            if (World != null)
            {
                if (IsDirty)
                {
                    worldBackup.CreateBackup(filePath, savedRevision, ConfigFilePath(filePath), DependenciesFilePath(filePath));
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

            IoC.Get<IProgressTool>()
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


    }
}
