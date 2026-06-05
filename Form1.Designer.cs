namespace SqlDeployerGui;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    // Colors below are dark-theme defaults; ApplyTheme() re-skins everything at runtime.
    private void InitializeComponent()
    {
        this.lblTitle = new System.Windows.Forms.Label();
        this.lblSubtitle = new System.Windows.Forms.Label();
        this.btnTheme = new SqlDeployerGui.RoundedButton();
        this.lblSecConn = new System.Windows.Forms.Label();
        this.label1 = new System.Windows.Forms.Label();
        this.txtServerName = new SqlDeployerGui.RoundedTextBox();
        this.label2 = new System.Windows.Forms.Label();
        this.txtLogin = new SqlDeployerGui.RoundedTextBox();
        this.label3 = new System.Windows.Forms.Label();
        this.txtpassword = new SqlDeployerGui.RoundedTextBox();
        this.label4 = new System.Windows.Forms.Label();
        this.txtDatabase = new SqlDeployerGui.RoundedTextBox();
        this.label5 = new System.Windows.Forms.Label();
        this.txtFilePath = new SqlDeployerGui.RoundedTextBox();
        this.btnBrowse = new SqlDeployerGui.RoundedButton();
        this.btnTestConnect = new SqlDeployerGui.RoundedButton();
        this.btnStartDeployment = new SqlDeployerGui.RoundedButton();
        this.btnCancel = new SqlDeployerGui.RoundedButton();
        this.lblmessage = new System.Windows.Forms.Label();
        this.pb1 = new System.Windows.Forms.ProgressBar();
        this.lblSecOutput = new System.Windows.Forms.Label();
        this.btnLogSuccess = new SqlDeployerGui.RoundedButton();
        this.btnLogError = new SqlDeployerGui.RoundedButton();
        this.pnlLogHost = new SqlDeployerGui.RoundedPanel();
        this.rtxtlog = new System.Windows.Forms.RichTextBox();
        this.rtxterr = new System.Windows.Forms.RichTextBox();
        this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
        this.pnlLogHost.SuspendLayout();
        this.SuspendLayout();
        //
        // lblTitle
        //
        this.lblTitle.AutoSize = true;
        this.lblTitle.Font = new System.Drawing.Font("Segoe UI Semibold", 19F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(236)))), ((int)(((byte)(236)))));
        this.lblTitle.Location = new System.Drawing.Point(22, 18);
        this.lblTitle.Name = "lblTitle";
        this.lblTitle.Size = new System.Drawing.Size(160, 34);
        this.lblTitle.TabIndex = 0;
        this.lblTitle.Text = "SQL Deploy";
        //
        // lblSubtitle
        //
        this.lblSubtitle.AutoSize = true;
        this.lblSubtitle.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(154)))));
        this.lblSubtitle.Location = new System.Drawing.Point(24, 54);
        this.lblSubtitle.Name = "lblSubtitle";
        this.lblSubtitle.Size = new System.Drawing.Size(186, 17);
        this.lblSubtitle.TabIndex = 1;
        this.lblSubtitle.Text = "Database migration console";
        //
        // btnTheme
        //
        this.btnTheme.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(28)))), ((int)(((byte)(30)))));
        this.btnTheme.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(58)))), ((int)(((byte)(58)))), ((int)(((byte)(60)))));
        this.btnTheme.BorderThickness = 1;
        this.btnTheme.CornerRadius = 9;
        this.btnTheme.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnTheme.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnTheme.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(236)))), ((int)(((byte)(236)))));
        this.btnTheme.Location = new System.Drawing.Point(486, 24);
        this.btnTheme.Name = "btnTheme";
        this.btnTheme.Size = new System.Drawing.Size(90, 32);
        this.btnTheme.TabIndex = 23;
        this.btnTheme.Text = "☀  Light";
        this.btnTheme.Click += new System.EventHandler(this.btnTheme_Click);
        //
        // lblSecConn
        //
        this.lblSecConn.AutoSize = true;
        this.lblSecConn.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblSecConn.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(154)))));
        this.lblSecConn.Location = new System.Drawing.Point(26, 90);
        this.lblSecConn.Name = "lblSecConn";
        this.lblSecConn.Size = new System.Drawing.Size(82, 15);
        this.lblSecConn.TabIndex = 2;
        this.lblSecConn.Text = "CONNECTION";
        //
        // label1
        //
        this.label1.AutoSize = true;
        this.label1.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(236)))), ((int)(((byte)(236)))));
        this.label1.Location = new System.Drawing.Point(26, 125);
        this.label1.Name = "label1";
        this.label1.Size = new System.Drawing.Size(50, 17);
        this.label1.TabIndex = 3;
        this.label1.Text = "Server";
        //
        // txtServerName
        //
        this.txtServerName.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(36)))), ((int)(((byte)(38)))));
        this.txtServerName.Location = new System.Drawing.Point(150, 114);
        this.txtServerName.Name = "txtServerName";
        this.txtServerName.PlaceholderText = "localhost\\SQLEXPRESS";
        this.txtServerName.Size = new System.Drawing.Size(422, 40);
        this.txtServerName.TabIndex = 4;
        //
        // label2
        //
        this.label2.AutoSize = true;
        this.label2.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.label2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(236)))), ((int)(((byte)(236)))));
        this.label2.Location = new System.Drawing.Point(26, 177);
        this.label2.Name = "label2";
        this.label2.Size = new System.Drawing.Size(43, 17);
        this.label2.TabIndex = 5;
        this.label2.Text = "Login";
        //
        // txtLogin
        //
        this.txtLogin.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(36)))), ((int)(((byte)(38)))));
        this.txtLogin.Location = new System.Drawing.Point(150, 166);
        this.txtLogin.Name = "txtLogin";
        this.txtLogin.PlaceholderText = "Leave blank for Windows auth";
        this.txtLogin.Size = new System.Drawing.Size(422, 40);
        this.txtLogin.TabIndex = 6;
        //
        // label3
        //
        this.label3.AutoSize = true;
        this.label3.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(236)))), ((int)(((byte)(236)))));
        this.label3.Location = new System.Drawing.Point(26, 229);
        this.label3.Name = "label3";
        this.label3.Size = new System.Drawing.Size(67, 17);
        this.label3.TabIndex = 7;
        this.label3.Text = "Password";
        //
        // txtpassword
        //
        this.txtpassword.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(36)))), ((int)(((byte)(38)))));
        this.txtpassword.EnablePasswordToggle = true;
        this.txtpassword.Location = new System.Drawing.Point(150, 218);
        this.txtpassword.Name = "txtpassword";
        this.txtpassword.PlaceholderText = "Enter password";
        this.txtpassword.Size = new System.Drawing.Size(422, 40);
        this.txtpassword.TabIndex = 8;
        this.txtpassword.UseSystemPasswordChar = true;
        //
        // label4
        //
        this.label4.AutoSize = true;
        this.label4.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.label4.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(236)))), ((int)(((byte)(236)))));
        this.label4.Location = new System.Drawing.Point(26, 281);
        this.label4.Name = "label4";
        this.label4.Size = new System.Drawing.Size(67, 17);
        this.label4.TabIndex = 9;
        this.label4.Text = "Database";
        //
        // txtDatabase
        //
        this.txtDatabase.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(36)))), ((int)(((byte)(38)))));
        this.txtDatabase.Location = new System.Drawing.Point(150, 270);
        this.txtDatabase.Name = "txtDatabase";
        this.txtDatabase.PlaceholderText = "Target database name";
        this.txtDatabase.Size = new System.Drawing.Size(422, 40);
        this.txtDatabase.TabIndex = 10;
        //
        // label5
        //
        this.label5.AutoSize = true;
        this.label5.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.label5.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(236)))), ((int)(((byte)(236)))));
        this.label5.Location = new System.Drawing.Point(26, 333);
        this.label5.Name = "label5";
        this.label5.Size = new System.Drawing.Size(75, 17);
        this.label5.TabIndex = 11;
        this.label5.Text = "Script path";
        //
        // txtFilePath
        //
        this.txtFilePath.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(36)))), ((int)(((byte)(38)))));
        this.txtFilePath.Location = new System.Drawing.Point(150, 322);
        this.txtFilePath.Name = "txtFilePath";
        this.txtFilePath.PlaceholderText = "Folder with .sql migration scripts";
        this.txtFilePath.Size = new System.Drawing.Size(316, 40);
        this.txtFilePath.TabIndex = 12;
        //
        // btnBrowse
        //
        this.btnBrowse.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
        this.btnBrowse.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(58)))), ((int)(((byte)(58)))), ((int)(((byte)(60)))));
        this.btnBrowse.BorderThickness = 1;
        this.btnBrowse.CornerRadius = 10;
        this.btnBrowse.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnBrowse.Font = new System.Drawing.Font("Segoe UI Semibold", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnBrowse.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(236)))), ((int)(((byte)(236)))));
        this.btnBrowse.Location = new System.Drawing.Point(478, 322);
        this.btnBrowse.Name = "btnBrowse";
        this.btnBrowse.Size = new System.Drawing.Size(94, 40);
        this.btnBrowse.TabIndex = 13;
        this.btnBrowse.Text = "Browse";
        this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
        //
        // btnTestConnect
        //
        this.btnTestConnect.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(28)))), ((int)(((byte)(30)))));
        this.btnTestConnect.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(58)))), ((int)(((byte)(58)))), ((int)(((byte)(60)))));
        this.btnTestConnect.BorderThickness = 1;
        this.btnTestConnect.CornerRadius = 11;
        this.btnTestConnect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnTestConnect.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnTestConnect.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(236)))), ((int)(((byte)(236)))), ((int)(((byte)(236)))));
        this.btnTestConnect.Location = new System.Drawing.Point(26, 380);
        this.btnTestConnect.Name = "btnTestConnect";
        this.btnTestConnect.Size = new System.Drawing.Size(168, 42);
        this.btnTestConnect.TabIndex = 14;
        this.btnTestConnect.Text = "Test Connection";
        this.btnTestConnect.Click += new System.EventHandler(this.btnTestConnect_Click);
        //
        // btnStartDeployment
        //
        this.btnStartDeployment.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(94)))), ((int)(((byte)(151)))), ((int)(((byte)(214)))));
        this.btnStartDeployment.CornerRadius = 11;
        this.btnStartDeployment.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnStartDeployment.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnStartDeployment.ForeColor = System.Drawing.Color.White;
        this.btnStartDeployment.Location = new System.Drawing.Point(210, 380);
        this.btnStartDeployment.Name = "btnStartDeployment";
        this.btnStartDeployment.Size = new System.Drawing.Size(200, 42);
        this.btnStartDeployment.TabIndex = 15;
        this.btnStartDeployment.Text = "Deploy";
        this.btnStartDeployment.Click += new System.EventHandler(this.btnStartDeployment_Click);
        //
        // btnCancel
        //
        this.btnCancel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(28)))), ((int)(((byte)(30)))));
        this.btnCancel.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(106)))), ((int)(((byte)(102)))));
        this.btnCancel.BorderThickness = 1;
        this.btnCancel.CornerRadius = 11;
        this.btnCancel.Enabled = false;
        this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnCancel.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnCancel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(106)))), ((int)(((byte)(102)))));
        this.btnCancel.Location = new System.Drawing.Point(426, 380);
        this.btnCancel.Name = "btnCancel";
        this.btnCancel.Size = new System.Drawing.Size(146, 42);
        this.btnCancel.TabIndex = 16;
        this.btnCancel.Text = "Cancel";
        this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
        //
        // lblmessage
        //
        this.lblmessage.AutoSize = true;
        this.lblmessage.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.lblmessage.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(154)))));
        this.lblmessage.Location = new System.Drawing.Point(26, 436);
        this.lblmessage.Name = "lblmessage";
        this.lblmessage.Size = new System.Drawing.Size(44, 17);
        this.lblmessage.TabIndex = 17;
        this.lblmessage.Text = "Ready";
        //
        // pb1
        //
        this.pb1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
        this.pb1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(94)))), ((int)(((byte)(151)))), ((int)(((byte)(214)))));
        this.pb1.Location = new System.Drawing.Point(26, 458);
        this.pb1.Name = "pb1";
        this.pb1.Size = new System.Drawing.Size(546, 6);
        this.pb1.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
        this.pb1.TabIndex = 18;
        //
        // lblSecOutput
        //
        this.lblSecOutput.AutoSize = true;
        this.lblSecOutput.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblSecOutput.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(154)))));
        this.lblSecOutput.Location = new System.Drawing.Point(26, 484);
        this.lblSecOutput.Name = "lblSecOutput";
        this.lblSecOutput.Size = new System.Drawing.Size(52, 15);
        this.lblSecOutput.TabIndex = 19;
        this.lblSecOutput.Text = "OUTPUT";
        //
        // btnLogSuccess
        //
        this.btnLogSuccess.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
        this.btnLogSuccess.CornerRadius = 9;
        this.btnLogSuccess.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnLogSuccess.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnLogSuccess.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(200)))), ((int)(((byte)(150)))));
        this.btnLogSuccess.Location = new System.Drawing.Point(26, 508);
        this.btnLogSuccess.Name = "btnLogSuccess";
        this.btnLogSuccess.Size = new System.Drawing.Size(160, 32);
        this.btnLogSuccess.TabIndex = 20;
        this.btnLogSuccess.Text = "Success Log(0)";
        this.btnLogSuccess.Click += new System.EventHandler(this.btnLogSuccess_Click);
        //
        // btnLogError
        //
        this.btnLogError.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(28)))), ((int)(((byte)(30)))));
        this.btnLogError.CornerRadius = 9;
        this.btnLogError.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnLogError.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnLogError.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(150)))), ((int)(((byte)(150)))), ((int)(((byte)(154)))));
        this.btnLogError.Location = new System.Drawing.Point(194, 508);
        this.btnLogError.Name = "btnLogError";
        this.btnLogError.Size = new System.Drawing.Size(160, 32);
        this.btnLogError.TabIndex = 21;
        this.btnLogError.Text = "Error Log(0)";
        this.btnLogError.Click += new System.EventHandler(this.btnLogError_Click);
        //
        // pnlLogHost
        //
        this.pnlLogHost.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(36)))), ((int)(((byte)(38)))));
        this.pnlLogHost.BorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(58)))), ((int)(((byte)(58)))), ((int)(((byte)(60)))));
        this.pnlLogHost.BorderThickness = 1;
        this.pnlLogHost.CornerRadius = 14;
        this.pnlLogHost.Controls.Add(this.rtxterr);
        this.pnlLogHost.Controls.Add(this.rtxtlog);
        this.pnlLogHost.Location = new System.Drawing.Point(26, 548);
        this.pnlLogHost.Name = "pnlLogHost";
        this.pnlLogHost.Padding = new System.Windows.Forms.Padding(14);
        this.pnlLogHost.Size = new System.Drawing.Size(546, 122);
        this.pnlLogHost.TabIndex = 22;
        //
        // rtxtlog
        //
        this.rtxtlog.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(36)))), ((int)(((byte)(38)))));
        this.rtxtlog.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this.rtxtlog.Dock = System.Windows.Forms.DockStyle.Fill;
        this.rtxtlog.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.rtxtlog.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(120)))), ((int)(((byte)(200)))), ((int)(((byte)(150)))));
        this.rtxtlog.Location = new System.Drawing.Point(14, 14);
        this.rtxtlog.Name = "rtxtlog";
        this.rtxtlog.ReadOnly = true;
        this.rtxtlog.Size = new System.Drawing.Size(518, 94);
        this.rtxtlog.TabIndex = 0;
        this.rtxtlog.Text = "";
        //
        // rtxterr
        //
        this.rtxterr.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(36)))), ((int)(((byte)(38)))));
        this.rtxterr.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this.rtxterr.Dock = System.Windows.Forms.DockStyle.Fill;
        this.rtxterr.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.rtxterr.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(106)))), ((int)(((byte)(102)))));
        this.rtxterr.Location = new System.Drawing.Point(14, 14);
        this.rtxterr.Name = "rtxterr";
        this.rtxterr.ReadOnly = true;
        this.rtxterr.Size = new System.Drawing.Size(518, 94);
        this.rtxterr.TabIndex = 1;
        this.rtxterr.Text = "";
        this.rtxterr.Visible = false;
        //
        // Form1
        //
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(28)))), ((int)(((byte)(28)))), ((int)(((byte)(30)))));
        this.ClientSize = new System.Drawing.Size(600, 694);
        this.Controls.Add(this.btnTheme);
        this.Controls.Add(this.pnlLogHost);
        this.Controls.Add(this.btnLogError);
        this.Controls.Add(this.btnLogSuccess);
        this.Controls.Add(this.lblSecOutput);
        this.Controls.Add(this.pb1);
        this.Controls.Add(this.lblmessage);
        this.Controls.Add(this.btnCancel);
        this.Controls.Add(this.btnStartDeployment);
        this.Controls.Add(this.btnTestConnect);
        this.Controls.Add(this.btnBrowse);
        this.Controls.Add(this.txtFilePath);
        this.Controls.Add(this.label5);
        this.Controls.Add(this.txtDatabase);
        this.Controls.Add(this.label4);
        this.Controls.Add(this.txtpassword);
        this.Controls.Add(this.label3);
        this.Controls.Add(this.txtLogin);
        this.Controls.Add(this.label2);
        this.Controls.Add(this.txtServerName);
        this.Controls.Add(this.label1);
        this.Controls.Add(this.lblSecConn);
        this.Controls.Add(this.lblSubtitle);
        this.Controls.Add(this.lblTitle);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Name = "Form1";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "SQL Deploy — Migration Console";
        this.pnlLogHost.ResumeLayout(false);
        this.ResumeLayout(false);
        this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.Label lblSubtitle;
    private SqlDeployerGui.RoundedButton btnTheme;
    private System.Windows.Forms.Label lblSecConn;
    private System.Windows.Forms.Label label1;
    private SqlDeployerGui.RoundedTextBox txtServerName;
    private System.Windows.Forms.Label label2;
    private SqlDeployerGui.RoundedTextBox txtLogin;
    private System.Windows.Forms.Label label3;
    private SqlDeployerGui.RoundedTextBox txtpassword;
    private System.Windows.Forms.Label label4;
    private SqlDeployerGui.RoundedTextBox txtDatabase;
    private System.Windows.Forms.Label label5;
    private SqlDeployerGui.RoundedTextBox txtFilePath;
    private SqlDeployerGui.RoundedButton btnBrowse;
    private SqlDeployerGui.RoundedButton btnTestConnect;
    private SqlDeployerGui.RoundedButton btnStartDeployment;
    private SqlDeployerGui.RoundedButton btnCancel;
    private System.Windows.Forms.Label lblmessage;
    private System.Windows.Forms.ProgressBar pb1;
    private System.Windows.Forms.Label lblSecOutput;
    private SqlDeployerGui.RoundedButton btnLogSuccess;
    private SqlDeployerGui.RoundedButton btnLogError;
    private SqlDeployerGui.RoundedPanel pnlLogHost;
    private System.Windows.Forms.RichTextBox rtxtlog;
    private System.Windows.Forms.RichTextBox rtxterr;
    private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
}
