namespace FSMosquitoClient
{
    using Microsoft.Extensions.Logging;
    using Microsoft.FlightSimulator.SimConnect;
    using System;
    using System.Timers;

    /// <summary>
    /// Represents a wrapper around SimConnect
    /// </summary>
    public sealed class FsSimConnect : IFsSimConnect
    {
        private readonly ILogger<FsSimConnect> _logger;
        private readonly Timer m_timer = new Timer();

        private SimConnect _simConnect;
        
        public event EventHandler SimConnectOpened;
        public event EventHandler SimConnectClosed;

        public FsSimConnect(ILogger<FsSimConnect> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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


            ConnectToSimConnect(handle);
        }

        private void ConnectToSimConnect(IntPtr handle)
        {
            _simConnect = new SimConnect("FSMosquitto", handle, Consts.WM_USER_SIMCONNECT, null, 0);

            /// Listen to connect and quit msgs
            _simConnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(SimConnect_OnRecvOpen);
            _simConnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(SimConnect_OnRecvQuit);

            // Listen to exceptions
            _simConnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(SimConnect_OnRecvException);

            ///// Catch a simobject data request
            //m_simConnect.OnRecvSimobjectDataBytype += new SimConnect.RecvSimobjectDataBytypeEventHandler(SimConnect_OnRecvSimobjectDataBytype);
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

            m_timer.Stop();

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
            var defineEnum = (Enum)Enum.ToObject(typeof(Enum), topic.DefineId);

            /// Define a data structure
            _simConnect.AddToDataDefinition(defineEnum, topic.DatumName, topic.Units, SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
            
            /// IMPORTANT: Register it with the simconnect managed wrapper marshaller
            /// If you skip this step, you will only receive a uint in the .dwData field.
            _simConnect.RegisterDataDefineStruct<double>(defineEnum);
        }

        /// <summary>
        /// Occurs when a connection is established to SimConnect.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            _logger.LogInformation("SimConnect_OnRecvOpen");
            m_timer.Start();
            OnSimConnectOpened();
        }

        /// <summary>
        /// Occurs when the user closes the game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            _logger.LogInformation("SimConnect_OnRecvQuit");
            OnSimConnectClosed();
            m_timer.Stop();
        }

        private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            SIMCONNECT_EXCEPTION eException = (SIMCONNECT_EXCEPTION)data.dwException;
            _logger.LogInformation("SimConnect_OnRecvException: " + eException.ToString());
        }

        //private void SimConnect_OnRecvSimobjectDataBytype(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA_BYTYPE data)
        //{
        //    Console.WriteLine("SimConnect_OnRecvSimobjectDataBytype");

        //    uint iRequest = data.dwRequestID;
        //    uint iObject = data.dwObjectID;
        //    if (!lObjectIDs.Contains(iObject))
        //    {
        //        lObjectIDs.Add(iObject);
        //    }
        //    foreach (SimvarRequest oSimvarRequest in lSimvarRequests)
        //    {
        //        if (iRequest == (uint)oSimvarRequest.eRequest && (!bObjectIDSelectionEnabled || iObject == m_iObjectIdRequest))
        //        {
        //            double dValue = (double)data.dwData[0];
        //            oSimvarRequest.dValue = dValue;
        //            oSimvarRequest.bPending = false;
        //            oSimvarRequest.bStillPending = false;
        //        }
        //    }
        //}

        private void OnSimConnectOpened()
        {
            if (SimConnectOpened != null)
            {
                SimConnectOpened.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnSimConnectClosed()
        {
            if (SimConnectClosed != null)
            {
                SimConnectClosed.Invoke(this, EventArgs.Empty);
            }
        }

        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    if (null != _simConnect)
                    {
                        m_timer.Stop();
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
