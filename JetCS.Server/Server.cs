using JetCS.Common;
using JetCS.Common.Helpers;
using JetCS.Common.Messaging;
using JetCS.Domain;
using JetCS.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Topshelf.Logging;
using Topshelf.Options;

namespace JetCS.Server
{
    public class Server:  IDisposable
    {
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IConfiguration config;

        private readonly IServiceScopeFactory scopeFactory;
        private readonly Config cfg;
        private readonly ILogger<Server> log;
        private Databases dbs;
        private SeedData seed;
        
        private CommandDispatcher commandDispatcher;
        private bool isRunning = false;        
        

        private SemaphoreSlim clientsLimit;
        public Server(Config config, CommandDispatcher commandDispatcher,Databases databases, IServiceScopeFactory scopeFactory, ILogger<Server> logger)
        {
            this.cfg = config;
            this.scopeFactory = scopeFactory;
            log = logger;
            this.clientsLimit = new SemaphoreSlim(1, this.cfg.Limits.MaxClients);
            this.commandDispatcher = commandDispatcher;
            this.dbs = databases;
        }

        // Resets the server by deleting the database if it is not running
        // Used for testing purposes
        public void Reset()
        {
            if (!isRunning)
            {
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<JetCSDbContext>();
                    db.Database.EnsureDeleted();
                    log.LogInformation("Server Reset");
                }
            }
            else {
                log.LogInformation("Cannot reset the server while it is running.");
            }
        }
                
        public bool SingleClient => this.cfg.SingleClient;
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
                        // allows running in single threaded mode for easier debugging.  Would not be used in production normally.
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
                
                
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                var startTime = DateTime.Now;
                
                using (NetworkStream stream = client.GetStream())
                {       
                    Command cmd = await GetCommandAsync(stream, cancellationToken);                       
                    CommandResult commandResult = await ProcessCommandAsync(cmd);
                    await RespondCommandAsync(stream, commandResult, cancellationToken);                    
                }
                
                var endTime = DateTime.Now;
                Console.WriteLine($"HandleClientAsync in: {(endTime - startTime).TotalMilliseconds}ms");
            }
            catch (Exception ex) {
                log.LogError(ex,null);
            }
            finally
            {
                clientsLimit.Release();
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

                // Encode/compress first
                if (this.cfg.CompressedMode)
                {
                    response = CompressionTools.CompressData(resultJson);
                }
                else
                {
                    response = Encoding.ASCII.GetBytes(resultJson);
                }

                // Check size ONCE and handle if too large
                if (response.Length > cfg.Limits.MaxCommandResultSizeBytes)
                {
                    // Clear the large result and create error response
                    commandResult.Result = null;
                    commandResult.ErrorMessage = $"Command result size exceeds {cfg.Limits.MaxCommandResultSizeBytes} bytes";

                    // Re-serialize the error response
                    resultJson = Common.Serialization.ConvertCommandAndResult.SerializeCommandResult(commandResult);

                    if (this.cfg.CompressedMode)
                    {
                        response = CompressionTools.CompressData(resultJson);
                    }
                    else
                    {
                        response = Encoding.ASCII.GetBytes(resultJson);
                    }
                    
                }

                await stream.WriteAsync(response, 0, response.Length, combined.Token);
            }
            catch (OperationCanceledException ex)
            {
                if (ctsTimeOut.IsCancellationRequested)
                {
                    log.LogError(new TimeoutException($"The RespondCommandAsync task timed out after {cfg.Limits.CommandResultTimeout} milliseconds."),null);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex,null);
            }
            finally
            {
                // Dispose of cancellation token sources
                ctsTimeOut?.Dispose();
                combined?.Dispose();
            }
        }


        private async Task<CommandResult> ProcessCommandAsync(Command cmd)
        {
            CommandResult commandResult = new CommandResult();
            if (cmd.ErrorMessage == null)
            {

                try
                {
                    commandResult = await commandDispatcher.DispatchAsync(cmd);
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
            dbs.Dispose();
        }

        public bool Start()
        {
            try
            {
                if (!isRunning)
                {
                    log.LogInformation("Beginning Server Startup");
                    // Create a scope for startup operations (like SyncDatabaseToFiles)
                    using (var scope = scopeFactory.CreateScope())  
                    {
                        var db = scope.ServiceProvider.GetRequiredService<JetCSDbContext>();
                        db.Database.Migrate();
                        var seedData = scope.ServiceProvider.GetRequiredService<SeedData>();
                        seedData.SetDefault();   
                    }
                    dbs.SyncDatabaseToFiles();
                    _cancellationTokenSource = new CancellationTokenSource();
                    _listener = new TcpListener(IPAddress.Any, cfg.ListenPort);
                    _listener.Start();
                    Console.WriteLine("Server started. Listening for connections...");
                    Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
                    log.LogInformation("Completing Server Startup");
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
                    log.LogInformation("Beginning Server Stop"); 
                    _cancellationTokenSource.Cancel();
                    _listener.Stop();
                    dbs.DeactivateFileSystemWatcher();
                    dbs.SyncDatabaseToFiles();                    
                    isRunning = false;
                    log.LogInformation("Completing Server Stop");
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
