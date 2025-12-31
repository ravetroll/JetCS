namespace Netade.Common.Messaging
{
    public enum QueryResultMode
    {
        Snapshot = 0, // return all rows (existing behavior)
        Cursor = 1    // open cursor and return first page + cursor id
    }

    public sealed class Command
    {
        public Command() : this("NO CONNECTION", "NO COMMAND") { }

        public Command(string connString, string commandText)
        {
            ConnectionString = connString;
            CommandText = commandText;
        }

        public string ConnectionString { get; set; } = "";
        public string CommandText { get; set; } = "";

        // NEW
        public QueryResultMode ResultMode { get; set; } = QueryResultMode.Snapshot;

        // NEW (used in cursor mode; also useful as “page size” if you later page snapshots)
        public int FetchSize { get; set; } = 500;

        public string? ErrorMessage { get; set; }
    }
}
