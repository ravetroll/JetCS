namespace Netade.Common.Messaging
{
    public enum CommandResultKind
    {
        None = 0,
        Snapshot = 1,
        CursorOpened = 2,
        CursorPage = 3,
        Ack = 4,
        Error = 9
    }

    public sealed class CommandResult
    {
        public CommandResult() { }
        public CommandResult(string? commandName) { CommandName = commandName; }

        public string? CommandName { get; set; }

        public CommandResultKind Kind { get; set; } = CommandResultKind.None;

        public string? ErrorMessage { get; set; }

        // Snapshot OR page data:
        public Rowset? Data { get; set; }

        // Cursor metadata:
        public string? CursorId { get; set; }
        public bool? HasMore { get; set; }

        public CommandResult SetErrorMessage(string msg)
        {
            Kind = CommandResultKind.Error;
            ErrorMessage = msg;
            return this;
        }
    }
}

