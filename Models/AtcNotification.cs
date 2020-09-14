namespace FSMosquitoClient
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System.Collections.Generic;

    public class AtcNotification
    {
        [JsonProperty("message")]
        public string Message
        {
            get;
            set;
        }

        [JsonIgnore]
        public bool Directed
        {
            get;
            set;
        }

        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }
    }
}
