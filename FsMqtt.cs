namespace FSMosquitoClient
{
    using Microsoft.Extensions.Logging;
    using Microsoft.FlightSimulator.SimConnect;
    using System;
    using System.Timers;

    /// <summary>
    /// Represents a shim that translates SimConnect events to MQTT messages and vice versa.
    /// </summary>
    public sealed class FsMqtt : IFsMqtt
    {
        private readonly ILogger<FsMqtt> _logger;
        private readonly Timer m_timer = new Timer();

        private SimConnect m_simConnect;

        public FsMqtt(ILogger<FsMqtt> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsConnected
        {
            get
            {
                return m_simConnect != null;
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

            m_simConnect = new SimConnect("FSMosquitto", handle, Consts.WM_USER_SIMCONNECT, null, 0);

            /// Listen to connect and quit msgs
            m_simConnect.OnRecvOpen += new SimConnect.RecvOpenEventHandler(SimConnect_OnRecvOpen);
            m_simConnect.OnRecvQuit += new SimConnect.RecvQuitEventHandler(SimConnect_OnRecvQuit);

            // Listen to exceptions
            m_simConnect.OnRecvException += new SimConnect.RecvExceptionEventHandler(SimConnect_OnRecvException);

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

            m_simConnect.Dispose();
            m_simConnect = null;
        }

        public void SignalReceiveSimConnectMessage()
        {
            if (IsDisposed)
            {
                return;
            }

            try
            {
                m_simConnect.ReceiveMessage();
            }
            catch
            {
                Disconnect();
            }
        }

        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Console.WriteLine("SimConnect_OnRecvOpen");
            Console.WriteLine("Connected to KH");

            //sConnectButtonLabel = "Disconnect";
            //bConnected = true;

            //// Register pending requests
            //foreach (SimvarRequest oSimvarRequest in lSimvarRequests)
            //{
            //    if (oSimvarRequest.bPending)
            //    {
            //        oSimvarRequest.bPending = !RegisterToSimConnect(oSimvarRequest);
            //        oSimvarRequest.bStillPending = oSimvarRequest.bPending;
            //    }
            //}

            m_timer.Start();
        }

        /// <summary>
        /// Occurs when the user closes the game.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="data"></param>
        private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Console.WriteLine("SimConnect_OnRecvQuit");
            Console.WriteLine("KH has exited");

            m_timer.Stop();
        }

        private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            SIMCONNECT_EXCEPTION eException = (SIMCONNECT_EXCEPTION)data.dwException;
            Console.WriteLine("SimConnect_OnRecvException: " + eException.ToString());

            //lErrorMessages.Add("SimConnect : " + eException.ToString());
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

        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    if (null != m_simConnect)
                    {
                        m_timer.Stop();
                        m_simConnect.Dispose();
                        m_simConnect = null;
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
