﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BIS.P3D;
using GameRealisticMap.Arma3.GameEngine.Roads;
using GameRealisticMap.Geometries;
using GameRealisticMap.Studio.Controls;
using GameRealisticMap.Studio.Modules.Arma3Data;
using Gemini.Framework;
using HugeImages;
using SixLabors.ImageSharp.PixelFormats;

namespace GameRealisticMap.Studio.Modules.Arma3WorldEditor.ViewModels
{
    internal class Arma3WorldMapViewModel : Document
    {
        private readonly Arma3WorldEditorViewModel parentEditor;
        private readonly IArma3DataModule arma3Data;

        public double BackgroundResolution { get; }

        private BackgroundMode _backgroundMode;

        public Arma3WorldMapViewModel(Arma3WorldEditorViewModel parent, IArma3DataModule arma3Data)
        {
            this.parentEditor = parent;
            this.arma3Data = arma3Data;
            BackgroundResolution = parentEditor.Imagery?.Resolution ?? 1;
            DisplayName = parent.DisplayName + " - Editor";
        }

        public IEditablePointCollection? EditPoints { get; set; }

        public TerrainSpacialIndex<TerrainObjectVM>? Objects { get; set; }


        public ICommand SelectItemCommand => new RelayCommand(SelectItem);

        public void SelectItem(object? item)
        {
            if (item is EditableArma3Road road)
            {
                EditPoints = new EditRoadEditablePointCollection(road, UndoRedoManager);
                EditPoints.CollectionChanged += MakeRoadsDirty;
            }
            else
            {
                EditPoints = null;
            }
            NotifyOfPropertyChange(nameof(EditPoints));
        }

        private void MakeRoadsDirty(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            parentEditor.IsRoadsDirty = true;
        }

        public EditableArma3Roads? Roads => parentEditor.Roads;

        public float SizeInMeters => parentEditor.SizeInMeters ?? 2500;

        public HugeImage<Rgb24>? SatMap { get; set; }

        public HugeImage<Rgb24>? IdMap { get; set; }

        public HugeImage<Rgb24>? BackgroundImage
        {
            get
            {
                switch (BackgroundMode)
                {
                    case BackgroundMode.Satellite: return SatMap;
                    case BackgroundMode.TextureMask: return IdMap;
                    default: return null;
                }
            }
        }

        public BackgroundMode BackgroundMode
        {
            get { return _backgroundMode; }
            set
            {
                if (_backgroundMode != value)
                {
                    _backgroundMode = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(BackgroundImage));
                }
            }
        }

        protected async override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            if (parentEditor.ConfigFile != null && !string.IsNullOrEmpty(parentEditor.ConfigFile.Roads))
            {
                if (parentEditor.Roads == null)
                {
                    parentEditor.LoadRoads();
                }
            }

            if (parentEditor.Imagery != null)
            {
                SatMap = parentEditor.Imagery.GetSatMap(arma3Data.ProjectDrive);

                var assets = await parentEditor.GetAssetsFromHistory();
                if (assets != null)
                {
                    IdMap = parentEditor.Imagery.GetIdMap(arma3Data.ProjectDrive, assets.Materials);
                }
            }

            _ = Task.Run(DoLoadWorld);

            await base.OnInitializeAsync(cancellationToken);
        }

        private void DoLoadWorld()
        {
            var world = parentEditor.World;
            if (world == null)
            {
                return;
            }

            var cache = new Dictionary<string, IModelInfo>(StringComparer.OrdinalIgnoreCase);
            var index = new TerrainSpacialIndex<TerrainObjectVM>(SizeInMeters);
            foreach (var obj in world.Objects)
            {
                // TODO: Add bounding box infos to asset manager and use it here
                if (!cache.TryGetValue(obj.Model, out var modelinfo))
                {
                    var odol = arma3Data.Library.ReadODOL(obj.Model);
                    if (odol != null)
                    {
                        cache.Add(obj.Model, modelinfo = odol.ModelInfo);
                    }
                }

                if (modelinfo != null)
                {
                    index.Insert(new TerrainObjectVM(obj, modelinfo));
                }
            }
            Objects = index;
            NotifyOfPropertyChange(nameof(Objects));
        }

        internal void InvalidateRoads()
        {
            NotifyOfPropertyChange(nameof(Roads));

            if (EditPoints != null)
            {
                EditPoints = null;
                NotifyOfPropertyChange(nameof(EditPoints));
            }
        }
    }
}
