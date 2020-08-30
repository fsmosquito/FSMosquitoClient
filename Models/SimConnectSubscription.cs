namespace FSMosquitoClient
{
    /// <summary>
    /// Represetns a subscription to a SimConnect topic.
    /// </summary>
    public class SimConnectSubscription
    {
        /// <summary>
        /// Gets or sets the id of the subscription
        /// </summary>
        public int Id
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets a SimConnect Topic
        /// </summary>
        public SimConnectTopic Topic
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the id of a pending request.
        /// </summary>
        public int? PendingRequestId
        {
            get;
            set;
        }

        public double? LastValue
        {
            get;
            set;
        }
    }
}
