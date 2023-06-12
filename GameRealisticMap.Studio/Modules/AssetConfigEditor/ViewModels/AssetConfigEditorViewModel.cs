﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Caliburn.Micro;
using GameRealisticMap.Arma3.Assets;
using GameRealisticMap.Arma3.Assets.Filling;
using GameRealisticMap.Arma3.IO;
using GameRealisticMap.ManMade.Buildings;
using GameRealisticMap.ManMade.Fences;
using GameRealisticMap.ManMade.Objects;
using GameRealisticMap.ManMade.Roads;
using GameRealisticMap.Studio.Modules.Arma3Data;
using GameRealisticMap.Studio.Modules.Arma3Data.Services;
using GameRealisticMap.Studio.Modules.AssetConfigEditor.ViewModels.Filling;
using GameRealisticMap.Studio.Modules.AssetConfigEditor.ViewModels.Individual;
using GameRealisticMap.Studio.Modules.CompositionTool;
using GameRealisticMap.Studio.Modules.CompositionTool.ViewModels;
using GameRealisticMap.Studio.Modules.Explorer;
using GameRealisticMap.Studio.Modules.Explorer.ViewModels;
using GameRealisticMap.Studio.Toolkit;
using GameRealisticMap.Studio.UndoRedo;
using Gemini.Framework;
using Gemini.Framework.Services;

namespace GameRealisticMap.Studio.Modules.AssetConfigEditor.ViewModels
{
    internal class AssetConfigEditorViewModel : PersistedDocument2, IExplorerRootTreeItem
    {
        private readonly IArma3DataModule _arma3Data;
        private readonly IShell _shell;
        private readonly ICompositionTool _compositionTool;

        public BindableCollection<IFillAssetCategory> Filling { get; } = new BindableCollection<IFillAssetCategory>();

        public BindableCollection<IAssetCategory> Individual { get; } = new BindableCollection<IAssetCategory>();

        public BindableCollection<MaterialViewModel> Materials { get; } = new BindableCollection<MaterialViewModel>();

        public BindableCollection<RoadViewModel> Roads { get; } = new BindableCollection<RoadViewModel>();

        public BindableCollection<PondViewModel> Ponds { get; } = new BindableCollection<PondViewModel>();

        public List<AdditionalFilling> AdditionalFillings { get; }

        public RelayCommand RemoveFilling { get; }

        public bool IsLoading { get; set; }

        public AssetConfigEditorViewModel(IArma3DataModule arma3Data, IShell shell, ICompositionTool compositionTool)
        {
            _arma3Data = arma3Data;
            _shell = shell;
            _compositionTool = compositionTool;
            Children = new List<IExplorerTreeItem>()
            {
                new ExplorerTreeItem("Natural areas and fences", Filling,"Nature"),
                new ExplorerTreeItem("Buildings", Individual.OfType<BuildingsViewModel>(), "Buildings"),
                new ExplorerTreeItem("Objects", Individual.OfType<ObjectsViewModel>(), "Objects"),
                new ExplorerTreeItem("Ground materials", Materials, "Materials"),
                new ExplorerTreeItem("Roads and bridges", Roads, "Road")
            };
            UndoRedoManager.PropertyChanged += (_, _) => IsDirty = true;
            AdditionalFillings = AdditionalFilling.Create(this);
            RemoveFilling = new RelayCommand(item => DoRemoveFilling((IFillAssetCategory)item));
        }

        private void DoRemoveFilling(IFillAssetCategory item)
        {
            if (Filling.Count(f => f != item && f.IsSameFillId(item.IdObj)) >= 1)
            {
                Filling.RemoveUndoable(UndoRedoManager, item);
            }
        }

        public double TextureSizeInMeters { get; set; }

        public IEnumerable<IExplorerTreeItem> Children { get; }

        public string TreeName => DisplayName;
        public string Icon => $"pack://application:,,,/GameRealisticMap.Studio;component/Resources/Icons/AssetConfig.png";

        public override string DisplayName { get => base.DisplayName; set { base.DisplayName = value; NotifyOfPropertyChange(nameof(TreeName)); } }

        protected override async Task DoLoad(string filePath)
        {
            IsLoading = true;
            NotifyOfPropertyChange(nameof(IsLoading));

            Arma3Assets json;
            try
            {
                json = await Arma3Assets.LoadFromFile(_arma3Data.Library, filePath);
            }
            catch (ApplicationException)
            {
                json = await AutoEnableMods(filePath);
            }
            FromJson(json);
            await _arma3Data.SaveLibraryCache();

            IsLoading = false;
            NotifyOfPropertyChange(nameof(IsLoading));
        }

