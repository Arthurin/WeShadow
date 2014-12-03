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
using KinectDepthSmoothing;

namespace WorkingWithDepthData
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool _usaFiltering = true;
        private FilteredSmoothing smoothingFilter = new FilteredSmoothing();

        private bool _usaAverage = true;
        private AveragedSmoothing smoothingAverage = new AveragedSmoothing();

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap colorBitmap;

        /// <summary>
        /// Intermediate storage for the depth data received from the camera
        /// </summary>
        private DepthImagePixel[] depthPixels;

        /// <summary>
        /// Intermediate storage for the depth data converted to color
        /// </summary>
        private byte[] colorPixels;

        //int birdCenterX;
        //int birdCenterY;
        int birdPositionX = 500;
        int birdPositionY = 95;

        public MainWindow()
        {
            InitializeComponent();
            Canvas.SetLeft(this.birdStatic, birdPositionX);
            Canvas.SetTop(this.birdStatic, birdPositionY);
            //birdCenterX = birdPositionX + ((int)this.birdStatic.Width / 2);
            //birdCenterY = birdPositionY + ((int)this.birdStatic.Height / 2);

            this.birdFly.Visibility = Visibility.Hidden;
           
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
                //image1.Source = BitmapSource.Create(depthFrame.Width, depthFrame.Height, 96, 96, PixelFormats.Bgr32, null, pixels, stride); 

            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame depthFrame = e.OpenDepthImageFrame())
            {
                if (depthFrame != null)
                {
                    // Copy the pixel data from the image to a temporary array
                    depthFrame.CopyDepthImagePixelDataTo(this.depthPixels);

                    if (this._usaFiltering)
                        this.depthPixels = this.smoothingFilter.CreateFilteredDepthArray(this.depthPixels, depthFrame.Width, depthFrame.Height);

                    
                    if (this._usaAverage)
                        this.depthPixels = this.smoothingAverage.CreateAverageDepthArray(this.depthPixels, depthFrame.Width, depthFrame.Height);

                    // Get the min and max reliable depth for the current frame
                    int minDepth = depthFrame.MinDepth;
                    int maxDepth = depthFrame.MaxDepth;

                    // Convert the depth to RGB
                    int colorPixelIndex = 0;
                    for (int i = 0; i < this.depthPixels.Length; ++i)
                    {
                        // Get the depth for this pixel
                        short depth = depthPixels[i].Depth;

                        // To convert to a byte, we're discarding the most-significant
                        // rather than least-significant bits.
                        // We're preserving detail, although the intensity will "wrap."
                        // Values outside the reliable depth range are mapped to 255 (white).

                        // Note: Using conditionals in this loop could degrade performance.
                        // Consider using a lookup table instead when writing production code.
                        // See the KinectDepthViewer class used by the KinectExplorer sample
                        // for a lookup table example.


                        if (depth > 0 && depth < 2500)
                        {
                            //we are a bit further away
                            this.colorPixels[colorPixelIndex++] = 0;
                            this.colorPixels[colorPixelIndex++] = 0;
                            this.colorPixels[colorPixelIndex++] = 0;

                            /*
                            if (this.birdStatic.Visibility == Visibility.Visible && birdIntersection(x, y, this.birdStatic))
                            {
                                this.birdFly.Visibility = Visibility.Visible;
                                this.birdStatic.Visibility = Visibility.Hidden;
                                System.Diagnostics.Debug.Write("Intersection with bird");
                            }*/
                        }
                        else
                        {
                            this.colorPixels[colorPixelIndex++] = 255;
                            this.colorPixels[colorPixelIndex++] = 255;
                            this.colorPixels[colorPixelIndex++] = 255;
                        }

                        /*
                        byte intensity = (byte)255;
                        //byte intensity = (byte)(depth >= minDepth && depth <= maxDepth ? depth : 255);

                        int newMax = depth - minDepth;
                        if (newMax > 0)
                            intensity = (byte)(255 - (255 * newMax / (3150)));

                        if (depthPixels[i].PlayerIndex == 0)
                            // Write out blue byte
                            this.colorPixels[colorPixelIndex++] = intensity;
                        else
                            this.colorPixels[colorPixelIndex++] = 255;

                        // Write out green byte
                        this.colorPixels[colorPixelIndex++] = intensity;

                        // Write out red byte                        
                        this.colorPixels[colorPixelIndex++] = intensity;*/

                        // We're outputting BGR, the last byte in the 32 bits is unused so skip it
                        // If we were outputting BGRA, we would write alpha here.
                        ++colorPixelIndex;
                    }

                    // Write the pixel data into our bitmap
                    this.colorBitmap.WritePixels(
                        new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                        this.colorPixels,
                        this.colorBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        private byte[] GenerateColoredBytes(DepthImageFrame depthFrame)
        {

            //get the raw data from kinect with the depth for every pixel
            short[] rawDepthData = new short[depthFrame.PixelDataLength];
            depthFrame.CopyPixelDataTo(rawDepthData);

            depthFrame.CopyDepthImagePixelDataTo(depthPixels);

           if (this._usaFiltering)
               depthPixels = this.smoothingFilter.CreateFilteredDepthArray(depthPixels, depthFrame.Width, depthFrame.Height);

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


                    if (this.birdStatic.Visibility == Visibility.Visible && birdIntersection(x, y, this.birdStatic))
                    {
                        this.birdFly.Visibility = Visibility.Visible;
                        this.birdStatic.Visibility = Visibility.Hidden;
                        System.Diagnostics.Debug.Write("Intersection with bird");
                    }
                }
                else
                {
                    //we are the farthest
                    pixels[colorIndex + BlueIndex] = 255;
                    pixels[colorIndex + GreenIndex] = 255;
                    pixels[colorIndex + RedIndex] = 255;
                }

                /*
                // color the touch area detection
                if (birdIntersection(x, y, this.birdStatic))
                {
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 255;
                }*/

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
            return (pointPositionX - 40 <= x && x <= pointPositionX + 40) && (pointPositionY - 40 <= y && y <= pointPositionY + 40);
        }

        private bool birdIntersection(int x, int y, Image img)
        {
            // how to get the position of the image ?
            return ((x >= birdPositionX && x <= birdPositionX + img.Width) && (y >= birdPositionY && y <= birdPositionY + img.Height));
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
            //sensor.SkeletonStream.Enable();

            //sign up for events if you want to get at API directly
            //sensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(newSensor_AllFramesReady);

            // Allocate space to put the depth pixels we'll receive
            this.depthPixels = new DepthImagePixel[sensor.DepthStream.FramePixelDataLength];

            // Allocate space to put the color pixels we'll create
            this.colorPixels = new byte[sensor.DepthStream.FramePixelDataLength * sizeof(int)];

            // This is the bitmap we'll display on-screen
            this.colorBitmap = new WriteableBitmap(sensor.DepthStream.FrameWidth, sensor.DepthStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

            // Set the image we display to point to the bitmap where we'll put the image data
            this.Image.Source = this.colorBitmap;

            // Turn on to get player masks
            sensor.SkeletonStream.Enable();

            // Add an event handler to be called whenever there is new depth frame data
            sensor.DepthFrameReady += this.SensorDepthFrameReady;

            sensor.Start();
        }

        private void kinectColorViewer1_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void image3_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

        private void kinectSensorChooser1_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void image1_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        } 



    }

}

