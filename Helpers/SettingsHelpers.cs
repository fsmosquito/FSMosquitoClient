namespace FSMosquitoClient.Helpers
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using System;
    using System.IO;

    public static class SettingsHelpers
    {
        public static bool AddOrUpdateAppSetting<T>(string sectionPathKey, T value)
        {
            try
            {
                var filePath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                var json = File.ReadAllText(filePath);
                var obj = JObject.Parse(json);

                var token = obj.SelectToken(sectionPathKey);
                if (token == null)
                {
                    return false;
                }
                token.Replace(JToken.FromObject(value));

                var output = obj.ToString(Formatting.Indented);
                File.WriteAllText(filePath, output);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing app settings | {0}", ex.Message);
            }
            return false;
        }
    }
}
