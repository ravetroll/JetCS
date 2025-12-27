using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Persistence
{
    public class NetadeDbContextFactory : IDesignTimeDbContextFactory<NetadeDbContext>
    {
        public NetadeDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<NetadeDbContext>();
#pragma warning disable CA1416 // Validate platform compatibility
            Console.WriteLine(Directory.GetCurrentDirectory() + "\\Netade.mdb;");
            optionsBuilder.UseJetOleDb($"Provider=Microsoft.ACE.OLEDB.16.0; Data Source={Directory.GetCurrentDirectory()}\\Netade.mdb;");
#pragma warning restore CA1416 // Validate platform compatibility

            return new NetadeDbContext(optionsBuilder.Options);
        }
    }
}
