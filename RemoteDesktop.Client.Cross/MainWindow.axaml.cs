using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RemoteDesktop.Core;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteDesktop.Client.Cross;

public partial class MainWindow : Window
{
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Size _remoteFrameSize = default;
    private long _lastMouseMoveAt;
    private int _renderBusy;

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += MainWindow_KeyDown;
        KeyUp += MainWindow_KeyUp;
        TextInput += MainWindow_TextInput;
    }

    private async void ConnectButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!int.TryParse(PortTextBox.Text, out int port))
        {
            StatusTextBlock.Text = "Invalid port";
            return;
        }

        string host = HostTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(host))
        {
            StatusTextBlock.Text = "Host IP required";
            return;
        }

        ConnectButton.IsEnabled = false;
        StatusTextBlock.Text = "Connecting...";

        try
        {
            await ReceiveLoopAsync(host, port, _cts.Token);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error: {ex.Message}";
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private async Task ReceiveLoopAsync(string host, int port, CancellationToken cancellationToken)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(host, port, cancellationToken);
        _client.NoDelay = true;
        _stream = _client.GetStream();
        StatusTextBlock.Text = $"Connected to {host}:{port}";
        ScreenImage.Focus();

        while (!cancellationToken.IsCancellationRequested)
        {
            (PacketType packetType, byte[] payload) = await RemoteProtocol.ReadPacketAsync(_stream, cancellationToken);
            if (packetType != PacketType.Frame)
            {
                continue;
            }

            if (Interlocked.Exchange(ref _renderBusy, 1) == 1)
            {
                continue;
            }

            using MemoryStream ms = new(payload);
            Bitmap bitmap = new(ms);
            _remoteFrameSize = new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height);

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    ScreenImage.Source = bitmap;
                }
                finally
                {
                    Interlocked.Exchange(ref _renderBusy, 0);
                }
            });
        }
    }

    private async Task SendPacketAsync(PacketType packetType, byte[] payload)
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
            // Ignore transient disconnect during UI event burst.
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private void ScreenImage_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_remoteFrameSize.Width <= 0 || _remoteFrameSize.Height <= 0)
        {
            return;
        }

        long now = Environment.TickCount64;
        if (now - Interlocked.Read(ref _lastMouseMoveAt) < 12)
        {
            return;
        }
        Interlocked.Exchange(ref _lastMouseMoveAt, now);

        if (!TryMapToRemoteCoordinates(e.GetPosition(ScreenImage), out int remoteX, out int remoteY))
        {
            return;
        }

        _ = SendPacketAsync(PacketType.MouseMove, RemoteProtocol.BuildMouseMovePayload(remoteX, remoteY));
    }

    private void ScreenImage_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ScreenImage.Focus();
        RemoteMouseButton? btn = ToRemoteButton(e.GetCurrentPoint(ScreenImage).Properties.PointerUpdateKind);
        if (btn is null)
        {
            return;
        }

        _ = SendPacketAsync(PacketType.MouseDown, RemoteProtocol.BuildMouseButtonPayload(btn.Value));
    }

    private void ScreenImage_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        RemoteMouseButton? btn = ToRemoteButton(e.GetCurrentPoint(ScreenImage).Properties.PointerUpdateKind);
        if (btn is null)
        {
            return;
        }

        _ = SendPacketAsync(PacketType.MouseUp, RemoteProtocol.BuildMouseButtonPayload(btn.Value));
    }

    private void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        // Turkish Mac layout: '#' is commonly Option+3.
        // Send it as text directly to avoid modifier mapping conflicts.
        if (e.Key == Key.D3 && (e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            _ = SendPacketAsync(PacketType.TextInput, RemoteProtocol.BuildTextInputPayload("#"));
            return;
        }

        if (ShouldSendAsTextInput(e))
        {
            return;
        }

        int? vk = MapToWindowsVirtualKey(e.Key);
        if (vk is null)
        {
            return;
        }

        // Lock keys are more reliable when sent as immediate press+release.
        if (vk.Value == 0x14)
        {
            _ = SendPacketAsync(PacketType.KeyDown, RemoteProtocol.BuildKeyPayload(vk.Value));
            _ = SendPacketAsync(PacketType.KeyUp, RemoteProtocol.BuildKeyPayload(vk.Value));
            return;
        }

        _ = SendPacketAsync(PacketType.KeyDown, RemoteProtocol.BuildKeyPayload(vk.Value));
    }

    private void MainWindow_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.D3 && (e.KeyModifiers & KeyModifiers.Alt) != 0)
        {
            return;
        }

        if (ShouldSendAsTextInput(e))
        {
            return;
        }

        int? vk = MapToWindowsVirtualKey(e.Key);
        if (vk is null)
        {
            return;
        }

        if (vk.Value == 0x14)
        {
            return;
        }

        _ = SendPacketAsync(PacketType.KeyUp, RemoteProtocol.BuildKeyPayload(vk.Value));
    }

    private void MainWindow_TextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        _ = SendPacketAsync(PacketType.TextInput, RemoteProtocol.BuildTextInputPayload(e.Text));
    }

    private void ScreenImage_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        int delta = (int)Math.Round(e.Delta.Y * 120.0);
        if (delta == 0)
        {
            return;
        }

        _ = SendPacketAsync(PacketType.MouseWheel, RemoteProtocol.BuildMouseWheelPayload(delta));
    }

    private bool TryMapToRemoteCoordinates(Point point, out int remoteX, out int remoteY)
    {
        remoteX = 0;
        remoteY = 0;

        Rect bounds = ScreenImage.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0 || _remoteFrameSize.Width <= 0 || _remoteFrameSize.Height <= 0)
        {
            return false;
        }

        double imageRatio = _remoteFrameSize.Width / _remoteFrameSize.Height;
        double boxRatio = bounds.Width / bounds.Height;
        Rect drawRect;

        if (boxRatio > imageRatio)
        {
            double width = bounds.Height * imageRatio;
            double left = (bounds.Width - width) / 2;
            drawRect = new Rect(left, 0, width, bounds.Height);
        }
        else
        {
            double height = bounds.Width / imageRatio;
            double top = (bounds.Height - height) / 2;
            drawRect = new Rect(0, top, bounds.Width, height);
        }

        if (!drawRect.Contains(point))
        {
            return false;
        }

        double relX = (point.X - drawRect.X) / drawRect.Width;
        double relY = (point.Y - drawRect.Y) / drawRect.Height;
        remoteX = (int)Math.Clamp(Math.Round(relX * (_remoteFrameSize.Width - 1)), 0, _remoteFrameSize.Width - 1);
        remoteY = (int)Math.Clamp(Math.Round(relY * (_remoteFrameSize.Height - 1)), 0, _remoteFrameSize.Height - 1);
        return true;
    }

    private static RemoteMouseButton? ToRemoteButton(PointerUpdateKind kind)
    {
        return kind switch
        {
            PointerUpdateKind.LeftButtonPressed => RemoteMouseButton.Left,
            PointerUpdateKind.LeftButtonReleased => RemoteMouseButton.Left,
            PointerUpdateKind.RightButtonPressed => RemoteMouseButton.Right,
            PointerUpdateKind.RightButtonReleased => RemoteMouseButton.Right,
            PointerUpdateKind.MiddleButtonPressed => RemoteMouseButton.Middle,
            PointerUpdateKind.MiddleButtonReleased => RemoteMouseButton.Middle,
            _ => null
        };
    }

    private static int? MapToWindowsVirtualKey(Key key)
    {
        // macOS Command/Windows key should behave like Ctrl for shortcut ergonomics.
        string keyName = key.ToString();
        if (keyName.Contains("Meta", StringComparison.OrdinalIgnoreCase) ||
            keyName.Contains("Win", StringComparison.OrdinalIgnoreCase) ||
            keyName.Contains("Command", StringComparison.OrdinalIgnoreCase))
        {
            return 0x11;
        }

        // macOS Option should behave like Alt on Windows.
        if (keyName.Contains("Option", StringComparison.OrdinalIgnoreCase) ||
            keyName.Contains("Alt", StringComparison.OrdinalIgnoreCase))
        {
            return 0x12;
        }

        if (key >= Key.A && key <= Key.Z)
        {
            return 'A' + (key - Key.A);
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return '0' + (key - Key.D0);
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return 0x60 + (key - Key.NumPad0);
        }

        return key switch
        {
            // Turkish Q / OEM keys
            Key.Oem1 => 0xBA,
            Key.Oem2 => 0xBF,
            Key.Oem3 => 0xC0,
            Key.Oem4 => 0xDB,
            Key.Oem5 => 0xDC,
            Key.Oem6 => 0xDD,
            Key.Oem7 => 0xDE,
            Key.Oem8 => 0xDF,
            Key.Oem102 => 0xE2,
            Key.OemComma => 0xBC,
            Key.OemPeriod => 0xBE,
            Key.OemMinus => 0xBD,
            Key.OemPlus => 0xBB,
            Key.Enter => 0x0D,
            Key.Back => 0x08,
            Key.Tab => 0x09,
            Key.Space => 0x20,
            Key.Escape => 0x1B,
            Key.CapsLock => 0x14,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.LeftShift or Key.RightShift => 0x10,
            Key.LeftCtrl or Key.RightCtrl => 0x11,
            Key.LeftAlt or Key.RightAlt => 0x12,
            Key.Delete => 0x2E,
            Key.Insert => 0x2D,
            Key.Home => 0x24,
            Key.End => 0x23,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.F1 => 0x70,
            Key.F2 => 0x71,
            Key.F3 => 0x72,
            Key.F4 => 0x73,
            Key.F5 => 0x74,
            Key.F6 => 0x75,
            Key.F7 => 0x76,
            Key.F8 => 0x77,
            Key.F9 => 0x78,
            Key.F10 => 0x79,
            Key.F11 => 0x7A,
            Key.F12 => 0x7B,
            _ => null
        };
    }

    private static bool ShouldSendAsTextInput(KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Meta)) != 0)
        {
            return false;
        }

        return (e.Key >= Key.A && e.Key <= Key.Z) ||
               (e.Key >= Key.D0 && e.Key <= Key.D9) ||
               (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) ||
               e.Key.ToString().StartsWith("Oem", StringComparison.OrdinalIgnoreCase);
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _cts.Cancel();
        _stream?.Dispose();
        _client?.Dispose();
        _sendLock.Dispose();
        base.OnClosing(e);
    }
}