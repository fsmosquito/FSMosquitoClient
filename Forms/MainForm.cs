namespace FSMosquitoClient.Forms
{
    using FSMosquitoClient.Extensions;
    using FSMosquitoClient.Properties;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Drawing;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    /// <summary>
    /// Represents the main FSMosquito form. Provides a minimalistic UI. Signals the Adapter when WinProc is called with a message of our indicated type.
    /// </summary>
    [System.ComponentModel.DesignerCategory("")]
    public class MainForm : Form
    {
        private const int PulseInterval = 1000;

        private readonly System.Timers.Timer _pulseMqttStatusTimer = new System.Timers.Timer(PulseInterval);
        private readonly System.Timers.Timer _pulseSimConnectStatusTimer = new System.Timers.Timer(PulseInterval);
        private readonly ILogger<MainForm> _logger;
        private readonly ISimConnectMqttAdapter _adapter;
        private bool _hasShown = false;
        private Color? _nextSimConnectStatusColor = null;
        private Color? _nextMqttStatusColor = null;

        private Panel _simConnectStatus;
        private Panel _mqttStatus;

        public MainForm(IFsMqtt fsMqtt, IFsSimConnect fsSimConnect, ISimConnectMqttAdapter adapter, ILogger<MainForm> logger)
        {
            FsMqtt = fsMqtt ?? throw new ArgumentNullException(nameof(fsMqtt));
            FsSimConnect = fsSimConnect ?? throw new ArgumentNullException(nameof(fsSimConnect));
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            FsMqtt.MqttConnectionOpened += FsMqtt_MqttConnectionOpened;
            FsMqtt.MqttConnectionClosed += FsMqtt_MqttConnectionClosed;
            FsMqtt.MqttMessageRecieved += FsMqtt_MqttMessageRecieved;
            FsMqtt.MqttMessageTransmitted += FsMqtt_MqttMessageTransmitted;

            FsSimConnect.SimConnectOpened += SimConnect_SimConnectOpened;
            FsSimConnect.SimConnectClosed += SimConnect_SimConnectClosed;
            FsSimConnect.SimConnectDataReceived += FsSimConnect_SimConnectDataReceived;
            FsSimConnect.SimConnectDataRequested += FsSimConnect_SimConnectDataRequested;

            _pulseMqttStatusTimer.Elapsed += PulseMqttStatusTimer_Elapsed;
            _pulseSimConnectStatusTimer.Elapsed += PulseSimConnectStatusTimer_Elapsed;
            InitializeControls();
        }

        public IFsMqtt FsMqtt
        {
            get;
            private set;
        }

        public IFsSimConnect FsSimConnect
        {
            get;
            private set;
        }

        protected override void WndProc(ref Message m)
        {
            if (FsSimConnect != null && m.Msg == Consts.WM_USER_SIMCONNECT)
            {
                // Fire and forget as to not block the UI thread.
                Task.Run(_adapter.SignalReceiveSimConnectMessage).Forget();
            }

            base.WndProc(ref m);
        }

        #region Form Event Handlers
        protected override void OnShown(EventArgs e)
        {
            _logger.LogInformation("Main Form Shown.");
            base.OnShown(e);
        }

        protected override void OnLoad(EventArgs e)
        {
            var handle = Handle;
            // Fire and forget as to not block the UI thread.
            Task.Run(() => { Task.Delay(1000); _adapter.Start(handle); }).Forget();

            base.OnLoad(e);
        }

        protected override void OnActivated(EventArgs e)
        {
            _logger.LogInformation("Main Form Activated.");
            base.OnActivated(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _logger.LogInformation("Main Form Hidden.");
            }
            else
            {
                _logger.LogInformation("Main Form Closing.");
            }
        }
        #endregion

        #region Pulse Event Handlers
        private void PulseSimConnectStatusTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_nextSimConnectStatusColor.HasValue == false || _nextSimConnectStatusColor.Value == _simConnectStatus.BackColor)
            {
                _simConnectStatus.BackColor = Color.Green;
                return;
            }

            _simConnectStatus.BackColor = _nextSimConnectStatusColor.Value;
            _nextSimConnectStatusColor = null;
        }

        private void PulseMqttStatusTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_nextMqttStatusColor.HasValue == false || _nextMqttStatusColor.Value == _mqttStatus.BackColor)
            {
                _mqttStatus.BackColor = Color.Green;
                return;
            }

            _mqttStatus.BackColor = _nextMqttStatusColor.Value;
            _nextMqttStatusColor = Color.Green;
        }
        #endregion

        #region SimConnect Event Handlers

        private void SimConnect_SimConnectOpened(object sender, EventArgs e)
        {
            _simConnectStatus.BackColor = Color.Green;
            _pulseSimConnectStatusTimer.Start();
        }

        private void SimConnect_SimConnectClosed(object sender, EventArgs e)
        {
            _simConnectStatus.BackColor = Color.Orange;
            _pulseSimConnectStatusTimer.Stop();
        }

        private void FsSimConnect_SimConnectDataRequested(object sender, EventArgs e)
        {
            if (_nextSimConnectStatusColor != Color.Purple)
                _nextSimConnectStatusColor = Color.Purple;
        }

        private void FsSimConnect_SimConnectDataReceived(object sender, EventArgs e)
        {
            if (_nextSimConnectStatusColor != Color.Blue)
                _nextSimConnectStatusColor = Color.Blue;
        }
        #endregion

        #region FsMqtt Event Handlers
        private void FsMqtt_MqttConnectionOpened(object sender, EventArgs e)
        {
            _mqttStatus.BackColor = Color.Green;
            _pulseMqttStatusTimer.Start();
        }

        private void FsMqtt_MqttConnectionClosed(object sender, EventArgs e)
        {
            _mqttStatus.BackColor = Color.Orange;
            _pulseMqttStatusTimer.Stop();
        }

        private void FsMqtt_MqttMessageTransmitted(object sender, EventArgs e)
        {
            if (_nextMqttStatusColor != Color.Purple)
                _nextMqttStatusColor = Color.Purple;
        }

        private void FsMqtt_MqttMessageRecieved(object sender, EventArgs e)
        {
            if (_nextMqttStatusColor != Color.Blue)
                _nextMqttStatusColor = Color.Blue;
        }
        #endregion

        #region Initialize Controls
        private void InitializeControls()
        {
            // Add the status panel
            var statusPanel = new Panel
            {
                Height = 20,
                Dock = DockStyle.Top
            };

            _simConnectStatus = new Panel
            {
                Width = 20,
                Height = 20,
                BackColor = Color.Orange,
                Dock = DockStyle.Left
            };

            var simConnectStatusToolTip = new ToolTip();
            simConnectStatusToolTip.SetToolTip(_simConnectStatus, "SimConnect.dll");

            statusPanel.Controls.Add(_simConnectStatus);

            _mqttStatus = new Panel
            {
                Width = 20,
                Height = 20,
                BackColor = Color.Orange,
                Dock = DockStyle.Right
            };

            var mqttStatusToolTip = new ToolTip();
            mqttStatusToolTip.SetToolTip(_mqttStatus, FsMqtt.MqttBrokerUrl);

            statusPanel.Controls.Add(_mqttStatus);

            // Add the Mosquito Picture
            var pb1 = new PictureBox
            {
                Location = new Point((Width / 2) - 80, (Height / 2) - 100),
                Width = 150,
                Height = 150,
                Image = Resources.mosquito,
                SizeMode = PictureBoxSizeMode.Zoom,
                Dock = DockStyle.Top,
            };

            var lblMosquito = new Label
            {
                Height = 140,
                Text = "FSMosquito",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Tahoma", 16, FontStyle.Bold),
                Dock = DockStyle.Top,
            };

            Controls.Add(lblMosquito);
            Controls.Add(pb1);
            Controls.Add(statusPanel);
        }
        #endregion
    }
}
