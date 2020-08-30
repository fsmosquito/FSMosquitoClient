namespace FSMosquitoClient
{
    using Newtonsoft.Json;

    public class SimConnectTopic
    {
        [JsonProperty("datumName")]
        public string DatumName
        {
            get;
            set;
        }

        [JsonProperty("units")]
        public string Units
        {
            get;
            set;
        }
    }
}
