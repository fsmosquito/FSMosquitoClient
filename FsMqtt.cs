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
    using System.Collections.Concurrent;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    public class FsMqtt : IFsMqtt
    {
        private static readonly Random s_random = new Random();

        private readonly ILogger<FsMqtt> _logger;

        private readonly string _serverUrl;
        private readonly string _clientId;
        private readonly IMqttClientOptions _mqttClientOptions;
        private readonly ConcurrentQueue<MqttApplicationMessage> _mqttMessageQueue = new ConcurrentQueue<MqttApplicationMessage>();

        public event EventHandler MqttConnectionOpened;
        public event EventHandler MqttConnectionClosed;
        public event EventHandler ReportSimConnectStatusRequestRecieved;
        public event EventHandler<SimConnectTopic[]> SubscribeRequestRecieved;
        public event EventHandler MqttMessageRecieved;
        public event EventHandler MqttMessageTransmitted;

        public FsMqtt(IConfigurationRoot configuration, ILogger<FsMqtt> logger)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Define configuration for the MQTT Broker connection
            _clientId = configuration["fs_mosquito_clientid"];
            _serverUrl = configuration["fs_mosquito_serverurl"];
            var fsmUsername = configuration["fs_mosquito_username"];
            var fsmPassword = configuration["fs_mosquito_password"];

            _mqttClientOptions = new MqttClientOptionsBuilder()
               .WithClientId(_clientId)
               .WithWebSocketServer(_serverUrl)
               .WithCredentials(fsmUsername, fsmPassword)
               .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
               .WithCommunicationTimeout(TimeSpan.FromSeconds(120))
               .WithWillDelayInterval(15 * 1000)
               .WithWillMessage(new MqttApplicationMessage()
               {
                   PayloadFormatIndicator = MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData,
                   ContentType = "text/plain",
                   Topic = string.Format(FSMosquitoTopic.ClientStatus, _clientId),
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

        public IFsSimConnect SimConnect
        {
            get;
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

        public async Task Disconnect()
        {
            await MqttClient.DisconnectAsync();
        }

        public async Task PublishSimConnectStatus(string simConnectStatus)
        {
            await Publish(FSMosquitoTopic.SimConnectStatus, simConnectStatus);
        }

        public async Task PublishTopicValue(SimConnectTopic topic, object value)
        {
            var normalizedTopicName = Regex.Replace(topic.DatumName.ToLower(), "\\s", "_");
            await Publish(FSMosquitoTopic.SimConnectTopicValue, value, true, new string[] { _clientId, normalizedTopicName });
        }

        /// <summary>
        /// Publishes a corresponding MQTT message for the specified topic. Adds the message to a queue in case of loss of signal
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="topic"></param>
        /// <param name="payload"></param>
        /// <param name="retain"></param>
        /// <param name="topicReplacementArgs"></param>
        /// <returns></returns>
        private async Task Publish<T>(string topic, T payload, bool retain = false, params string[] topicReplacementArgs)
        {
            if (topicReplacementArgs == null || topicReplacementArgs.Length == 0)
            {
                topicReplacementArgs = new string[] { _clientId };
            }

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

            var composedTopic = string.Format(topic, topicReplacementArgs);
            var newApplicationMessage = new MqttApplicationMessage()
            {
                PayloadFormatIndicator = contentType == "application/octet-stream" ? MQTTnet.Protocol.MqttPayloadFormatIndicator.Unspecified : MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData,
                ContentType = contentType,
                Topic = composedTopic,
                Payload = payloadBytes,
                Retain = retain
            };
            _mqttMessageQueue.Enqueue(newApplicationMessage);
            await ProcessMessageQueue();
        }

        private async Task OnConnected(MqttClientConnectedEventArgs e)
        {
            _logger.LogInformation($"Connected to {_serverUrl}.");

            // Subscribe to all FSMosquitoClient related event topics.
            await MqttClient.SubscribeAsync(
                // SimConnect Events Subscription
                new MqttTopicFilterBuilder().WithTopic(string.Format(FSMosquitoTopic.ReportSimConnectStatus, _clientId)).Build(),
                new MqttTopicFilterBuilder().WithTopic(string.Format(FSMosquitoTopic.SubscribeToSimConnect, _clientId)).Build(),
                new MqttTopicFilterBuilder().WithTopic(string.Format(FSMosquitoTopic.InvokeSimConnectFunction, _clientId)).Build()
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
                return;
            }
            catch
            {
                _logger.LogInformation($"Reconnection failed.");
            }

            _logger.LogInformation($"Disconnected from {_serverUrl}.");
        }

        /// <summary>
        /// Occurs when we recieve a message on a topic that we've subscribed to.
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private Task OnApplicationMessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            _logger.LogInformation($"Received Application Message for topic {e.ApplicationMessage.Topic}");
            try
            {
                var payload = string.Empty;
                
                if (e.ApplicationMessage.Payload != null && e.ApplicationMessage.Payload.Length > 0)
                    payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);

                switch (e.ApplicationMessage.Topic)
                {
                    // SimConnect Report Status
                    case FSMosquitoTopic.ReportSimConnectStatus:
                        OnReportSimConnectStatusRequestRecieved();
                        break;
                    // SimConnect Subscription 
                    case var subscription when subscription == string.Format(FSMosquitoTopic.SubscribeToSimConnect, _clientId):
                        var typedPayload = JsonConvert.DeserializeObject<SimConnectTopic[]>(payload);
                        OnSubscribeRequestRecieved(typedPayload);
                        break;
                }

                OnMqttMessageRecieved();
            }
            catch(Exception ex)
            {
                _logger.LogError($"Error deserializing Application Message for topic {e.ApplicationMessage.Topic}: {ex.Message}", ex);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Processes the pending message queue.
        /// </summary>
        /// <returns></returns>
        private async Task ProcessMessageQueue()
        {
            if (IsConnected == false)
            {
                return;
            }

            while(_mqttMessageQueue.Count > 0)
            {
                if (_mqttMessageQueue.TryDequeue(out MqttApplicationMessage message))
                {
                    try
                    {
                        await MqttClient.PublishAsync(message);
                    }
                    catch
                    {
                        _mqttMessageQueue.Enqueue(message);
                        break;
                    }

                    OnMqttMessageTransmitted();
                }
            }
        }

        #region Event Handlers
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

        private void OnReportSimConnectStatusRequestRecieved()
        {
            if (ReportSimConnectStatusRequestRecieved != null)
            {
                ReportSimConnectStatusRequestRecieved.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnSubscribeRequestRecieved(SimConnectTopic[] topics)
        {
            if (SubscribeRequestRecieved != null)
            {
                SubscribeRequestRecieved.Invoke(this, topics);
            }
        }

        private void OnMqttMessageRecieved()
        {
            if (MqttMessageRecieved != null)
            {
                MqttMessageRecieved.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnMqttMessageTransmitted()
        {
            if (MqttMessageTransmitted != null)
            {
                MqttMessageTransmitted.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion

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
