namespace FSMosquitoClient.Forms
{
    using FSMosquitoClient.Properties;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Drawing;
    using System.Windows.Forms;

    /// <summary>
    /// Represents the main FSMosquito form. Encapsulates and forwards windows messages to an instance of FsMqtt.
    /// </summary>
    [System.ComponentModel.DesignerCategory("")]
    public class MainForm : Form
    {
        private readonly ILogger<MainForm> _logger;
        private Panel _simConnectStatus;
        private Panel _mqttStatus;

        public MainForm(IFsMqtt fsMqtt, ILogger<MainForm> logger)
        {
            FsMqtt = fsMqtt ?? throw new ArgumentNullException(nameof(fsMqtt));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            InitializeControls();
        }

        protected override void OnShown(EventArgs e)
        {
            if (!FsMqtt.IsConnected)
            {
                FsMqtt.Connect(Handle);
            }
            
            base.OnShown(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            _logger.LogInformation("Main Form Closing.");
        }

        public IFsMqtt FsMqtt
        {
            get;
            private set;
        }

        protected override void WndProc(ref Message m)
        {
            if (FsMqtt != null && m.Msg == Consts.WM_USER_SIMCONNECT)
            {
                FsMqtt.SignalReceiveSimConnectMessage();
            }

            base.WndProc(ref m);
        }

        void InitializeControls()
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
                BackColor = Color.Green,
                Dock = DockStyle.Left
            };

            statusPanel.Controls.Add(_simConnectStatus);

            _mqttStatus = new Panel
            {
                Width = 20,
                Height = 20,
                BackColor = Color.Green,
                Dock = DockStyle.Right
            };

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

            var label = new Label
            {
                Height = 140,
                Text = "FSMosquito",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Tahoma", 16, FontStyle.Bold),
                Dock = DockStyle.Top,
            };

            Controls.Add(label);
            Controls.Add(pb1);
            Controls.Add(statusPanel);
        }
    }
}
