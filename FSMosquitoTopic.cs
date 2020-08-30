namespace FSMosquitoClient
{
    /// <summary>
    /// Defines the various topics in use by FSMosquito
    /// </summary>
    public static class FSMosquitoTopic
    {
        // Status Messages (Egress)
        public static string ClientStatus = "fsm/client/{0}/status";

        // Invoke Function Calls (Ingress)
        public static string InvokeSimConnectFunction = "fsm/client/{0}/simconnect/invoke";

        // Subscribe to a SimConnect topic (Ingress)
        public static string SubscribeToSimConnectTopic = "fsm/client/{0}/simconnect/subscribe";

        // Simconnect topic value (Egress)
        public static string SimConnectTopicValue = "fsm/client/{0}/v/{1}";
    }
}