        private async Task<Arma3Assets> AutoEnableMods(string filePath)
        {
            var deps = await Arma3Assets.LoadDependenciesFromFile(filePath); 

            var activeMods = _arma3Data.ActiveMods.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var mods = IoC.Get<IArma3ModsService>();
            var toEnablePaths = new List<string>();
            var toInstallSteamId = new List<string>();
            foreach (var dependency in deps.Dependencies)
            {
                var mod = mods.GetMod(dependency.SteamId);
                if (mod == null)
                {
                    // => NOT INSTALLED, must prompt user
                    toInstallSteamId.Add(dependency.SteamId);
                }
                else if (!activeMods.Contains(mod.Path))
                {
                    // => NOT ENABLED
                    toEnablePaths.Add(mod.Path);
                }
            }
            if (toEnablePaths.Count > 0)
            {
                await _arma3Data.ChangeActiveMods(_arma3Data.ActiveMods.Concat(toEnablePaths));
            }
            if (toInstallSteamId != null)
            {
                MissingMods = toInstallSteamId.Select(m => new MissingMod(m)).ToList();
                NotifyOfPropertyChange(nameof(HasMissingMods));
                NotifyOfPropertyChange(nameof(MissingMods));
            }
            try
            {
                // Last attempt
                return await Arma3Assets.LoadFromFile(_arma3Data.Library, filePath);
            }
            catch (ApplicationException)
            {
                return await Arma3Assets.LoadFromFile(_arma3Data.Library, filePath, true);
            }
        }

        internal Task CopyFrom(string filePath)
        {
            return DoLoad(filePath);
        }

        private void FromJson(Arma3Assets arma3Assets)
        {
            Filling.Clear();
            Individual.Clear();
            Roads.Clear();
            Materials.Clear();
            Ponds.Clear();

            Filling.AddRange(GetFilling(arma3Assets));
            Individual.AddRange(GetIndividual(arma3Assets));

            foreach (var id in Enum.GetValues<TerrainMaterialUsage>().OrderByDescending(i => i))
            {
                Materials.Add(new MaterialViewModel(id, arma3Assets.Materials.GetMaterialByUsage(id), this));
            }
            TextureSizeInMeters = arma3Assets.Materials.TextureSizeInMeters;
            foreach (var id in Enum.GetValues<RoadTypeId>().OrderByDescending(i => i))
            {
                Roads.Add(new RoadViewModel(id, arma3Assets.Roads.FirstOrDefault(m => m.Id == id), arma3Assets.GetBridge(id), this));
            }
            foreach (var id in Enum.GetValues<PondSizeId>())
            {
                Ponds.Add(new PondViewModel(id, arma3Assets.Ponds.Count == 0 ? null : arma3Assets.GetPond(id), _arma3Data.Library));
            }
            NotifyOfPropertyChange(nameof(TextureSizeInMeters));
            BaseWorldName = arma3Assets.BaseWorldName;
            BaseDependency = arma3Assets.BaseDependency;
        }

        private List<IAssetCategory> GetIndividual(Arma3Assets arma3Assets)
        {
            var list = new List<IAssetCategory>();
            foreach (var id in Enum.GetValues<BuildingTypeId>())
            {
                list.Add(new BuildingsViewModel(id, arma3Assets.GetBuildings(id), this));
            }
            foreach (var id in Enum.GetValues<ObjectTypeId>())
            {
                list.Add(new ObjectsViewModel(id, arma3Assets.GetObjects(id), this));
            }
            list.Sort((a, b) => a.PageTitle.CompareTo(b.PageTitle));
            return list;
        }

        private List<IFillAssetCategory> GetFilling(Arma3Assets arma3Assets)
        {
            var list = new List<IFillAssetCategory>();
            foreach (var id in Enum.GetValues<BasicCollectionId>())
            {
                foreach (var entry in AtLeastOne(arma3Assets.GetBasicCollections(id)))
                {
                    list.Add(new FillingAssetBasicViewModel(id, entry, this));
                }
            }
            foreach (var id in Enum.GetValues<ClusterCollectionId>())
            {
                foreach (var entry in AtLeastOne(arma3Assets.GetClusterCollections(id)))
                {
                    list.Add(new FillingAssetClusterViewModel(id, entry, this));
                }
            }
            foreach (var id in Enum.GetValues<FenceTypeId>())
            {
                foreach (var entry in AtLeastOne(arma3Assets.GetFences(id)))
                {
                    list.Add(new FencesViewModel(id, entry, this));
                }
            }
            list.Sort((a, b) => a.PageTitle.CompareTo(b.PageTitle));
            return list;
        }

        private IEnumerable<T?> AtLeastOne<T>(IReadOnlyCollection<T> collection, T? one = null) where T : class
        {
            if (collection.Count == 0)
            {
                return new T?[] { one };
            }
            return collection;
        }

