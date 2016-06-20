using Orleans.CodeGeneration;
using Orleans.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FireAnt.Interfaces
{
    public class XunitTestCaseProxy : IXunitTestCase
    {
        public string DisplayName { get; private set; }
        public IMethodInfo Method { get; private set; }
        public string SkipReason { get; private set; }
        private SourceInformationData sourceInformation;
        public ISourceInformation SourceInformation
        {
            get { return sourceInformation; }
            set { sourceInformation = (SourceInformationData)value; }
        }
        public ITestMethod TestMethod { get; private set; }
        public object[] TestMethodArguments { get; private set; }
        public Dictionary<string, List<string>> Traits { get; private set; }
        public string UniqueID { get; private set; }

        public XunitTestCaseProxy(IXunitTestCase test)
        {
            DisplayName = test.DisplayName;
            Method = MethodInfoData.Create(test.Method);
            SkipReason = test.SkipReason;
            SourceInformation = SourceInformationData.Create(test.SourceInformation);
            TestMethod = TestMethodData.Create(test.TestMethod);
            TestMethodArguments = test.TestMethodArguments;
            Traits = test.Traits;
            UniqueID = test.UniqueID;
        }

        Task<RunSummary> IXunitTestCase.RunAsync(IMessageSink diagnosticMessageSink, IMessageBus messageBus, object[] constructorArguments, ExceptionAggregator aggregator, CancellationTokenSource cancellationTokenSource)
        {
            throw new InvalidOperationException();
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            throw new InvalidOperationException();
        }

        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {

            throw new InvalidOperationException();
        }
    }
}
