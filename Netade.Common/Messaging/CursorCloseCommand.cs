namespace Netade.Common.Messaging
{
   
    public sealed class CursorCloseCommand
    {
        public string ConnectionString { get; set; } = "";
        public string CursorId { get; set; } = "";
    }
}
