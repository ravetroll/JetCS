using System;
using System.Collections.Generic;

namespace JetCS.Domain;

public partial class Database
{
    public int DatabaseId { get; set; }

    public string Name { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public virtual ICollection<DatabaseLogin> DatabaseLogins { get; set; } = new List<DatabaseLogin>();
}
