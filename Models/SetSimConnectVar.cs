namespace FSMosquitoClient
{
    using Newtonsoft.Json;

    class SetSimConnectVarRequest
    {
        [JsonProperty("objectId")]
        public uint? ObjectId
        {
            get;
            set;
        }

        [JsonProperty("value")]
        public object Value
        {
            get;
            set;
        }
    }
}
