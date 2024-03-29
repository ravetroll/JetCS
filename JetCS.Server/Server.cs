using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Data.OleDb;
using Microsoft.Extensions.Configuration;
using Topshelf.Options;
using JetCS.Domain;
using JetCS.Common.Messaging;
using JetCS.Common;
using Microsoft.Extensions.DependencyInjection;
using JetCS.Persistence;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using JetCS.Common.Helpers;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace JetCS.Server
{
    public class Server: IDisposable
    {
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IConfiguration config;
        private readonly IServiceProvider serviceProvider;
        private Config cfg;
        private Databases dbs;
        private SeedData seed;
        private JetCSDbContext db;
        private CommandDispatcher commandDispatcher;
        private bool isRunning = false;

        public Server(Config config,IServiceProvider provider)
        {
            this.cfg = config;  
            this.serviceProvider = provider;
            this.db = serviceProvider.GetRequiredService<JetCSDbContext>();
            this.commandDispatcher = serviceProvider.GetRequiredService<CommandDispatcher>();
            this.seed = serviceProvider.GetRequiredService<SeedData>();
            this.dbs = serviceProvider.GetRequiredService<Databases>();
           

        }

        public void Start()
        {
            if (!isRunning)
            {
                db.Database.Migrate();
                seed.SetDefault();
                dbs.SyncDatabaseToFiles();
                _cancellationTokenSource = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Loopback, 1549);
                _listener.Start();
                Console.WriteLine("Server started. Listening for connections...");
                isRunning = true;
                Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
            }
        }

        public void Reset()
        {
            if (!isRunning)
            {
                db.Database.EnsureDeleted();
            }
        }

        public void Stop()
        {
            if (isRunning)
            {
                dbs.SyncDatabaseToFiles();
                _cancellationTokenSource.Cancel();
                _listener.Stop();
                isRunning = false;
                Console.WriteLine("Server stopped.");
            }
        }

        public Databases Databases => dbs;

        public bool CompressedMode => this.cfg.CompressedMode;

        public bool IsRunning => this.isRunning;

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var startTime = DateTime.Now;
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    //Console.WriteLine("Client connected.");
                    var endTime = DateTime.Now;
                    Console.WriteLine($"AcceptClientsAsync in: {(endTime - startTime).TotalMilliseconds}ms");
                    // Handle client in a new thread
                    await Task.Run(() => HandleClientAsync(client, cancellationToken));
                    
                }
                catch (SocketException)
                {
                    // Handle socket exception (e.g., when the server is stopped)
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            
            using (NetworkStream stream = client.GetStream())
            {
                using (Databases databases = serviceProvider.GetRequiredService<Databases>())
                {
                    Command cmd = await GetCommand(stream, cancellationToken);
                    CommandResult commandResult = ProcessCommand(cmd,databases);
                    await RespondCommand(stream, commandResult, cancellationToken);
                }
            }
            client.Close();
            var endTime = DateTime.Now;
            Console.WriteLine($"HandleClientAsync in: {(endTime-startTime).TotalMilliseconds}ms");
        }

        private  async Task RespondCommand(NetworkStream stream, CommandResult commandResult, CancellationToken cancellationToken)
        {
            string resultJson = Common.Serialization.Convert.SerializeCommandResult(commandResult);
            byte[] response;
            if (this.cfg.CompressedMode)
            {
                response = CompressionTools.CompressData(resultJson);
            }
            else
            {
                response = Encoding.ASCII.GetBytes(resultJson);
            }
           
            await stream.WriteAsync(response, 0, response.Length, cancellationToken);
        }

        private CommandResult ProcessCommand(Command cmd, Databases databases)
        {
            CommandResult commandResult = new CommandResult();
           
            try
            {
                commandResult = commandDispatcher.Dispatch(cmd,databases);
            }
            catch (Exception ex)
            {
                commandResult.ErrorMessage = ex.Message;
            }

            return commandResult;
        }

        private  async Task<Command> GetCommand(NetworkStream stream, CancellationToken cancellationToken)
        {


            using (MemoryStream ms = new MemoryStream())
            {
                // Receive response from server (optional)
                StringBuilder responseData = new StringBuilder();
                byte[] buffer = new byte[1024];
                int bytesRead = 0;
                bool moreAvailable = true;
                int bytesReadSum = 0;
                while (moreAvailable && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                   
                    ms.Write(buffer, 0, bytesRead);
                    bytesReadSum += bytesRead;
                    moreAvailable = stream.DataAvailable;
                }
                
                byte[] receivedData = ms.ToArray();
                string dataReceived;
                if (this.cfg.CompressedMode)
                {
                    dataReceived = CompressionTools.DecompressData(receivedData, bytesReadSum);

                }
                else
                {
                    dataReceived = Encoding.ASCII.GetString(receivedData);
                }
                
                Command cmd = Common.Serialization.Convert.DeSerializeCommand(dataReceived);
               
                return cmd;
            }
        }

        public void Dispose()
        {
            if (this.isRunning) this.Stop();
        }
    }
}
