using System;
using Xunit.Abstractions;

namespace FireAnt.Interfaces
{
    [Serializable]
    class TestMethodData : ITestMethod
    {
        public IMethodInfo Method { get; set; }
        public ITestClass TestClass { get; set; }

        private TestMethodData(ITestMethod testMethod)
        {
            this.Method = MethodInfoData.Create(testMethod.Method);
            this.TestClass = TestClassData.Create(testMethod.TestClass);
        }

        internal static TestMethodData Create(ITestMethod test)
        {
            return test == null ? null : new TestMethodData(test);
        }

        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            throw new NotImplementedException();
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            throw new NotImplementedException();
        }
    }
}