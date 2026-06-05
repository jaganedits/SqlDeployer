using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SqlDeployer;

namespace SqlDeployerGui
{
    public partial class Form1 : Form
    {
        private CancellationTokenSource? _cts;
        private int _successCount = 0;
        private int _errorCount = 0;

        private ThemePalette _theme = ThemePalette.Dark;
        private bool _showSuccessTab = true;

        public Form1()
        {
            InitializeComponent();
            ApplyTheme(_theme);
        }

        // Paint the native window title bar to match the theme (Win10+).
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTitleBarTheme();
        }

        private void ApplyTitleBarTheme()
        {
            try
            {
                int useDark = _theme.IsDark ? 1 : 0; // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
                DwmSetWindowAttribute(this.Handle, 20, ref useDark, sizeof(int));
            }
            catch { /* older Windows without dark-mode DWM attribute */ }
        }

        private void btnTheme_Click(object sender, EventArgs e)
        {
            _theme = _theme.IsDark ? ThemePalette.Light : ThemePalette.Dark;
            ApplyTheme(_theme);
            ApplyTitleBarTheme();
        }

        // Repaints every control from the active palette. Called at startup and on toggle.
        private void ApplyTheme(ThemePalette t)
        {
            BackColor = t.Page;

            lblTitle.ForeColor = t.TextPrimary;
            lblSubtitle.ForeColor = t.TextMuted;
            lblSecConn.ForeColor = t.TextMuted;
            lblSecOutput.ForeColor = t.TextMuted;
            lblmessage.ForeColor = t.TextMuted;
            label1.ForeColor = t.TextPrimary;
            label2.ForeColor = t.TextPrimary;
            label3.ForeColor = t.TextPrimary;
            label4.ForeColor = t.TextPrimary;
            label5.ForeColor = t.TextPrimary;

            foreach (var tb in new[] { txtServerName, txtLogin, txtpassword, txtDatabase, txtFilePath })
            {
                tb.BackColor = t.InputBg;
                tb.TextColor = t.TextPrimary;
                tb.IdleBorderColor = t.Line;
                tb.FocusBorderColor = t.Primary;
                tb.EyeColor = t.TextMuted;
                tb.Invalidate();
            }

            StyleOutline(btnBrowse, t, t.Surface);
            StyleOutline(btnTestConnect, t, t.Page);

            btnStartDeployment.BackColor = t.Primary;
            btnStartDeployment.ForeColor = t.OnPrimary;
            btnStartDeployment.HoverColor = t.PrimaryHover;
            btnStartDeployment.PressColor = t.PrimaryPress;
            btnStartDeployment.Invalidate();

            btnCancel.BackColor = t.Page;
            btnCancel.BorderColor = t.Danger;
            btnCancel.ForeColor = t.Danger;
            btnCancel.HoverColor = t.DangerHover;
            btnCancel.Invalidate();

            btnTheme.BackColor = t.Page;
            btnTheme.BorderColor = t.Line;
            btnTheme.ForeColor = t.TextPrimary;
            btnTheme.HoverColor = t.Surface;
            btnTheme.Text = t.IsDark ? "☀  Light" : "☽  Dark";
            btnTheme.Invalidate();

            pnlLogHost.BackColor = t.Card;
            pnlLogHost.BorderColor = t.Line;
            pnlLogHost.Invalidate();

            rtxtlog.BackColor = t.Card;
            rtxtlog.ForeColor = t.Success;
            rtxterr.BackColor = t.Card;
            rtxterr.ForeColor = t.Danger;

            pb1.BackColor = t.ProgressTrack;
            pb1.ForeColor = t.Primary;

            foreach (var b in new[] { btnBrowse, btnTestConnect, btnStartDeployment, btnCancel, btnTheme, btnLogSuccess, btnLogError })
            {
                b.DisabledBackColor = t.DisabledBg;
                b.DisabledForeColor = t.DisabledText;
                b.Invalidate();
            }

            ShowLogTab(_showSuccessTab);
        }

