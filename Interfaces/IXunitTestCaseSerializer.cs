using Orleans.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Sdk;

namespace FireAnt.Interfaces
{
    class IXunitTestCaseSerializer
    {
        static IXunitTestCaseSerializer()
        {
            SerializationManager.Register(
                typeof(IXunitTestCase),
                DeepCopier,
                Serializer,
                Deserializer);
        }

        static object DeepCopier(object original)
        {
            return original;
        }

        static void Serializer(object raw, BinaryTokenStreamWriter stream, Type _)
        {
            IXunitTestCase test = (IXunitTestCase)raw;
            SerializationManager.Serialize(new Xunit2TestCaseProxy(test), stream);
        }

        static object Deserializer(Type _, BinaryTokenStreamReader stream)
        {
            return SerializationManager.Deserialize<Xunit2TestCaseProxy>(stream);
        }
    }
}
