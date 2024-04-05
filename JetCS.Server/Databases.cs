using EntityFrameworkCore.Jet.Data;
using Microsoft.EntityFrameworkCore;
using JetCS.Common.Helpers;
using JetCS.Domain;
using JetCS.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetCS.Common.Messaging;
using System.Collections.Concurrent;

namespace JetCS.Server
{
    public class Databases: IDisposable
    {
       
        private readonly Config config;
        private readonly JetCSDbContext dbContext;
        
        private static ConcurrentDictionary<string, ReaderWriterLockSlim> locks = new ConcurrentDictionary<string, ReaderWriterLockSlim>();
        

        public Databases(Config config, JetCSDbContext db) {
            this.config = config;  
            this.dbContext = db;
            locks.TryAdd("", new ReaderWriterLockSlim());
           
        }

        public string Path => this.Configuration.DatabasePath;


        public Config Configuration => this.config;
        public JetCSDbContext DbContext => dbContext;

        public string Provider => this.Configuration.Provider;

        public void Dispose()
        {
                    
        }

        public void SyncDatabaseToFiles()
        {
            try
            {
                this.EnterWriteLock("");
                DirectoryInfo di = new DirectoryInfo(config.DatabasePath);
                bool exists = System.IO.Directory.Exists(di.FullName);
                if (!exists)
                    System.IO.Directory.CreateDirectory(di.FullName);
                var databases = di.GetFiles("*.mdb").Select(t => new Database()
                {
                    Name = t.Name.Substring(0, t.Name.Length - 4),
                    FilePath = t.FullName
                }).ToList();
                
                var dbs = dbContext.Databases.ToList();
                var databasesNames = databases.Select(t => t.Name).ToList();
                var dbsNames = dbs.Select(t => t.Name).ToList();
                var indatabasesOnly = databasesNames.Except(dbsNames);
                var indbsOnly = dbsNames.Except(databasesNames);
                foreach (Database d in databases)
                {
                    if (indatabasesOnly.Contains(d.Name))
                    {
                        dbContext.Databases.Add(d);
                    }
                }
                foreach (Database d in dbs)
                {
                    if (indbsOnly.Contains(d.Name))
                    {
                        dbContext.Databases.Remove(d);
                    }
                }
                dbContext.SaveChanges();
            }
            finally
            {
                this.ExitWriteLock("");
            }

        }

        public void DeleteDatabase(string name)
        {
            try
            {
                this.EnterWriteLock("");
                string path = config.DatabasePath + "\\" + name + ".mdb";
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                
                var db = dbContext.Databases.FirstOrDefault(t => t.Name == name);
                if (db != null)
                {
                    dbContext.Databases.Remove(db);
                    dbContext.SaveChanges();
                }
            } finally
            {
                this.ExitWriteLock("");
            }
        }

        public void CreateDatabase(string name, string connectionString)
        {
            try
            {
                this.EnterWriteLock("");
                PreciseDatabaseCreator cr = new();
                cr.CreateDatabase(connectionString);
                var db = dbContext.Databases.FirstOrDefault(t => t.Name == name);
                if (db == null)
                {
                    Database dbx = new Database() { Name = name, FilePath = config.DatabasePath + "\\" + name + ".mdb" };
                    dbContext.Databases.Add(dbx);
                    dbContext.SaveChanges();
                }
            }
            finally
            {
                this.ExitWriteLock("");
            }
        }

        public string GetDatabaseConnectionString(string name)
        {
            try
            {
                this.EnterReadLock("");
                DirectoryInfo di = new DirectoryInfo(config.DatabasePath);
                var databases = di.GetFiles("*.mdb").Select(t => new Database()
                {
                    Name = t.Name.Substring(0, t.Name.Length - 4),
                    FilePath = t.FullName
                }).ToList();
                var db = databases.SingleOrDefault(t => t.Name.ToLower() == name.ToLower());




                if (db?.FilePath == null)
                {
                    return "";
                }
                else
                {
                    return $"Provider={config.Provider};Data Source={(db?.FilePath ?? "")};";
                }
            }
            finally
            {
                this.ExitReadLock("");
            }
        }


        public async Task<Auth> LoginWithoutDatabaseAsync(string loginName, string password)
        {
            Auth auth = new Auth();
            try
            {
                this.EnterReadLock("");
                auth.LoginName = loginName;
                var dblogin = await dbContext.Logins.Include(t => t.DatabaseLogins).ThenInclude(t => t.Database).FirstOrDefaultAsync(t => t.LoginName.ToLower() == loginName.ToLower());
                if (dblogin == null)
                {

                    auth.Authenticated = false;
                    auth.StatusMessage = $"Invalid Login Name {loginName}";
                    return auth;
                }
                if (!PasswordTools.VerifyPassword(password, dblogin.Hash, dblogin.Salt))
                {

                    auth.Authenticated = false;
                    auth.StatusMessage = $"Invalid Password";

                    return auth;
                }
                auth.IsAdmin = dblogin.IsAdmin ?? false;
                auth.DatabaseNames = dblogin.DatabaseLogins.Select(t => t.Database.Name).ToList();
                auth.StatusMessage = "Authenticated";
                auth.Authenticated = true;

                
            }
            finally
            {
                this.ExitReadLock("");
               
            }
            return auth;

        }
        public async Task<Auth> LoginWithDatabaseAsync(string name, string loginName, string password)
        {
            Auth auth = await LoginWithoutDatabaseAsync(loginName, password);
            if (!auth.Authenticated)
            {
                return auth;
            }
            if (!auth.HasDatabase(name) && !auth.IsAdmin) {
                auth.StatusMessage = $"Permission denied on {name}";
                return auth;
            }
            auth.Authorized = true;
            auth.StatusMessage = "Authorized";
            return auth;
        }


        public string CreateDatabaseConnectionString(string name)
        {
            
            if (GetDatabaseConnectionString(name) == "")
            {
                return $"Provider={config.Provider};Data Source={config.DatabasePath}\\{name}.mdb;";
            }
            else
            {
                return "";
            }
        }

        public void EnterReadLock(string name)
        {
            ReaderWriterLockSlim rwLock = locks.GetOrAdd(name, _ => new ReaderWriterLockSlim());
            rwLock.EnterReadLock();
        }

        public void ExitReadLock(string name)
        {
            if (locks.TryGetValue(name, out var rwLock))
            {
                rwLock.ExitReadLock();
            }
        }

        public void EnterWriteLock(string name)
        {
            ReaderWriterLockSlim rwLock = locks.GetOrAdd(name, _ => new ReaderWriterLockSlim());
            rwLock.EnterWriteLock();            
        }

        public void ExitWriteLock(string name)
        {
            if (locks.TryGetValue(name, out var rwLock))
            {
                rwLock.ExitWriteLock();
                Console.WriteLine(rwLock.WaitingReadCount);
            }
        }





    }
}
