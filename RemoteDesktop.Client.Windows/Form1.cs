using System.Net.Sockets;
using RemoteDesktop.Core;

namespace RemoteDesktop.Client.Windows;

public partial class Form1 : Form
{
    private readonly CancellationTokenSource _cts = new();

    public Form1()
    {
        InitializeComponent();
    }

    private async void connectButton_Click(object sender, EventArgs e)
    {
        connectButton.Enabled = false;
        statusLabel.Text = "Connecting...";

        try
        {
            await Task.Run(() => ReceiveLoop(hostTextBox.Text.Trim(), int.Parse(portTextBox.Text.Trim()), _cts.Token));
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            connectButton.Enabled = true;
        }
    }

    private async Task ReceiveLoop(string host, int port, CancellationToken cancellationToken)
    {
        using TcpClient client = new();
        await client.ConnectAsync(host, port, cancellationToken);

        Invoke(() => statusLabel.Text = $"Connected to {host}:{port}");
        using NetworkStream stream = client.GetStream();

        while (!cancellationToken.IsCancellationRequested)
        {
            (PacketType packetType, byte[] payload) = await RemoteProtocol.ReadPacketAsync(stream, cancellationToken);
            if (packetType != PacketType.Frame)
            {
                continue;
            }

            using MemoryStream ms = new(payload);
            using Image frame = Image.FromStream(ms);
            Image cloned = (Image)frame.Clone();

            Invoke(() =>
            {
                Image? old = screenPictureBox.Image;
                screenPictureBox.Image = cloned;
                old?.Dispose();
            });
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts.Cancel();
        base.OnFormClosing(e);
    }
}
