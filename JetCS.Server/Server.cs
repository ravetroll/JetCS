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
using Topshelf;
using Microsoft.Extensions.Logging;
using Topshelf.Logging;

namespace JetCS.Server
{
    public class Server:  IDisposable
    {
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IConfiguration config;
       
        private Config cfg;
        private Databases dbs;
        private SeedData seed;
        private JetCSDbContext db;
        private CommandDispatcher commandDispatcher;
        private bool isRunning = false;        
        private static readonly LogWriter Log = HostLogger.Get<Server>();

        private SemaphoreSlim clientsLimit;
        public Server(
            Config config,
            JetCSDbContext dbContext,
            CommandDispatcher commandDispatcher,
            SeedData seedData,
            Databases databases)
        {
            this.cfg = config;
            this.db = dbContext;
            this.commandDispatcher = commandDispatcher;
            this.seed = seedData;
            this.dbs = databases;
            this.clientsLimit = new SemaphoreSlim(1, this.cfg.Limits.MaxClients);
        }



        public void Reset()
        {
            if (!isRunning)
            {
                db.Database.EnsureDeleted();
                Log.Info("Server Reset");
            }
        }

        
        public bool SingleClient { get; } = false; // If true limits the system to a single TcpClient Handler
        public Databases Databases => dbs;

        public bool CompressedMode => this.cfg.CompressedMode;

        public bool IsRunning => this.isRunning;

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await clientsLimit.WaitAsync(cancellationToken);
                    var startTime = DateTime.Now;
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    //Console.WriteLine("Client connected.");
                    var endTime = DateTime.Now;
                    Console.WriteLine($"AcceptClientsAsync in: {(endTime - startTime).TotalMilliseconds}ms");
                    // Handle client in a new thread
                    if (SingleClient)
                    {
                        await Task.Run(() => HandleClientAsync(client, cancellationToken));
                    } else
                    {
                        _ = HandleClientAsync(client, cancellationToken);
                    }
                    
                        

                }
                catch (SocketException se)
                {
                    Console.WriteLine($"AcceptClientsAsync experienced a SocketException:{se.Message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"AcceptClientsAsync experienced an Exception:{e.Message}");
                }
                finally
                {
                    clientsLimit.Release();                    
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                var startTime = DateTime.Now;

                using (NetworkStream stream = client.GetStream())
                {
                    using (this.Databases)
                    {
                        
                        Command cmd = await GetCommandAsync(stream, cancellationToken);                       
                        CommandResult commandResult = await ProcessCommandAsync(cmd, this.Databases);
                        await RespondCommandAsync(stream, commandResult, cancellationToken);
                    }
                }
                
                var endTime = DateTime.Now;
                Console.WriteLine($"HandleClientAsync in: {(endTime - startTime).TotalMilliseconds}ms");
            }
            catch (Exception ex) {
                Log.Error(ex);
            }
            finally
            {
                client.Close();
            }
        }

