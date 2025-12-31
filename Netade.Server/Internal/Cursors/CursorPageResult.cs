using Netade.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server.Internal.Cursors
{
    public sealed class CursorPageResult
    {
        public required Rowset Page { get; init; }
        public required bool HasMore { get; init; }
    }
}

