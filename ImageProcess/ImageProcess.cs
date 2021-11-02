using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TurboJpegWrapper;
using static PackViewer.RAWLib;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;

namespace PackViewer
{
    public static class ImageProcess
    {
        static IntPtr librawHandler;
        static TJDecompressor _decompressor;

        [Obsolete]
        public static void GetRaw(string file, System.Windows.Controls.Image dImage)
        {
            // open RAW file and get basic info
            var r = libraw_open_file(librawHandler, file);
            if (r != LibRaw_errors.LIBRAW_SUCCESS)
            {
                //Console.WriteLine("Open file:       " + Marshal.PtrToStringAnsi(libraw_strerror(r)));
                libraw_close(librawHandler);
                return;
            }

            //Console.WriteLine("\nRAW width:       " + libraw_get_raw_width(librawHandler));
            //Console.WriteLine("RAW height:      " + libraw_get_raw_height(librawHandler));
            //Console.WriteLine("IMG width:       " + libraw_get_iwidth(librawHandler));
            //Console.WriteLine("IMG height:      " + libraw_get_iheight(librawHandler));

            // get decoder information
            var decp = Marshal.AllocHGlobal(Marshal.SizeOf<libraw_decoder_info_t>());
            r = libraw_get_decoder_info(librawHandler, decp);
            var decoder = Marshal.PtrToStructure<libraw_decoder_info_t>(decp);
            //Console.WriteLine("\nDecoder function: " + decoder.decoder_name);
            //Console.WriteLine("Decoder flags:    " + decoder.decoder_flags);
            Marshal.FreeHGlobal(decp);

            /*
            // get image parameters
            Console.WriteLine("\nImage parameters:");
            var piparam = libraw_get_iparams(librawHandler);
            var iparam = Marshal.PtrToStructure<libraw_iparams_t>(piparam);
            Console.WriteLine("Guard:        {0}", iparam.guard);
            Console.WriteLine("Make:         {0}", iparam.make);
            Console.WriteLine("Model:        {0}", iparam.model);
            Console.WriteLine("Software:     {0}", iparam.software);
            Console.WriteLine("Norm. Make:   {0}", iparam.normalized_make);
            Console.WriteLine("Norm. Model:  {0}", iparam.normalized_model);
            Console.WriteLine("Vendor:       {0}", iparam.maker_index);
            Console.WriteLine("Num. of RAWs: {0}", iparam.raw_count);
            Console.WriteLine("DNG version:  {0}", iparam.dng_version);
            Console.WriteLine("Sigma Foveon: {0}", iparam.is_foveon);
            Console.WriteLine("Colors:       {0}", iparam.colors);
            Console.WriteLine("Filterbits:   {0:X8}", iparam.filters);
            Console.WriteLine("Color desc:   {0}", iparam.cdesc);
            Console.WriteLine("XMP data len: {0:X8}", iparam.xmplen);*/

            /*
            // other image parameters
            Console.WriteLine("\nOther image parameters:");
            var poparam = libraw_get_imgother(librawHandler);
            var oparam = Marshal.PtrToStructure<libraw_imgother_t>(poparam);
            Console.WriteLine("ISO:          {0}", oparam.iso_speed);
            Console.WriteLine("Shutter:      {0}s", oparam.shutter);
            Console.WriteLine("Aperture:     f/{0}", oparam.aperture);
            Console.WriteLine("Focal length: {0}mm", oparam.focal_len);
            // C-style time_t equals to seconds elapsed since 1970-1-1
            var ts = new DateTime(1970, 1, 1).AddSeconds(oparam.timestamp).ToLocalTime();
            Console.WriteLine("Timestamp:    {0}", ts.ToString("yyyy-MMM-dd HH:mm:ss"));
            Console.WriteLine("Img serial no {0}", oparam.shot_order);
            Console.WriteLine("Description:  {0}", oparam.desc);
            Console.WriteLine("Artist:       {0}", oparam.artist);
            Console.WriteLine("Analog balance: {0}", oparam.analogbalance[0]);

            // get lens info
            Console.WriteLine("\nLens information:");
            var plensparam = libraw_get_lensinfo(librawHandler);
            var lensparam = PtrToStructure<libraw_lensinfo_t>(plensparam);
            Console.WriteLine("Minimum focal length:                     {0}mm", lensparam.MinFocal);
            Console.WriteLine("Maximum focal length:                     {0}mm", lensparam.MaxFocal);
            Console.WriteLine("Maximum aperture at minimum focal length: {0}mm", lensparam.MaxAp4MinFocal);
            Console.WriteLine("Maximum aperture at maximum focal length: {0}mm", lensparam.MaxAp4MaxFocal);
            Console.WriteLine("EXIF tag 0x9205:                          {0}", lensparam.EXIF_MaxAp);
            Console.WriteLine("Lens make:                                {0}", lensparam.LensMake);
            Console.WriteLine("Lens:                                     {0}", lensparam.Lens);
            Console.WriteLine("Lens serial:                              {0}", lensparam.LensSerial);
            Console.WriteLine("Internal lens serial:                     {0}", lensparam.InternalLensSerial);
            Console.WriteLine("EXIF tag 0xA405:                          {0}", lensparam.FocalLengthIn35mmFormat);
            Console.WriteLine("Makernotes lens:                          {0}\n", lensparam.makernotes.Lens);
            */
            // unpack data from raw file
            r = libraw_unpack(librawHandler);
            if (r != LibRaw_errors.LIBRAW_SUCCESS)
            {
                //Console.WriteLine("Unpack: " + Marshal.PtrToStringAnsi(libraw_strerror(r)));
                libraw_close(librawHandler);
                return;
            }

            // process data using previously defined settings
            r = libraw_dcraw_process(librawHandler);
            if (r != LibRaw_errors.LIBRAW_SUCCESS)
            {
                //Console.WriteLine("Process: " + Marshal.PtrToStringAnsi(libraw_strerror(r)));
                libraw_close(librawHandler);
                return;
            }

            // extract raw data into allocated memory buffer
            var errc = 0;
            var ptr = libraw_dcraw_make_mem_image(librawHandler, ref errc);
            if (errc != 0)
            {
                //Console.WriteLine("Mem_img: " + Marshal.PtrToStringAnsi(strerror(errc)));
                libraw_close(librawHandler);
                return;
            }

            // convert pointer to structure to get image info and raw data
            var img = Marshal.PtrToStructure<libraw_processed_image_t>(ptr);
            
            Console.WriteLine("\nImage type:   " + img.type);
            Console.WriteLine("Image height: " + img.height);
            Console.WriteLine("Image width:  " + img.width);
            Console.WriteLine("Image colors: " + img.colors);
            Console.WriteLine("Image bits:   " + img.bits);
            Console.WriteLine("Data size:    " + img.data_size);
            Console.WriteLine("Checksum:     " + img.height * img.width * img.colors * (img.bits / 8));

            // rqeuired step before accessing the "data" array
            Array.Resize(ref img.data, (int)img.data_size);
            var adr = ptr + Marshal.OffsetOf(typeof(libraw_processed_image_t), "data").ToInt32();
            Marshal.Copy(adr, img.data, 0, (int)img.data_size);

            // calculate padding for lines and add padding
            var num = img.width % 4;
            var padding = new byte[num];
            var stride = img.width * img.colors * (img.bits / 8);
            var line = new byte[stride];
            var tmp = new List<byte>();
            for (var i = 0; i < img.height; i++)
            {
                Buffer.BlockCopy(img.data, stride * i, line, 0, stride);
                tmp.AddRange(line);
                tmp.AddRange(padding);
            }

            // release memory allocated by [libraw_dcraw_make_mem_image]
            libraw_dcraw_clear_mem(ptr);

            // create/save bitmap from mem image/array
            using (var bmp = new Bitmap(img.width, img.height, PixelFormat.Format24bppRgb))
            {
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
                Marshal.Copy(tmp.ToArray(), 0, bmpData.Scan0, (int)img.data_size);
                bmp.UnlockBits(bmpData);
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                dImage.Source = src;
            }
        }

