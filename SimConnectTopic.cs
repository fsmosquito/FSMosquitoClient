namespace FSMosquitoClient
{
    using Newtonsoft.Json;

    public class SimConnectTopic
    {
        [JsonProperty("defineId")]
        public int DefineId
        {
            get;
            set;
        }

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
