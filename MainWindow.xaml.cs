using Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TurboJpegWrapper;
using static PackViewer.RAWLib;

namespace PackViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ViewModel vm;
        PackView PackView;
        List<string> filesInFolder;
        int currentIndex;
        TJDecompressor _decompressor;
        IntPtr librawHandler;
        bool enableLibRaw = false;
        string _file;
        public MainWindow(string file)
        {
            InitializeComponent();
                        
            vm = new ViewModel();
            _file = file;
            enableLibRaw = PackView.IsRaw(file);
            DataContext = vm;
        }

        private void ShowImage()
        {
            try
            {
                if (filesInFolder == null || currentIndex >= filesInFolder.Count)
                    return;

                var file = filesInFolder[currentIndex];
                vm.Status = $"[{PackView.CurrentFolderIndex + 1}/{PackView.FoldersCount}] {file}";
                var numRetries = 15;
                byte[] BitmapStream = null;
                while (numRetries > 0)
                {
                    try
                    {
                        BitmapStream = PackView.GetImage(file);
                        break;
                    }
                    catch
                    {
                        numRetries--;
                        Thread.Sleep(200);
                    }
                }
                if (BitmapStream == null)
                    throw new Exception($"Cannot acces file {file}");

                if (PackView.IsRaw(file) && enableLibRaw)
                {
                    GetRawFromBuffer(BitmapStream, BitmapStream.Length);
                }
                else
                {
                    var result = _decompressor.Decompress(BitmapStream, ConvertPixelFormat(System.Drawing.Imaging.PixelFormat.Format32bppArgb), TJFlags.NONE);
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
                        DImage.Source = src;
                    }
                }
                BitmapStream = null;
            }
            catch (Exception e) {
                MessageBox.Show(e.Message);
            }
        }

        public void GetRaw(string file)
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
            /*
            Console.WriteLine("\nImage type:   " + img.type);
            Console.WriteLine("Image height: " + img.height);
            Console.WriteLine("Image width:  " + img.width);
            Console.WriteLine("Image colors: " + img.colors);
            Console.WriteLine("Image bits:   " + img.bits);
            Console.WriteLine("Data size:    " + img.data_size);
            Console.WriteLine("Checksum:     " + img.height * img.width * img.colors * (img.bits / 8));*/

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
                DImage.Source = src;
            }
        }

        public void GetRawFromBuffer(byte[] arr, long size)
        {
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
                DImage.Source = src;
            }
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            if (e.Key== Key.Escape)
                Close();
            if (e.Key == Key.Left)
                PrevImage();
            if (e.Key == Key.Right)
                NextImage();
            if (e.Key == Key.Up)
                FolderUp();
            if (e.Key == Key.Down)
                FolderDown();
            if (e.Key == Key.End)
                LastImage();
            if (e.Key == Key.Home)
                FirstImage();
            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && e.Key == Key.Delete)
                ThrashIt();
            if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) && e.Key == Key.Insert)
                SaveIt();
            e.Handled = true;
        }

        private void FirstImage()
        {
            currentIndex = 0;
            ShowImage();
        }

        private void LastImage()
        {
            currentIndex = filesInFolder.Count - 1;
            ShowImage();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var source = DImage.Source;
            DImage.Source = null;
            var delete = false;
            var deleteOriginal = false;
            var cancel = false;
            var save = false;

            var cntDel = PackView.FoldersTrashed.Count;
            if (cntDel != 0)
            {
                var confirm = MessageBox.Show($"There {cntDel} folder marked for deletion. Do you really want them to delete?", "Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Cancel)
                    cancel = true;

                if (confirm == MessageBoxResult.Yes)
                    delete=true;
            }
            var cntSave = PackView.FoldersSaved.Count;
            if (cntSave != 0)
            {
                var confirm = MessageBox.Show($"There {cntSave} folders marked for save. Do you want them to copy to _Saved folder?", "Confirmation", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Cancel)
                    cancel=true;

                if (confirm == MessageBoxResult.Yes)
                {
                    save = true;
                    confirm = MessageBox.Show($"Do you want to remove original folders?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    deleteOriginal = confirm == MessageBoxResult.Yes;
                }
            }

            if (cancel)
            {
                e.Cancel = true;
                DImage.Source = source;
                return;
            }

            this.WindowState = WindowState.Minimized;

            PackView.Finalize(delete, save, deleteOriginal);
            libraw_close(librawHandler);
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ThrashButton_Click(object sender, RoutedEventArgs e)
        {
            ThrashIt();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveIt();
        }

        private void SaveIt()
        {
            PackView.SaveIt();
            vm.IsSaved = PackView.FolderIsSaved;
        }

        private void ThrashIt()
        {
            PackView.ThrashIt();
            vm.IsInThrash = PackView.FolderInThrash;
        }

        private void FastForwardButton_Click(object sender, RoutedEventArgs e)
        {
            currentIndex = Math.Min(currentIndex + 10, filesInFolder.Count - 1);
            ShowImage();
        }

        private void ToTheEndButton_Click(object sender, RoutedEventArgs e)
        {
            currentIndex = filesInFolder.Count - 1;
            ShowImage();
        }
        private void FolderUpButton_Click(object sender, RoutedEventArgs e)
        {
            FolderUp();
        }
        private void FolderUp()
        {
            PackView.FolderUp();
            GetFolderImages();
        }
        private void FolderDownButton_Click(object sender, RoutedEventArgs e)
        {
            FolderDown();
        }

        private void FolderDown()
        {
            PackView.FolderDown();
            GetFolderImages();
        }

        private void GetFolders()
        {
            Task foldersAsync = Task.Factory.StartNew(() =>
            {
                try
                {
                    PackView = new PackView(_file, vm);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            });

            foldersAsync.ContinueWith(FoldersReceived);
        }

        private void FoldersReceived(Task obj)
        {
            Dispatcher.Invoke(() =>
            {
                if (!PackView.CanStartView)
                    Environment.Exit(0);

                GetFolderImages();
            });
        }

        private void GetFolderImages()
        {
            DImage.Source = null;
            Task imagesAsync = Task.Factory.StartNew(() =>
            {
                try
                {
                    filesInFolder = PackView.GetCurrentFolderImages;
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            });

            imagesAsync.ContinueWith(FolderImagesReceived);
        }

        private void FolderImagesReceived(Task obj)
        {
            Dispatcher.Invoke(() =>
            {
                currentIndex = 0;
                vm.IsInThrash = PackView.FolderInThrash;
                vm.IsSaved = PackView.FolderIsSaved;
                ShowImage();
            });
        }

        private void PrevImageButton_Click(object sender, RoutedEventArgs e)
        {
            PrevImage();
        }

        private void PrevImage()
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                ShowImage();
            }
        }

        private void NextImageButton_Click(object sender, RoutedEventArgs e)
        {
            NextImage();
        }

        private void NextImage()
        {
            if (currentIndex < filesInFolder.Count - 1)
            {
                currentIndex++;
                ShowImage();
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
                PrevImage();

            else if (e.Delta < 0)
                NextImage();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            try
            {
                if (enableLibRaw)
                    librawHandler = libraw_init(LibRaw_init_flags.LIBRAW_OPTIONS_NONE);

                TJInitializer.Initialize(logger: Console.WriteLine);
                _decompressor = new TJDecompressor();

                GetFolders();
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
                Environment.Exit(0);
            }
        }
    }

    public class ViewModel : ViewModelBase
    {
        bool _inThrash;
        bool _isSaved;
        private string status;
        private string status2;

        public bool IsInThrash
        {
            get { return _inThrash; }
            set
            {
                if (_inThrash == value) return;

                _inThrash = value;
                OnPropertyChanged(nameof(IsInThrash));
            }
        }

        public bool IsSaved
        {
            get { return _isSaved; }
            set
            {
                if (_isSaved == value) return;

                _isSaved = value;
                OnPropertyChanged(nameof(IsSaved));
            }
        }

        public string Status
        {
            get => status;
            set
            {
                if (status == value) return;

                status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public string Status2
        {
            get => status2;
            set
            {
                if (status2 == value) return;

                status2 = value;
                OnPropertyChanged(nameof(Status2));
            }
        }

        public ViewModel()
        {
            uiSynchronizationContext = SynchronizationContext.Current;
            ControlsEnabled = true;
            IsInThrash = false;
        }
    }
}
