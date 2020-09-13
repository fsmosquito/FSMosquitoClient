namespace FSMosquitoClient
{
    using FSMosquitoClient.Forms;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using System.Security.Cryptography;
    using System.Windows.Forms;

    public class Startup
    {
        private static readonly SHA256 _sha256 = SHA256.Create();

        public IConfigurationRoot Configuration { get; }

        public Startup(IConfigurationRoot configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder
                .AddSerilog()
                .AddFile("Logs/FSMosquitoClient-{Date}.txt");
            });

            services.AddSingleton(Configuration);

            services.AddSingleton<HashAlgorithm>(_sha256);

            services.AddSingleton<IFsMqtt, FsMqtt>();
            services.AddSingleton<IFsSimConnect, FsSimConnect>();
            services.AddSingleton<ISimConnectMqttAdapter, SimConnectMqttAdapter>();

            services.AddSingleton(f =>
            {
                return new MainForm(f.GetService<IFsMqtt>(), f.GetService<IFsSimConnect>(), f.GetService<ISimConnectMqttAdapter>(), f.GetService<ILogger<MainForm>>())
                {
                    StartPosition = FormStartPosition.CenterScreen,
                    ShowInTaskbar = false,
                    ShowIcon = false,
                    MinimizeBox = false,
                    MaximizeBox = false,
                    FormBorderStyle = FormBorderStyle.FixedSingle,
                };
            });
        }
    }
}
