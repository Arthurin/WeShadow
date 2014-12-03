using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Kinect;

namespace KinectDepthSmoothing
{
    public class FilteredSmoothing
    {
        // Will specify how many non-zero pixels within a 1 pixel band
        // around the origin there should be before a filter is applied
        private int _innerBandThreshold;
        public int InnerBandThreshold { get { return _innerBandThreshold; } set { if (value > 0 && value <= MaxInnerBandThreshold) _innerBandThreshold = value; } }
        // Will specify how many non-zero pixels within a 2 pixel band
        // around the origin there should be before a filter is applied
        private int _outerBandThreshold;
        public int OuterBandThreshold { get { return _outerBandThreshold; } set { if (value > 0 && value <= MaxOuterBandThreshold) _outerBandThreshold = value; } }

        public static readonly int MaxInnerBandThreshold = 8;
        public static readonly int MaxOuterBandThreshold = 16;

        public FilteredSmoothing()
        {
            this._innerBandThreshold = 2;
            this._outerBandThreshold = 5;
        }

        public FilteredSmoothing(int InnerBandThreshold, int OuterbandThreshold)
            : this()
        {
            this.InnerBandThreshold = InnerBandThreshold;
            this.OuterBandThreshold = OuterBandThreshold;
        }

        public DepthImagePixel[] CreateFilteredDepthArray(DepthImagePixel[] depthArray, int width, int height)
        {
            /////////////////////////////////////////////////////////////////////////////////////
            // I will try to comment this as well as I can in here, but you should probably refer
            // to my Code Project article for a more in depth description of the method.
            /////////////////////////////////////////////////////////////////////////////////////

            DepthImagePixel[] smoothDepthArray = new DepthImagePixel[depthArray.Length];

            // We will be using these numbers for constraints on indexes
            int widthBound = width - 1;
            int heightBound = height - 1;

            // We process each row in parallel
            Parallel.For(0, height, depthArrayRowIndex =>
            {
                // Process each pixel in the row
                for (int depthArrayColumnIndex = 0; depthArrayColumnIndex < width; depthArrayColumnIndex++)
                {
                    var depthIndex = depthArrayColumnIndex + (depthArrayRowIndex * width);

                    // We are only concerned with eliminating 'white' noise from the data.
                    // We consider any pixel with a depth of 0 as a possible candidate for filtering.
                    if (!depthArray[depthIndex].IsKnownDepth || depthArray[depthIndex].Depth == 0)
                    {
                        // From the depth index, we can determine the X and Y coordinates that the index
                        // will appear in the image.  We use this to help us define our filter matrix.
                        int x = depthIndex % width;
                        int y = (depthIndex - x) / width;

                        // The filter collection is used to count the frequency of each
                        // depth value in the filter array.  This is used later to determine
                        // the statistical mode for possible assignment to the candidate.
                        short[,] filterCollection = new short[24, 2];

                        // The player index collection is used to count the frequency of each
                        // player index value in the filter array.  This is used later to demtermine
                        // the statistical mode for possible assignment to the candidate.
                        short[] playerIndexCollection = new short[7];

                        // The inner and outer band counts are used later to compare against the threshold 
                        // values set in the UI to identify a positive filter result.
                        int innerBandCount = 0;
                        int outerBandCount = 0;

                        // The following loops will loop through a 5 X 5 matrix of pixels surrounding the 
                        // candidate pixel.  This defines 2 distinct 'bands' around the candidate pixel.
                        // If any of the pixels in this matrix are non-0, we will accumulate them and count
                        // how many non-0 pixels are in each band.  If the number of non-0 pixels breaks the
                        // threshold in either band, then the average of all non-0 pixels in the matrix is applied
                        // to the candidate pixel.
                        for (int yi = -2; yi < 3; yi++)
                        {
                            for (int xi = -2; xi < 3; xi++)
                            {
                                // yi and xi are modifiers that will be subtracted from and added to the
                                // candidate pixel's x and y coordinates that we calculated earlier.  From the
                                // resulting coordinates, we can calculate the index to be addressed for processing.

                                // We do not want to consider the candidate pixel (xi = 0, yi = 0) in our process at this point.
                                // We already know that it's 0
                                if (xi != 0 || yi != 0)
                                {
                                    // We then create our modified coordinates for each pass
                                    var xSearch = x + xi;
                                    var ySearch = y + yi;

                                    // While the modified coordinates may in fact calculate out to an actual index, it 
                                    // might not be the one we want.  Be sure to check to make sure that the modified coordinates
                                    // match up with our image bounds.
                                    if (xSearch >= 0 && xSearch <= widthBound && ySearch >= 0 && ySearch <= heightBound)
                                    {
                                        var index = xSearch + (ySearch * width);
                                        // We only want to look for non-0 values
                                        if (depthArray[index].IsKnownDepth || depthArray[depthIndex].Depth != 0)
                                        {
                                            // We want to find count the frequency of each depth
                                            for (int i = 0; i < 24; i++)
                                            {
                                                if (filterCollection[i, 0] == depthArray[index].Depth)
                                                {
                                                    // When the depth is already in the filter collection
                                                    // we will just increment the frequency.
                                                    filterCollection[i, 1]++;
                                                    break;
                                                }
                                                else if (filterCollection[i, 0] == 0)
                                                {
                                                    // When we encounter a 0 depth in the filter collection
                                                    // this means we have reached the end of values already counted.
                                                    // We will then add the new depth and start it's frequency at 1.
                                                    filterCollection[i, 0] = depthArray[index].Depth;
                                                    filterCollection[i, 1]++;
                                                    break;
                                                }
                                            }

                                            // We want to find the frequency of each player index
                                            playerIndexCollection[(int)depthArray[index].PlayerIndex]++;

                                            // We will then determine which band the non-0 pixel
                                            // was found in, and increment the band counters.
                                            if (yi != 2 && yi != -2 && xi != 2 && xi != -2)
                                                innerBandCount++;
                                            else
                                                outerBandCount++;
                                        }
                                    }
                                }
                            }
                        }

                        // Once we have determined our inner and outer band non-zero counts, and accumulated all of those values,
                        // we can compare it against the threshold to determine if our candidate pixel will be changed to the
                        // statistical mode of the non-zero surrounding pixels.
                        if (innerBandCount >= _innerBandThreshold || outerBandCount >= _outerBandThreshold)
                        {
                            short frequencyDepth = 0;
                            short depth = 0;
                            // This loop will determine the statistical mode
                            // of the surrounding pixels for assignment to
                            // the candidate.
                            for (int i = 0; i < 24; i++)
                            {
                                // This means we have reached the end of our
                                // frequency distribution and can break out of the
                                // loop to save time.
                                if (filterCollection[i, 0] == 0)
                                    break;
                                if (filterCollection[i, 1] > frequencyDepth)
                                {
                                    depth = filterCollection[i, 0];
                                    frequencyDepth = filterCollection[i, 1];
                                }
                            }

                            smoothDepthArray[depthIndex].Depth = depth;

                            short frequencyPlayer = 0;
                            short index = 0;
                            // This loop will determine the statistical mode
                            // of the surrounding pixels for assignment to
                            // the candidate.
                            for (int i = 0; i < 6; i++)
                            {
                                if (playerIndexCollection[i] > frequencyPlayer)
                                {
                                    index = (short)i;
                                    frequencyPlayer = playerIndexCollection[i];
                                }
                            }

                            smoothDepthArray[depthIndex].PlayerIndex = index;
                        }

                    }
                    else
                    {
                        // If the pixel is not zero, we will keep the original depth.
                        smoothDepthArray[depthIndex] = depthArray[depthIndex];
                    }
                }
            });

            return smoothDepthArray;
        }
    }
}