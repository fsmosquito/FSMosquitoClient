namespace FSMosquitoClient
{
    /// <summary>
    /// Defines the various topics in use by FSMosquito
    /// </summary>
    public static class FSMosquitoTopic
    {
        // Status Messages (Egress)
        public const string ClientStatus = "fsm/client/{0}/status";

        // Report SimConnect Status (Ingress)
        public const string ReportSimConnectStatus = "fsm/client/all/simconnect/report_status";

        // Invoke Function Calls (Ingress)
        public const string InvokeSimConnectFunction = "fsm/client/{0}/simconnect/invoke";

        // Subscribe to a SimConnect topic (Ingress)
        public const string SubscribeToSimConnect = "fsm/client/{0}/simconnect/subscribe";

        // SimConnect Status Messages (Egress)
        public const string SimConnectStatus = "fsm/client/{0}/simconnect/status";

        // Simconnect topic value (Egress)
        public const string SimConnectTopicValue = "fsm/client/{0}/v/{1}";
    }
}
