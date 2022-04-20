using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PedigreeManagement.Static
{
    public class WPF_QRCode
    {
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        public static Bitmap QRCode_Creator(string str)
        {
            QRCodeGenerator QR = new QRCodeGenerator();
            QRCodeData cd = QR.CreateQrCode(str, QRCodeGenerator.ECCLevel.Q);
            QRCode qRCode = new QRCode(cd);
            Bitmap qrCodeImage = qRCode.GetGraphic(5);
            return qrCodeImage;
        }

        public static ImageSource ImageSourceFromBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(
                    handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(handle); }
        }
    }
}
