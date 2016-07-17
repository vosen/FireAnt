using Orleans.CodeGeneration;
using Orleans.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FireAnt.Interfaces
{
    [RegisterSerializer]
    internal static class TestResultSerializer
    {
        static TestResultSerializer()
        {
            SerializationManager.Register(typeof(TestResult), DeepCopier, Serializer, Deserializer);
        }

        static object DeepCopier(object obj)
        {
            return obj;
        }

        static void Serializer(object raw, BinaryTokenStreamWriter stream, Type expected)
        {
            TestResult result = (TestResult)raw;
            stream.Write(result.Tag);
            if(result.IsSkipped)
                stream.Write(((TestResult.Skipped)result).reason);
        }

        static object Deserializer(Type expected, BinaryTokenStreamReader stream)
        {
            int tag = stream.ReadInt();
            if(tag == TestResult.Tags.Failed)
                return TestResult.Failed;
            if (tag == TestResult.Tags.Passed)
                return TestResult.Passed;
            string reason = stream.ReadString();
            return TestResult.NewSkipped(reason);
        }
    }
}