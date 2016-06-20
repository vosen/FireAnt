using System;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace FireAnt.Interfaces
{
    [Serializable]
    class AssemblyInfoData : IAssemblyInfo
    {
        public string AssemblyPath { get; set; }
        public string Name { get; set; }

        private AssemblyInfoData(IAssemblyInfo assembly)
        {
            AssemblyPath = assembly.AssemblyPath;
            Name = assembly.Name;
        }

        internal static AssemblyInfoData Create(IAssemblyInfo assembly)
        {
            return assembly == null ? null : new AssemblyInfoData(assembly);
        }

        public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName)
        {
            throw new InvalidOperationException();
        }

        public ITypeInfo GetType(string typeName)
        {
            throw new InvalidOperationException();
        }

        public IEnumerable<ITypeInfo> GetTypes(bool includePrivateTypes)
        {
            throw new InvalidOperationException();
        }
    }
}