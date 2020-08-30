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

        public MainForm(IFsMqtt fsMqtt, IFsSimConnect fsSimConnect, ILogger<MainForm> logger)
        {
            FsMqtt = fsMqtt ?? throw new ArgumentNullException(nameof(fsMqtt));
            FsSimConnect = fsSimConnect ?? throw new ArgumentNullException(nameof(fsSimConnect));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            FsMqtt.MqttConnectionOpened += FsMqtt_MqttConnectionOpened;
            FsMqtt.MqttConnectionClosed += FsMqtt_MqttConnectionClosed;
            FsMqtt.SubscribeRequestRecieved += FsMqtt_SubscribeRequestRecieved;

            FsSimConnect.SimConnectOpened += SimConnect_SimConnectOpened;
            FsSimConnect.SimConnectClosed += SimConnect_SimConnectClosed;
            FsSimConnect.TopicValueChanged += SimConnect_TopicValueChanged;

            InitializeControls();
        }

        protected override void OnShown(EventArgs e)
        {
            if (!FsSimConnect.IsConnected)
            {
                FsSimConnect.Connect(Handle);
            }

            if (!FsMqtt.IsConnected)
            {
                FsMqtt.Connect();
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

        public IFsSimConnect FsSimConnect
        {
            get;
            private set;
        }

        protected override void WndProc(ref Message m)
        {
            if (FsSimConnect != null && m.Msg == Consts.WM_USER_SIMCONNECT)
            {
                FsSimConnect.SignalReceiveSimConnectMessage();
            }

            base.WndProc(ref m);
        }


        private void SimConnect_SimConnectOpened(object sender, EventArgs e)
        {
            _simConnectStatus.BackColor = Color.Green;
        }

        private void SimConnect_SimConnectClosed(object sender, EventArgs e)
        {
            _simConnectStatus.BackColor = Color.Orange;
        }

        private void SimConnect_TopicValueChanged(object sender, (SimConnectTopic topic, double value) topicValue)
        {
            if (FsMqtt.IsConnected)
            {
                FsMqtt.PublishTopicValue(topicValue.topic, topicValue.value);
            }
        }

        private void FsMqtt_MqttConnectionOpened(object sender, EventArgs e)
        {
            _mqttStatus.BackColor = Color.Green;
        }

        private void FsMqtt_MqttConnectionClosed(object sender, EventArgs e)
        {
            _mqttStatus.BackColor = Color.Orange;
        }


        private void FsMqtt_SubscribeRequestRecieved(object _, SimConnectTopic[] topics)
        {
            if (FsSimConnect.IsConnected)
            {
                foreach(var topic in topics)
                {
                    FsSimConnect.Subscribe(topic);
                }
            }
        }

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

            statusPanel.Controls.Add(_simConnectStatus);

            _mqttStatus = new Panel
            {
                Width = 20,
                Height = 20,
                BackColor = Color.Orange,
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