        private static void StyleOutline(RoundedButton b, ThemePalette t, Color back)
        {
            b.BackColor = back;
            b.BorderColor = t.Line;
            b.ForeColor = t.TextPrimary;
            b.HoverColor = t.Surface;
            b.Invalidate();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    txtFilePath.Text = dialog.SelectedPath;
                }
            }
        }

        private string GetConnectionString()
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            {
                DataSource = txtServerName.Text.Trim(),
                InitialCatalog = txtDatabase.Text.Trim(),
                Encrypt = false
            };

            var login = txtLogin.Text.Trim();
            if (!string.IsNullOrEmpty(login))
            {
                builder.UserID = login;
                builder.Password = txtpassword.Text;
            }
            else
            {
                builder.IntegratedSecurity = true;
            }

            return builder.ConnectionString;
        }

        private async void btnTestConnect_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtServerName.Text) || string.IsNullOrWhiteSpace(txtDatabase.Text))
            {
                MessageBox.Show("Server Name and Database are required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnTestConnect.Enabled = false;
            lblmessage.Text = "Testing connection...";

            try
            {
                var connString = GetConnectionString();
                using (var conn = new Microsoft.Data.SqlClient.SqlConnection(connString))
                {
                    await conn.OpenAsync();
                }
                lblmessage.Text = "Connection Success";
                MessageBox.Show("Database connection successful!", "Connection Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                lblmessage.Text = "Connection Failed";
                MessageBox.Show($"Database connection failed:\n{ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnTestConnect.Enabled = true;
            }
        }

        private async void btnStartDeployment_Click(object sender, EventArgs e)
        {
            string scriptsPath = txtFilePath.Text.Trim();
            if (string.IsNullOrWhiteSpace(txtServerName.Text) || 
                string.IsNullOrWhiteSpace(txtDatabase.Text) || 
                string.IsNullOrWhiteSpace(scriptsPath))
            {
                MessageBox.Show("Server Name, Database, and File Path are required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(scriptsPath))
            {
                MessageBox.Show("Selected script folder does not exist.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Reset UI
            _successCount = 0;
            _errorCount = 0;
            rtxtlog.Clear();
            rtxterr.Clear();
            UpdateTabHeaders();
            pb1.Value = 0;
            lblmessage.Text = "Loading pending scripts...";

            ToggleInputControls(false);
            _cts = new CancellationTokenSource();
            btnCancel.Enabled = true;

            try
            {
                var connString = GetConnectionString();
                var deployer = new SqlServerDeployer();

                // Retrieve pending scripts
                var pendingScripts = await deployer.GetPendingScripts(scriptsPath, "GUI", connString, _cts.Token);

                if (pendingScripts.Count == 0)
                {
                    lblmessage.Text = "Deployment Completed. No pending scripts.";
                    MessageBox.Show("No new pending scripts found to deploy.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                pb1.Maximum = pendingScripts.Count;
                pb1.Value = 0;

                for (int i = 0; i < pendingScripts.Count; i++)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    var script = pendingScripts[i];
                    var filename = Path.GetFileName(script.FileName);
                    lblmessage.Text = $"Processing {i + 1}/{pendingScripts.Count}: {filename}...";

                    try
                    {
                        await deployer.ExecuteScript(connString, script.FileName, script.Version, "GUI", _cts.Token);

                        _successCount++;
                        AppendSuccessLog($"{filename} :: Processed successfully");
                    }
                    catch (OperationCanceledException)
                    {
                        // Bubble up to the outer handler so a cancelled script isn't logged as an error
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _errorCount++;
                        AppendErrorLog($"{filename} :: {ex.Message}");
                    }

                    pb1.Value = i + 1;
                    UpdateTabHeaders();
                }

                lblmessage.Text = $"Finished: {_successCount} succeeded, {_errorCount} failed.";

                if (_errorCount > 0)
                {
                    MessageBox.Show($"Deployment completed with errors.\nSucceeded: {_successCount}\nFailed: {_errorCount}", "Deployment Alert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show("Deployment completed successfully!", "Deployment Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (OperationCanceledException)
            {
                lblmessage.Text = "Deployment cancelled.";
                MessageBox.Show("Deployment was cancelled by user.", "Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                lblmessage.Text = "Deployment Failed";
                MessageBox.Show($"An unexpected deployment error occurred:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                btnCancel.Enabled = false;
                ToggleInputControls(true);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                lblmessage.Text = "Cancelling...";
                btnCancel.Enabled = false;
                _cts.Cancel();
            }
        }

        private void ToggleInputControls(bool enabled)
        {
            txtServerName.Enabled = enabled;
            txtLogin.Enabled = enabled;
            txtpassword.Enabled = enabled;
            txtDatabase.Enabled = enabled;
            txtFilePath.Enabled = enabled;
            btnBrowse.Enabled = enabled;
            btnTestConnect.Enabled = enabled;
            btnStartDeployment.Enabled = enabled;
        }

        private void AppendSuccessLog(string message)
        {
            if (rtxtlog.InvokeRequired)
            {
                rtxtlog.Invoke(new Action<string>(AppendSuccessLog), message);
            }
            else
            {
                rtxtlog.AppendText(message + Environment.NewLine);
            }
        }

        private void AppendErrorLog(string message)
        {
            if (rtxterr.InvokeRequired)
            {
                rtxterr.Invoke(new Action<string>(AppendErrorLog), message);
            }
            else
            {
                rtxterr.AppendText(message + Environment.NewLine);
            }
        }

        private void btnLogSuccess_Click(object sender, EventArgs e) => ShowLogTab(showSuccess: true);

        private void btnLogError_Click(object sender, EventArgs e) => ShowLogTab(showSuccess: false);

        // Custom segmented tabs: toggle which log is visible and restyle the buttons
        // (active = raised surface + accent text, inactive = header surface + muted text).
        private void ShowLogTab(bool showSuccess)
        {
            _showSuccessTab = showSuccess;
            rtxtlog.Visible = showSuccess;
            rtxterr.Visible = !showSuccess;

            var active = _theme.Surface;
            var inactive = _theme.Page;

            btnLogSuccess.BackColor = showSuccess ? active : inactive;
            btnLogSuccess.ForeColor = showSuccess ? _theme.Success : _theme.TextMuted;
            btnLogError.BackColor = showSuccess ? inactive : active;
            btnLogError.ForeColor = showSuccess ? _theme.TextMuted : _theme.Danger;
            btnLogSuccess.Invalidate();
            btnLogError.Invalidate();
        }

        private void UpdateTabHeaders()
        {
            if (btnLogSuccess.InvokeRequired)
            {
                btnLogSuccess.Invoke(new Action(UpdateTabHeaders));
            }
            else
            {
                btnLogSuccess.Text = $"Success Log({_successCount})";
                btnLogError.Text = $"Error Log({_errorCount})";
            }
        }
    }
}
