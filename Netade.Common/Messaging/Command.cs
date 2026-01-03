namespace Netade.Common.Messaging
{
   

    

    public sealed class Command
    {
        public Command() : this("NO CONNECTION", "NO COMMAND") { }

        public Command(string connString, string commandText, CommandOptions? options = null)
        {
            ConnectionString = connString;
            CommandText = commandText;
            Options = options ?? new CommandOptions();
        }

        public string ConnectionString { get; set; } = "";
        public string CommandText { get; set; } = "";

        // Unified options bag
        public CommandOptions Options { get; set; } = new();

        // Keep ErrorMessage if you’re using it for client-side plumbing,
        // but server results should generally carry errors in CommandResult.
        public string? ErrorMessage { get; set; }

        
    }
}
