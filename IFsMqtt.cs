using System;

namespace FSMosquitoClient
{
    /// <summary>
    /// Represents an interface to a FsMqtt shim.
    /// </summary>
    public interface IFsMqtt : IDisposable
    {
        /// <summary>
        /// Gets a value that indicates if the current instance is connected to SimConnect and MQTT
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets a value that indicates if the current instance has been disposed
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Establish the translation between SimConnect and MQTT
        /// </summary>
        /// <param name="handle"></param>
        void Connect(IntPtr handle);

        /// <summary>
        /// Stops translating messages between SimConnect and MQTT
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Instruct the FsMqtt instance to signal SimConnect to recieve a message.
        /// </summary>
        void SignalReceiveSimConnectMessage();
    }
}