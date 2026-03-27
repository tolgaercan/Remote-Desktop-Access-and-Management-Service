using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using RemoteDesktop.Core;

const int defaultPort = 5050;
const int defaultFps = 12;
const long jpegQuality = 55L;
const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
const uint MOUSEEVENTF_LEFTUP = 0x0004;
const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
const uint MOUSEEVENTF_RIGHTUP = 0x0010;
const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
const uint KEYEVENTF_KEYUP = 0x0002;

int port = args.Length > 0 && int.TryParse(args[0], out int parsedPort) ? parsedPort : defaultPort;
int fps = args.Length > 1 && int.TryParse(args[1], out int parsedFps) ? parsedFps : defaultFps;
long quality = args.Length > 2 && long.TryParse(args[2], out long parsedQuality)
    ? Math.Clamp(parsedQuality, 25L, 90L)
    : jpegQuality;
int frameDelayMs = Math.Max(15, 1000 / Math.Max(1, fps));

Console.WriteLine($"Host starting on port {port} at {fps} FPS, JPEG quality {quality}");

TcpListener listener = new(IPAddress.Any, port);
listener.Start();

while (true)
{
    Console.WriteLine("Waiting for client...");
    using TcpClient client = await listener.AcceptTcpClientAsync();
    client.NoDelay = true;
    using NetworkStream stream = client.GetStream();
    Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");

    try
    {
        using CancellationTokenSource clientCts = new();
        Task senderTask = SendFramesLoopAsync(stream, frameDelayMs, quality, clientCts.Token);
        Task receiverTask = ReceiveInputLoopAsync(stream, clientCts.Token);
        Task completed = await Task.WhenAny(senderTask, receiverTask);
        clientCts.Cancel();
        await completed;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Client disconnected: {ex.Message}");
    }
}

static async Task SendFramesLoopAsync(NetworkStream stream, int frameDelayMs, long quality, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        byte[] frame = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? CaptureWindowsFrame(quality)
            : CaptureMacFramePlaceholder();

        await RemoteProtocol.WritePacketAsync(stream, PacketType.Frame, frame, cancellationToken);
        await Task.Delay(frameDelayMs, cancellationToken);
    }
}

static async Task ReceiveInputLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        (PacketType packetType, byte[] payload) = await RemoteProtocol.ReadPacketAsync(stream, cancellationToken);
        HandleInputPacket(packetType, payload);
    }
}

static void HandleInputPacket(PacketType packetType, byte[] payload)
{
    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        return;
    }

    switch (packetType)
    {
        case PacketType.MouseMove:
        {
            (int x, int y) = RemoteProtocol.ParseMouseMovePayload(payload);
            SetCursorPos(x, y);
            break;
        }
        case PacketType.MouseDown:
        {
            RemoteMouseButton button = RemoteProtocol.ParseMouseButtonPayload(payload);
            SendMouseButton(button, down: true);
            break;
        }
        case PacketType.MouseUp:
        {
            RemoteMouseButton button = RemoteProtocol.ParseMouseButtonPayload(payload);
            SendMouseButton(button, down: false);
            break;
        }
        case PacketType.KeyDown:
        {
            int vk = RemoteProtocol.ParseKeyPayload(payload);
            keybd_event((byte)vk, 0, 0, UIntPtr.Zero);
            break;
        }
        case PacketType.KeyUp:
        {
            int vk = RemoteProtocol.ParseKeyPayload(payload);
            keybd_event((byte)vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            break;
        }
        default:
            break;
    }
}

static void SendMouseButton(RemoteMouseButton button, bool down)
{
    uint flag = button switch
    {
        RemoteMouseButton.Left => down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP,
        RemoteMouseButton.Right => down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP,
        RemoteMouseButton.Middle => down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP,
        _ => 0
    };

    if (flag != 0)
    {
        mouse_event(flag, 0, 0, 0, UIntPtr.Zero);
    }
}

static byte[] CaptureWindowsFrame(long quality)
{
    int width = GetSystemMetrics(0);
    int height = GetSystemMetrics(1);
    if (width <= 0 || height <= 0)
    {
        throw new InvalidOperationException("Could not detect primary screen size.");
    }
    Rectangle bounds = new(0, 0, width, height);

    using Bitmap bitmap = new(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
    using Graphics g = Graphics.FromImage(bitmap);
    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);

    using MemoryStream ms = new();
    ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageEncoders().First(codec => codec.MimeType == "image/jpeg");
    EncoderParameters encoderParameters = new(1);
    encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
    bitmap.Save(ms, jpegCodec, encoderParameters);
    return ms.ToArray();
}

static byte[] CaptureMacFramePlaceholder()
{
    string tempFile = Path.Combine(Path.GetTempPath(), $"rd-host-{Guid.NewGuid():N}.jpg");
    ProcessStartInfo psi = new()
    {
        FileName = "screencapture",
        Arguments = $"-x -t jpg \"{tempFile}\"",
        RedirectStandardOutput = false,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using Process process = Process.Start(psi)
        ?? throw new InvalidOperationException("Failed to start macOS screencapture process.");
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        string err = process.StandardError.ReadToEnd();
        throw new InvalidOperationException($"screencapture failed with exit code {process.ExitCode}: {err}");
    }

    if (!File.Exists(tempFile))
    {
        throw new InvalidOperationException("screencapture did not produce an output file.");
    }

    byte[] jpegBytes = File.ReadAllBytes(tempFile);
    try
    {
        File.Delete(tempFile);
    }
    catch
    {
        // Best-effort cleanup only; capture should still continue.
    }

    if (jpegBytes.Length == 0)
    {
        throw new InvalidOperationException("screencapture returned empty frame.");
    }

    return jpegBytes;
}

[DllImport("user32.dll")]
static extern int GetSystemMetrics(int nIndex);

[DllImport("user32.dll")]
static extern bool SetCursorPos(int x, int y);

[DllImport("user32.dll")]
static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

[DllImport("user32.dll")]
static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
