namespace FSMosquitoClient
{
    using FSMosquitoClient.Extensions;
    using FSMosquitoClient.Forms;
    using FSMosquitoClient.Properties;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.IO;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;
    using Tmds.Utils;

    [System.ComponentModel.DesignerCategory("")]
    public class Program : ApplicationContext
    {
        private static ServiceProvider _serviceProvider;
        private static ILogger<Program> _logger;
        private readonly NotifyIcon _trayIcon;

        private static readonly FunctionExecutor FSMosquitoExecutor = new FunctionExecutor(
            o =>
            {
                o.StartInfo.RedirectStandardError = true;
                o.OnExit = p =>
                {
                    if (p.ExitCode != 0)
                    {
                        string message = $"FSMosquito execution failed with exit code: {p.ExitCode}" + Environment.NewLine +
                                        p.StandardError.ReadToEnd();

                        FSMosquitoExecutor.Run(() =>
                        {
                            Launch(false);
                        });
                    }
                };
            });

        #region Tray Application Initialization
        public Program(bool showWindowOnStartup = true)
        {
            // Initialize Tray Icon
            _trayIcon = new NotifyIcon()
            {
                Icon = Icon.FromHandle(Resources.mosquito.GetHicon()),
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true,
                Text = "FSMosquito Client",
            };

            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (sender, e) => Application.Exit());
            _trayIcon.Click += (sender, e) =>
            {
                if (MainForm == null)
                {
                    MainForm = _serviceProvider.GetService<MainForm>();
                }

                if (!MainForm.Visible)
                {
                    MainForm.Show();
                }
                MainForm.Activate();
            };

            _trayIcon.BalloonTipClosed += (sender, e) =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            };

            // Associate the form with the adapter
            var form = _serviceProvider.GetService<MainForm>();
            var adapter = _serviceProvider.GetService<ISimConnectMqttAdapter>();
            var handle = form.Handle; // Use a separate variable as to not pass the thread context
            form.SimConnectMessageReceived += (object sender, EventArgs e) =>
                {
                    adapter.SignalReceiveSimConnectMessage();
                };

            // Fire and forget as to not block the UI thread.
            Task.Run(() => { Task.Delay(2500); adapter.Start(handle); }).Forget();

            if (showWindowOnStartup)
            {
                MainForm = form;
                MainForm.Show();
                MainForm.Activate();
            }
        }
        #endregion

        /// <summary>
        /// Entry point for the application
        /// </summary>
        /// <param name="args"></param>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length < 1 || !bool.TryParse(args[0], out bool showWindowOnStartup))
            {
                showWindowOnStartup = true;
            }
#if DEBUG
            Launch(showWindowOnStartup);
#else

            if (ExecFunction.IsExecFunctionCommand(args))
            {
                ExecFunction.Program.Main(args);
            }
            else
            {
                FSMosquitoExecutor.Run((string[] args) =>
                {
                    if (args.Length < 1 || !bool.TryParse(args[0], out bool showWindowOnStartup))
                    {
                        showWindowOnStartup = true;
                    }

                    Launch(showWindowOnStartup);
                }, new string[] { showWindowOnStartup.ToString() });
            }
#endif
        }

        /// <summary>
        /// Launch the application
        /// </summary>
        private static void Launch(bool showWindowOnStartup = true)
        {
#if !DEBUG
            // Associate with all unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += GlobalExceptionHandler;
#endif

            string appGuid =
                ((GuidAttribute)Assembly.GetExecutingAssembly().
                    GetCustomAttributes(typeof(GuidAttribute), false).
                        GetValue(0)).Value.ToString();

            // Stand up DI
            var services = new ServiceCollection();
            var builder = new ConfigurationBuilder()
                .SetBasePath(GetBasePath())
                .AddJsonFile("appsettings.json", false)
#if DEBUG
                .AddJsonFile("appsettings.dev.json", true)
#endif
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

#if DEBUG
            var ctx = new Program(showWindowOnStartup);
            Application.Run(ctx);
#else
            try
            {
                var ctx = new Program(showWindowOnStartup);
                Application.Run(ctx);

                _logger.LogInformation("FSMosquitoClient is shutting down.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"FSMosquitoClient shut down unexpectedly: {ex.Message}", ex);
                if (Debugger.IsAttached)
                {
                    throw;
                }
            }
#endif
            _logger.LogInformation("FSMosquitoClient Stopped.");
        }

        private static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs e)
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

        private static string GetBasePath()
        {
            using var processModule = Process.GetCurrentProcess().MainModule;
            return Path.GetDirectoryName(processModule?.FileName);
        }
    }
}
