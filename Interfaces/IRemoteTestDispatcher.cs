using Orleans;
using Orleans.Concurrency;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FireAnt.Interfaces
{
    public interface IRemoteTestDispatcher : IGrainWithGuidKey
    {
        Task Run(string testId);
    }
}
