using System.Net.Sockets;
using RemoteDesktop.Core;

namespace RemoteDesktop.Client.Windows;

public partial class Form1 : Form
{
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _renderBusy;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Size _remoteFrameSize = Size.Empty;
    private long _lastMouseMoveAtTicks;

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
        _client = new TcpClient();
        await _client.ConnectAsync(host, port, cancellationToken);
        _client.NoDelay = true;

        Invoke(() => statusLabel.Text = $"Connected to {host}:{port}");
        _stream = _client.GetStream();

        while (!cancellationToken.IsCancellationRequested)
        {
            (PacketType packetType, byte[] payload) = await RemoteProtocol.ReadPacketAsync(_stream, cancellationToken);
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
                    _remoteFrameSize = frame.Size;
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

    private async Task SendPacketIfConnectedAsync(PacketType packetType, byte[] payload)
    {
        if (_stream is null || _client is null || !_client.Connected || _cts.IsCancellationRequested)
        {
            return;
        }

        await _sendLock.WaitAsync();
        try
        {
            if (_stream is not null)
            {
                await RemoteProtocol.WritePacketAsync(_stream, packetType, payload, _cts.Token);
            }
        }
        catch
        {
            // Connection may be closed while sending; ignore in UI event flow.
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void screenPictureBox_MouseMove(object sender, MouseEventArgs e)
    {
        if (_remoteFrameSize.Width <= 0 || _remoteFrameSize.Height <= 0)
        {
            return;
        }

        long nowTicks = Environment.TickCount64;
        if (nowTicks - Interlocked.Read(ref _lastMouseMoveAtTicks) < 12)
        {
            return;
        }
        Interlocked.Exchange(ref _lastMouseMoveAtTicks, nowTicks);

        if (!TryMapToRemoteCoordinates(e.Location, out int remoteX, out int remoteY))
        {
            return;
        }

        _ = SendPacketIfConnectedAsync(PacketType.MouseMove, RemoteProtocol.BuildMouseMovePayload(remoteX, remoteY));
    }

    private void screenPictureBox_MouseDown(object sender, MouseEventArgs e)
    {
        RemoteMouseButton? button = ToRemoteButton(e.Button);
        if (button is null)
        {
            return;
        }

        _ = SendPacketIfConnectedAsync(PacketType.MouseDown, RemoteProtocol.BuildMouseButtonPayload(button.Value));
    }

    private void screenPictureBox_MouseUp(object sender, MouseEventArgs e)
    {
        RemoteMouseButton? button = ToRemoteButton(e.Button);
        if (button is null)
        {
            return;
        }

        _ = SendPacketIfConnectedAsync(PacketType.MouseUp, RemoteProtocol.BuildMouseButtonPayload(button.Value));
    }

    private void Form1_KeyDown(object sender, KeyEventArgs e)
    {
        _ = SendPacketIfConnectedAsync(PacketType.KeyDown, RemoteProtocol.BuildKeyPayload((int)e.KeyCode));
    }

    private void Form1_KeyUp(object sender, KeyEventArgs e)
    {
        _ = SendPacketIfConnectedAsync(PacketType.KeyUp, RemoteProtocol.BuildKeyPayload((int)e.KeyCode));
    }

    private bool TryMapToRemoteCoordinates(Point localPoint, out int remoteX, out int remoteY)
    {
        remoteX = 0;
        remoteY = 0;

        Rectangle drawRect = GetImageDisplayRectangle(screenPictureBox.ClientRectangle, _remoteFrameSize);
        if (!drawRect.Contains(localPoint))
        {
            return false;
        }

        double relX = (localPoint.X - drawRect.Left) / (double)Math.Max(1, drawRect.Width);
        double relY = (localPoint.Y - drawRect.Top) / (double)Math.Max(1, drawRect.Height);

        remoteX = (int)Math.Clamp(Math.Round(relX * _remoteFrameSize.Width), 0, _remoteFrameSize.Width - 1);
        remoteY = (int)Math.Clamp(Math.Round(relY * _remoteFrameSize.Height), 0, _remoteFrameSize.Height - 1);
        return true;
    }

    private static Rectangle GetImageDisplayRectangle(Rectangle box, Size imageSize)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || box.Width <= 0 || box.Height <= 0)
        {
            return Rectangle.Empty;
        }

        double imageRatio = imageSize.Width / (double)imageSize.Height;
        double boxRatio = box.Width / (double)box.Height;

        if (boxRatio > imageRatio)
        {
            int height = box.Height;
            int width = (int)(height * imageRatio);
            int x = box.Left + (box.Width - width) / 2;
            return new Rectangle(x, box.Top, width, height);
        }
        else
        {
            int width = box.Width;
            int height = (int)(width / imageRatio);
            int y = box.Top + (box.Height - height) / 2;
            return new Rectangle(box.Left, y, width, height);
        }
    }

    private static RemoteMouseButton? ToRemoteButton(MouseButtons button)
    {
        return button switch
        {
            MouseButtons.Left => RemoteMouseButton.Left,
            MouseButtons.Right => RemoteMouseButton.Right,
            MouseButtons.Middle => RemoteMouseButton.Middle,
            _ => null
        };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _cts.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _sendLock.Dispose();
        base.OnFormClosing(e);
    }
}
