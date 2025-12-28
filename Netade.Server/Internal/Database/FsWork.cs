using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;

namespace Netade.Server.Internal.Database
{
    internal enum FsWorkKind
    {
        RenameOnly,
        SyncOnly,
        RenameThenSync
    }

    internal sealed record FsWork(
        FsWorkKind Kind,
        string? OldPath,
        string? NewPath,
        string? Reason // optional for logging
    );
}





