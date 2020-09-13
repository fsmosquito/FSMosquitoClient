namespace FSMosquitoClient
{
    using System;

    /// <summary>
    /// Adapts the functionality of SimConnect with Mqtt-based messaging.
    /// </summary>
    public interface ISimConnectMqttAdapter
    {
        IFsMqtt FsMqtt { get; }
        IFsSimConnect FsSimConnect { get; }

        /// <summary>
        /// Start adapting SimConnect with Mqtt
        /// </summary>
        /// <param name="handle"></param>
        void Start(IntPtr handle);

        /// <summary>
        /// Signal the adapter that a SimConnect message is ready to be recieved.
        /// </summary>
        void SignalReceiveSimConnectMessage();
    }
}