namespace CNSDemo
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.AspNetCore.Builder;
    using System.Text;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using Amqp;
    using CNSDemo.Models;
    using System.Text.Json.Nodes;
    using CNSDemo.AppOptions;
    using System.Net.Security;

    public class CnsDemoService : IHostedService
    {
        private CadNotificationClient _cadNotificationClient;
        private readonly ApplicationOptions _configuration;
        private readonly ILogger _logger;
        private readonly IHostApplicationLifetime _applicationLifetime;

        private readonly int _ampqPortNumber = 5671; //this port is a set value

        private SubscriptionType _subscriptionType;
        private ResponseType _responseType;

        public CnsDemoService(CadNotificationClient cadNotificationClient, IOptions<ApplicationOptions> configuration, ILoggerFactory loggerFactory, IHostApplicationLifetime applicationLifetime)
        {
            ArgumentNullException.ThrowIfNull(cadNotificationClient);
            ArgumentNullException.ThrowIfNull(configuration);
  
            _configuration = configuration.Value;
            _cadNotificationClient = cadNotificationClient;
            _applicationLifetime = applicationLifetime;
            _logger = loggerFactory.CreateLogger<CnsDemoService>();          
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _applicationLifetime.ApplicationStarted.Register(async () =>
            {                
                while (!cancellationToken.IsCancellationRequested)
                {
                    _subscriptionType = ShowPickSubscriptionType();
                    switch( _subscriptionType )
                    {
                        case SubscriptionType.Cancel:
                            _logger.LogInformation("Cancelled - Exiting");
                            return;
                        case SubscriptionType.Subscribe:
                            Console.WriteLine("The Demo app will create a new subscription");
                            await RunSubscription(cancellationToken);
                            break;
                        case SubscriptionType.Listen:
                            Console.WriteLine("No subscription only listen (this allows a restart without resubscribing)");
                            _responseType = ShowPickResponseType();
                            RunResponseType(cancellationToken);
                            break;
                        case SubscriptionType.Resubscribe:
                            Console.WriteLine("The Demo app will retreive subscription information and restart the subsciption based on the returned information");
                            await Resubscribe(cancellationToken);
                            break;
                        case SubscriptionType.HealthCheck:
                            await RunHealthCheck();
                            break;
                        case SubscriptionType.Unsubscribe:
                            await Unsubscribe();
                            break;
                    }
                }
            });

            return Task.CompletedTask;
        }

        private async Task RunSubscription(CancellationToken cancellationToken)
        {
            _responseType = ShowPickResponseType();

            if (_responseType == ResponseType.Cancel)
            {
                _logger.LogInformation("Exiting");
                return;
            }

            var subscribed = await _cadNotificationClient.Subscribe(_responseType);
            if (!subscribed)
            {
                _logger.LogCritical("Unable to subscribe - Exiting");
                return;
            }

            RunResponseType(cancellationToken);     
        }

        private void RunResponseType(CancellationToken cancellationToken)
        {
            if (_responseType == ResponseType.HTTP)
            {
                RunHttp(cancellationToken);
            }

            if (_responseType == ResponseType.RabbitMQ)
            {
                RunRabbitMQ(cancellationToken);
            }

            if (_responseType == ResponseType.AMQP)
            {
                RunAMQP(cancellationToken);
            }
        }

        //https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-quickstart-portal

        private void  RunAMQP(CancellationToken cancellationToken)
        {       
            var address = new Address(_configuration.AmqpOptions.AmqpNameSpace,
                                      _ampqPortNumber,
                                      _configuration.AmqpOptions.AmqpKeyName,
                                      _configuration.AmqpOptions.AmqpKeyValue);
            
            Connection connection =  Connection.Factory.CreateAsync(address).Result;
            var session = new Session(connection);
            var receiver = new ReceiverLink(session, "CadNotificationService", _configuration.AmqpOptions.AmqpAddress);
 
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = receiver.Receive(TimeSpan.FromMilliseconds(2000));

                if (message != null)
                {
 
                    Console.WriteLine("AMQP Message Received: ");
                    Console.WriteLine(System.Text.Encoding.UTF8.GetString((byte[])message.Body));
                    receiver.Complete(message, new Amqp.Framing.Accepted());
                }
            }

            session.CloseAsync();
            connection.CloseAsync();      
        }
        private async void RunRabbitMQ(CancellationToken cancellationToken)
        {
            var endpoints = AmqpTcpEndpoint.ParseMultiple(_configuration.RabbitMqOptions.RabbitMqHostName);
            var factory = new RabbitMQ.Client.ConnectionFactory()
            {
                UserName = _configuration.RabbitMqOptions.RabbitMqUserName,
                Password = _configuration.RabbitMqOptions.RabbitMqPassword,
                VirtualHost = _configuration.RabbitMqOptions.RabbitMqVirtualHost
            };

            if (_configuration.RabbitMqOptions.RabbitMqIsSslEnabled)
            {
                SslPolicyErrors sslPolicyErrors = SslPolicyErrors.None;
                if (!string.IsNullOrWhiteSpace(_configuration.RabbitMqOptions.RabbitMqSslAcceptablePolicyErrors) && Enum.TryParse<SslPolicyErrors>(_configuration.RabbitMqOptions.RabbitMqSslAcceptablePolicyErrors, out var result))
                {
                    sslPolicyErrors = result;
                }

                Array.ForEach(endpoints, ep => ep.Ssl =
                                    new SslOption
                                    {
                                        Enabled = true,
                                        ServerName = ep.HostName,
                                        AcceptablePolicyErrors = sslPolicyErrors
                                    });
            }

            using (var connection = await factory.CreateConnectionAsync(endpoints))
            {
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.ExchangeDeclareAsync(_configuration.RabbitMqOptions.RabbitMqExchange, ExchangeType.Direct);

                    await channel.QueueDeclareAsync(queue: _configuration.RabbitMqOptions.RabbitMqQueue,
                                         durable: false,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null);

                    await channel.QueueBindAsync(_configuration.RabbitMqOptions.RabbitMqQueue, 
                                      _configuration.RabbitMqOptions.RabbitMqExchange,
                                      _configuration.RabbitMqOptions.RabbitMqRoutingKey);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += (model, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);
                        Console.WriteLine(" [x] Received {0}", message);
                        return Task.CompletedTask;
                    };
                    await channel.BasicConsumeAsync(queue: _configuration.RabbitMqOptions.RabbitMqQueue,
                                         autoAck: true,
                                         consumer: consumer);
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }
                    await channel.CloseAsync();
                }
                await connection.CloseAsync();
            }
        }

        private void RunHttp(CancellationToken cancellationToken)
        {
            var app = WebApplication.Create();

            app.Urls.Add(_configuration.HttpListenerOptions.HttpListenerUrl);

            app.MapPost("/ListenerPacket/", async (HttpRequest request) =>
            {
                using var sr = new StreamReader(request.Body);
                var data = await sr.ReadToEndAsync();
                //Highly suggested that at this point data should be added to a queue.
                //With a consumer reading off the queue to keep from holding several tcp connections open.
                if(request.Headers.TryGetValue("X-TransactionID", out var transactionId))
                {
                    Console.WriteLine($"X-TransactionID: {transactionId}");
                }   

                Console.WriteLine(data);
                Console.WriteLine("\n");
                return Results.Ok();
            });

            while (!cancellationToken.IsCancellationRequested)
            {
                app.Run();
            }
        }

        private ResponseType ShowPickResponseType()
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║ Pick Response Type                     ║");
            Console.WriteLine("║ 1) HTTP (default)                      ║");
            Console.WriteLine("║ 2) AMQP                                ║");
            Console.WriteLine("║ 3) RabbitMQ                            ║");
            Console.WriteLine("║ 4) Cancel                              ║");
            Console.WriteLine("╚════════════════════════════════════════╝");

            var packetType = Console.ReadLine();
            switch (packetType)
            {
                case ("1"):
                    Console.Clear();
                    Console.WriteLine("Receiving CNS packets via HTTP listener");
                    Console.WriteLine("If you don't see notifications, please see ReadMe.md for how to setup the ssl cert");
                    return ResponseType.HTTP;
                case ("2"):
                    Console.Clear();
                    Console.WriteLine("Receiving CNS packets via AMQP messages");
                    return ResponseType.AMQP;
                case ("3"):
                    Console.Clear();
                    Console.WriteLine("Receiving CNS packets via RabbitMQ messages");
                    return ResponseType.RabbitMQ;
                case ("4"):
                default:
                    Console.Clear();
                    return ResponseType.Cancel;
            };
        }

        private SubscriptionType ShowPickSubscriptionType()
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║ Pick SubscriptionType                  ║");
            Console.WriteLine("║ 1) Subcribe                            ║");
            Console.WriteLine("║ 2) Skip Subscription only Listen       ║");
            Console.WriteLine("║ 3) Resubscribe existing subscription   ║");
            Console.WriteLine("║ 4) Health Check                        ║");
            Console.WriteLine("║ 5) Unsubscribe                         ║");
            Console.WriteLine("║ 6) Cancel                              ║");
            Console.WriteLine("╚════════════════════════════════════════╝");

            var packetType = Console.ReadLine();

            switch (packetType)
            {
                case ("1"):
                    return SubscriptionType.Subscribe;
                 
                case ("2"):
                    return SubscriptionType.Listen;
                case ("3"):
                    return SubscriptionType.Resubscribe;
                case ("4"):
                    return SubscriptionType.HealthCheck;
                case ("5"):
                    return SubscriptionType.Unsubscribe;
                default:
                    return SubscriptionType.Cancel;
            };          
        }

        private async Task<bool> Resubscribe(CancellationToken cancellationToken)
        {
            Console.Clear();
            Console.WriteLine("Re-subscribing for Cad Event notifications");
            var list = await _cadNotificationClient.GetSubscriptions();
            var mySubscription = list.FirstOrDefault(a => a.DisplayName == _configuration.SubscriberDisplayName);
            if (mySubscription == null)
            {
                Console.WriteLine("Subscription for " + _configuration.SubscriberDisplayName + " not found");
                return false;
            }

            var responseType = ProcessSubscription(mySubscription);
            if (responseType == ResponseType.Cancel)
            {
                return false;
            }

            var success = await _cadNotificationClient.Resubscribe(mySubscription.SubscriberId);

            if (success)
            {
                _responseType = responseType;
                RunResponseType(cancellationToken);
            }
            Console.WriteLine("Re-subscribed for Cad Event notifications");
            return true;
        }

        private async Task<bool> Unsubscribe()
        {
            Console.Clear();
            Console.WriteLine("Unsubscribing from Cad Event notifications");
            var list = await _cadNotificationClient.GetSubscriptions();
            var mySubscription = list.FirstOrDefault(a => a.DisplayName == _configuration.SubscriberDisplayName);
            if (mySubscription == null)
            {
                Console.WriteLine("Subscription for " + _configuration.SubscriberDisplayName + " not found");
                return false;
            }

         
            var success = await _cadNotificationClient.Unsubscribe(mySubscription.SubscriberId);

            if (success)
            {
                _responseType = ResponseType.Cancel;
            }
            Console.WriteLine("Unsubscribed from Cad Event notifications");
            return true;
        }

        private ResponseType ProcessSubscription(GetSubscriptionResponse subscription)
        {
            string? subscriptionString = subscription.Subscription.ToString();

            if (string.IsNullOrEmpty(subscriptionString))
            {
                return ResponseType.Cancel;
            }

           JsonNode? subNode = JsonNode.Parse(subscriptionString);
            var responseTypeString = subNode?["Response"]?["Type"]?.ToString();
           
            if (string.IsNullOrEmpty(responseTypeString))
            {
                return ResponseType.Cancel;
            }
            if (responseTypeString.Contains("HTTP", StringComparison.InvariantCultureIgnoreCase))
            {
                return ResponseType.HTTP;
            }
            if (responseTypeString.Contains("RabbitMQ", StringComparison.InvariantCultureIgnoreCase))
            {
                return ResponseType.RabbitMQ;
            }
            if (responseTypeString.Contains("AMQP", StringComparison.InvariantCultureIgnoreCase))
            {
                return ResponseType.AMQP;
            }
            return ResponseType.Cancel;
        }

        private async Task RunHealthCheck()
        {
           var result = await _cadNotificationClient.RunHealthCheck();
           Console.WriteLine($"api/v1/Subscriptions HealthCheck: {result}");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
