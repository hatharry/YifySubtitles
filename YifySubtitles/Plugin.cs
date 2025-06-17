using System;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using System.IO;

namespace YifySubtitles
{

    public class Plugin : BasePlugin, IHasThumbImage
    {
        public override string Name => "Yify Subtitles";

        public override string Description => "Download subtitles from Yify Subtitles";

        public override Guid Id => new Guid("84CAD6FA-8662-4676-9BE9-D5E140707D44");

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.png");
        }
    }
}
