﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Caliburn.Micro;
using GameRealisticMap.Arma3.GameEngine.Roads;
using GameRealisticMap.Geometries;
using GameRealisticMap.Studio.Controls;
using GameRealisticMap.Studio.Modules.Arma3Data;
using GameRealisticMap.Studio.Modules.AssetBrowser.Services;
using Gemini.Framework;
using Gemini.Framework.Commands;
using Gemini.Modules.Shell.Commands;
using HugeImages;
using SixLabors.ImageSharp.PixelFormats;

namespace GameRealisticMap.Studio.Modules.Arma3WorldEditor.ViewModels
{
    internal class Arma3WorldMapViewModel : Document,
        ICommandHandler<SaveFileCommandDefinition>,
        ICommandHandler<SaveFileAsCommandDefinition>
    {
        private readonly Arma3WorldEditorViewModel parentEditor;
        private readonly IArma3DataModule arma3Data;

        private IEditablePointCollection? editPoints;

        public double BackgroundResolution { get; }

        private BackgroundMode _backgroundMode;
        private EditTool _editTool;
        private GrmMapEditMode _editMode;
        private EditableArma3RoadTypeInfos? selectectedRoadType;

        public Arma3WorldMapViewModel(Arma3WorldEditorViewModel parent, IArma3DataModule arma3Data)
        {
            this.parentEditor = parent;
            this.arma3Data = arma3Data;
            BackgroundResolution = parentEditor.Imagery?.Resolution ?? 1;
            DisplayName = parent.DisplayName + " - Editor";
        }

        public IEditablePointCollection? EditPoints
        {
            get { return editPoints; }
            set { if (editPoints != value) { editPoints = value; NotifyOfPropertyChange(); } }
        }

        public TerrainSpacialIndex<TerrainObjectVM>? Objects { get; set; }

        public ICommand SelectItemCommand => new RelayCommand(SelectItem);

        public ICommand InsertPointCommand => new RelayCommand(p => InsertPoint((TerrainPoint)p));

        private void InsertPoint(TerrainPoint point)
        {
            var roads = Roads;
            if (roads != null && SelectectedRoadType != null)
            {
                var road = new EditableArma3Road(
                    SelectectedRoadType.Id,
                    SelectectedRoadType,
                    new TerrainPath(point));
                road.IsRemoved = true;
                roads.Roads.Add(road);
                SelectItem(road);
                _editMode = GrmMapEditMode.ContinuePath;
                NotifyOfPropertyChange(nameof(GrmMapEditMode));
            }
        }

        public void SelectItem(object? item)
        {
            if (item is EditableArma3Road road)
            {
                EditPoints = new EditRoadEditablePointCollection(road, this);
                EditPoints.CollectionChanged += MakeRoadsDirty;
            }
            else
            {
                EditPoints = null;
            }
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

        public EditTool EditTool
        {
            get { return _editTool; }
            set
            {
                if (_editTool != value) // Value was sent by toolbar
                {
                    _editTool = value;
                    NotifyOfPropertyChange();

                    if (_editTool == EditTool.AddRoad)
                    {
                        _editMode = GrmMapEditMode.InsertPoint;
                        EditPoints = null;
                    }
                    else
                    {
                        _editMode = GrmMapEditMode.None;
                    }
                    NotifyOfPropertyChange(nameof(GrmMapEditMode));
                }
            }
        }

        public GrmMapEditMode GrmMapEditMode
        {
            get { return _editMode; }
            set
            {
                if (_editMode != value) // Value was sent by map control
                {
                    _editMode = value;
                    NotifyOfPropertyChange();

                    if (_editMode == GrmMapEditMode.None)
                    {
                        _editTool = EditTool.Cursor;
                    }
                    else
                    {
                        _editTool = EditTool.AddRoad;
                    }
                    NotifyOfPropertyChange(nameof(EditTool));
                }
            }
        }

        public List<RoadTypeSelectVM> RoadTypes { get; private set; } = new List<RoadTypeSelectVM>();

        public EditableArma3RoadTypeInfos? SelectectedRoadType
        {
            get { return selectectedRoadType; }
            set
            {
                if (selectectedRoadType != value)
                {
                    selectectedRoadType = value;
                    NotifyOfPropertyChange();
                    foreach (var item in RoadTypes)
                    {
                        item.NotifyOfPropertyChange(nameof(item.IsSelected));
                    }
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

        private async Task DoLoadWorld()
        {
            var world = parentEditor.World;
            if (world == null)
            {
                return;
            }

            var models = world.Objects.Select(o => o.Model).Where(m => !string.IsNullOrEmpty(m)).Distinct();

            var itemsByPath = await IoC.Get<IAssetsCatalogService>().GetItems(models).ConfigureAwait(false);

            var index = new TerrainSpacialIndex<TerrainObjectVM>(SizeInMeters);
            foreach (var obj in world.Objects)
            {
                if (itemsByPath.TryGetValue(obj.Model, out var modelinfo))
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

            var roads = Roads;
            if (roads != null)
            {
                RoadTypes = roads.RoadTypeInfos.Select(rti => new RoadTypeSelectVM(rti, this)).ToList();
                SelectectedRoadType = roads.RoadTypeInfos.FirstOrDefault();
            }
            else
            {
                RoadTypes = new List<RoadTypeSelectVM>();
            }

        }


        void ICommandHandler<SaveFileCommandDefinition>.Update(Command command)
        {
            ((ICommandHandler<SaveFileCommandDefinition>)parentEditor).Update(command);
        }

        Task ICommandHandler<SaveFileCommandDefinition>.Run(Command command)
        {
            return ((ICommandHandler<SaveFileCommandDefinition>)parentEditor).Run(command);
        }

        void ICommandHandler<SaveFileAsCommandDefinition>.Update(Command command)
        {
            ((ICommandHandler<SaveFileAsCommandDefinition>)parentEditor).Update(command);
        }

        Task ICommandHandler<SaveFileAsCommandDefinition>.Run(Command command)
        {
            return ((ICommandHandler<SaveFileAsCommandDefinition>)parentEditor).Run(command);
        }
    }
}
