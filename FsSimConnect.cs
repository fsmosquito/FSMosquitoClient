namespace FSMosquitoClient
{
    using Microsoft.Extensions.Logging;
    using Microsoft.FlightSimulator.SimConnect;
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Timers;

    /// <summary>
    /// Represents a wrapper around SimConnect
    /// </summary>
    public sealed class FsSimConnect : IFsSimConnect
    {
        private static int s_subscriptionCount = 0;
        private static int s_currentRequestId = 0;

        private readonly ILogger<FsSimConnect> _logger;
        private readonly Timer _pulseTimer = new Timer(1000);
        private readonly Timer _reconnectTimer = new Timer(15 * 1000);
        private readonly ConcurrentDictionary<string, SimConnectSubscription> _subscriptions = new ConcurrentDictionary<string, SimConnectSubscription>();
        private readonly ConcurrentDictionary<int, SimConnectSubscription> _pendingSubscriptions = new ConcurrentDictionary<int, SimConnectSubscription>();

        private IntPtr _lastHandle;
        private SimConnect _simConnect;
        
        public event EventHandler SimConnectOpened;
        public event EventHandler SimConnectClosed;
        public event EventHandler<(SimConnectTopic, double)> TopicValueChanged;

        public FsSimConnect(ILogger<FsSimConnect> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _pulseTimer.Elapsed += OnPulse_Elapsed;
            _reconnectTimer.Elapsed += OnReconnect_Elapsed;
        }

        public bool IsConnected
        {
            get
            {
                return _simConnect != null;
            }
        }

        public bool IsDisposed
        {
            get;
            private set;
        }

        public void Connect(IntPtr handle)
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException("FsMqtt Is Disposed.");
            }

            if (IsConnected)
            {
                throw new InvalidOperationException("FsMqtt is already connected.");
            }

            _lastHandle = handle;
            ConnectToSimConnect();
        }

        private void ConnectToSimConnect()
        {
            try
            {
                _simConnect = new SimConnect("FSMosquito", _lastHandle, Consts.WM_USER_SIMCONNECT, null, 0);

                /// Listen to connect and quit msgs
                _simConnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(SimConnect_OnRecvOpen);
                _simConnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(SimConnect_OnRecvQuit);

                // Listen to exceptions
                _simConnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(SimConnect_OnRecvException);

                // Listen to simobject data request
                _simConnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(SimConnect_OnRecvSimObjectDataByType);

                _reconnectTimer.Stop();
            }
            catch(Exception ex)
            {
                _simConnect = null;
                _logger.LogError($"Unable to connect to SimConnect: {ex.Message}", ex);
                _reconnectTimer.Start();
            }
        }

        public void Disconnect()
        {
            if (IsDisposed)
            {
                throw new InvalidOperationException("FsMqtt Is Disposed.");
            }

            if (!IsConnected)
            {
                throw new InvalidOperationException("FsMqtt is not connected.");
            }

            _pulseTimer.Stop();

            _simConnect.Dispose();
            _simConnect = null;
        }

        public void SignalReceiveSimConnectMessage()
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                _simConnect.ReceiveMessage();
            }
            catch
            {
                Disconnect();
            }
        }

        public void Subscribe(SimConnectTopic topic)
        {
            if (_subscriptions.ContainsKey(topic.DatumName))
            {
                return;
            }

            var newSubscription = new SimConnectSubscription()
            {
                Id = System.Threading.Interlocked.Increment(ref s_subscriptionCount),
                Topic = topic,
            };

            if (_subscriptions.TryAdd(topic.DatumName, newSubscription) == false)
            {
                return;
            }

            var def = (Definition)Enum.ToObject(typeof(Definition), newSubscription.Id);

            /// Define a data structure
            _simConnect.AddToDataDefinition(def, topic.DatumName, topic.Units, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            
            /// IMPORTANT: Register it with the simconnect managed wrapper marshaller
            /// If you skip this step, you will only receive a uint in the .dwData field.
            _simConnect.RegisterDataDefineStruct<double>(def);
        }

        /// <summary>
        /// Occurs when a connection is established to SimConnect.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            _logger.LogInformation("SimConnect_OnRecvOpen");
            _pulseTimer.Start();
            OnSimConnect_Opened();
        }

        /// <summary>
        /// Occurs when the user closes the game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            _logger.LogInformation("SimConnect_OnRecvQuit");
            OnSimConnect_Closed();
            
            _pulseTimer.Stop();
            _reconnectTimer.Start();
            _subscriptions.Clear();
        }

        private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            SIMCONNECT_EXCEPTION eException = (SIMCONNECT_EXCEPTION)data.dwException;
            _logger.LogInformation("SimConnect_OnRecvException: " + eException.ToString());
            _reconnectTimer.Start();
        }

        private void SimConnect_OnRecvSimObjectDataByType(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        {
            uint requestId = data.dwRequestID;
            //uint objectId = data.dwObjectID;
            double currentValue = (double)data.dwData[0];

            _pendingSubscriptions.TryRemove((int)requestId, out SimConnectSubscription subscription);
            subscription.PendingRequestId = null;
            if (subscription.LastValue != currentValue)
            {
                subscription.LastValue = currentValue;
                OnTopicValue_Changed(subscription.Topic, currentValue);
            }
        }

        private void OnSimConnect_Opened()
        {
            if (SimConnectOpened != null)
            {
                SimConnectOpened.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnSimConnect_Closed()
        {
            if (SimConnectClosed != null)
            {
                SimConnectClosed.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnTopicValue_Changed(SimConnectTopic topic, double value)
        {
            if (TopicValueChanged != null)
            {
                TopicValueChanged.Invoke(this, (topic, value));
            }
        }

        private void OnPulse_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_simConnect == null)
            {
                return;
            }

            foreach(var subscription in _subscriptions.Where(s => s.Value.PendingRequestId.HasValue == false))
            {
                var nextRequestId = GetNextRequestId();
                if (_pendingSubscriptions.TryAdd(nextRequestId, subscription.Value) == false)
                {
                    continue;
                }

                subscription.Value.PendingRequestId = nextRequestId;
                var req = (Request)Enum.ToObject(typeof(Request), subscription.Value.PendingRequestId);
                var def = (Definition)Enum.ToObject(typeof(Definition), subscription.Value.Id);
                _simConnect.RequestDataOnSimObjectType(req, def, 0, SIMCONNECT_SIMOBJECT_TYPE.USER);
            }            
        }

        private void OnReconnect_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (IsConnected == false)
            {
                ConnectToSimConnect();
            }
        }

        private int GetNextRequestId()
        {
            System.Threading.Interlocked.Increment(ref s_currentRequestId);
            return System.Threading.Interlocked.CompareExchange(ref s_currentRequestId, 0, int.MaxValue - 1);
        }

        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    if (null != _simConnect)
                    {
                        _pulseTimer.Stop();
                        _pulseTimer.Dispose();

                        _reconnectTimer.Stop();
                        _reconnectTimer.Dispose();

                        _simConnect.Dispose();
                        _simConnect = null;
                    }
                }

                IsDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
