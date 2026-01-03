using Netade.Common;
using Netade.Common.Helpers;
using Netade.Common.Messaging;
using Netade.Domain;
using Netade.Persistence;
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
using Netade.Server.Services;
using Topshelf;
using Netade.Server.Services.Interfaces;

namespace Netade.Server
{
    public class Server:  IDisposable
    {
        
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly IConfiguration config;

        private readonly IServiceScopeFactory scopeFactory;
        private readonly Config cfg;
        private readonly ILogger<Server> log;
        private readonly ICursorRegistryService cursors;
        private Databases dbs;
        
        
        private CommandDispatcher commandDispatcher;
        private bool isRunning = false;        
        

        private SemaphoreSlim clientsLimit;
        public Server(Config config, ICursorRegistryService cursors,  CommandDispatcher commandDispatcher,Databases databases, IServiceScopeFactory scopeFactory, ILogger<Server> logger)
        {
            this.cfg = config;
            this.scopeFactory = scopeFactory;
            log = logger;
            this.clientsLimit = new SemaphoreSlim(1, this.cfg.Limits.MaxClients);
            this.commandDispatcher = commandDispatcher;
            this.dbs = databases;
            this.cursors = cursors;

        }

       

        // Resets the server by deleting the database if it is not running
        // Used for testing purposes
        public void Reset()
        {
            if (!isRunning)
            {
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<NetadeDbContext>();
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
                    CommandResult commandResult = await ProcessCommandAsync(cmd, cancellationToken);
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
            using var ctsTimeOut = new CancellationTokenSource(cfg.Limits.CommandResultTimeout);
            using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsTimeOut.Token);

            try
            {
                string resultJson = Common.Serialization.ConvertCommandAndResult.SerializeCommandResult(commandResult);

                byte[] payload = this.cfg.CompressedMode
                    ? CompressionTools.CompressData(resultJson)
                    : Encoding.ASCII.GetBytes(resultJson);

                // Enforce max size based on payload length (not including 4-byte header)
                if (payload.Length > cfg.Limits.MaxCommandResultSizeBytes)
                {
                    commandResult.Data = null;
                    commandResult.ErrorMessage = $"Command result size exceeds {cfg.Limits.MaxCommandResultSizeBytes} bytes";

                    resultJson = Common.Serialization.ConvertCommandAndResult.SerializeCommandResult(commandResult);

                    payload = this.cfg.CompressedMode
                        ? CompressionTools.CompressData(resultJson)
                        : Encoding.ASCII.GetBytes(resultJson);

                    // If even the error payload is too large, you may want a last-resort minimal payload here.
                }

                // 4-byte length prefix (big-endian)
                int len = payload.Length;
                byte[] header = new byte[4];
                header[0] = (byte)((len >> 24) & 0xFF);
                header[1] = (byte)((len >> 16) & 0xFF);
                header[2] = (byte)((len >> 8) & 0xFF);
                header[3] = (byte)(len & 0xFF);

                await stream.WriteAsync(header, 0, header.Length, combined.Token);
                await stream.WriteAsync(payload, 0, payload.Length, combined.Token);
            }
            catch (OperationCanceledException)
            {
                if (ctsTimeOut.IsCancellationRequested)
                {
                    log.LogError(
                        new TimeoutException($"The RespondCommandAsync task timed out after {cfg.Limits.CommandResultTimeout} milliseconds."),
                        null);
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, null);
            }
        }



        private async Task<CommandResult> ProcessCommandAsync(Command cmd, CancellationToken cancellationToken)
        {
            CommandResult commandResult = new CommandResult();
            if (cmd.ErrorMessage == null)
            {

                try
                {
                    commandResult = await commandDispatcher.DispatchAsync(cmd, cancellationToken);
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

        private async Task<Command> GetCommandAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            using var ctsTimeOut = new CancellationTokenSource(cfg.Limits.CommandTimeout);
            using var combined = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ctsTimeOut.Token);

            try
            {
                // Read 4-byte length prefix (big-endian)
                byte[] header = await ReadExactlyAsync(stream, 4, combined.Token);
                int len =
                    (header[0] << 24) |
                    (header[1] << 16) |
                    (header[2] << 8) |
                    (header[3]);

                if (len <= 0)
                    return new Command() { ErrorMessage = "Invalid message length." };

                if (len > cfg.Limits.MaxCommandSizeBytes)
                    return new Command() { ErrorMessage = $"Command size exceeds {cfg.Limits.MaxCommandSizeBytes} bytes" };

                // Read payload exactly
                byte[] payload = await ReadExactlyAsync(stream, len, combined.Token);

                string dataReceived = this.cfg.CompressedMode
                    ? CompressionTools.DecompressData(payload, payload.Length)
                    : Encoding.ASCII.GetString(payload);

                return Common.Serialization.ConvertCommandAndResult.DeSerializeCommand(dataReceived);
            }
            catch (OperationCanceledException)
            {
                if (ctsTimeOut.IsCancellationRequested)
                    return new Command() { ErrorMessage = $"The GetCommandAsync task timed out after {cfg.Limits.CommandTimeout} milliseconds." };

                return new Command() { ErrorMessage = "Command receive cancelled." };
            }
            catch (Exception ex)
            {
                return new Command() { ErrorMessage = ex.Message };
            }
        }

        // Helper (keep private inside Server class)
        private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int length, CancellationToken ct)
        {
            byte[] buffer = new byte[length];
            int offset = 0;

            while (offset < length)
            {
                int read = await stream.ReadAsync(buffer, offset, length - offset, ct);
                if (read == 0)
                    throw new EndOfStreamException("Remote closed the connection while reading.");

                offset += read;
            }

            return buffer;
        }


        public void Dispose()
        {
            if (this.isRunning) this.Stop();
            dbs.Dispose();
        }

        public bool Start(HostControl hostControl)
        {
            
            try
            {
                if (!isRunning)
                {
                    log.LogInformation("Beginning Server Startup");
                    // Create a scope for startup operations (like SyncDatabaseToFiles)
                    using (var scope = scopeFactory.CreateScope())  
                    {
                        var db = scope.ServiceProvider.GetRequiredService<NetadeDbContext>();
                        db.Database.Migrate();
                        var seedData = scope.ServiceProvider.GetRequiredService<SeedData>();
                        seedData.SetDefault();   
                    }
                    _cancellationTokenSource = new CancellationTokenSource();
                    Task.Run(async () =>
                    {
                        try
                        {
                            await dbs.SyncDatabaseMetadataToFilesAsync(_cancellationTokenSource.Token);
                        }
                        catch (Exception ex)
                        {
                            hostControl.Stop();
                            log.LogError(ex, "Error during SyncDatabaseToFilesAsync");
                        }
                    });
                    
                    
                    _listener = new TcpListener(IPAddress.Any, cfg.ListenPort);
                    _listener.Start();
                    log.LogInformation("Server started. Listening for connections...");
                    Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token));
                    log.LogInformation("Completing Server Startup");
                    isRunning = true;
                }
                return true;
            }
            catch (Exception ex)
            {
               
                log.LogError(ex, "Error in Server Startup");
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
                    cursors.CloseAllAsync().Wait();
                    isRunning = false;
                    log.LogInformation("Completing Server Stop");
                }
                return true;
            }
            catch (Exception ex)
            {
               log.LogError(ex, "Error in Server Stop");
                return false;
            }
        }
    }
}
