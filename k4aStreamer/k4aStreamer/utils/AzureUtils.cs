using System;
using System.Drawing;
using Microsoft.Azure.Kinect.Sensor;

namespace k4aStreamer.utils
{
    public class AzureUtils
    {
        /// <param name="resolution"></param>
        /// <returns>width, height</returns>
        public static (int, int) GetWidthHeightByResolution(ColorResolution resolution)
        {
            switch (resolution)
            {
                case ColorResolution.R720p:
                    return (1280, 720);
                case ColorResolution.R1080p:
                    return (1920, 1080);
                case ColorResolution.R1440p:
                    return (2560, 1440);
                case ColorResolution.R1536p:
                    return (2048, 1536);
                case ColorResolution.R2160p:
                    return (3840, 2160);
                case ColorResolution.R3072p:
                    return (4096, 3072);
                default:
                    return (0, 0);
            }
        }
    }
}