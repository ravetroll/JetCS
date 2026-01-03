using Netade.Common.Helpers;
using Netade.Common.Messaging;
using System;
using System.Data;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Netade.Common
{
    public sealed class NetadeClientAsync
    {
        private readonly bool compressedMode;
        private readonly ConnectionStringBuilder csb;

        public NetadeClientAsync(string connection, bool compressedMode)
        {
            csb = new ConnectionStringBuilder(connection);
            this.compressedMode = compressedMode;
        }

        public bool CompressedMode => compressedMode;

        public async Task<CommandResult> SendCommandAsync(
            string command,
            CommandOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (!csb.Initialized)
                return new CommandResult() { ErrorMessage = "ConnectionString not initialized" };

            TcpClient client = new TcpClient();
            try
            {
                await client.ConnectAsync(csb.Server, csb.Port, cancellationToken).ConfigureAwait(false);

                await using NetworkStream stream = client.GetStream();

                // Payload
                byte[] payload = PrepareCommand(command, options);

                // 4-byte length prefix (big-endian)
                int len = payload.Length;
                byte[] header = new byte[4];
                header[0] = (byte)((len >> 24) & 0xFF);
                header[1] = (byte)((len >> 16) & 0xFF);
                header[2] = (byte)((len >> 8) & 0xFF);
                header[3] = (byte)(len & 0xFF);

                // Send header + payload
                await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                // Receive response payload using header framing
                byte[] receivedPayload = await ReceiveResultAsync(stream, cancellationToken).ConfigureAwait(false);

                // Decode/deserialize
                return BuildResponse(receivedPayload, receivedPayload.Length);
            }
            catch (OperationCanceledException)
            {
                return new CommandResult() { ErrorMessage = "Command cancelled." };
            }
            catch (SocketException ex)
            {
                return new CommandResult() { ErrorMessage = $"Unable to connect to server: {ex.Message}" };
            }
            catch (Exception ex)
            {
                return new CommandResult() { ErrorMessage = ex.Message };
            }
            finally
            {
                try { client.Close(); } catch { /* ignore */ }
            }
        }

        private async Task<byte[]> ReceiveResultAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            // Read 4-byte header
            byte[] header = await ReadExactlyAsync(stream, 4, cancellationToken).ConfigureAwait(false);

            int len =
                (header[0] << 24) |
                (header[1] << 16) |
                (header[2] << 8) |
                (header[3]);

            if (len <= 0)
                throw new InvalidDataException("Invalid message length.");

            // Read payload exactly
            return await ReadExactlyAsync(stream, len, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[length];
            int offset = 0;

            while (offset < length)
            {
                int read = await stream.ReadAsync(buffer, offset, length - offset, cancellationToken)
                                       .ConfigureAwait(false);

                if (read == 0)
                    throw new EndOfStreamException("Remote closed the connection while reading.");

                offset += read;
            }

            return buffer;
        }

        private CommandResult BuildResponse(byte[] receivedData, int bytesReadSum)
        {
            string dataReceived = compressedMode
                ? CompressionTools.DecompressData(receivedData, bytesReadSum)
                : Encoding.ASCII.GetString(receivedData);

            return Netade.Common.Serialization.ConvertCommandAndResult.DeSerializeCommandResult(dataReceived);
        }

        private byte[] PrepareCommand(string command, CommandOptions? options = null)
        {
            Command cmd = new Command(csb.ToString(), command, options);
            string cmdJson = Netade.Common.Serialization.ConvertCommandAndResult.SerializeCommand(cmd);

            return compressedMode
                ? CompressionTools.CompressData(cmdJson)
                : Encoding.ASCII.GetBytes(cmdJson);
        }

        // Retained from sync version (if you still need it somewhere)
        private static DataTable? RemoveEmptyRow(DataTable? t)
        {
            if (t != null)
            {
                if (t.Rows.Count == 1)
                {
                    var isEmptyRow = true;
                    for (int i = 0; i < t.Columns.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(t.Rows[0][i].ToString())) isEmptyRow = false;
                        break;
                    }
                    if (isEmptyRow) t.Rows.RemoveAt(0);
                }
                return t;
            }
            return null;
        }
    }
}
