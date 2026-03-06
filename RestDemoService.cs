namespace RestDemo
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using System.Text.Json;

    public class RestDemoService : IHostedService
    {
        private readonly RestfulCadClient _restfulCadClient;
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _applicationLifetime;

        public RestDemoService( RestfulCadClient restfulCadClient, ILoggerFactory loggerFactory, IHostApplicationLifetime applicationLifetime)
        {
            ArgumentNullException.ThrowIfNull(restfulCadClient);
            _restfulCadClient = restfulCadClient;
            _applicationLifetime = applicationLifetime;
          
            _logger = loggerFactory.CreateLogger<RestDemoService>();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting command input loop");
            _applicationLifetime.ApplicationStarted.Register(() =>
            {           
                while (!cancellationToken.IsCancellationRequested)
                {
                    PrintPossibleCommands(); 
                    try
                    {
                        var keyCommand = Console.ReadKey();
                        if(!CheckKeyPress(keyCommand).Result)
                        {
                            _applicationLifetime.StopApplication();
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message);
                    }                      
                }
            });

            return Task.CompletedTask;
        }

        private async Task<bool> CheckKeyPress(ConsoleKeyInfo keyCommand)
        {
            switch (keyCommand.Key)
            {
                case ConsoleKey.X:
                    await _restfulCadClient.RunClearSession();
                    return false;
                case ConsoleKey.U:
                    await _restfulCadClient.RunGetActiveUnits();
                    return true; 
                case ConsoleKey.E:
                    await _restfulCadClient.RunGetEvents();
                    return true;
                case ConsoleKey.C: //have OnCall dispatcher running to see the event
                    await _restfulCadClient.RunCreateEvent();
                    return true;
                default:
                    return true;
            }
        }

        private void PrintPossibleCommands()
        {
            Console.WriteLine(""); 
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║ Pick an endpoint to run:               ║");
            Console.WriteLine("║ U) Get Active Units                    ║");
            Console.WriteLine("║ E) Get Open Events                     ║");
            Console.WriteLine("║ C) Create Event                        ║");
            Console.WriteLine("║ x) Exit                                ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
