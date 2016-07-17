using System;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace FireAnt.Interfaces
{
    public class Xunit1TestCaseProxy : ITestCase
    {
        public string DisplayName { get; private set; }
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
        public string UniqueID { get { throw new InvalidOperationException(); } }

        public Xunit1TestCaseProxy(ITestCase test)
        {
            DisplayName = test.DisplayName;
            SkipReason = test.SkipReason;
            SourceInformation = SourceInformationData.Create(test.SourceInformation);
            TestMethod = TestMethodData.Create(test.TestMethod);
            TestMethodArguments = test.TestMethodArguments;
            Traits = test.Traits;
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
