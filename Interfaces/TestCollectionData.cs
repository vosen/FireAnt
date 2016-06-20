using System;
using Xunit.Abstractions;

namespace FireAnt.Interfaces
{
    [Serializable]
    class TestCollectionData : ITestCollection
    {
        public ITypeInfo CollectionDefinition { get; set; }
        public string DisplayName { get; set; }
        public ITestAssembly TestAssembly { get; set; }
        public Guid UniqueID { get; set; }

        private TestCollectionData(ITestCollection testCollection)
        {
            CollectionDefinition = TypeInfoData.Create(testCollection.CollectionDefinition);
            DisplayName = testCollection.DisplayName;
            TestAssembly = TestAssemblyData.Create(testCollection.TestAssembly);
            UniqueID = testCollection.UniqueID;
        }

        internal static TestCollectionData Create(ITestCollection assembly)
        {
            return assembly == null ? null : new TestCollectionData(assembly);
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