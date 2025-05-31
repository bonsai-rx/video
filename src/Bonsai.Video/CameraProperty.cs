using AForge.Video.DirectShow;

namespace Bonsai.Video
{
    public class CameraProperty
    {
        public CameraControlProperty Property { get; set; } = CameraControlProperty.Exposure;

        public int Value { get; set; }

        public CameraControlFlags ControlFlags { get; set; } = CameraControlFlags.Manual;

        public override string ToString()
        {
            var flags = ControlFlags;
            if (flags == CameraControlFlags.None) return string.Format("{0}: {1}", Property, Value);
            return string.Format("{0}: {1} ({2})", Property, Value, ControlFlags);
        }
    }
}
