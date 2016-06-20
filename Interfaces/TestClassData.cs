using System;
using Xunit.Abstractions;

namespace FireAnt.Interfaces
{
    [Serializable]
    class TestClassData : ITestClass
    {
        public ITypeInfo Class { get; set; }
        public ITestCollection TestCollection { get; set; }

        private TestClassData(ITestClass testClass)
        {
            Class = TypeInfoData.Create(testClass.Class);
            TestCollection = TestCollectionData.Create(testClass.TestCollection);
        }

        internal static TestClassData Create(ITestClass testClass)
        {
            return testClass == null ? null : new TestClassData(testClass);
        }

        #region IXunitSerializable
        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            throw new NotImplementedException();
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}