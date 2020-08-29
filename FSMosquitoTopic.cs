namespace FSMosquitoClient
{
    /// <summary>
    /// Defines the various topics in use by FSMosquito
    /// </summary>
    public static class FSMosquitoTopic
    {
        // Status Messages (Egress)
        public static string ClientStatus = "fsm/client/{0}/status";

        // Function Calls (Ingress)

        // Subscribe to a SimConnect topic (Ingress)
        public static string SubscribeToSimConnectTopic = "fsm/client/{0}/simconnect/subscribe";
    }
}
