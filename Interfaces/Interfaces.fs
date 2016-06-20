namespace FireAnt

open Orleans
open Orleans.CodeGeneration
open Orleans.Concurrency
open Orleans.Serialization
open System
open System.Threading.Tasks
open Xunit.Abstractions
open Xunit.Sdk

module Interfaces =
    type IRemoteTestRunner =
        inherit IGrainWithGuidKey
        abstract RunXunit1: Immutable<ITestCase> -> Task<RunSummary>
        abstract RunXunit2: Immutable<IXunitTestCase> -> Task<RunSummary>

    // For some reason BinaryTokenStreamWriter has Write(...) overload for bool,
    // but BinaryTokenStreamReader doesn't have ReadBool()
    type BinaryTokenStreamReader with
        member t.ReadBool() : bool =
            t.ReadByte() = 3uy
    type BinaryTokenStreamWriter with
        member t.WriteBool(b: bool) =
            t.Write(if b then 3uy else 4uy)

    [<RegisterSerializer>]
    module private IXunitTestCaseSerializer =
        type SimpleIXunitTestCase(stream: BinaryTokenStreamReader) =
            let displayName = stream.ReadString()
            let skipReason = stream.ReadString()
            let sourceInformation = SerializationManager.DeserializeInner(stream)
            let testMethod = SerializationManager.DeserializeInner(stream)
            let testMethodArgs = SerializationManager.DeserializeInner(stream)
            let traits = SerializationManager.DeserializeInner(stream)
            let uniqueID = stream.ReadString()
            let methodInfo = SerializationManager.DeserializeInner(stream)
            interface IXunitTestCase with
                member t.DisplayName = displayName
                member t.SkipReason = skipReason
                member val SourceInformation = sourceInformation with get, set
                member t.TestMethod = testMethod
                member t.TestMethodArguments = testMethodArgs
                member t.Traits = traits
                member t.UniqueID = uniqueID
                member t.Method = methodInfo
                member t.RunAsync(_, _, _, _, _) =
                    raise <| InvalidOperationException()
                member t.Deserialize(_) =
                    raise <| InvalidOperationException()
                member t.Serialize(_) =
                    raise <| InvalidOperationException()

        let DeepCopier (original: obj) : obj =
            original

        let Serializer (original: obj) (stream: BinaryTokenStreamWriter) (_: Type) =
            let original = original :?> IXunitTestCase
            stream.Write(original.DisplayName)
            stream.Write(original.SkipReason)
            SerializationManager.SerializeInner(original.SourceInformation, stream, null)
            SerializationManager.SerializeInner(original.TestMethod, stream, null)
            SerializationManager.SerializeInner(original.TestMethodArguments, stream, null)
            SerializationManager.SerializeInner(original.Traits, stream, null)
            stream.Write(original.UniqueID)
            SerializationManager.SerializeInner(original.Method, stream, null)

        let Deserializer (_: Type) (stream: BinaryTokenStreamReader) : obj =
            SimpleIXunitTestCase(stream) :> obj

        SerializationManager.Register
            (
                typeof<IXunitTestCase>,
                SerializationManager.DeepCopier(DeepCopier),
                SerializationManager.Serializer(Serializer),
                SerializationManager.Deserializer(Deserializer)
            )

    [<RegisterSerializer>]
    module private ISourceInformationSerializer =
        type SimpleISourceInformation(stream: BinaryTokenStreamReader) =
            let fileName = stream.ReadString()
            let lineNumber = SerializationManager.DeserializeInner(stream)
            interface ISourceInformation with
                member val FileName = fileName with get, set
                member val LineNumber = lineNumber with get, set
                member t.Deserialize(_) =
                    raise <| InvalidOperationException()
                member t.Serialize(_) =
                    raise <| InvalidOperationException()

        let DeepCopier (original: obj) : obj =
            original

        let Serializer (original: obj) (stream: BinaryTokenStreamWriter) (_: Type) =
            let original = original :?> ISourceInformation
            stream.Write(original.FileName)
            SerializationManager.SerializeInner(original.LineNumber, stream, null)

        let Deserializer (_: Type) (stream: BinaryTokenStreamReader) : obj =
            SimpleISourceInformation(stream) :> obj

        SerializationManager.Register
            (
                typeof<ISourceInformation>,
                SerializationManager.DeepCopier(DeepCopier),
                SerializationManager.Serializer(Serializer),
                SerializationManager.Deserializer(Deserializer)
            )

    [<RegisterSerializer>]
    module private ITestMethodSerializer =
        type SimpleITestMethod(stream: BinaryTokenStreamReader) =
            let methodInfo = SerializationManager.DeserializeInner(stream)
            let testClass = SerializationManager.DeserializeInner(stream)
            interface ITestMethod with
                member t.Method = methodInfo
                member t.TestClass = testClass
                member t.Deserialize(_) =
                    raise <| InvalidOperationException()
                member t.Serialize(_) =
                    raise <| InvalidOperationException()

        let DeepCopier (original: obj) : obj =
            original

        let Serializer (original: obj) (stream: BinaryTokenStreamWriter) (_: Type) =
            let original = original :?> ITestMethod
            SerializationManager.SerializeInner(original.Method, stream, null)
            SerializationManager.SerializeInner(original.TestClass, stream, null)

        let Deserializer (_: Type) (stream: BinaryTokenStreamReader) : obj =
            SimpleITestMethod(stream) :> obj

        SerializationManager.Register
            (
                typeof<ITestMethod>,
                SerializationManager.DeepCopier(DeepCopier),
                SerializationManager.Serializer(Serializer),
                SerializationManager.Deserializer(Deserializer)
            )

    [<RegisterSerializer>]
    module private IMethodInfoSerializer =
        type SimpleIMethodInfo(stream: BinaryTokenStreamReader) =
            let isAbstract = stream.ReadBool()
            let isGenericMethodDefinition = stream.ReadBool()
            let isPublic = stream.ReadBool()
            let isStatic = stream.ReadBool()
            let name = stream.ReadString()
            let returnType = SerializationManager.DeserializeInner(stream)
            let typeInfo = SerializationManager.DeserializeInner(stream)
            interface IMethodInfo with
                member t.IsAbstract = isAbstract
                member t.IsGenericMethodDefinition = isGenericMethodDefinition
                member t.IsPublic = isPublic
                member t.IsStatic = isStatic
                member t.Name = name
                member t.ReturnType = returnType
                member t.Type = typeInfo
                member t.GetCustomAttributes(_) =
                    raise <| NotImplementedException()
                member t.GetGenericArguments() =
                    raise <| NotImplementedException()
                member t.GetParameters() =
                    raise <| NotImplementedException()
                member t.MakeGenericMethod(_) =
                    raise <| NotImplementedException()

        let DeepCopier (original: obj) : obj =
            original

        let Serializer (original: obj) (stream: BinaryTokenStreamWriter) (_: Type) =
            let original = original :?> IMethodInfo
            stream.WriteBool(original.IsAbstract)
            stream.WriteBool(original.IsGenericMethodDefinition)
            stream.WriteBool(original.IsPublic)
            stream.WriteBool(original.IsStatic)
            stream.Write(original.Name)
            SerializationManager.SerializeInner(original.ReturnType, stream, null)
            SerializationManager.SerializeInner(original.Type, stream, null)

        let Deserializer (_: Type) (stream: BinaryTokenStreamReader) : obj =
            SimpleIMethodInfo(stream) :> obj

        SerializationManager.Register
            (
                typeof<IMethodInfo>,
                SerializationManager.DeepCopier(DeepCopier),
                SerializationManager.Serializer(Serializer),
                SerializationManager.Deserializer(Deserializer)
            )

    [<RegisterSerializer>]
    module private ITypeInfoSerializer =
        type SimpleITypeInfo(stream: BinaryTokenStreamReader) =
            let assembly = SerializationManager.DeserializeInner(stream)
            let baseType = SerializationManager.DeserializeInner(stream)
            let interfaces = SerializationManager.DeserializeInner(stream)
            let isAbstract = stream.ReadBool()
            let isGenericParameter = stream.ReadBool()
            let isGenericType = stream.ReadBool()
            let isSealed = stream.ReadBool()
            let isValueType = stream.ReadBool()
            let name = stream.ReadString()
            interface ITypeInfo with
                member t.Assembly = assembly
                member t.BaseType = baseType
                member t.Interfaces = interfaces
                member t.IsAbstract = isAbstract
                member t.IsGenericParameter = isGenericParameter
                member t.IsGenericType = isGenericType
                member t.IsSealed = isSealed
                member t.IsValueType = isValueType
                member t.Name = name
                member t.GetCustomAttributes(_) =
                    raise <| NotImplementedException()
                member t.GetGenericArguments() =
                    raise <| NotImplementedException()
                member t.GetMethod(_, _) =
                    raise <| NotImplementedException()
                member t.GetMethods(_) =
                    raise <| NotImplementedException()

        let DeepCopier (original: obj) : obj =
            original

        let Serializer (original: obj) (stream: BinaryTokenStreamWriter) (_: Type) =
            let original = original :?> ITypeInfo
            SerializationManager.SerializeInner(original.Assembly, stream, null)
            SerializationManager.SerializeInner(original.BaseType, stream, null)
            SerializationManager.SerializeInner(original.Interfaces, stream, null)
            stream.WriteBool(original.IsAbstract)
            stream.WriteBool(original.IsGenericParameter)
            stream.WriteBool(original.IsGenericType)
            stream.WriteBool(original.IsSealed)
            stream.WriteBool(original.IsValueType)
            stream.Write(original.Name)

        let Deserializer (_: Type) (stream: BinaryTokenStreamReader) : obj =
            SimpleITypeInfo(stream) :> obj

        SerializationManager.Register
            (
                typeof<ITypeInfo>,
                SerializationManager.DeepCopier(DeepCopier),
                SerializationManager.Serializer(Serializer),
                SerializationManager.Deserializer(Deserializer)
            )