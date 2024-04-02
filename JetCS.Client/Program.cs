// See https://aka.ms/new-console-template for more information
using System.Data;
using System.IO;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Text;
using JetCS.Common;
using JetCS.Common.Helpers;
using JetCS.Common.Messaging;


Console.ReadLine();
try
{
    // Connect to the server
    ConnectionStringBuilder connStrBuild = new ConnectionStringBuilder("db","127.0.0.1");
    string connStr = connStrBuild.ToString();
    while (1 == 1)
    {
        
        
        RunCommand(connStr, "INSERT INTO YourTable (Column1, Column2) VALUES ('Value1', 'Value2')");
        RunCommand(connStr, "SELECT * FROM YourTable");
        RunCommand(connStr, "CREATE DATABASE TEST");        
        RunCommand(connStr, "CREATE LOGIN user1 password");
        RunCommand(connStr, "GRANT DATABASE TEST user1");
        connStrBuild = new ConnectionStringBuilder("TEST", "127.0.0.1","user1","password");
        connStr = connStrBuild.ToString();
        RunCommand(connStr, "CREATE TABLE YourTable (ID COUNTER, Column1 VARCHAR, Column2 VARCHAR,CONSTRAINT [PrimaryKey] PRIMARY KEY ([ID]))");
        RunCommand(connStr, "INSERT INTO YourTable (Column1, Column2) VALUES ('Value1', 'Value2')");
        RunCommand(connStr, "SELECT * FROM YourTable");
        RunCommand(connStr, "UPDATE YourTable SET Column2 = 'Value3'");
        RunCommand(connStr, "INSERT INTO YourTable (Column1, Column2) SELECT Column1, Column2 FROM YourTable");
        RunCommand(connStr, "SELECT * FROM YourTable");
        RunCommand(connStr, "DROP TABLE YourTable");
        connStrBuild = new ConnectionStringBuilder("db", "127.0.0.1");
        connStr = connStrBuild.ToString();
        RunCommand(connStr, "REVOKE DATABASE TEST user1");
        RunCommand(connStr, "DROP LOGIN user1");
        RunCommand(connStr, "DROP DATABASE TEST");
        //Console.ReadLine();
    }

}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex.Message);
}

static void RunCommand(string connStr, string dropStatement)
{
    Console.WriteLine();
    var startTime = DateTime.Now;
    CommandResult result;
    var cli = new JetCSClient(connStr,true);
    result = cli.SendCommand(dropStatement);
    if (result.ErrorMessage == null)
    {
        if (result.Result != null)
        {
            Console.WriteLine(DataTableFormatter.DataTableToString(result.Result));
        }
    }
    else
    {
        Console.WriteLine("ERROR: " + result.CommandName + ":" + result.ErrorMessage);
    }
    Console.WriteLine($"{(DateTime.Now - startTime).TotalSeconds} seconds to run {dropStatement}");
}