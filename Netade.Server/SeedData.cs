using Netade.Common.Helpers;
using Netade.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Server
{
    public class SeedData
    {
        private readonly IDbContextFactory<NetadeDbContext> dbContextFactory;

        public SeedData(IDbContextFactory<NetadeDbContext> dbContextFactory)
        {
            this.dbContextFactory = dbContextFactory;
        }

        public void SetDefault()
        {
            using (var dbContext = dbContextFactory.CreateDbContext())
            {
                if (dbContext.Logins.Where(t => (t.IsAdmin ?? false) == true).Count() == 0)
                {

                    var hash = PasswordTools.HashPassword("");
                    dbContext.Logins.Add(new Domain.Login() { LoginName = "admin", Hash = hash.Key, Salt = hash.Value, IsAdmin = true });
                }
                dbContext.SaveChanges();
            }

        }
    }
}
