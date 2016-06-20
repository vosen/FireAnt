using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FireAnt.Interfaces
{
    public interface IRemoteTestRunner : IGrainWithGuidKey
    {
        Task<RunSummary> RunXunit1(Immutable<ITestCase> test);
        Task<RunSummary> RunXunit2(Immutable<XunitTestCaseProxy> test);
    }
}
