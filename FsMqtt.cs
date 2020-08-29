namespace FSMosquitoClient
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using MQTTnet;
    using MQTTnet.Client;
    using MQTTnet.Client.Connecting;
    using MQTTnet.Client.Disconnecting;
    using MQTTnet.Client.Options;
    using Newtonsoft.Json;
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class FsMqtt : IFsMqtt
    {
        private static readonly Random s_random = new Random();

        private readonly ILogger<FsMqtt> _logger;

        private readonly string _serverUrl;
        private readonly string _clientId;
        private readonly IMqttClientOptions _mqttClientOptions;

        public event EventHandler MqttConnectionOpened;
        public event EventHandler MqttConnectionClosed;
        public event EventHandler<SimConnectTopic> SubscribeRequestRecieved;

        public FsMqtt(IConfigurationRoot configuration, ILogger<FsMqtt> logger)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Connect to the Nexus
            _clientId = configuration["fs_mosquito_clientid"];
            _serverUrl = configuration["fs_mosquito_serverurl"];
            var fsmUsername = configuration["fs_mosquito_username"];
            var fsmPassword = configuration["fs_mosquito_password"];

            _mqttClientOptions = new MqttClientOptionsBuilder()
               .WithClientId(_clientId)
               .WithWebSocketServer(_serverUrl)
               .WithCredentials(fsmUsername, fsmPassword)
               .WithKeepAlivePeriod(TimeSpan.FromSeconds(10))
               .WithCommunicationTimeout(TimeSpan.FromSeconds(30))
               .WithWillDelayInterval(60 * 1000)
               .WithWillMessage(new MqttApplicationMessage()
               {
                   PayloadFormatIndicator = MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData,
                   ContentType = "text/plain",
                   Topic = FSMosquitoTopic.ClientStatus,
                   Payload = Encoding.UTF8.GetBytes("Disconnected"),
                   Retain = true
               })
               .WithCleanSession()
               .Build();

            // Create a new MQTT client.
            var factory = new MqttFactory();
            var mqttClient = factory.CreateMqttClient();

            mqttClient.UseConnectedHandler(OnConnected);
            mqttClient.UseDisconnectedHandler(OnDisconnected);
            mqttClient.UseApplicationMessageReceivedHandler(OnApplicationMessageReceived);

            MqttClient = mqttClient;
        }

        public bool IsConnected
        {
            get
            {
                return MqttClient.IsConnected;
            }
        }

        public bool IsDisposed
        {
            get;
            private set;
        }

        public IMqttClient MqttClient
        {
            get;
        }

        public async Task Connect()
        {
            _logger.LogInformation($"Connecting to {_serverUrl}...");
            await MqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
        }

        public void Disconnect()
        {
            throw new NotImplementedException();
        }

        public async Task Publish<T>(string topic, T payload, bool retain = false)
        {
            var contentType = "application/octet-stream";
            byte[] payloadBytes;
            if (payload is byte[])
            {
                payloadBytes = payload as byte[];
            }
            else if (payload is string)
            {
                contentType = "text/plain";
                payloadBytes = Encoding.UTF8.GetBytes(payload as string);
            }
            else
            {
                contentType = "application/json";
                var payloadString = JsonConvert.SerializeObject(payload);
                payloadBytes = Encoding.UTF8.GetBytes(payloadString);
            }

            await MqttClient.PublishAsync(new MqttApplicationMessage()
            {
                PayloadFormatIndicator = contentType == "application/octet-stream" ? MQTTnet.Protocol.MqttPayloadFormatIndicator.Unspecified : MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData,
                ContentType = contentType,
                Topic = string.Format(topic, _clientId),
                Payload = payloadBytes,
                Retain = retain
            });
        }

        private async Task OnConnected(MqttClientConnectedEventArgs e)
        {
            _logger.LogInformation($"Connected to {_serverUrl}.");

            // Subscribe to all FSMosquitoClient related event topics.
            await MqttClient.SubscribeAsync(
                // SimConnect Events Subscription
                new MqttTopicFilterBuilder().WithTopic(string.Format(FSMosquitoTopic.SubscribeToSimConnectTopic, _clientId)).Build()
                );

            // Report that we've connected.
            OnMqttConnectionOpened();
            await Publish(FSMosquitoTopic.ClientStatus, "Connected", true);
        }

        private async Task OnDisconnected(MqttClientDisconnectedEventArgs e)
        {
            OnMqttConnectionClosed();
            _logger.LogInformation($"Reconnecting to {_serverUrl}.");
            await Task.Delay(TimeSpan.FromSeconds(s_random.Next(2, 12) * 5));

            try
            {
                await MqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
            }
            catch
            {
                _logger.LogInformation($"Reconnection failed.");
            }
        }

        private Task OnApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            _logger.LogInformation($"Received Application Message for topic {e.ApplicationMessage.Topic}");

            switch (e.ApplicationMessage.Topic)
            {
                // SimConnect Subscription 
                case var subscription when (subscription == string.Format(FSMosquitoTopic.SubscribeToSimConnectTopic, _clientId)):
                    var topic = JsonConvert.DeserializeObject<SimConnectTopic>(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
                    OnSubscribeRequestRecieved(topic);
                    break;
            }

            return Task.CompletedTask;
        }

        private void OnMqttConnectionOpened()
        {
            if (MqttConnectionOpened != null)
            {
                MqttConnectionOpened.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnMqttConnectionClosed()
        {
            if (MqttConnectionClosed != null)
            {
                MqttConnectionClosed.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnSubscribeRequestRecieved(SimConnectTopic topic)
        {
            if (SubscribeRequestRecieved != null)
            {
                SubscribeRequestRecieved.Invoke(this, topic);
            }
        }

        #region IDisposable Support
        private bool _isDisposed = false;

        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
