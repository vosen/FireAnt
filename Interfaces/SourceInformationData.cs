using System;
using Orleans.CodeGeneration;
using Xunit.Abstractions;

namespace FireAnt.Interfaces
{
    [Serializable]
    class SourceInformationData : ISourceInformation
    {
        public string FileName { get; set; }
        public int? LineNumber { get; set; }

        private SourceInformationData(ISourceInformation sourceInformation)
        {
            FileName = sourceInformation.FileName;
            LineNumber = sourceInformation.LineNumber;
        }

        internal static SourceInformationData Create(ISourceInformation si)
        {
            return si == null ? null : new SourceInformationData(si);
        }

        #region IXunitSerializable
        void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
        {
            throw new InvalidOperationException();
        }

        void IXunitSerializable.Serialize(IXunitSerializationInfo info)
        {
            throw new InvalidOperationException();
        }
        #endregion
    }
}