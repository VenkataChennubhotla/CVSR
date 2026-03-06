namespace CNSDemo.AppOptions
{

    public class RabbitMqOptions
    {
        public string RabbitMqHostName { get; set; }

        public string RabbitMqExchange { get; set; }

        public string RabbitMqQueue { get; set; }

        public string RabbitMqRoutingKey { get; set; }

        public string RabbitMqUserName { get; set; }

        public string RabbitMqPassword { get; set; }

        public string RabbitMqVirtualHost { get; set; }

        public bool RabbitMqIsSslEnabled { get; set; }

        public string RabbitMqSslAcceptablePolicyErrors { get; set; }
    }
}
