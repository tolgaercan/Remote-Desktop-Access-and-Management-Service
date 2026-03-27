using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using RemoteDesktop.Core;

const int defaultPort = 5050;
const int defaultFps = 12;
const long jpegQuality = 55L;

int port = args.Length > 0 && int.TryParse(args[0], out int parsedPort) ? parsedPort : defaultPort;
int fps = args.Length > 1 && int.TryParse(args[1], out int parsedFps) ? parsedFps : defaultFps;
int frameDelayMs = Math.Max(15, 1000 / Math.Max(1, fps));

Console.WriteLine($"Host starting on port {port} at {fps} FPS");

TcpListener listener = new(IPAddress.Any, port);
listener.Start();

while (true)
{
    Console.WriteLine("Waiting for client...");
    using TcpClient client = await listener.AcceptTcpClientAsync();
    using NetworkStream stream = client.GetStream();
    Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");

    try
    {
        while (client.Connected)
        {
            byte[] frame = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? CaptureWindowsFrame(jpegQuality)
                : CaptureMacFramePlaceholder();

            await RemoteProtocol.WritePacketAsync(stream, PacketType.Frame, frame, CancellationToken.None);
            await Task.Delay(frameDelayMs);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Client disconnected: {ex.Message}");
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
