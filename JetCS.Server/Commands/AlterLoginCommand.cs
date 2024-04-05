using EntityFrameworkCore.Jet.Data;
using JetCS.Common.Messaging;
using JetCS.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetCS.Domain;
using JetCS.Common.Helpers;
using System.Net;
using Microsoft.EntityFrameworkCore;

namespace JetCS.Server.Commands
{
    public class AlterLoginCommand : CommandBase, ICommand
    {
        private readonly Databases databases;

        public AlterLoginCommand()
        {

        }
        public string Name => "ALTER LOGIN";

        public string Description => $"JetCS {Name} Statement; {Name} <login> <password> ADMIN";

        public string[] Identifiers => [$"^{Name}"];
        public async Task<CommandResult> ExecuteAsync(Command cmd, Databases databases)
        {
            
            CommandResult commandResult = new(Name);
            ConnectionStringBuilder csb = new ConnectionStringBuilder(cmd.ConnectionString);
            if (!csb.Initialized)
            {
                return commandResult.SetErrorMessage($"Connection string {csb} format is incorrect");
            }

            //  Authentication and Authorization
            var auth = await databases.LoginWithoutDatabaseAsync(csb.Login, csb.Password);
            if (!auth.Authenticated)
            {
                return commandResult.SetErrorMessage(auth.StatusMessage);
            }

            if (!auth.IsAdmin)
            {
                return commandResult.SetErrorMessage($"This command required admin priviledges");
            }


            string[] commandString = cmd.CommandText.Split(" ");
            if (!(commandString.Length == 4 || commandString.Length == 5))
            {
                commandResult.SetErrorMessage($"Invalid '{Name}' Command:{cmd.CommandText}");
            }

            try
            {
                databases.EnterWriteLock("");
                Login? login = await databases.DbContext.Logins.FirstOrDefaultAsync(t => t.LoginName == commandString[2]);
                if (login != null)
                {

                    var pw = PasswordTools.HashPassword(commandString[3]);
                    login.Hash = pw.Key;
                    login.Salt = pw.Value;
                    // If User is changing their own login they cannot drop their own admin status
                    if ((commandString.Length == 5 && commandString[4].ToUpper() == "ADMIN") || login.LoginName.ToLower() == auth.LoginName.ToLower())
                    {
                        login.IsAdmin = true;
                    }
                    else
                    {
                        login.IsAdmin = false;
                    }
                    commandResult.RecordCount = await databases.DbContext.SaveChangesAsync();
                }
                else
                {
                    commandResult.ErrorMessage = $"Login '{commandString[2]}' does not exist";
                }
            }
            finally
            {
                databases.ExitWriteLock("");
            }

            return commandResult;

            


        }
    }
}
