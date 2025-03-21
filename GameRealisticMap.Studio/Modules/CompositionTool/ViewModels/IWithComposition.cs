﻿using Gemini.Modules.UndoRedo;

namespace GameRealisticMap.Studio.Modules.CompositionTool.ViewModels
{
    internal interface IWithComposition
    {
        CompositionViewModel Composition { get; }

        void CompositionWasRotated(int degrees);
    }
}
