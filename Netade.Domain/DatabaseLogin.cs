using System;
using System.Collections.Generic;

namespace Netade.Domain;

public partial class DatabaseLogin
{
    public int DatabaseLoginId { get; set; }

    public int DatabaseId { get; set; }

    public int LoginId { get; set; }

    public virtual Database Database { get; set; } = null!;

    public virtual Login Login { get; set; } = null!;
}
