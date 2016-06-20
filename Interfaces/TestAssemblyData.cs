using System;
using Xunit.Abstractions;

namespace FireAnt.Interfaces
{
    [Serializable]
    class TestAssemblyData : ITestAssembly
    {
        public IAssemblyInfo Assembly { get; set; }
        public string ConfigFileName { get; set; }

        private TestAssemblyData(ITestAssembly testAssembly)
        {
            Assembly = AssemblyInfoData.Create(testAssembly.Assembly);
            ConfigFileName = testAssembly.ConfigFileName;
        }

        internal static TestAssemblyData Create(ITestAssembly assembly)
        {
            return assembly == null ? null : new TestAssemblyData(assembly);
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