        internal Arma3Assets ToJson()
        {
            EquilibrateProbabilities();
            var json = new Arma3Assets();
            json.ClusterCollections = Filling.OfType<FillingAssetClusterViewModel>().Where(c => !c.IsEmpty).GroupBy(c => c.FillId).OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Select(o => o.ToDefinition()).ToList());
            json.BasicCollections = Filling.OfType<FillingAssetBasicViewModel>().Where(c => !c.IsEmpty).GroupBy(c => c.FillId).OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Select(o => o.ToDefinition()).ToList());
            json.Fences = Filling.OfType<FencesViewModel>().Where(c => !c.IsEmpty).GroupBy(c => c.FillId).OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Select(o => o.ToDefinition()).ToList());
            json.Buildings = Individual.OfType<BuildingsViewModel>().Where(c => !c.IsEmpty).OrderBy(k => k.FillId).ToDictionary(k => k.FillId, k => k.ToDefinition());
            json.Objects = Individual.OfType<ObjectsViewModel>().Where(c => !c.IsEmpty).OrderBy(k => k.FillId).ToDictionary(k => k.FillId, k => k.ToDefinition());
            var materialDefintions = Materials
                .Where(m => m.SameAs == null)
                .Select(m => new TerrainMaterialDefinition(m.ToDefinition(), Materials.Where(o => o == m || o.SameAs == m).Select(o => o.FillId).OrderBy(m => m).ToArray()))
                .OrderBy(m => m.Usages.Min())
                .ToList();
            json.Materials = new TerrainMaterialLibrary(materialDefintions, TextureSizeInMeters);
            json.Roads = Roads.OrderBy(k => k.FillId).Select(r => r.ToDefinition()).ToList();
            json.Bridges = Roads.Select(r => new { Key = r.FillId, Bridge = r.ToBridgeDefinition() })
                .Where(b => b.Bridge != null)
                .OrderBy(k => k.Key)
                .ToDictionary(k => k.Key, k => k.Bridge!);
            json.Ponds = Ponds.ToDictionary(p => p.Id, k => k.ToDefinition());
            json.BaseDependency = baseDependency;
            json.BaseWorldName = baseWorldName;
            json.Dependencies = ComputeModDependencies().Select(GetSteamId).Where(s => s != null).Select(m => new ModDependencyDefinition(m!)).ToList();
            return json;
        }

        private string? GetSteamId(string m)
        {
            // FIXME: it assumes workshop directory
            return Path.GetFileName(Path.GetDirectoryName(m))!; 
        }

        private void EquilibrateProbabilities()
        {
            foreach (var list in Filling.OfType<FillingAssetClusterViewModel>().GroupBy(c => c.FillId))
            {
                DefinitionHelper.EquilibrateProbabilities(list.ToList());
            }
            foreach (var list in Filling.OfType<FillingAssetBasicViewModel>().GroupBy(c => c.FillId))
            {
                DefinitionHelper.EquilibrateProbabilities(list.ToList());
            }
            foreach (var list in Filling.OfType<FencesViewModel>().GroupBy(c => c.FillId))
            {
                DefinitionHelper.EquilibrateProbabilities(list.ToList());
            }
            foreach (var item in Filling.Concat(Individual))
            {
                item.Equilibrate();
            }
        }

        protected override Task DoNew()
        {
            FromJson(new Arma3Assets());
            return Task.CompletedTask;
        }

        protected override async Task DoSave(string filePath)
        {
            using var stream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(stream, ToJson(), Arma3Assets.CreateJsonSerializerOptions(_arma3Data.Library));
        }

        internal Task EditAssetCategory(IDocument document)
        {
            return _shell.OpenDocumentAsync(document);
        }

        internal void EditComposition(IWithComposition document)
        {
            _shell.ShowTool(_compositionTool);
            _compositionTool.Current = document;
            _compositionTool.UndoRedoManager = UndoRedoManager;
        }

        private string baseWorldName = string.Empty;
        public string BaseWorldName
        {
            get { return baseWorldName; }
            set { baseWorldName = value; NotifyOfPropertyChange(); }
        }

        private string baseDependency = string.Empty;
        public string BaseDependency
        {
            get { return baseDependency; }
            set { baseDependency = value; NotifyOfPropertyChange(); }
        }

        public bool HasMissingMods => MissingMods.Count > 0;
        public List<MissingMod> MissingMods { get; private set; } = new List<MissingMod>();

        public IEnumerable<string> ListReferencedModels()
        {
            return Filling.SelectMany(f => f.GetModels())
                .Concat(Individual.SelectMany(f => f.GetModels()))
                .Concat(Roads.SelectMany(f => f.GetModels()))
                .Concat(Ponds.Where(p => !string.IsNullOrEmpty(p.Model)).Select(p => p.Model!))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public IEnumerable<string> ComputeModDependencies()
        {
            return (_arma3Data.ProjectDrive.SecondarySource as PboFileSystem)?.GetModPaths(ListReferencedModels()) ?? Enumerable.Empty<string>();
        }

        public async Task Reload()
        {
            if (!IsNew)
            {
                MissingMods.Clear();
                NotifyOfPropertyChange(nameof(HasMissingMods));
                NotifyOfPropertyChange(nameof(MissingMods));

                await DoLoad(FilePath);
            }
        }
    }
}
