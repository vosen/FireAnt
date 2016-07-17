using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FireAnt.Interfaces
{
    public interface IRemoteTestRunner : IGrainWithGuidKey
    {
        Task<Immutable<RemoteRunResult>> RunXunit1(string runId, Immutable<Xunit1TestCaseProxy[]> test);
    }
}
