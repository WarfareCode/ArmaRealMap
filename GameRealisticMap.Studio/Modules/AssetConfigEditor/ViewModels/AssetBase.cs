﻿using System;
using System.Collections.Generic;
using GameRealisticMap.Arma3.Assets.Detection;
using GameRealisticMap.Arma3.TerrainBuilder;
using GameRealisticMap.Studio.Modules.CompositionTool.ViewModels;
using Gemini.Framework;

namespace GameRealisticMap.Studio.Modules.AssetConfigEditor.ViewModels
{
    internal abstract class AssetBase<TId, TDefinition> : Document, IModelImporterTarget
        where TId : struct, Enum 
        where TDefinition : class
    {

        protected AssetBase(TId id, AssetConfigEditorViewModel parent)
        {
            FillId = id;
            DisplayName = parent.FileName + ": " + IdText;
            ParentEditor = parent;
            Edit = new AsyncCommand(() => parent.EditAssetCategory(this));
            EditComposition = new RelayCommand(c => parent.EditComposition((IWithComposition)c));
            CompositionImporter = new CompositionImporter(this);
        }

        public TId FillId { get; }

        public string IdText => FillId.ToString();

        public string Label { get; set; } = string.Empty;

        public AssetConfigEditorViewModel ParentEditor { get; }

        public AsyncCommand Edit { get; }

        public RelayCommand EditComposition { get; }

        public CompositionImporter CompositionImporter { get; }

        public virtual void AddSingleObject(ModelInfo model, ObjectPlacementDetectedInfos detected)
        {
            throw new NotImplementedException();
        }

        public abstract TDefinition ToDefinition();
    }
}
