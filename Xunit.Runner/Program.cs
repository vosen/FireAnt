using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Sdk;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Remoting.Messaging;
using System.Xml;

namespace FireAnt.Xunit.Runner
{
    using FireAnt.Transport;
    using System.Globalization;
    using System.Threading;

    static class Program
    {
        /*
         * args are
         * - mode (either 'discovery' or 'run')
         * - pipe name
         * - assembly
         * - (if 'run' mode) array of:
         *   - type
         *   - method
         */
        static void Main(string[] args)
        {
            if (args.Length < 3)
                throw new ArgumentException("args");
            Mode mode = GetMode(args[0]);
            using (var pipeClient = new NamedPipeClientStream(args[1]))
            {
                pipeClient.Connect();
                var executor = new Executor(args[2]);
                if (mode == Mode.Discovery)
                {
                    var messageSink = new SerializingDiscoveryMessageSink(pipeClient);
                    new Executor.EnumerateTests(executor, messageSink);
                }
                else if (mode == Mode.Run)
                {
                    Dictionary<string, List<string>> classes = UnpackClassArguments(args);
                    foreach (var classMethods in classes)
                    {
                        var messageSink = new SerializingRunMessageSink(pipeClient);
                        new Executor.RunTests(executor, classMethods.Key, classMethods.Value, messageSink);
                        messageSink.Finished.Wait();
                    }
                }
                else
                {
                    throw new ArgumentException("mode");
                }
                pipeClient.WaitForPipeDrain();
            }
        }

        static Mode GetMode(string mode)
        {
            switch (mode)
            {
                case "discovery":
                    return Mode.Discovery;
                case "run":
                    return Mode.Run;
            }
            throw new ArgumentException("mode");
        }

        static Dictionary<string, List<string>> UnpackClassArguments(string[] args)
        {
            var dict = new Dictionary<string, List<string>>((args.Length - 3) / 2, StringComparer.Ordinal);
            for (int i = 3; i < args.Length; i += 2)
                Append(dict, args[i], args[i + 1]);
            return dict;
        }

        static void Append(Dictionary<string, List<string>> dict, string key, string name)
        {
            List<string> value;
            if (dict.TryGetValue(key, out value))
            {
                value.Add(name);
            }
            else
            {
                var list = new List<string>();
                list.Add(name);
                dict[key] = new List<string>(list);
            }
        }
    }

    enum Mode
    {
        Discovery,
        Run
    }

    abstract class DiscoveryMessageSink : IMessageSink
    {
        readonly protected NamedPipeClientStream pipeClient;

        public DiscoveryMessageSink(NamedPipeClientStream pipeClient)
        {
            this.pipeClient = pipeClient;
        }

        protected abstract void Read(XmlTextReader reader);

        public IMessage SyncProcessMessage(IMessage msg)
        {
            string xmlResult = (string)msg.Properties["data"];
            using (var stringReader = new StringReader(xmlResult))
            {
                using (var reader = new XmlTextReader(stringReader))
                {
                    Read(reader);
                }
            }
            return null;
        }

        IMessageSink IMessageSink.NextSink { get { return null; } }

        IMessageCtrl IMessageSink.AsyncProcessMessage(IMessage msg, IMessageSink replySink)
        {
            throw new NotImplementedException();
        }
    }

    sealed class SerializingDiscoveryMessageSink : DiscoveryMessageSink
    {
        public SerializingDiscoveryMessageSink(NamedPipeClientStream pipeClient) : base(pipeClient)
        {
        }

        protected override void Read(XmlTextReader reader)
        {
            while (reader.ReadToFollowing("method"))
            {
                pipeClient.WriteByte(0);
                TestCase.CreateBuilder()
                        .SetType(reader.GetAttribute("type"))
                        .SetMethod(reader.GetAttribute("method"))
                        .Build()
                        .WriteDelimitedTo(pipeClient);
            }
        }
    }

    sealed class SerializingRunMessageSink : DiscoveryMessageSink
    {
        public ManualResetEventSlim Finished { get; private set; }

        public SerializingRunMessageSink(NamedPipeClientStream pipeClient) : base(pipeClient)
        {
            Finished = new ManualResetEventSlim();
        }

        protected override void Read(XmlTextReader reader)
        {
            while (reader.Read())
            {
                if (String.Equals(reader.Name, "class", StringComparison.Ordinal))
                {
                    Finished.Set();
                    break;
                }
                else if (String.Equals(reader.Name, "test", StringComparison.Ordinal))
                {
                    var result = TestResultSummary.CreateBuilder();
                    result.DisplayName = reader.GetAttribute("name");
                    result.Result = GetTestResult(reader.GetAttribute("result"));
                    string time = reader.GetAttribute("time");
                    if (time != null)
                        result.Time = Double.Parse(time, NumberFormatInfo.InvariantInfo);
                    // TODO: read output for failure
                    if (result.Result == TestResult.Fail && reader.ReadToDescendant("failure"))
                    {
                        result.ExceptionType = reader.GetAttribute(0);
                        int failureDepth = reader.Depth;
                        reader.Read();
                        if (reader.Depth == failureDepth + 1)
                            ReadFailureChildren(reader, result, failureDepth + 1);
                    }
                    else if (result.Result == TestResult.Skip && reader.ReadToDescendant("reason") && reader.ReadToDescendant("message"))
                    {
                        result.Reason = reader.ReadElementContentAsString();
                    }
                    else if (reader.ReadToDescendant("output"))
                    {
                        result.Output = reader.ReadElementContentAsString();
                    }
                    pipeClient.WriteByte(0);
                    result.Build().WriteDelimitedTo(pipeClient);
                }
            }
        }

        private void Finish()
        {
            throw new NotImplementedException();
        }

        static TestResult GetTestResult(string result)
        {
            char start = result[0];
            if (start == 'P')
                return TestResult.Pass;
            if (start == 'F')
                return TestResult.Fail;
            return TestResult.Skip;
        }

        static void ReadFailureChildren(XmlTextReader reader, TestResultSummary.Builder result, int atDepth)
        {
            bool seenMessage = false;
            bool seenTrace = false;
            while (reader.Read())
            {
                int currentDepth = reader.Depth;
                if (currentDepth < atDepth)
                    return;
                if (currentDepth > atDepth)
                    continue;
                if (!seenMessage && String.Equals(reader.Name, "message", StringComparison.Ordinal))
                {
                    result.ExceptionMessage = reader.ReadElementContentAsString();
                    seenMessage = true;
                    if (seenTrace)
                        return;
                }
                else if (!seenTrace && String.Equals(reader.Name, "stack-trace", StringComparison.Ordinal))
                {
                    result.ExceptionStackTrace = reader.ReadElementContentAsString();
                    seenTrace = true;
                    if (seenMessage)
                        return;
                }
            }
        }
    }
}
