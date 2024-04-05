using EntityFrameworkCore.Jet.Data;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Data.OleDb;

namespace JetCS.Server.Internal.Database
{
    public  static class ProviderDetection
    {
        public static string? ApplyValidProvider(ref Config config) 
        {
            string[] providerNames = { "Microsoft.ACE.OLEDB.16.0", "Microsoft.ACE.OLEDB.12.0", "Microsoft.Jet.OLEDB.4.0" };
            if (!CreateAndDelete(config.Provider))
            {
                foreach (string providerName in providerNames)
                {
                    if(CreateAndDelete(providerName))
                    {
                        config.Provider = providerName;
                        return $"Provider changed to {providerName} as one in config not installed";
                    }
                }
                return $"No valid provider found.  Install Access Database Engine";
            }
            return null;
        }

       

       
        private static bool CreateAndDelete(string providerName) {
            try
            {
                bool valid = false;
                string name = Guid.NewGuid().ToString();
                string path = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + "\\" + name + ".mdb";
                string connectionString = $"Provider={providerName};Data Source={path};";
                PreciseDatabaseCreator cr = new();
                cr.CreateDatabase(connectionString);
                try
                {
                    using (OleDbConnection connection = new OleDbConnection( connectionString))
                    {
                        connection.Open();

                    }
                    valid = true;
                }
                catch (Exception ex)
                {

                    valid = false; 
                }
                if (File.Exists(path))
                {
                    
                    File.Delete(path);
                   
                }
                return valid;
            } catch(Exception ex) { }
            {
                return false;
            }
        }
    }
}
