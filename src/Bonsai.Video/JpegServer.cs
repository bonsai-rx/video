using OpenCV.Net;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reactive.Linq;

namespace Bonsai.Video
{
    [DefaultProperty(nameof(UriPrefixes))]
    [Description("Publishes the sequence of images as a JPEG image server.")]
    public class JpegServer : Sink<IplImage>
    {
        [Description("The URI prefixes handled by the JPEG image server.")]
        public Collection<string> UriPrefixes { get; } = new Collection<string>();

        [DefaultValue("")]
        [Browsable(false)]
        public string SourceUrl
        {
            get { return string.Empty; }
            set
            {
                UriPrefixes.Add(value);
            }
        }

        internal static IObservable<byte[]> EncodeImage(IObservable<IplImage> source)
        {
            return source.Select(input =>
            {
                var data = CV.EncodeImage(".jpg", input);
                var result = new byte[data.Cols];
                using (var header = Mat.CreateMatHeader(result))
                {
                    CV.Copy(data, header);
                }
                return result;
            });
        }

        public override IObservable<IplImage> Process(IObservable<IplImage> source)
        {
            return source.Publish(ps => ps.Merge(Observable.Using(
                () => new HttpListener(),
                listener =>
                {
                    var frames = EncodeImage(ps).PublishReconnectable().RefCount();
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
                                    var data = await frames.FirstAsync();
                                    await response.OutputStream.WriteAsync(data, 0, data.Length);
                                }
                            }
                            catch (HttpListenerException) { }
                            return default(IplImage);
                        })
                        .IgnoreElements();
                })));
        }
    }
}
