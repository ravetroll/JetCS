using JetCS.Common.Helpers;
using JetCS.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Server
{
    public class SeedData
    {
        private readonly JetCSDbContext context;

        public SeedData(JetCSDbContext context)
        {
            this.context = context;
        }

        public void SetDefault()
        {
            if (context.Logins.Where(t=>(t.IsAdmin ?? false)==true).Count() == 0)
            {
                
                var hash = PasswordTools.HashPassword("");
                context.Logins.Add(new Domain.Login() { LoginName="admin",Hash=hash.Key,Salt=hash.Value, IsAdmin=true});
            }
            context.SaveChanges();

        }
    }
}
