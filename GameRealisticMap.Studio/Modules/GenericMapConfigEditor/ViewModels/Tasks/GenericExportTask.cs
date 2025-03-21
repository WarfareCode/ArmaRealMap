﻿using System.Threading.Tasks;
using GameRealisticMap.Configuration;
using GameRealisticMap.Generic;
using GameRealisticMap.Studio.Modules.Reporting;
using GameRealisticMap.Studio.Toolkit;

namespace GameRealisticMap.Studio.Modules.GenericMapConfigEditor.ViewModels.Tasks
{
    internal class GenericExportTask : IProcessTask
    {
        private readonly GenericMapConfig config;
        private readonly ISourceLocations sources;

        public GenericExportTask(GenericMapConfig config, ISourceLocations sources)
        {
            this.config = config;
            this.sources = sources;
        }

        public string Title => Labels.GenericMapGenerate;

        public bool Prompt => true;

        public async Task Run(IProgressTaskUI ui)
        {
            var generator = new GenericMapGenerator(sources);

            await generator.Generate(ui.Scope, config);

            ui.AddSuccessAction(() => ShellHelper.OpenUri(config.TargetDirectory), Labels.ViewInFileExplorer);
        }

    }
}
