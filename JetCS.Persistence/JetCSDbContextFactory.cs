using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Persistence
{
    public class JetCSDbContextFactory : IDesignTimeDbContextFactory<JetCSDbContext>
    {
        public JetCSDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<JetCSDbContext>();
#pragma warning disable CA1416 // Validate platform compatibility
            Console.WriteLine(Directory.GetCurrentDirectory() + "\\JetCS.mdb;");
            optionsBuilder.UseJetOleDb($"Provider=Microsoft.ACE.OLEDB.16.0; Data Source={Directory.GetCurrentDirectory()}\\JetCS.mdb;");
#pragma warning restore CA1416 // Validate platform compatibility

            return new JetCSDbContext(optionsBuilder.Options);
        }
    }
}
