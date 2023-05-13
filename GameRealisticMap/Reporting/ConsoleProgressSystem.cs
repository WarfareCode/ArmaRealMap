﻿namespace GameRealisticMap.Reporting
{
    public class ConsoleProgressSystem : ProgressSystemBase
    {
        public override IProgressInteger CreateStep(string name, int total)
        {
            return new ConsoleProgressReport(Scope.Prefix + name, total);
        }

        public override IProgressPercent CreateStepPercent(string name)
        {
            return new ConsoleProgressReport(Scope.Prefix + name, 1000);
        }
    }
}
