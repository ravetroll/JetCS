namespace Netade.Common.Messaging
{
    public sealed class CursorFetchCommand
    {
        public string ConnectionString { get; set; } = "";
        public string CursorId { get; set; } = "";
        public int FetchSize { get; set; } = 500;
    }
       
}
