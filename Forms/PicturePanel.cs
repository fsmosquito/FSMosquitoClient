namespace FSMosquitoClient.Forms
{
    using System.Drawing;
    using System.Windows.Forms;

    internal class PicturePanel : Panel
    {
        public PicturePanel()
        {
            DoubleBuffered = true;
            AutoScroll = true;
            BackgroundImageLayout = ImageLayout.Center;
        }
        public override Image BackgroundImage
        {
            get
            {
                return base.BackgroundImage;
            }
            set
            {
                base.BackgroundImage = value;
                if (value != null) AutoScrollMinSize = value.Size;
            }
        }
    }
}
