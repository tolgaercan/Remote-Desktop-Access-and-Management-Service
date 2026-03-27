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
const uint MOUSEEVENTF_WHEEL = 0x0800;
const uint MOUSEEVENTF_MOVE = 0x0001;
const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
const uint KEYEVENTF_KEYUP = 0x0002;
const uint KEYEVENTF_UNICODE = 0x0004;
const int INPUT_MOUSE = 0;
const int INPUT_KEYBOARD = 1;

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
        if (packetType != PacketType.Frame)
        {
            Console.WriteLine($"Input packet received: {packetType}");
        }
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
            SendMouseMoveAbsolute(x, y);
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
            SendKeyboard(vk, keyUp: false);
            break;
        }
        case PacketType.KeyUp:
        {
            int vk = RemoteProtocol.ParseKeyPayload(payload);
            SendKeyboard(vk, keyUp: true);
            break;
        }
        case PacketType.MouseWheel:
        {
            int delta = RemoteProtocol.ParseMouseWheelPayload(payload);
            SendMouseWheel(delta);
            break;
        }
        case PacketType.TextInput:
        {
            string text = RemoteProtocol.ParseTextInputPayload(payload);
            SendUnicodeText(text);
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
        INPUT input = new()
        {
            type = INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dwFlags = flag
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<INPUT>());
    }
}

static void SendMouseMoveAbsolute(int x, int y)
{
    int width = GetSystemMetrics(0);
    int height = GetSystemMetrics(1);
    if (width <= 1 || height <= 1)
    {
        return;
    }

    int clampedX = Math.Clamp(x, 0, width - 1);
    int clampedY = Math.Clamp(y, 0, height - 1);

    int normalizedX = clampedX * 65535 / (width - 1);
    int normalizedY = clampedY * 65535 / (height - 1);

    INPUT input = new()
    {
        type = INPUT_MOUSE,
        U = new InputUnion
        {
            mi = new MOUSEINPUT
            {
                dx = normalizedX,
                dy = normalizedY,
                dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE
            }
        }
    };

    SendInput(1, [input], Marshal.SizeOf<INPUT>());
}

static void SendKeyboard(int virtualKey, bool keyUp)
{
    INPUT input = new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)virtualKey,
                dwFlags = keyUp ? KEYEVENTF_KEYUP : 0
            }
        }
    };

    SendInput(1, [input], Marshal.SizeOf<INPUT>());
}

static void SendMouseWheel(int delta)
{
    INPUT input = new()
    {
        type = INPUT_MOUSE,
        U = new InputUnion
        {
            mi = new MOUSEINPUT
            {
                mouseData = (uint)delta,
                dwFlags = MOUSEEVENTF_WHEEL
            }
        }
    };

    SendInput(1, [input], Marshal.SizeOf<INPUT>());
}

static void SendUnicodeText(string text)
{
    foreach (char ch in text)
    {
        INPUT down = new()
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wScan = ch,
                    dwFlags = KEYEVENTF_UNICODE
                }
            }
        };

        INPUT up = new()
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wScan = ch,
                    dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP
                }
            }
        };

        SendInput(1, [down], Marshal.SizeOf<INPUT>());
        SendInput(1, [up], Marshal.SizeOf<INPUT>());
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
static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

struct INPUT
{
    public int type;
    public InputUnion U;
}

[StructLayout(LayoutKind.Explicit)]
struct InputUnion
{
    [FieldOffset(0)]
    public MOUSEINPUT mi;
    [FieldOffset(0)]
    public KEYBDINPUT ki;
}

struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
}

struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
}
