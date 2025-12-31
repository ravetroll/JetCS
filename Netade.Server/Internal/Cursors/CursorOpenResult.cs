using Netade.Common.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server.Internal.Cursors
{
    public sealed class CursorOpenResult
    {
        public required string CursorId { get; init; }
        public required Rowset FirstPage { get; init; }
        public required bool HasMore { get; init; }
    }
}


