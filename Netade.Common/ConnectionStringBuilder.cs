using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Netade.Common
{
    public class ConnectionStringBuilder
    {
        private const string providerKey = "Provider";
        private const string providerValue = "sqlnetade";
        private const string databaseKey = "Database";
        private const string serverKey = "Server";
        private const string portKey = "Port";
        private const string loginKey = "User Id";
        private const string passwordKey = "Password";

        private string databaseValue = "";
        private string serverValue = "";
        private string loginValue = "admin";
        private string passwordValue = "";
        private int portValue = 1549;
        private bool initialized = false;


        public ConnectionStringBuilder(string connectionString)
        {
            initialized = Deserialize(connectionString);
        }

        public ConnectionStringBuilder(string databaseName, string serverName, int port, string login, string password)
        {
            initialized = Build(databaseName, serverName, port, login, password);  
        }

        public ConnectionStringBuilder(string databaseName, string serverName,  string login, string password)
        {
            initialized = Build(databaseName, serverName, login, password);

            
        }
        public ConnectionStringBuilder(string databaseName, string serverName)
        {
            initialized = Build(databaseName, serverName);
        }

        public bool Deserialize(string? connectionString)
        {
            bool result = true;
            try
            {
                
                string? providerIn;
                string? databaseIn;
                string? serverIn;
                string? portIn;
                string? loginIn;
                string? passwordIn;
                var kv = connectionString.ToLower().Split(";");
                var dict = kv.ToDictionary(t => t.Split("=").First(), t => t.Split("=").Last());
                if (dict != null)
                {
                    if (!dict.TryGetValue(providerKey.ToLower(), out providerIn))
                    {
                        result = false;
                    }
                    else
                    {
                        if (providerValue != providerIn) result = false;
                    }
                    if (!dict.TryGetValue(databaseKey.ToLower(), out databaseIn))
                    {
                        result = false;
                    }
                    else
                    {
                        databaseValue = databaseIn;
                    }
                    if (!dict.TryGetValue(serverKey.ToLower(), out serverIn))
                    {
                        result = false;
                    }
                    else
                    {
                        serverValue = serverIn ?? "";
                    }
                    if (!dict.TryGetValue(portKey.ToLower(), out portIn))
                    {
                        // use default 
                    }
                    else
                    {
                        portValue = int.Parse(portIn);
                    }
                    if (!dict.TryGetValue(loginKey.ToLower(), out loginIn))
                    {
                        // use default
                    }
                    else
                    {
                        loginValue = loginIn;
                    }
                    if (!dict.TryGetValue(passwordKey.ToLower(), out passwordIn))
                    {
                        // use default
                    }
                    else
                    {
                        passwordValue = passwordIn;
                    }

                }
                
            }
            catch
            {
                result = false;
            }
            return result;
        }
        public static string ProviderKey { get { return providerKey; } }
        public static string DatabaseKey { get { return databaseKey; } }
        public static string ServerKey { get { return serverKey; } }
        public static string PortKey { get { return portKey; } }
        public static string LoginKey { get { return loginKey; } }
        public static string PasswordKey { get { return passwordKey; } }



        public string Database => databaseValue;
        public static string Provider => providerValue;
        public string Server => serverValue;
        public int Port => portValue;
        public bool Initialized => initialized;
        public string Login => loginValue;
        public string Password => passwordValue;
        
        public bool Build(string databaseName, string serverName, int port, string login, string password)
        {
            databaseValue = databaseName;
            serverValue = serverName;
            portValue = port;
            loginValue = login;
            passwordValue = password;
            return true;
        }

        public bool Build(string databaseName, string serverName, string login, string password)
        {
            databaseValue = databaseName;
            serverValue = serverName;            
            loginValue = login;
            passwordValue = password;
            return true;
        }
        public bool Build(string databaseName, string serverName)
        {
            databaseValue = databaseName;
            serverValue = serverName;            
            return true;
        }



        public override string ToString()
        {
            return  initialized ? providerKey + "=" + providerValue + ";" + databaseKey + "=" + databaseValue + 
                ";" + serverKey + "=" + serverValue + ";" + portKey + "=" + portValue.ToString() + ";" + loginKey +
                "=" + loginValue + ";" + passwordKey + "=" + passwordValue  : "";
        }
    }
}
