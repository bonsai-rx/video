using OpenCV.Net;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text;

namespace Bonsai.Video
{
    [DefaultProperty(nameof(UriPrefixes))]
    [Description("Publishes the sequence of images as an MJPEG image stream.")]
    public class MjpegServer : Sink<IplImage>
    {
        [Description("The URI prefixes handled by the MJPEG image server.")]
        public Collection<string> UriPrefixes { get; } = new Collection<string>();

        public override IObservable<IplImage> Process(IObservable<IplImage> source)
        {
            return source.Publish(ps => ps.Merge(Observable.Using(
                () => new HttpListener(),
                listener =>
                {
                    var frames = JpegServer.EncodeImage(ps).PublishReconnectable().RefCount();
                    foreach (var prefix in UriPrefixes)
                    {
                        listener.Prefixes.Add(prefix);
                    }
                    listener.Start();
                    return Observable
                        .FromAsync(listener.GetContextAsync).Repeat()
                        .SelectMany(async context =>
                        {
                            try
                            {
                                using (var response = context.Response)
                                {
                                    response.ContentType = "multipart/x-mixed-replace; boundary=--boundary";
                                    var stream = response.OutputStream;
                                    var builder = new StringBuilder();
                                    return await frames.SelectMany(async data =>
                                    {
                                        builder.AppendLine();
                                        builder.AppendLine("--boundary");
                                        builder.AppendLine("Content-Type: image/jpeg");
                                        builder.AppendLine("Content-Length: " + data.Length.ToString());
                                        builder.AppendLine();
                                        var header = Encoding.ASCII.GetBytes(builder.ToString());
                                        await stream.WriteAsync(header, 0, header.Length);
                                        await stream.WriteAsync(data, 0, data.Length);
                                        builder.Clear();
                                        return default(IplImage);
                                    });
                                }
                            }
                            catch (HttpListenerException) { }
                            return default;
                        })
                        .IgnoreElements();
                })));
        }
    }
}
