﻿namespace FSMosquitoClient
{
    using System;
    using System.Threading.Tasks;

    public interface IFsMqtt : IDisposable
    {
        /// <summary>
        /// Event that is raised when an MQTT Connection is successfully established.
        /// </summary>
        public event EventHandler MqttConnectionOpened;

        /// <summary>
        /// Event that is raised when an MQTT Connection is closed. Usually due to server shutdown/issues.
        /// </summary>
        public event EventHandler MqttConnectionClosed;

        /// <summary>
        /// Event that is raised when a MQTT Message is recieved.
        /// </summary>
        public event EventHandler MqttMessageRecieved;

        /// <summary>
        /// Event that is raised when a MQTT Message is transmitted.
        /// </summary>
        public event EventHandler MqttMessageTransmitted;

        /// <summary>
        /// Event that is raised when an ApplicationMessage is recieved on a ATC Notification related topic.
        /// </summary>
        public event EventHandler<AtcNotification> AtcNotificationReceived;

        /// <summary>
        /// Event that is raised when a SimConnect Topic Subscription request is received.
        /// </summary>
        public event EventHandler<(string, SimConnectTopic[])> SubscribeRequestReceived;

        /// <summary>
        /// Event that is raised when a SimConnect Set SimVar value request is recieved.
        /// </summary>
        public event EventHandler<(string datumName, uint? objectId, object value)> SetSimVarValueRequestReceived;

        /// <summary>
        /// Gets a value that indicates if the current instance is connected to MQTT
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets a value that indicates if the current instance has been disposed
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets the url of the Mqtt Broker that the implementation is using
        /// </summary>
        string MqttBrokerUrl { get;  }

        /// <summary>
        /// Start receiving messages from MQTT
        /// </summary>
        Task Connect();

        /// <summary>
        /// Stops receiving messages from MQTT
        /// </summary>
        Task Disconnect();

        /// <summary>
        /// Publishes the specified simconnect status
        /// </summary>
        /// <param name="simConnectStatus"></param>
        /// <returns></returns>
        Task PublishSimConnectStatus(string simConnectStatus);

        /// <summary>
        /// Publishes the specified topic value to the MQTT broker
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="objectId"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        Task PublishTopicValue(SimConnectTopic topic, uint objectId, object value);
    }
}
