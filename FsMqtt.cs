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
        private readonly string _username;
        private readonly IMqttClientOptions _mqttClientOptions;
        private readonly ConcurrentQueue<MqttApplicationMessage> _mqttMessageQueue = new ConcurrentQueue<MqttApplicationMessage>();

        public event EventHandler MqttConnectionOpened;
        public event EventHandler MqttConnectionClosed;
        public event EventHandler<AtcNotification> AtcNotificationReceived;
        public event EventHandler<(string, SimConnectTopic[])> SubscribeRequestReceived;
        public event EventHandler<(string datumName, uint? objectId, object value)> SetSimVarValueRequestReceived;
        public event EventHandler MqttMessageRecieved;
        public event EventHandler MqttMessageTransmitted;

        public FsMqtt(IConfigurationRoot configuration, ILogger<FsMqtt> logger)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Define configuration for the MQTT Broker connection
            MqttBrokerUrl = configuration["fs_mosquito_serverurl"];
            var fsmUsername = _username = configuration["fs_mosquito_username"];
            var fsmPassword = configuration["fs_mosquito_authentication_token"];

            if (!int.TryParse(configuration["fs_mosquito_keep_alive_period"], out int keepAlivePeriod)) {
                keepAlivePeriod = 10000;
            }

            if (!int.TryParse(configuration["fs_mosquito_communication_timeout"], out int communicationTimeout)) {
                communicationTimeout = 30000;
            }

            if (!uint.TryParse(configuration["fs_mosquito_delay_interval"], out uint delayInterval)) {
                delayInterval = 15000;
            }

            _mqttClientOptions = new MqttClientOptionsBuilder()
               .WithClientId(_username)
               .WithWebSocketServer(MqttBrokerUrl)
               .WithCredentials(fsmUsername, fsmPassword)
               .WithKeepAlivePeriod(TimeSpan.FromMilliseconds(keepAlivePeriod))
               .WithCommunicationTimeout(TimeSpan.FromMilliseconds(communicationTimeout))
               .WithWillDelayInterval(delayInterval)
               .WithWillMessage(new MqttApplicationMessage()
               {
                   PayloadFormatIndicator = MQTTnet.Protocol.MqttPayloadFormatIndicator.CharacterData,
                   ContentType = "text/plain",
                   Topic = string.Format(FSMosquitoTopic.ClientStatus, _username),
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

        public string MqttBrokerUrl { get; }

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
            _logger.LogInformation($"Connecting to {MqttBrokerUrl}...");
            await MqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
        }

        public async Task Disconnect()
        {
            await MqttClient.DisconnectAsync();
        }

        public async Task PublishSimConnectStatus(string simConnectStatus)
        {
            await Publish(FSMosquitoTopic.ClientStatus, simConnectStatus);
        }

        public async Task PublishTopicValue(SimConnectTopic topic, uint objectId, object value)
        {
            var normalizedTopicName = Regex.Replace(topic.DatumName.ToLower(), "\\s", "_");
            await Publish(FSMosquitoTopic.SimConnectTopicValue, value, true, new string[] { _username, objectId.ToString(), normalizedTopicName });
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
                topicReplacementArgs = new string[] { _username };
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
            _logger.LogInformation($"Connected to {MqttBrokerUrl}.");

            // Subscribe to all FSMosquitoClient related event topics.
            await MqttClient.SubscribeAsync(
                // SimConnect Events Subscription
                new MqttTopicFilterBuilder().WithTopic(string.Format(FSMosquitoTopic.AtcNotifications)).Build(),
                new MqttTopicFilterBuilder().WithTopic(string.Format(FSMosquitoTopic.AtcUserNotifications, _username)).Build(),
                new MqttTopicFilterBuilder().WithTopic(string.Format(FSMosquitoTopic.SubscribeToSimConnect, _username, "+")).Build(),
                new MqttTopicFilterBuilder().WithTopic(string.Format(FSMosquitoTopic.SetSimVarValue, _username, "+", "+")).Build(),
                new MqttTopicFilterBuilder().WithTopic(string.Format(FSMosquitoTopic.InvokeSimConnectFunction, _username)).Build()
                );

            // Report that we've connected.
            OnMqttConnectionOpened();
            await Publish(FSMosquitoTopic.ClientStatus, "Connected", true);
        }

        private async Task OnDisconnected(MqttClientDisconnectedEventArgs e)
        {
            OnMqttConnectionClosed();
            _logger.LogInformation($"Reconnecting to {MqttBrokerUrl}.");
            await Task.Delay(TimeSpan.FromSeconds(s_random.Next(2, 12) * 5));

            try
            {
                await MqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
                return;
            }
            catch(Exception ex)
            {
                _logger.LogInformation($"Reconnection failed.");
                _logger.LogError($"Exception thrown on Reconnect: {ex.Message}", ex);
            }

            _logger.LogInformation($"Disconnected from {MqttBrokerUrl}.");
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
                    // Atc Report Status
                    case FSMosquitoTopic.AtcNotifications:
                        OnAtcNotificationReceived(payload, false);
                        break;
                    case var atcDirectedNotificationTopic when atcDirectedNotificationTopic == string.Format(FSMosquitoTopic.AtcUserNotifications, _username):
                        OnAtcNotificationReceived(payload, true);
                        break;
                    // SimConnect Subscription 
                    case var subscribeTopic when Regex.IsMatch(subscribeTopic, string.Format(FSMosquitoTopic.SubscribeToSimConnect, _username, ".*(?!/)?")):
                        var objectType = GetTopicSegmentByIndex(subscribeTopic, 4);
                        OnSubscribeRequestRecieved(objectType, payload);
                        break;
                    case var setSimVarValueTopic when Regex.IsMatch(setSimVarValueTopic, string.Format(FSMosquitoTopic.SetSimVarValue, _username, ".*(?!/)?", ".*(?!/)?")):
                        var objectId = GetTopicSegmentByIndex(setSimVarValueTopic, 4);
                        var datumName = GetTopicSegmentByIndex(setSimVarValueTopic, 5);
                        OnSetSimVarValueRequestRecieved(datumName, objectId, payload);
                        break;
                }

                OnMqttMessageRecieved();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deserializing Application Message for topic {e.ApplicationMessage.Topic}: {ex.Message}", ex);
            }
            return Task.CompletedTask;
        }

        private string GetTopicSegmentByIndex(string topic, int ix)
        {
            return topic.Split("/")[ix];
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

            while (_mqttMessageQueue.Count > 0)
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

        /// <summary>
        /// Raises the AtcNotificationReceived event.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="isDirected"></param>
        private void OnAtcNotificationReceived(string payload, bool isDirected)
        {
            var atcNotification = JsonConvert.DeserializeObject<AtcNotification>(payload);
            atcNotification.Directed = isDirected;

            if (AtcNotificationReceived != null)
            {
                AtcNotificationReceived.Invoke(this, atcNotification);
            }
        }

        /// <summary>
        /// Raises the SubscribeRequestReceived event.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="objectType"></param>
        private void OnSubscribeRequestRecieved(string objectType, string payload)
        {
            var topics = JsonConvert.DeserializeObject<SimConnectTopic[]>(payload);
            if (SubscribeRequestReceived != null)
            {
                SubscribeRequestReceived.Invoke(this, (objectType, topics));
            }
        }

        private void OnSetSimVarValueRequestRecieved(string metricName, string objectId, string payload)
        {
            var datumName = Regex.Replace(metricName.ToUpper(), "_", " ");
            if (!uint.TryParse(objectId, out uint uintObjectId))
            {
                uintObjectId = 0;
            }

            var metricValue = JsonConvert.DeserializeObject<SetSimConnectVarRequest>(payload);

            if (SetSimVarValueRequestReceived != null)
            {
                SetSimVarValueRequestReceived.Invoke(this, (datumName, uintObjectId, metricValue.Value));
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
