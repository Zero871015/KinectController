//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------
namespace Microsoft.Samples.Kinect.ColorBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Threading;
    using System.Runtime.InteropServices;

    

    

    static public class Mouse
    {
        [DllImport("User32.Dll")]
        public static extern long SetCursorPos(int x, int y);

        [DllImport("User32.Dll")]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern Int32 SendInput(Int32 cInputs, ref INPUT pInputs, Int32 cbSize);
        [DllImport("user32.dll")]
        public static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 28)]
        public struct INPUT
        {
            [FieldOffset(0)]
            public INPUTTYPE dwType;
            [FieldOffset(4)]
            public MOUSEINPUT mi;
            [FieldOffset(4)]
            public KEYBOARDINPUT ki;
            [FieldOffset(4)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct MOUSEINPUT
        {
            public Int32 dx;
            public Int32 dy;
            public Int32 mouseData;
            public MOUSEFLAG dwFlags;
            public Int32 time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct KEYBOARDINPUT
        {
            public Int16 wVk;
            public Int16 wScan;
            public KEYBOARDFLAG dwFlags;
            public Int32 time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct HARDWAREINPUT
        {
            public Int32 uMsg;
            public Int16 wParamL;
            public Int16 wParamH;
        }

        public enum INPUTTYPE : int
        {
            Mouse = 0,
            Keyboard = 1,
            Hardware = 2
        }

        [Flags()]
        public enum MOUSEFLAG : int
        {
            MOVE = 0x1,
            LEFTDOWN = 0x2,
            LEFTUP = 0x4,
            RIGHTDOWN = 0x8,
            RIGHTUP = 0x10,
            MIDDLEDOWN = 0x20,
            MIDDLEUP = 0x40,
            XDOWN = 0x80,
            XUP = 0x100,
            VIRTUALDESK = 0x400,
            WHEEL = 0x800,
            ABSOLUTE = 0x8000
        }

        [Flags()]
        public enum KEYBOARDFLAG : int
        {
            EXTENDEDKEY = 1,
            KEYUP = 2,
            UNICODE = 4,
            SCANCODE = 8
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Reader for color frames
        /// </summary>
        private ColorFrameReader colorFrameReader = null;

        /// <summary>
        /// Bitmap to display
        /// </summary>
        private WriteableBitmap colorBitmap = null;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;
        private BodyFrameReader bodyFrameReader = null;

        private CoordinateMapper coordinateMapper = null;
        private Body[] bodies = null;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {

            // get the kinectSensor object
            this.kinectSensor = KinectSensor.GetDefault();

            // open the reader for the color frames
            this.colorFrameReader = this.kinectSensor.ColorFrameSource.OpenReader();

            // wire handler for frame arrival
            this.colorFrameReader.FrameArrived += this.Reader_ColorFrameArrived;


            // create the colorFrameDescription from the ColorFrameSource using Bgra format
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            // create the bitmap to display
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width, colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;
            
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();
            this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            image1.Visibility = Visibility.Hidden;

        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.colorBitmap;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.colorFrameReader != null)
            {
                // ColorFrameReder is IDisposable
                this.colorFrameReader.Dispose();
                this.colorFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the user clicking on the screenshot button
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void ScreenshotButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.colorBitmap != null)
            {
                // create a png bitmap encoder which knows how to save a .png file
                BitmapEncoder encoder = new PngBitmapEncoder();

                // create frame from the writable bitmap and add to encoder
                encoder.Frames.Add(BitmapFrame.Create(this.colorBitmap));

                string time = System.DateTime.Now.ToString("hh'-'mm'-'ss", CultureInfo.CurrentUICulture.DateTimeFormat);

                string myPhotos = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

                string path = Path.Combine(myPhotos, "KinectScreenshot-Color-" + time + ".png");

                // write the new file to disk
                try
                {
                    // FileStream is IDisposable
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }

                    this.StatusText = string.Format(Properties.Resources.SavedScreenshotStatusTextFormat, path);
                }
                catch (IOException)
                {
                    this.StatusText = string.Format(Properties.Resources.FailedScreenshotStatusTextFormat, path);
                }
            }
        }

        /// <summary>
        /// Handles the color frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private int bytesPerPixel = 4;
        private int color = 0;
        private bool triggerClick = false;

        private void Reader_ColorFrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {

            // ColorFrame is IDisposable
            using (ColorFrame colorFrame = e.FrameReference.AcquireFrame())
            {
                if (colorFrame != null)
                {
                    byte[] buffer = new byte[this.colorBitmap.PixelWidth * this.colorBitmap.PixelHeight * 4];
                    colorFrame.CopyConvertedFrameDataToArray(buffer, ColorImageFormat.Bgra);

                    FrameDescription colorFrameDescription = colorFrame.FrameDescription;

                    using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                    {
                        this.colorBitmap.Lock();
                        
                        // verify data and write the new color frame data to the display bitmap
                        if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(
                              this.colorBitmap.BackBuffer,
                              (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                              ColorImageFormat.Bgra);
                            this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));



                            for (int i = 0; i < (colorFrameDescription.LengthInPixels * bytesPerPixel); i += 4)
                            {
                                switch (color)
                                {
                                    case 1:
                                        buffer[i] = 0;
                                        buffer[i + 1] = 0;
                                        break;
                                    case 2:
                                        buffer[i] = 0;
                                        buffer[i + 2] = 0;
                                        break;
                                    case 3:
                                        buffer[i + 1] = 0;
                                        buffer[i + 2] = 0;
                                        break;
                                    default: break;

                                }

                            }
                            this.colorBitmap.WritePixels(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight), buffer, colorFrameDescription.Width * 4, 0);

                        }

                        
                        this.colorBitmap.Unlock();
                    }
                }
            }
        }

        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;
            using (BodyFrame bodyframe = e.FrameReference.AcquireFrame())
            {
                if (bodyframe != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyframe.BodyCount];
                    }
                    bodyframe.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }
            if (dataReceived)
            {
                foreach (var it in bodies)
                {
                    if (it.IsTracked)
                    {
                        Pen drawPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 1.0);
                        IReadOnlyDictionary<JointType, Joint> joints = it.Joints;

                        // convert the joint points to depth (display) space
                        Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();
                        CameraSpacePoint position = joints[JointType.HandRight].Position;
                        ColorSpacePoint colorSpacePoint = this.coordinateMapper.MapCameraPointToColorSpace(position);
                        textBox.Text = "x : " + colorSpacePoint.X.ToString() + " y : " + colorSpacePoint.Y.ToString();
                        if (it.HandRightState == HandState.Open)
                        {
                            triggerClick = false;
                            image.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            if (triggerClick == false)
                                Click();
                            triggerClick = true;
                            image.Visibility = Visibility.Hidden;
                        }


                        if(!float.IsInfinity(colorSpacePoint.Y))
                        {
                            //Console.WriteLine(colorSpacePoint.X.ToString());
                            Canvas.SetLeft(image, colorSpacePoint.X * (mycanvas.ActualWidth / 1920));
                            Canvas.SetTop(image, colorSpacePoint.Y * (mycanvas.ActualHeight / 1080));
                            //Point p = windowbody.PointFromScreen(new Point(0, 0));
                            Mouse.SetCursorPos((int)(colorSpacePoint.X), (int)(colorSpacePoint.Y));
                        }
                        
                        
                        break;
                    }
                }
            }
        }
        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }
        
        private void RGBbtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender.Equals(RGBbtn))
                color = 0;
            else if (sender.Equals(Rbtn))
                color = 1;
            else if (sender.Equals(Gbtn))
                color = 2;
            else
                color = 3;
          //  MessageBox.Show(color.ToString());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            image1.Source = new BitmapImage(new Uri("C:/Users/a3265/Desktop/ControlsBasics-WPF/pic.png"));

            MyCanvas.Width = image1.Width;
            MyCanvas.Height = image1.Height;

            Image[] pics = new Image[9];

            //BitmapImage[] subImages = new BitmapImage[9];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int index = i * 3 + j;
                    pics[index] = new Image();

                    pics[index].Height = MyCanvas.Height / 3;
                    pics[index].Width = MyCanvas.Width / 3;
                    MyCanvas.Children.Add(pics[index]);
                    pics[index].Margin = new Thickness((MyCanvas.Width / 3) * j, (MyCanvas.Height / 3) * i, 0, 0);

                }
            }

            // Quick and dirty, get the BitmapSource from an existing <Image> element
            // in the XAML
            BitmapSource source = image1.Source as BitmapSource;

            // Calculate stride of source
            int stride = source.PixelWidth * (source.Format.BitsPerPixel / 8);

            // Create data array to hold source pixel data
            byte[] data = new byte[stride * source.PixelHeight];

            // Copy source image pixels to the data array
            source.CopyPixels(data, stride, 0);

            WriteableBitmap[] writeableBitmaps = new WriteableBitmap[9];


            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int index = i * 3 + j;

                    writeableBitmaps[index] = new WriteableBitmap(
              source.PixelWidth / 3,
              source.PixelHeight / 3,
              source.DpiX, source.DpiY,
              source.Format, null);

                    // Write the pixel data to the WriteableBitmap.
                    writeableBitmaps[index].WritePixels(
                      new Int32Rect((source.PixelWidth / 3) * j, (source.PixelHeight / 3) * i, source.PixelWidth / 3, source.PixelHeight / 3),
                      data, stride, 0, 0);

                    // Set the WriteableBitmap as the source for the <Image> element 
                    // in XAML so you can see the result of the copy
                    //image_Copy.Source = target;
                    pics[index].Source = writeableBitmaps[index];
                }
            }
        }

        static public void Click()
        {
            Console.WriteLine("123");
            Mouse.mouse_event(0x02, 0, 0, 0, 0);
            Mouse.mouse_event(0x04, 0, 0, 0, 0);
        }

        static public void LeftDown()
        {
            Mouse.INPUT leftdown = new Mouse.INPUT();

            leftdown.dwType = 0;
            leftdown.mi = new Mouse.MOUSEINPUT();
            leftdown.mi.dwExtraInfo = IntPtr.Zero;
            leftdown.mi.dx = 0;
            leftdown.mi.dy = 0;
            leftdown.mi.time = 0;
            leftdown.mi.mouseData = 0;
            leftdown.mi.dwFlags = Mouse.MOUSEFLAG.LEFTDOWN;

            Mouse.SendInput(1, ref leftdown, Marshal.SizeOf(typeof(Mouse.INPUT)));
        }

        static public void LeftUp()
        {
            Mouse.INPUT leftup = new Mouse.INPUT();

            leftup.dwType = 0;
            leftup.mi = new Mouse.MOUSEINPUT();
            leftup.mi.dwExtraInfo = IntPtr.Zero;
            leftup.mi.dx = 0;
            leftup.mi.dy = 0;
            leftup.mi.time = 0;
            leftup.mi.mouseData = 0;
            leftup.mi.dwFlags = Mouse.MOUSEFLAG.LEFTUP;

            Mouse.SendInput(1, ref leftup, Marshal.SizeOf(typeof(Mouse.INPUT)));
        }

        private void Windowbody_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            
        }
    }
}
