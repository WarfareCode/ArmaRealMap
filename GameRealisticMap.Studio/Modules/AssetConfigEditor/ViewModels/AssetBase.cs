﻿using System;
using System.Collections.Generic;
using System.Linq;
using GameRealisticMap.Arma3.Assets;
using GameRealisticMap.Arma3.Assets.Detection;
using GameRealisticMap.Studio.Modules.CompositionTool.ViewModels;
using GameRealisticMap.Studio.Modules.Explorer.ViewModels;
using Gemini.Framework;
using Gemini.Modules.UndoRedo;

namespace GameRealisticMap.Studio.Modules.AssetConfigEditor.ViewModels
{
    internal abstract class AssetBase<TId, TDefinition> : Document, IModelImporterTarget, IAssetCategory
        where TId : struct, Enum 
        where TDefinition : class
    {

        protected AssetBase(TId id, AssetConfigEditorViewModel parent)
        {
            FillId = id;
            PageTitle = Labels.ResourceManager.GetString("Asset" + IdText) ?? IdText;
            DisplayName = parent.FileName + ": " + IdText;
            ParentEditor = parent;
            Edit = new AsyncCommand(() => parent.EditAssetCategory(this));
            EditComposition = new RelayCommand(c => parent.EditComposition((IWithComposition)c));
            Back = new AsyncCommand(() => parent.EditAssetCategory(parent));
            CompositionImporter = new CompositionImporter(this);
        }

        protected override IUndoRedoManager CreateUndoRedoManager()
        {
            return ParentEditor.UndoRedoManager;
        }

        public TId FillId { get; }

        public string IdText => FillId.ToString();

        public virtual string Icon => $"pack://application:,,,/GameRealisticMap.Studio;component/Resources/Icons/{IdText}.png";

        public string PageTitle { get; }

        public string TreeName => PageTitle;

        public string Label { get; set; } = string.Empty;

        public AssetConfigEditorViewModel ParentEditor { get; }

        public AsyncCommand Edit { get; }

        public RelayCommand EditComposition { get; }

        public AsyncCommand Back { get; }

        public CompositionImporter CompositionImporter { get; }

        public virtual IEnumerable<IExplorerTreeItem> Children => Enumerable.Empty<IExplorerTreeItem>();

        public abstract void AddComposition(Composition model, ObjectPlacementDetectedInfos detected);

        public abstract void Equilibrate();

        public abstract TDefinition ToDefinition();
    }
}
