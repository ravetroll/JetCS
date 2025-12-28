using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace Netade.ServerTest
{
    

    public sealed class TestHostControl : HostControl
    {
        public int StopCalls { get; private set; }
        public TopshelfExitCode? LastExitCode { get; private set; }

        public void RequestAdditionalTime(TimeSpan timeRemaining)
        {
            // usually no-op for unit tests; record if you care
        }

        public void Stop()
        {
            StopCalls++;
            LastExitCode = TopshelfExitCode.Ok;
        }

        public void Stop(TopshelfExitCode exitCode)
        {
            StopCalls++;
            LastExitCode = exitCode;
        }

        public void Restart()
        {
            // optional; implement if your Server uses it
            throw new NotImplementedException();
        }
    }

}
