using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FireAnt.Interfaces
{
    public class Xunit2TestCaseProxy : Xunit1TestCaseProxy, IXunitTestCase
    {
        public IMethodInfo Method { get; private set; }

        public Xunit2TestCaseProxy(IXunitTestCase test) : base(test)
        {
            Method = MethodInfoData.Create(test.Method);
        }

        Task<RunSummary> IXunitTestCase.RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            throw new InvalidOperationException();
        }
    }
}
