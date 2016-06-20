using System;
using System.Linq;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace FireAnt.Interfaces
{
    [Serializable]
    class TypeInfoData : ITypeInfo
    {
        private TypeInfoData(ITypeInfo returnType)
        {
            Assembly = AssemblyInfoData.Create(returnType.Assembly);
            BaseType = null; //TypeInfoData.Create(returnType.BaseType);
            Interfaces = returnType.Interfaces.Select(i => TypeInfoData.Create(i)).ToList();
            IsAbstract = returnType.IsAbstract;
            IsGenericParameter = returnType.IsGenericParameter;
            IsGenericType = returnType.IsGenericType;
            IsSealed = returnType.IsSealed;
            IsValueType = returnType.IsValueType;
            Name = returnType.Name;
        }

        public static TypeInfoData Create(ITypeInfo type)
        {
            return type == null ? null : new TypeInfoData(type);
        }

        public IAssemblyInfo Assembly { get; set; }
        public ITypeInfo BaseType { get; set; }
        public IEnumerable<ITypeInfo> Interfaces { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsGenericParameter { get; set; }
        public bool IsGenericType { get; set; }
        public bool IsSealed { get; set; }
        public bool IsValueType { get; set; }
        public string Name { get; set; }

        public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ITypeInfo> GetGenericArguments()
        {
            throw new NotImplementedException();
        }

        public IMethodInfo GetMethod(string methodName, bool includePrivateMethod)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<IMethodInfo> GetMethods(bool includePrivateMethods)
        {
            throw new NotImplementedException();
        }
    }
}