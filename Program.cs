namespace FSMosquitoClient
{
    using FSMosquitoClient.Forms;
    using FSMosquitoClient.Properties;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Windows.Forms;

    [System.ComponentModel.DesignerCategory("")]
    public class Program : ApplicationContext
    {
        private static ServiceProvider _serviceProvider;
        private static ILogger<Program> _logger;
        private readonly NotifyIcon _trayIcon;

        public Program()
        {
            MainForm = _serviceProvider.GetService<MainForm>();

            // Initialize Tray Icon
            _trayIcon = new NotifyIcon()
            {
                Icon = Icon.FromHandle(Resources.mosquito.GetHicon()),
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true
            };

            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, MenuExit_Click);
            _trayIcon.Click += TrayIcon_Click;

            MainForm.Show();
            MainForm.Activate();
        }

        private void TrayIcon_Click(object sender, EventArgs e)
        {
            if (!MainForm.Visible)
            {
                MainForm.Show();
            }
            MainForm.Activate();
        }

        void MenuExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        /// <summary>
        /// Entry point for the application
        /// </summary>
        /// <param name="args"></param>
        [STAThread]
        static void Main(string[] args)
        {
            // Associate with all unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += GlobalExceptionHandler;

            string appGuid =
                ((GuidAttribute)Assembly.GetExecutingAssembly().
                    GetCustomAttributes(typeof(GuidAttribute), false).
                        GetValue(0)).Value.ToString();

            // Stand up DI
            var services = new ServiceCollection();
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables();
            var configuration = builder.Build();
            var startup = new Startup(configuration);
            startup.ConfigureServices(services);

            _serviceProvider = services.BuildServiceProvider();
            _logger = _serviceProvider.GetService<ILogger<Program>>();

            using Mutex mutex = new Mutex(false, $"Global\\{appGuid}");
            if (!mutex.WaitOne(0, false))
            {
                _logger.LogInformation("Prevented a second instance of the FSMosquito client from opening.");
                MessageBox.Show("Another instance of FSMosquito client is already running");
                return;
            }

            _logger.LogInformation("Starting FSMosquitoClient...");

            // Ensure that SimConnect.dll exists in the current folder.
            if (!File.Exists("./SimConnect.dll"))
            {
                _logger.LogInformation("SimConnect.dll did not exist. Adding SimConnect.dll from resource.");
                File.WriteAllBytes("./SimConnect.dll", Resources.SimConnect);
            }
            else
            {
                _logger.LogInformation("SimConnect.dll already exists. Continuing.");
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                var ctx = new Program();
                Application.Run(ctx);

                _logger.LogInformation("FSMosquitoClient is shutting down.");
            }
            catch(Exception ex)
            {
                _logger.LogError($"FSMosquitoClient shut down unexpectedly: {ex.Message}", ex);
            }
        }

        static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if (_logger != null)
            {
                _logger.LogError($"An unhandled exception occurred: {e.ExceptionObject}", e);
            }

            Console.WriteLine(e.ExceptionObject.ToString());
            Console.WriteLine("Press Enter to continue");
            Console.ReadLine();
            Environment.Exit(1);
        }
    }
}
