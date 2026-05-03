using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Channels;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using ReVision.Dispatch;
using ReVision.EyeDevice;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using Swan.Logging;

namespace ReVision.Streamer;

public class MJPEGStreamServer
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ImageStreamController))]
    public MJPEGStreamServer()
    {
        var server = new WebServer(o => o
            .WithUrlPrefix("http://127.0.0.1:4442/")
            .WithMode(HttpListenerMode.Microsoft)
        ).WithWebApi("/", m => m.WithController<ImageStreamController>());
        
        server.StateChanged += (s, e) => $"WebServer New State - {e.NewState}".Info();
        server.RunAsync();
    }
    
    internal class ImageStreamController : WebApiController
    {
        private static byte[] WriteBoundary(int contentLength)
        {
            var builder = new StringBuilder();

            builder.Append("--");
            builder.Append("frame-boundary");
            builder.Append("\r\n");

            builder.Append("Content-Type: image/jpeg");
            builder.Append("\r\n");

            builder.AppendFormat("Content-Length: {0}", contentLength);
            builder.Append("\r\n");
            builder.Append("\r\n");

            return Encoding.ASCII.GetBytes(builder.ToString());
        }
        
        [Route(HttpVerbs.Get, "/{device}/image/left")]
        public async Task SubscribeImageStreamLeft()
        {
            var eyeDeviceManager = StreamServerManager.Get().GetEyeDeviceManager();
            if (!eyeDeviceManager.GetImageDataDispatcher(Route["device"], out var imageDataDispatcher))
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }
            
            HttpContext.Response.SendChunked = true;
            HttpContext.Response.ContentType = "multipart/x-mixed-replace; boundary=frame-boundary";
            var dataStream = HttpContext.OpenResponseStream(false, false);

            Channel<byte[]> pixelDataChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(16)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
            });
            
            void FeedImage(ImageDataDispatcher.Eye eye, byte[] data)
            {
                if (eye != ImageDataDispatcher.Eye.Left)
                    return;
                
                // Convert Luminance data to Rgba32
                byte[] convertedPixels = new byte[200 * 200 * 4];
                for (int y = 0; y < 200; y++)
                {
                    for (int x = 0; x < 200; x++)
                    {
                        byte grayscaleValue = data[y * 200 + x];
                                
                        int index = (y * 200 + x) * 4;
                        convertedPixels[index + 0] = grayscaleValue; // R
                        convertedPixels[index + 1] = grayscaleValue; // G
                        convertedPixels[index + 2] = grayscaleValue; // B
                        convertedPixels[index + 3] = 255; // A (fully opaque)
                    }
                }
                
                pixelDataChannel.Writer.TryWrite(convertedPixels);
            }
            
            imageDataDispatcher.Register(FeedImage);
            
            while (dataStream.CanWrite)
            {
                await foreach (byte[] pixelData in pixelDataChannel.Reader.ReadAllAsync())
                {
                    using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixelData, 200, 200);
                    using MemoryStream memoryStream = new MemoryStream();
                    await image.SaveAsJpegAsync(memoryStream);

                    try
                    {
                        await dataStream.WriteAsync(WriteBoundary((int)memoryStream.Length));
                        memoryStream.Position = 0;
                        await memoryStream.CopyToAsync(dataStream);
                        await dataStream.FlushAsync();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
            
            imageDataDispatcher.Unregister(FeedImage);
        }
        
        [Route(HttpVerbs.Get, "/{device}/image/right")]
        public async Task SubscribeImageStreamRight()
        {
            var eyeDeviceManager = StreamServerManager.Get().GetEyeDeviceManager();
            if (!eyeDeviceManager.GetImageDataDispatcher(Route["device"], out var imageDataDispatcher))
            {
                HttpContext.Response.StatusCode = 404;
                return;
            }
            
            HttpContext.Response.SendChunked = true;
            HttpContext.Response.ContentType = "multipart/x-mixed-replace; boundary=frame-boundary";
            var dataStream = HttpContext.OpenResponseStream(false, false);

            Channel<byte[]> pixelDataChannel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(16)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
            });
            
            void FeedImage(ImageDataDispatcher.Eye eye, byte[] data)
            {
                if (eye != ImageDataDispatcher.Eye.Right)
                    return;
                
                // Convert Luminance data to Rgba32
                byte[] convertedPixels = new byte[200 * 200 * 4];
                for (int y = 0; y < 200; y++)
                {
                    for (int x = 0; x < 200; x++)
                    {
                        byte grayscaleValue = data[y * 200 + x];
                                
                        int index = (y * 200 + x) * 4;
                        convertedPixels[index + 0] = grayscaleValue; // R
                        convertedPixels[index + 1] = grayscaleValue; // G
                        convertedPixels[index + 2] = grayscaleValue; // B
                        convertedPixels[index + 3] = 255; // A (fully opaque)
                    }
                }
                
                pixelDataChannel.Writer.TryWrite(convertedPixels);
            }
            
            imageDataDispatcher.Register(FeedImage);
            
            while (dataStream.CanWrite)
            {
                await foreach (byte[] pixelData in pixelDataChannel.Reader.ReadAllAsync())
                {
                    using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(pixelData, 200, 200);
                    using MemoryStream memoryStream = new MemoryStream();
                    await image.SaveAsJpegAsync(memoryStream);

                    try
                    {
                        await dataStream.WriteAsync(WriteBoundary((int)memoryStream.Length));
                        memoryStream.Position = 0;
                        await memoryStream.CopyToAsync(dataStream);
                        await dataStream.FlushAsync();
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                }
            }
            
            imageDataDispatcher.Unregister(FeedImage);
        }
    }
}