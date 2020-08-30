namespace FSMosquitoClient
{
    using Newtonsoft.Json;

    public class GenericPayload<T>
    {
        [JsonProperty("pattern")]
        public string Pattern
        {
            get;
            set;
        }

        [JsonProperty("data")]
        public T Data
        {
            get;
            set;
        }
    }
}
