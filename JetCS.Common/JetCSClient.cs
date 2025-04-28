using JetCS.Common.Helpers;
using JetCS.Common.Messaging;
using JetCS.Common.Serialization;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace JetCS.Common
{
    public class JetCSClient
    {
        
        private readonly bool compressedMode;
        private ConnectionStringBuilder csb;
        

        public JetCSClient(string connection, bool compressedMode)
        {
            csb = new ConnectionStringBuilder(connection);
            
            this.compressedMode = compressedMode;
            

        }

        public bool CompressedMode => compressedMode;

        public CommandResult SendCommand(string command)
        {
           

            if (csb.Initialized)
            {
               
                
                int bytesReadSum = 0;
                var startTime = DateTime.Now;
                TcpClient client = new TcpClient(csb.Server, csb.Port);
                var endTime = DateTime.Now;
                //Console.WriteLine($"Creates TcpClient in: {(endTime - startTime).TotalMilliseconds}ms");
                NetworkStream stream;
                stream = client.GetStream();

                // Send SQL INSERT statement to server

                byte[] sendData = PrepareCommand(command);
                stream.Write(sendData, 0, sendData.Length);

                // Receive response from server (optional)
                byte[] receivedData = ReceiveResult(ref bytesReadSum, stream);              

                CommandResult result = BuildResponse(receivedData, bytesReadSum);

                // Close connection
                stream.Close();
                client.Close();
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
            CommandResult result = JetCS.Common.Serialization.ConvertCommandAndResult.DeSerializeCommandResult(dataReceived);
            result.Result = RemoveEmptyRow(result.Result);
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

        private byte[] PrepareCommand(string command)
        {
            Command cmd = new Command(csb.ToString(), command);
            string cmdJson = JetCS.Common.Serialization.ConvertCommandAndResult.SerializeCommand(cmd);
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
