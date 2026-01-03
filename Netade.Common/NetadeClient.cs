using Netade.Common.Helpers;
using Netade.Common.Messaging;
using Netade.Common.Serialization;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Netade.Common
{
    public class NetadeClient
    {
        
        private readonly bool compressedMode;
        private ConnectionStringBuilder csb;
        

        public NetadeClient(string connection, bool compressedMode)
        {
            csb = new ConnectionStringBuilder(connection);
            
            this.compressedMode = compressedMode;
            

        }

        public bool CompressedMode => compressedMode;

        public CommandResult SendCommand(string command, CommandOptions? options = null)
        {
           

            if (csb.Initialized)
            {

                CommandResult result;
                TcpClient client;
                int bytesReadSum = 0;
                
                try
                {
                    client = new TcpClient(csb.Server, csb.Port);
                }
                catch (SocketException ex)
                {
                    return new CommandResult() { ErrorMessage = $"Unable to connect to server: {ex.Message}" };
                }
                catch (Exception ex)
                {
                    return new CommandResult() { ErrorMessage = $"An error occurred: {ex.Message}" };
                }
                
                
                NetworkStream stream;

                try
                {
                    stream = client.GetStream();
                }
                catch (Exception ex)
                {
                    return new CommandResult() { ErrorMessage = $"Unable to get stream: {ex.Message}" };
                }

                // Send SQL INSERT statement to server

                byte[] sendData;
                try
                {
                    sendData = PrepareCommand(command, options);
                   
                }
                catch (Exception ex)
                {
                    return new CommandResult() { ErrorMessage = $"Unable to prepare command: {ex.Message}" };
                }

                try
                {
                    stream.Write(sendData, 0, sendData.Length);
                }
                catch (Exception ex)
                {
                    return new CommandResult() { ErrorMessage = $"Unable to send command: {ex.Message}" };
                }

                // Receive response from server (optional)
                byte[] receivedData;

                try
                {
                    receivedData = ReceiveResult(ref bytesReadSum, stream);
                }
                catch (Exception ex)
                {
                    return new CommandResult() { ErrorMessage = $"Unable to receive response: {ex.Message}" };
                }

                stream.Close();
                client.Close();

                try
                {
                    result = BuildResponse(receivedData, bytesReadSum);
                }
                catch (Exception ex)
                {
                    return new CommandResult() { ErrorMessage = $"Unable to build response: {ex.Message}" };
                }

                // Close connection
               
                return result;


            }
            else
            {
                return new CommandResult() { ErrorMessage = "ConnectionString not initialized" };
            }
        }

        private CommandResult BuildResponse(byte[] receivedData, int bytesReadSum)
        {
            string dataReceived;
            if (compressedMode)
            {
                dataReceived = CompressionTools.DecompressData(receivedData, bytesReadSum);

            }
            else
            {
                dataReceived = Encoding.ASCII.GetString(receivedData);
            }
            CommandResult result = Netade.Common.Serialization.ConvertCommandAndResult.DeSerializeCommandResult(dataReceived);
            //result.Result = RemoveEmptyRow(result.Result);
            return result;
        }

        private static byte[] ReceiveResult(ref int bytesReadSum, NetworkStream stream)
        {
            byte[] receivedData;
            
            using (MemoryStream ms = new MemoryStream())
            {
                StringBuilder responseData = new StringBuilder();
                byte[] buffer = new byte[1024];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    bytesReadSum += bytesRead;
                    ms.Write(buffer, 0, bytesRead);
                }
                receivedData = ms.ToArray();
            }
            
            return receivedData;
        }

        private byte[] PrepareCommand(string command, CommandOptions? options = null)
        {
            Command cmd = new Command(csb.ToString(), command, options);
            string cmdJson = Netade.Common.Serialization.ConvertCommandAndResult.SerializeCommand(cmd);
            byte[] data;
            if (compressedMode)
            {
                data = CompressionTools.CompressData(cmdJson);
            }
            else
            {
                data = Encoding.ASCII.GetBytes(cmdJson);
            }

            return data;
        }

        //  Would rather do this via a custom Jsonconverter but just can't make it work right now
        static DataTable? RemoveEmptyRow(DataTable? t)
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
