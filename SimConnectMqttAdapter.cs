namespace FSMosquitoClient
{
    using Microsoft.Extensions.Logging;
    using System;

    public class SimConnectMqttAdapter : ISimConnectMqttAdapter
    {
        private readonly ILogger<SimConnectMqttAdapter> _logger;

        public SimConnectMqttAdapter(IFsMqtt fsMqtt, IFsSimConnect fsSimConnect, ILogger<SimConnectMqttAdapter> logger)
        {
            FsMqtt = fsMqtt ?? throw new ArgumentNullException(nameof(fsMqtt));
            FsSimConnect = fsSimConnect ?? throw new ArgumentNullException(nameof(fsSimConnect));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            FsSimConnect.SimConnectOpened += SimConnect_SimConnectOpened;
            FsSimConnect.SimConnectClosed += SimConnect_SimConnectClosed;
            FsSimConnect.TopicValueChanged += SimConnect_TopicValueChanged;

            FsMqtt.AtcNotificationReceived += FsMqtt_AtcNotificationReceived;
            FsMqtt.SubscribeRequestReceived += FsMqtt_SubscribeRequestReceived; ;
            FsMqtt.SetSimVarValueRequestReceived += FsMqtt_SetSimVarValueRequestReceived;
        }

        public IFsMqtt FsMqtt
        {
            get;
            private set;
        }

        public IFsSimConnect FsSimConnect
        {
            get;
            private set;
        }

        public void Start(IntPtr handle)
        {
            _logger.LogInformation("Starting SimConnectMqttAdapter...");
            if (!FsSimConnect.IsConnected)
            {
                FsSimConnect.Connect(handle);
            }

            if (!FsMqtt.IsConnected)
            {
                FsMqtt.Connect();
            }
        }

        public void SignalReceiveSimConnectMessage()
        {
            FsSimConnect.SignalReceiveSimConnectMessage();
        }

        private void SimConnect_SimConnectOpened(object sender, EventArgs e)
        {
            FsMqtt.PublishSimConnectStatus("Opened");
        }

        private void SimConnect_SimConnectClosed(object sender, EventArgs e)
        {
            FsMqtt.PublishSimConnectStatus("Closed");
        }

        private void SimConnect_TopicValueChanged(object sender, (SimConnectTopic topic, uint objectId, object value) topicValue)
        {
            if (FsMqtt.IsConnected)
            {
                FsMqtt.PublishTopicValue(topicValue.topic, topicValue.objectId, topicValue.value);
            }
        }

        private void FsMqtt_AtcNotificationReceived(object _, AtcNotification e)
        {
            switch (e.Message?.ToLower())
            {
                case "report_status":
                    {
                        FsMqtt.PublishSimConnectStatus(FsSimConnect.IsConnected ? "Opened" : "Closed");
                    }
                    break;
            }
        }

        private void FsMqtt_SubscribeRequestReceived(object _, (string objectType, SimConnectTopic[] topics) e)
        {
            if (FsSimConnect.IsConnected)
            {
                // We're ignoring the object type for now...
                foreach (var topic in e.topics)
                {
                    FsSimConnect.Subscribe(topic);
                }
            }
        }

        private void FsMqtt_SetSimVarValueRequestReceived(object _, (string datumName, uint? objectId, object value) e)
        {
            if (FsSimConnect.IsConnected)
            {
                FsSimConnect.Set(e.datumName, e.objectId, e.value);
            }
        }
    }
}
