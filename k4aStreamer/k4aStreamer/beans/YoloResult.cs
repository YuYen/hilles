using System.Collections.Generic;
using Yolov5Net.Scorer;

namespace k4aStreamer.beans
{
    public class YoloResult
    {
        public double totalMilliseconds { get; }
        public List<YoloPrediction> predictions { get; }

        public YoloResult(List<YoloPrediction> predictions, double totalMilliseconds)
        {
            this.totalMilliseconds = totalMilliseconds;
            this.predictions = predictions;
        }
    }
}