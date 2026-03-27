using System.Net.Sockets;
using RemoteDesktop.Core;

namespace RemoteDesktop.Client.Windows;

public partial class Form1 : Form
{
    private readonly CancellationTokenSource _cts = new();
    private int _renderBusy;

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
        client.NoDelay = true;

        Invoke(() => statusLabel.Text = $"Connected to {host}:{port}");
        using NetworkStream stream = client.GetStream();

        while (!cancellationToken.IsCancellationRequested)
        {
            (PacketType packetType, byte[] payload) = await RemoteProtocol.ReadPacketAsync(stream, cancellationToken);
            if (packetType != PacketType.Frame)
            {
                continue;
            }

            // If UI is still rendering previous frame, drop this one to keep latency low.
            if (Interlocked.Exchange(ref _renderBusy, 1) == 1)
            {
                continue;
            }

            BeginInvoke(() =>
            {
                try
                {
                    using MemoryStream ms = new(payload);
                    using Image frame = Image.FromStream(ms);
                    Image cloned = (Image)frame.Clone();

                    Image? old = screenPictureBox.Image;
                    screenPictureBox.Image = cloned;
                    old?.Dispose();
                }
                finally
                {
                    Interlocked.Exchange(ref _renderBusy, 0);
                }
            });
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts.Cancel();
        base.OnFormClosing(e);
    }
}
