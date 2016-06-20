using System;
using System.Collections.Generic;
using Xunit.Abstractions;

namespace FireAnt.Interfaces
{
    [Serializable]
    class MethodInfoData : IMethodInfo
    {
        public bool IsAbstract { get; set; }
        public bool IsGenericMethodDefinition { get; set; }
        public bool IsPublic { get; set; }
        public bool IsStatic { get; set; }
        public string Name { get; set; }
        public ITypeInfo ReturnType { get; set; }
        public ITypeInfo Type { get; set; }

        private MethodInfoData(IMethodInfo method)
        {
            IsAbstract = method.IsAbstract;
            IsGenericMethodDefinition = method.IsGenericMethodDefinition;
            IsPublic = method.IsPublic;
            IsStatic = method.IsStatic;
            Name = method.Name;
            ReturnType = TypeInfoData.Create(method.ReturnType);
            Type = TypeInfoData.Create(method.Type);
        }

        internal static MethodInfoData Create(IMethodInfo method)
        {
            return method == null ? null : new MethodInfoData(method);
        }

        public IEnumerable<IAttributeInfo> GetCustomAttributes(string assemblyQualifiedAttributeTypeName)
        {
            throw new InvalidOperationException();
        }

        public IEnumerable<ITypeInfo> GetGenericArguments()
        {
            throw new InvalidOperationException();
        }

        public IEnumerable<IParameterInfo> GetParameters()
        {
            throw new InvalidOperationException();
        }

        public IMethodInfo MakeGenericMethod(params ITypeInfo[] typeArguments)
        {
            throw new InvalidOperationException();
        }
    }
}