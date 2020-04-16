using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Video
{
    [Description("Publishes the sequence of images as a JPEG image server.")]
    public class JpegServer : Sink<IplImage>
    {
        [Description("The URL which will provide JPEG image files.")]
        public string SourceUrl { get; set; }

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
                    listener.Prefixes.Add(SourceUrl);
                    listener.Start();
                    return Observable
                        .FromAsync(listener.GetContextAsync).Repeat().Retry()
                        .SelectMany(context => frames.FirstAsync().Do(data =>
                        {
                            context.Response.OutputStream.Write(data, 0, data.Length);
                            context.Response.Close();
                        })).Retry().IgnoreElements().Select(x => default(IplImage));
                })));
        }
    }
}
