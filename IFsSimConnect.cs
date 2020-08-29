namespace FSMosquitoClient
{
    using System;

    /// <summary>
    /// Represents an interface to a SimConnect wrapper.
    /// </summary>
    public interface IFsSimConnect : IDisposable
    {
        /// <summary>
        /// Event that is raised when a SimConnect connection is successfully established.
        /// </summary>
        public event EventHandler SimConnectOpened;

        /// <summary>
        /// Event that is raised when a SimConnect connection is closed. Usually if the game exits.
        /// </summary>
        public event EventHandler SimConnectClosed;

        /// <summary>
        /// Gets a value that indicates if the current instance is connected to SimConnect
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets a value that indicates if the current instance has been disposed
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Start receiving messages from SimConnect
        /// </summary>
        /// <param name="handle"></param>
        void Connect(IntPtr handle);

        /// <summary>
        /// Stops receiving messages from SimConnect
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Subscribes to a SimConnect Datum Topic
        /// </summary>
        /// <param name="topic"></param>
        void Subscribe(SimConnectTopic topic);

        /// <summary>
        /// Instruct the FsSimConnect instance to signal SimConnect to recieve a message.
        /// </summary>
        void SignalReceiveSimConnectMessage();
    }
}