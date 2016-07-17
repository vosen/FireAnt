using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FireAnt.Interfaces
{
    public class RemoteRunResult
    {
        public RunSummary RunSummary { get; set; }
        public IReadOnlyList<TestCaseSummary> TestSummaries { get; set; }

        public RemoteRunResult(RunSummary runSummary, IReadOnlyList<TestCaseSummary> testSummaries)
        {
            RunSummary = runSummary;
            TestSummaries = testSummaries;
        }
    }
}
