// (c) Copyright Microsoft Corporation.
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using System.Diagnostics;
using WpfAnimatedGif; 

namespace WorkingWithDepthData
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        DateTime now;
        int birdPositionX = 500;
        int birdPositionY = 50;

        SparkleManager sparkleManager;

        public MainWindow()
        {
            InitializeComponent();
            Canvas.SetLeft(this.birdStatic, 500);
            Canvas.SetBottom(this.birdStatic, 50);

            this.birdFly.Visibility = Visibility.Hidden;

            System.Diagnostics.Debug.WriteLine("coucou");

            sparkleManager = new SparkleManager(canvas);

            //ImageBehavior.SetAnimatedSource(birdImage, image);
        }
        const float MaxDepthDistance = 4095; // max value returned
        const float MinDepthDistance = 850; // min value returned
        const float MaxDepthDistanceOffset = MaxDepthDistance - MinDepthDistance;
        

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            kinectSensorChooser1.KinectSensorChanged += new DependencyPropertyChangedEventHandler(kinectSensorChooser1_KinectSensorChanged);

        }

        void kinectSensorChooser1_KinectSensorChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

            var oldSensor = (KinectSensor)e.OldValue;

            //stop the old sensor
            StopKinect(oldSensor);

            //get the new sensor
            var newSensor = (KinectSensor)e.NewValue;
            try
            {
                StartKinect(newSensor);
            }
            catch (System.IO.IOException)
            {
                //this happens if another app is using the Kinect
                kinectSensorChooser1.AppConflictOccurred();
            }
        }

        void newSensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            now = DateTime.Now;
            sparkleManager.Update(now);

            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame == null)
                {
                    return; 
                }

                byte[] pixels = GenerateColoredBytes(depthFrame);

                //number of bytes per row width * 4 (B,G,R,Empty)
                int stride = depthFrame.Width * 4;

                //create image
                image1.Source = 
                    BitmapSource.Create(depthFrame.Width, depthFrame.Height, 
                    96, 96, PixelFormats.Bgr32, null, pixels, stride);

                sparkleManager.Generate(now, depthFrame, pixels);
            }
        }

        private byte[] GenerateColoredBytes(DepthImageFrame depthFrame)
        {

            //get the raw data from kinect with the depth for every pixel
            short[] rawDepthData = new short[depthFrame.PixelDataLength];
            depthFrame.CopyPixelDataTo(rawDepthData); 

            //use depthFrame to create the image to display on-screen
            //depthFrame contains color information for all pixels in image
            //Height x Width x 4 (Red, Green, Blue, empty byte)
            Byte[] pixels = new byte[depthFrame.Height * depthFrame.Width * 4];

            //Bgr32  - Blue, Green, Red, empty byte
            //Bgra32 - Blue, Green, Red, transparency 
            //You must set transparency for Bgra as .NET defaults a byte to 0 = fully transparent

            //hardcoded locations to Blue, Green, Red (BGR) index positions       
            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2;
            int x = 0;
            int y = 0;

            
            //loop through all distances
            //pick a RGB color based on distance
            for (int depthIndex = 0, colorIndex = 0; 
                depthIndex < rawDepthData.Length && colorIndex < pixels.Length; 
                depthIndex++, colorIndex += 4)
            {
                x++;
                if (x == depthFrame.Width)
                {
                    y++;
                    x = 0;
                }
                //get the player (requires skeleton tracking enabled for values)
                int player = rawDepthData[depthIndex] & DepthImageFrame.PlayerIndexBitmask;

                //gets the depth value
                int depth = rawDepthData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                if (depth <= 0)
                {
                    //we are very close
                    pixels[colorIndex + BlueIndex] = 255;
                    pixels[colorIndex + GreenIndex] = 255;
                    pixels[colorIndex + RedIndex] = 255;

                }
                else if (depth > 0 && depth < 2500)
                {
                    //we are a bit further away
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 0;


                    if (this.birdStatic.Visibility == Visibility.Visible && isNearPoint(x, y, birdPositionX, birdPositionY))
                    {
                        this.birdFly.Visibility = Visibility.Visible;
                        this.birdStatic.Visibility = Visibility.Hidden;
                        System.Diagnostics.Debug.Write(". On change tout ");
                    }
                }
                else
                {
                    //we are the farthest
                    pixels[colorIndex + BlueIndex] = 255;
                    pixels[colorIndex + GreenIndex] = 255;
                    pixels[colorIndex + RedIndex] = 255;
                }


                //////equal coloring for monochromatic histogram
                //byte intensity = CalculateIntensityFromDepth(depth);
                //pixels[colorIndex + BlueIndex] = intensity;
                //pixels[colorIndex + GreenIndex] = intensity;
                //pixels[colorIndex + RedIndex] = intensity;


                ////Color all players "gold"
                //if (player > 0)
                //{
                //    pixels[colorIndex + BlueIndex] = Colors.Black.B;
                //    pixels[colorIndex + GreenIndex] = Colors.Black.G;
                //    pixels[colorIndex + RedIndex] = Colors.Black.R;
                //}

                /*
                if ((x >= 100 && x <= 200) && (y >= 100 && y <= 150))
                {
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 255;
                }*/


            }
          

            return pixels;
        }

        private bool isNearPoint(int x, int y, int pointPositionX, int pointPositionY)
        {
            //System.Diagnostics.Debug.WriteLine("Point detection: ");
            //System.Diagnostics.Debug.WriteLine(p);
            return (pointPositionX - 80 <= x && x <= pointPositionX + 80) && (pointPositionY - 80 <= y && y <= pointPositionY + 80);
        }


        public static byte CalculateIntensityFromDepth(int distance)
        {
            //formula for calculating monochrome intensity for histogram
            return (byte)(255 - (255 * Math.Max(distance - MinDepthDistance, 0) 
                / (MaxDepthDistanceOffset)));
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopKinect(kinectSensorChooser1.Kinect); 
        }

        private void StopKinect(KinectSensor sensor)
        {
            if (sensor != null)
            {
                if (sensor.IsRunning)
                {
                    //stop sensor 
                    sensor.Stop();

                    //stop audio if not null
                    if (sensor.AudioSource != null)
                    {
                        sensor.AudioSource.Stop();
                    }


                }
            }
        }

        private void StartKinect(KinectSensor sensor)
        {
            if (sensor == null)
                return;

            //turn on features that you need
            sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            sensor.SkeletonStream.Enable();

            //sign up for events if you want to get at API directly
            sensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(newSensor_AllFramesReady);

            sensor.Start();
        }

        bool creatingFakeSpakles = false;
        Point mousePosition;
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                creatingFakeSpakles = true;
                mousePosition = e.GetPosition(this);
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released)
            {
                creatingFakeSpakles = false;
                if (e.RightButton == MouseButtonState.Released)
                {
                    // create fake sparkle
                    sparkleManager.AddSparkleFromMouse(mousePosition, e.GetPosition(this), now);
                }
            }
        }

    }

    class MovingSparkle
    {
        public System.Windows.Controls.Image Image;
        public double MinSize, MaxSize, SparkleTime;
        public double StartX, StartY, SpeedX, SpeedY;
        bool IsSparkling, IsMoving;
        DateTime StartTime;

        public MovingSparkle()
        {
            Image = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri("/WorkingWithDepthData;component/Images/estrela1.png", UriKind.Relative)),
            };
        }

        /**
         * Como o
         * @param minSize Tamanho mínimo do sparkle
         * @param maxSize Tamanho máximo do sparkle
         * @param time Quanto demora um ciclo
         */
        public void SetSparkle(double minSize, double maxSize, double time)
        {
            this.MinSize = minSize;
            this.MaxSize = maxSize;
            this.SparkleTime = time;
            this.IsSparkling = (minSize != maxSize && time > 0.0);
        }

        public void SetMovement(double x, double y, double speedX, double speedY, DateTime startTime)
        {
            this.StartX = x;
            this.StartY = y;
            this.SpeedX = speedX;
            this.SpeedY = speedY;
            this.IsMoving = (Math.Abs(speedX) > 0.0 || Math.Abs(speedY) > 0.0);
            this.StartTime = startTime;
        }

        /**
         * Update the size and position of the sparkle.
         */
        public void Update(DateTime now)
        {
            TimeSpan time = (now - StartTime);

            if (IsSparkling)
            {
                double percent = 1.0 - Math.Abs((time.TotalSeconds / SparkleTime) % 1.0 - 0.5) * 2.0; // [0,1] (up, down, up, down, ...)
                double size = (MaxSize - MinSize) * percent + MinSize;
                Image.Width = size;
                Image.Height = size;
            }
            else if (Image.Width != Image.Height)
            {
                double size = (Image.Width + Image.Height) / 2.0;
                Image.Width = size;
                Image.Height = size;
            }

            if (IsMoving)
            {
                Canvas.SetLeft(Image, StartX + time.TotalSeconds * SpeedX - Image.Width / 2);
                Canvas.SetTop(Image, StartY + time.TotalSeconds * SpeedY - Image.Height / 2);
            }
            else
            {
                Canvas.SetLeft(Image, StartX - Image.Width / 2);
                Canvas.SetTop(Image, StartY - Image.Height / 2);
            }
        }
    }

    class SparkleManager
    {
        LinkedList<MovingSparkle> sparkles = new LinkedList<MovingSparkle>();
        Canvas canvas;
        byte[] lastPixels;
        DateTime lastNow;
        System.Windows.Controls.Image image;

        public SparkleManager(Canvas canvas)
        {
            Debug.Assert(canvas != null);
            this.canvas = canvas;
            image = new System.Windows.Controls.Image
            {
                Width = 640,
                Height = 480,
            };
            canvas.Children.Add(image);
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
        }

        /**
         * Add a fake sparkle from a mouse interaction.
         */
        public void AddSparkleFromMouse(Point start, Point end, DateTime startTime)
        {
            MovingSparkle sparkle = new MovingSparkle();
            sparkle.SetSparkle(15, 25, 1);
            sparkle.SetMovement(start.X, start.Y, (end.X - start.X) * 2.0, (end.Y - start.Y) * 2.0, startTime);
            canvas.Children.Add(sparkle.Image);
            sparkles.AddLast(sparkle);
            sparkle.Update(startTime);
        }

        /**
         * Generate new sparkles based on the pixel changes.
         */
        public void Generate(DateTime now, DepthImageFrame depthFrame, byte[] pixels)
        {
            Debug.Assert(now > lastNow);
            Debug.Assert(pixels != null);
            Debug.Assert(sparkles != null);
            Debug.Assert(depthFrame.Width * depthFrame.Height * 4 == pixels.Length);
            if (lastPixels == null)
            {
                // nothing to do
                lastPixels = pixels;
                lastNow = now;
                return;
            }
            if ((now - lastNow).TotalSeconds < 0.15)
            {
                // once per 150ms
                return;
            }
            /*
            // XXX test sparkle every 3 seconds
            if (now.Second % 3 == 0 && (now - lastNow).TotalSeconds > 1.0)
            {
                MovingSparkle sparkle = new MovingSparkle();
                sparkle.SetSparkle(15, 25, 1);
                sparkle.SetMovement(30, 30, 15, 15, now);
                canvas.Children.Add(sparkle.Image);
                sparkles.AddLast(sparkle);
                sparkle.Update(now);
                lastNow = now;
            }
            // TODO detect fast movement and generate sparkles based on speed and position
            // compute changes in areas 10,8,5,4,2
            const int CHANGE_SIZE = 5;
            int width = depthFrame.Width / CHANGE_SIZE;
            int height = depthFrame.Height / CHANGE_SIZE;
            byte[,] oldCount = new byte[width, height];
            byte[,] newCount = new byte[width, height];
            int x = 0, y = 0;
            for (int i = 0; i < pixels.Length; i+=4)
            {
                if (lastPixels[i] > 0)
                    oldCount[x / CHANGE_SIZE, y / CHANGE_SIZE]++;
                if (pixels[i] > 0)
                    newCount[x / CHANGE_SIZE, y / CHANGE_SIZE]++;
                // next
                x++;
                if (x == depthFrame.Width)
                {
                    x = 0;
                    y++;
                }
            }
            // generate test image Bgra32
            byte[] testPixels = new byte[width * height * 4];
            x = 0;
            y = 0;
            for (int i = 0; i < testPixels.Length; i += 4)
            {
                if (oldCount[x, y] < newCount[x, y])
                {
                    // 50% blue
                    testPixels[i + 0] = 255;
                    testPixels[i + 1] = 0;
                    testPixels[i + 2] = 0;
                    testPixels[i + 3] = 128;
                }
                else if (oldCount[x, y] > newCount[x, y])
                {
                    // 50% red
                    testPixels[i + 0] = 0;
                    testPixels[i + 1] = 0;
                    testPixels[i + 2] = 255;
                    testPixels[i + 3] = 128;
                }

                if (hasRadius(oldCount, newCount, x, y, 3))
                {
                    //Debug.WriteLine("{0} {1}", x, y);
                }

                // next
                x++;
                if (x == width)
                {
                    x = 0;
                    y++;
                }
            }
            int stride = width * 4;
            image.Source = BitmapSource.Create(width, height,
                    96, 96, PixelFormats.Bgra32, null, testPixels, stride);
            */
            lastPixels = pixels;
            lastNow = now;
        }

        private bool hasRadius(byte[,] oldCount, byte[,] newCount, int x, int y, int radius)
        {
            Debug.Assert(oldCount != null);
            Debug.Assert(newCount != null);
            Debug.Assert(oldCount.GetLength(0) == newCount.GetLength(0));
            Debug.Assert(oldCount.GetLength(1) == newCount.GetLength(1));
            int width = oldCount.GetLength(0);
            int height = oldCount.GetLength(1);
            if (radius < 0 ||
                x - radius < 0 || x + radius >= width ||
                y - radius < 0 || y + radius >= height)
                return false; // impossible
            bool hasOld = false;
            bool hasNew = false;
            for (int testX = x - radius; testX <= x + radius; testX++)
            {
                for (int testY = y - radius; testY <= y + radius; testY++)
                {
                    int diff = (int)oldCount[testX, testY] - newCount[testX, testY];
                    if (diff < 0)
                    {
                        hasOld = true;
                        if (hasNew)
                            return false; // mixed
                    }
                    else if (diff > 0)
                    {
                        hasNew = true;
                        if (hasOld)
                            return false; // mixed
                    }
                    else
                    {
                        return false; // never included
                    }
                }
            }
            // yup
            return true;
        }

        /**
         * Update the position and size of all sparkles.
         * Remove sparkles that leave the canvas.
         */
        public void Update(DateTime now)
        {
            LinkedListNode<MovingSparkle> node = sparkles.First;
            while (node != null)
            {
                // update sparkle
                MovingSparkle sparkle = node.Value;
                sparkle.Update(now);
                // remove sparkle if outside the canvas
                double left = Canvas.GetLeft(sparkle.Image);
                double top = Canvas.GetTop(sparkle.Image);
                if (left + sparkle.Image.Width < 0 || left > canvas.ActualWidth ||
                    top + sparkle.Image.Height < 0 || top > canvas.ActualHeight)
                {
                    LinkedListNode<MovingSparkle> next = node.Next;
                    canvas.Children.Remove(sparkle.Image);
                    sparkles.Remove(node);
                    node = next;
                    continue;
                }
                node = node.Next;
            }
        }

    }

}

