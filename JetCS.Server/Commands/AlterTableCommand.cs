﻿using JetCS.Common.Messaging;
using JetCS.Persistence;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Server.Commands
{
    public class AlterTableCommand : CommandBase, ICommand
    {
        public string Name => "ALTER TABLE";

        public string Description => $"SQL {Name} Statement";

        public string[] Identifiers => [$"^{Name}"];
        public CommandResult Execute(Command cmd, Databases databases)
        {
            return ExecuteNonQueryResult(Name, cmd, databases);
        }
    }
}
