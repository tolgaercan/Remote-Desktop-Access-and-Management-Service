namespace RemoteDesktop.Client.Windows;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        hostTextBox = new TextBox();
        portTextBox = new TextBox();
        connectButton = new Button();
        statusLabel = new Label();
        screenPictureBox = new PictureBox();
        ((System.ComponentModel.ISupportInitialize)screenPictureBox).BeginInit();
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1200, 760);
        Controls.Add(screenPictureBox);
        Controls.Add(statusLabel);
        Controls.Add(connectButton);
        Controls.Add(portTextBox);
        Controls.Add(hostTextBox);
        MinimumSize = new Size(900, 600);
        Name = "Form1";
        Text = "Remote Desktop Client (LAN)";
        KeyPreview = true;
        KeyDown += Form1_KeyDown;
        KeyUp += Form1_KeyUp;
        hostTextBox.Location = new Point(12, 12);
        hostTextBox.Name = "hostTextBox";
        hostTextBox.PlaceholderText = "Host IP (example: 192.168.1.10)";
        hostTextBox.Size = new Size(260, 27);
        hostTextBox.TabIndex = 0;
        hostTextBox.Text = "127.0.0.1";
        portTextBox.Location = new Point(278, 12);
        portTextBox.Name = "portTextBox";
        portTextBox.Size = new Size(90, 27);
        portTextBox.TabIndex = 1;
        portTextBox.Text = "5050";
        connectButton.Location = new Point(374, 11);
        connectButton.Name = "connectButton";
        connectButton.Size = new Size(94, 29);
        connectButton.TabIndex = 2;
        connectButton.Text = "Connect";
        connectButton.UseVisualStyleBackColor = true;
        connectButton.Click += connectButton_Click;
        statusLabel.AutoSize = true;
        statusLabel.Location = new Point(474, 16);
        statusLabel.Name = "statusLabel";
        statusLabel.Size = new Size(43, 20);
        statusLabel.TabIndex = 3;
        statusLabel.Text = "Idle...";
        screenPictureBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        screenPictureBox.BackColor = Color.Black;
        screenPictureBox.Location = new Point(12, 48);
        screenPictureBox.Name = "screenPictureBox";
        screenPictureBox.Size = new Size(1176, 700);
        screenPictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        screenPictureBox.TabIndex = 4;
        screenPictureBox.TabStop = false;
        screenPictureBox.MouseMove += screenPictureBox_MouseMove;
        screenPictureBox.MouseDown += screenPictureBox_MouseDown;
        screenPictureBox.MouseUp += screenPictureBox_MouseUp;
        ((System.ComponentModel.ISupportInitialize)screenPictureBox).EndInit();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private TextBox hostTextBox;
    private TextBox portTextBox;
    private Button connectButton;
    private Label statusLabel;
    private PictureBox screenPictureBox;
}
