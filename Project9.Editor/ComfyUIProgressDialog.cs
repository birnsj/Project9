using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Project9.Editor
{
    public partial class ComfyUIProgressDialog : Form
    {
        private Label _statusLabel = null!;
        private ProgressBar _progressBar = null!;
        private Button _cancelButton = null!;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isCompleted;

        public ComfyUIProgressDialog()
        {
            InitializeComponent();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void InitializeComponent()
        {
            this.Text = "ComfyUI Workflow Progress";
            this.Size = new Size(500, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = true;

            _statusLabel = new Label
            {
                Text = "Initializing...",
                Location = new Point(20, 20),
                Size = new Size(440, 30),
                AutoSize = false
            };

            _progressBar = new ProgressBar
            {
                Location = new Point(20, 60),
                Size = new Size(440, 23),
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30
            };

            _cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(385, 95),
                Size = new Size(75, 25),
                DialogResult = DialogResult.Cancel
            };
            _cancelButton.Click += CancelButton_Click;

            this.Controls.Add(_statusLabel);
            this.Controls.Add(_progressBar);
            this.Controls.Add(_cancelButton);
        }

        public void UpdateStatus(string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateStatus), status);
                return;
            }

            _statusLabel.Text = status;
            Application.DoEvents();
        }

        public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;

        private void CancelButton_Click(object? sender, EventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                _statusLabel.Text = "Cancelling...";
                _cancelButton.Enabled = false;
            }
        }

        public void SetCompleted()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(SetCompleted));
                return;
            }

            _isCompleted = true;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;
            _statusLabel.Text = "Completed!";
            _cancelButton.Text = "Close";
            _cancelButton.DialogResult = DialogResult.OK;
        }

        public void SetError(string errorMessage)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(SetError), errorMessage);
                return;
            }

            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 0;
            _statusLabel.Text = $"Error: {errorMessage}";
            _cancelButton.Text = "Close";
            _cancelButton.DialogResult = DialogResult.OK;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isCompleted && _cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellationTokenSource?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

