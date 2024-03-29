using System;
using System.Collections.Generic;

namespace JetCS.Domain;

public partial class Login
{
    public int LoginId { get; set; }

    public string LoginName { get; set; } = null!;

    public string Hash { get; set; } = null!;

    public string Salt { get; set; } = null!;

    public bool? IsAdmin { get; set; }

    public virtual ICollection<DatabaseLogin> DatabaseLogins { get; set; } = new List<DatabaseLogin>();
}
