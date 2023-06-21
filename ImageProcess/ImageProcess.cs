using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TurboJpegWrapper;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using Sdcb.LibRaw;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows;
using static System.Formats.Asn1.AsnWriter;
using System.Windows.Media.Media3D;

namespace PackViewer
{
    public static class ImageProcess
    {
        static IntPtr librawHandler;
        static TJDecompressor _decompressor;

        public static void DecompressRaw(byte[] bitmapStream, System.Windows.Controls.Image dImage)
        {
            // open RAW file and get basic info
            using (RawContext r = RawContext.FromBuffer(bitmapStream))
            {
                r.Unpack();
                r.DcrawProcess();
                using (ProcessedImage image = r.MakeDcrawMemoryImage())
                {
                    //using (var bmp = ProcessedImageToBitmapSource(image))
                    //{
                        //BitmapSizeOptions szOpt = BitmapSizeOptions.FromEmptyOptions();
                        //var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, szOpt);
                        //dImage.Source = src;
                        dImage.Source = ProcessedImageToBitmapSource(image);
                    //}

                }
            }
        }

        private static BitmapSource ProcessedImageToBitmapSource(ProcessedImage rgbImage)
        {
            unsafe
            {
                fixed (void* data = rgbImage.GetData<byte>())
                {
                    //SwapRedAndBlue(rgbImage.GetData<byte>(), rgbImage.Width, rgbImage.Height);
                    var dpiX = 96d;
                    var dpiY = 96d;
                    var pixelFormat = System.Windows.Media.PixelFormats.Rgb24; 

                    var src = BitmapSource.Create(rgbImage.Width, rgbImage.Height, dpiX, dpiY,
                        pixelFormat, null, (IntPtr)data, rgbImage.Width*rgbImage.Height*3, rgbImage.Width * 3);

                    return src;
                }
            }
        }

        private static Bitmap ProcessedImageToBitmap(ProcessedImage rgbImage)
        {
            unsafe
            {
                fixed (void* data = rgbImage.GetData<byte>())
                {
                    SwapRedAndBlue(rgbImage.GetData<byte>(), rgbImage.Width, rgbImage.Height);
                    using Bitmap bmp = new Bitmap(rgbImage.Width, rgbImage.Height, rgbImage.Width * 3,
                        System.Drawing.Imaging.PixelFormat.Format24bppRgb, (IntPtr)data);
                    return new Bitmap(bmp);
                }
            }
        }

        private static void SwapRedAndBlue(Span<byte> rgbData, int width, int height)
        {
            int totalPixels = width * height;
            for (int i = 0; i < totalPixels; i++)
            {
                int pixelIndex = i * 3;
                byte red = rgbData[pixelIndex];
                byte blue = rgbData[pixelIndex + 2];

                rgbData[pixelIndex] = blue;
                rgbData[pixelIndex + 2] = red;
            }
        }

        internal static void DecompressJpeg(byte[] bitmapStream, System.Windows.Controls.Image dImage, Rotation rot)
        {
            var result = _decompressor.Decompress(bitmapStream, ImageProcess.ConvertPixelFormat(System.Drawing.Imaging.PixelFormat.Format32bppArgb), TJFlags.NONE);
            using (var bmp = new Bitmap(result.Width, result.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                BitmapData bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0,
                                                                bmp.Width,
                                                                bmp.Height),
                                                  ImageLockMode.WriteOnly,
                                                  bmp.PixelFormat);

                IntPtr pNative = bmpData.Scan0;
                Marshal.Copy(result.Data, 0, pNative, result.Data.Length);
                bmp.UnlockBits(bmpData);
                BitmapSizeOptions szOpt = rot == Rotation.Rotate0 ? BitmapSizeOptions.FromEmptyOptions() : BitmapSizeOptions.FromRotation(rot);
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, szOpt);
                dImage.Source = src;
            }
        }

        internal static void Close()
        {
        }


        public static void Init()
        {
            TJInitializer.Initialize(logger: Console.WriteLine);
            _decompressor = new TJDecompressor();
        }

        public static TJPixelFormats ConvertPixelFormat(System.Drawing.Imaging.PixelFormat pixelFormat)
        {
            switch (pixelFormat)
            {
                case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
                case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
                    return TJPixelFormats.TJPF_BGRA;
                case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                    return TJPixelFormats.TJPF_BGR;
                case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                    return TJPixelFormats.TJPF_GRAY;
                default:
                    throw new NotSupportedException($"Provided pixel format \"{pixelFormat}\" is not supported");
            }
        }
    }
}
