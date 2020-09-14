namespace FSMosquitoClient
{
    /// <summary>
    /// Defines the various topics in use by FSMosquito
    /// </summary>
    public static class FSMosquitoTopic
    {
        // Broadcast ATC Notifications (Ingress)
        public const string AtcNotifications = "atc/notifications";

        // Directed ATC Notifications (Ingress)
        public const string AtcUserNotifications = "u/{0}/atc/notifications";

        // Subscribe to a SimConnect topic by type (Ingress)
        public const string SubscribeToSimConnect = "u/{0}/atc/subscribe/{1}";

        // Set Simconnect object value (Ingress)
        public const string SetSimVarValue = "u/{0}/atc/set_data/{1}/{2}";

        // Invoke Function Calls (Ingress)
        public const string InvokeSimConnectFunction = "u/{0}/atc/invoke";

        // Status Messages (Egress)
        public const string ClientStatus = "u/{0}/c/notifications";

        // SimConnect object value (Egress)
        public const string SimConnectTopicValue = "u/{0}/c/data/{1}/{2}";
    }
}