        private async Task RespondCommandAsync(NetworkStream stream, CommandResult commandResult, CancellationToken cancellationToken)
        {

            CancellationTokenSource ctsTimeOut = new CancellationTokenSource(cfg.Limits.CommandResultTimeout);
            CancellationTokenSource combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsTimeOut.Token);
            try
            {
                string resultJson = Common.Serialization.ConvertCommandAndResult.SerializeCommandResult(commandResult);
                byte[] response;
                if (this.cfg.CompressedMode)
                {
                    response = CompressionTools.CompressData(resultJson);
                }
                else
                {
                    response = Encoding.ASCII.GetBytes(resultJson);
                }



                await stream.WriteAsync(response, 0, response.Length, combined.Token);
            }
            catch (OperationCanceledException ex)
            {
                try
                {
                    if (ctsTimeOut.IsCancellationRequested)
                    {
                        throw new TimeoutException($"The RespondCommandAsync task timed out after {cfg.Limits.CommandResultTimeout} milliseconds.");
                    }
                }
                catch (Exception ex2)
                {
                    Log.Error(ex2);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);

            }
        }

        private async Task<CommandResult> ProcessCommandAsync(Command cmd, Databases databases)
        {
            CommandResult commandResult = new CommandResult();
            if (cmd.ErrorMessage == null)
            {

                try
                {
                    commandResult = await commandDispatcher.DispatchAsync(cmd, databases);
                }
                catch (Exception ex)
                {
                    commandResult.ErrorMessage = ex.Message;
                }
            }
            else
            {
                commandResult.ErrorMessage = "Command Error: " + cmd.ErrorMessage;
            }

            return commandResult;
        }

        private  async Task<Command> GetCommandAsync(NetworkStream stream, CancellationToken cancellationToken)
        {

            CancellationTokenSource ctsTimeOut = new CancellationTokenSource(cfg.Limits.CommandTimeout);
            CancellationTokenSource combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsTimeOut.Token);
            using (MemoryStream ms = new MemoryStream())
            {
                // Receive response from server (optional)
               
                int bytesReadSum = 0;
                byte[] receivedData = [];
                string dataReceived;
                try
                {
                    byte[] buffer = new 
                    byte[1024];
                    int bytesRead = 0;
                    bool moreAvailable = true;
                    
                    while (moreAvailable && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, combined.Token)) > 0)
                    {

                        ms.Write(buffer, 0, bytesRead);
                        bytesReadSum += bytesRead;
                        moreAvailable = stream.DataAvailable;
                        if (bytesReadSum > cfg.Limits.MaxCommandSizeBytes)
                        {
                            throw new InvalidDataException($"Command size exceeds {cfg.Limits.MaxCommandSizeBytes} bytes");
                        }
                    }
                    receivedData = ms.ToArray();
                } 
                catch (OperationCanceledException ex)
                {
                    try
                    {
                        if (ctsTimeOut.IsCancellationRequested)
                        {
                            throw new TimeoutException($"The GetCommandAsync task timed out after {cfg.Limits.CommandTimeout} milliseconds.");
                        }


                    }
                    catch (Exception ex2)
                    {
                        return new Command() { ErrorMessage = ex2.Message };
                    }
                }
                catch (Exception ex)
                {
                    return new Command() { ErrorMessage = ex.Message};
                }

                try
                {

                    if (this.cfg.CompressedMode)
                    {
                        dataReceived = CompressionTools.DecompressData(receivedData, bytesReadSum);

                    }
                    else
                    {
                        dataReceived = Encoding.ASCII.GetString(receivedData);
                    }
                } catch (Exception ex)
                {
                    return new Command() { ErrorMessage = ex.Message };
                }
                try
                {
                    Command cmd = Common.Serialization.ConvertCommandAndResult.DeSerializeCommand(dataReceived);

                    return cmd;
                }
                catch (Exception ex)
                {
                    return new Command() { ErrorMessage = ex.Message };
                }
            }
        }

        public void Dispose()
        {
            if (this.isRunning) this.Stop();
        }

        public bool Start()
        {
            try
            {
                if (!isRunning)
                {
                    Log.Info("Beginning Server Startup");
                    db.Database.Migrate();
                    seed.SetDefault();
                    dbs.SyncDatabaseToFiles();
                    _cancellationTokenSource = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Loopback, 1549);
                    _listener.Start();
                    Console.WriteLine("Server started. Listening for connections...");
                    Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
                    Log.Info("Completing Server Startup");
                    isRunning = true;
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Error in Server Startup",ex);
                return false;
            }
        }

        public bool Stop()
        {
            try
            {
                if (isRunning)
                {
                    Log.Info("Beginning Server Stop");
                    dbs.SyncDatabaseToFiles();
                    _cancellationTokenSource.Cancel();
                    _listener.Stop();
                    isRunning = false;
                    Log.Info("Completing Server Stop");
                }
                return true;
            }
            catch (Exception ex)
            {
               Log.Error("Error in Server Stop",ex);
                return false;
            }
        }
    }
}