        internal static void DecompressJpeg(byte[] bitmapStream, System.Windows.Controls.Image dImage)
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
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                dImage.Source = src;
            }
        }

        internal static void Close()
        {
            libraw_close(librawHandler);
        }

        public static void DecompressRaw(byte[] arr, System.Windows.Controls.Image dImage)
        {
            var size = arr.Length;
            // open RAW file and get basic info
            var r = libraw_open_buffer(librawHandler, arr, size);
            if (r != LibRaw_errors.LIBRAW_SUCCESS)
            {
                libraw_close(librawHandler);
                return;
            }

            // unpack data from raw file
            r = libraw_unpack(librawHandler);
            if (r != LibRaw_errors.LIBRAW_SUCCESS)
            {
                libraw_close(librawHandler);
                return;
            }

            // process data using previously defined settings
            r = libraw_dcraw_process(librawHandler);
            if (r != LibRaw_errors.LIBRAW_SUCCESS)
            {
                libraw_close(librawHandler);
                return;
            }

            // extract raw data into allocated memory buffer
            var errc = 0;
            var ptr = libraw_dcraw_make_mem_image(librawHandler, ref errc);
            if (errc != 0)
            {
                libraw_close(librawHandler);
                return;
            }

            // convert pointer to structure to get image info and raw data
            var img = Marshal.PtrToStructure<libraw_processed_image_t>(ptr);

            // rqeuired step before accessing the "data" array
            Array.Resize(ref img.data, (int)img.data_size);
            var adr = ptr + Marshal.OffsetOf(typeof(libraw_processed_image_t), "data").ToInt32();
            Marshal.Copy(adr, img.data, 0, (int)img.data_size);

            // calculate padding for lines and add padding
            var num = img.width % 4;
            var padding = new byte[num];
            var stride = img.width * img.colors * (img.bits / 8);
            var line = new byte[stride];
            var tmp = new List<byte>();
            for (var i = 0; i < img.height; i++)
            {
                Buffer.BlockCopy(img.data, stride * i, line, 0, stride);
                tmp.AddRange(line);
                tmp.AddRange(padding);
            }

            // release memory allocated by [libraw_dcraw_make_mem_image]
            libraw_dcraw_clear_mem(ptr);
            var swapRedBlue = true;
            // create/save bitmap from mem image/array
            using (var bmp = new Bitmap(img.width, img.height, PixelFormat.Format24bppRgb))
            {
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
                Marshal.Copy(tmp.ToArray(), 0, bmpData.Scan0, (int)img.data_size);
                if (swapRedBlue)
                {
                    int length = Math.Abs(bmpData.Stride) * bmp.Height;
                    unsafe
                    {
                        byte* rgbValues = (byte*)bmpData.Scan0.ToPointer();

                        for (int i = 0; i < length; i += 3)
                        {
                            byte dummy = rgbValues[i];
                            rgbValues[i] = rgbValues[i + 2];
                            rgbValues[i + 2] = dummy;
                        }
                    }
                }
                bmp.UnlockBits(bmpData);
                var src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(bmp.GetHbitmap(), IntPtr.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                dImage.Source = src;
            }
        }

        public static void Init(bool enableLibRaw)
        {
            if (enableLibRaw)
                librawHandler = libraw_init(LibRaw_init_flags.LIBRAW_OPTIONS_NONE);

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
