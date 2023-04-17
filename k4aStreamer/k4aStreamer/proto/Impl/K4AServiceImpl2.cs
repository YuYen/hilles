using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Core.Logging;
using k4aStreamer.beans;
using k4aStreamer.utils;
using Microsoft.Azure.Kinect.BodyTracking;
using Microsoft.Azure.Kinect.Sensor;
using Yolov5Net.Scorer;
using Yolov5Net.Scorer.Models;
using Calibration = Microsoft.Azure.Kinect.Sensor.Calibration;
using Capture = Microsoft.Azure.Kinect.Sensor.Capture;
using ColorResolution = Microsoft.Azure.Kinect.Sensor.ColorResolution;
using DepthMode = Microsoft.Azure.Kinect.Sensor.DepthMode;
using Device = Microsoft.Azure.Kinect.Sensor.Device;
using DeviceConfiguration = Microsoft.Azure.Kinect.Sensor.DeviceConfiguration;
using Image = Microsoft.Azure.Kinect.Sensor.Image;
using ImageFormat = Microsoft.Azure.Kinect.Sensor.ImageFormat;

namespace k4aStreamer.proto.Impl
{
    // stable version

    // full tracker in same thread
    public class K4AServiceImpl2 : K4aService.K4aServiceBase
    {
        private DateTime _initTime;
        private double _initTimeEpochMillsecond;
        private double _deviceTimeOffsetMillsecond;
        
        private Device _device;
        private Calibration _calibration;

        private int _isCaptureServiceOn = 0;
        private CancellationTokenSource _captureServiceCancellationTokenSource;
        
        private object _registrationLock = new object();
        private int _imgUpdateFreq = 50;

        private readonly ConcurrentQueue<BlockingCollection<Capture>> registeredQueue =
            new ConcurrentQueue<BlockingCollection<Capture>>();

        private int _registeredCount = 0;

        // preset config
        private static readonly DeviceConfiguration Configuration = new DeviceConfiguration
        {
            ColorResolution = ColorResolution.R720p,
            ColorFormat = ImageFormat.ColorBGRA32,
            // ColorFormat = ImageFormat.ColorMjpg, // fail as streaming
            // DepthMode = DepthMode.NarrowViewUnbinned,
            DepthMode = DepthMode.NFOV_Unbinned,
            CameraFPS = FPS.FPS30,
            SynchronizedImagesOnly = true
        };

        private static byte[] _image_buffer = new byte[16 * 1024 * 1024];

        private void StartCameraService()
        {
            if (Interlocked.Exchange(ref _isCaptureServiceOn, 1) != 0)
            {
                Program.logger.Debug("capture service already started");
                return;
            }

            Initialize();

            _captureServiceCancellationTokenSource = new CancellationTokenSource();
            _device.StartCameras(Configuration);
            Program.logger.Debug("capture service start");
            Task.Run(() => CameraServiceTask(_captureServiceCancellationTokenSource.Token));
        }

        private void StopCameraService()
        {
            if (Interlocked.Exchange(ref _isCaptureServiceOn, 0) != 1)
            {
                Program.logger.Debug("capture service has stopped");
                return;
            }

            _captureServiceCancellationTokenSource.Cancel();
            _device.StopCameras();
            Program.logger.Debug("capture service stop");
        }

        private void CameraServiceTask(CancellationToken token)
        {
            int count = 0;
            int updateFreq = 30;
            double tmpOffset = 0;
            
            while (!token.IsCancellationRequested)
            {
                var capture = _device.GetCapture();
                var elapse = DateTime.UtcNow - _initTime;
                
                // update device offset
                if (count % updateFreq == 0)
                {
                    if (count == 0)
                    {
                        Interlocked.Exchange(ref _deviceTimeOffsetMillsecond, elapse.TotalMilliseconds - capture.Depth.DeviceTimestamp.TotalMilliseconds);
                    }
                    else
                    {
                        Interlocked.Exchange(ref _deviceTimeOffsetMillsecond, tmpOffset / updateFreq);
                    }
                    tmpOffset = 0;
                }
                tmpOffset += elapse.TotalMilliseconds - capture.Depth.DeviceTimestamp.TotalMilliseconds;
                count++;

                using (capture)
                {
                    foreach (var queue in registeredQueue)
                    {
                        if (!queue.IsAddingCompleted && queue.Count < queue.BoundedCapacity)
                        {
                            queue.Add(capture.Reference(), token);
                        }
                    }
                }
            }
        }

        private void RegisterCaptureQueue(BlockingCollection<Capture> queue)
        {
            Program.logger.Debug("registering ...");
            registeredQueue.Enqueue(queue);
            if (Interlocked.Increment(ref _registeredCount) == 1)
            {
                StartCameraService();
            }
        }

        private void UnregisterCaptureQueue(BlockingCollection<Capture> queue)
        {
            var success = false;
            Program.logger.Debug("unregistering ...");
            lock (_registrationLock)
            {
                var len = registeredQueue.Count;
                for (var i = 0; i < len; i++)
                {
                    if (registeredQueue.TryDequeue(out var ele))
                    {
                        if (!ele.Equals(queue))
                        {
                            registeredQueue.Enqueue(ele);
                        }
                        else
                        {
                            ele.CompleteAdding();
                            foreach (var capture in ele.ToArray())
                            {
                                using (capture)
                                {
                                }
                            }

                            ele.Dispose();
                            success = true;
                            break;
                        }
                    }
                    else
                    {
                        Program.logger.Error("Unexpected issue as unregistering");
                    }
                }
            }

            if (success && Interlocked.Decrement(ref _registeredCount) == 0)
            {
                StopCameraService();
            }
        }

        /// <summary>
        /// Initialize the device
        /// </summary>
        /// <exception cref="K4AdotNet.Sensor.DeviceNotFoundException"></exception>
        private void Initialize()
        {
            if (_device != null)
                return;
            
            _device = Device.Open();
            _initTime = DateTime.UtcNow;
            _initTimeEpochMillsecond = (_initTime - CommUtils.EpochUtc).TotalMilliseconds;
            _calibration = _device.GetCalibration(Configuration.DepthMode, Configuration.ColorResolution);
        }

        private ByteString Image2ByteString(Image image)
        {
            if (image != null)
            {
                //// copy to ByteString through pointer
                unsafe
                {
                    using (var pin = image.Memory.Pin())
                    {
                        using( var readStream = new UnmanagedMemoryStream((byte*) ((IntPtr) pin.Pointer).ToPointer(),
                            image.Size, image.Size, FileAccess.Read))
                        {
                            return ByteString.FromStream(readStream);
                        }
                        
                    }
                }
                // return ByteString.CopyFrom( image.Memory.ToArray());
            }

            return ByteString.Empty;
        }

        private static double count;

        private void PrintFPS(Object source, ElapsedEventArgs e)
        {
            Program.logger.Debug("FPS: " + (1000 * count / Program.regularReporter.Interval).ToString("00.000"));
            count = 0;
        }

        #region video stream
        /// <summary>
        /// get camera capture
        /// </summary>
        /// <param name="request"></param>
        /// <param name="responseStream"></param>
        /// <param name="context"></param>
        public override async Task GetCapture(EmptyRequest request, IServerStreamWriter<CaptureResponse> responseStream,
            ServerCallContext context)
        {
            var queue = new BlockingCollection<Capture>(1);
            var cur = 0;
            try
            {
                RegisterCaptureQueue(queue);
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var capture = queue.Take(context.CancellationToken);
                    var res = new CaptureResponse();
                    using (capture)
                    {
                        res.ColorImage = Image2ByteString(capture.Color);
                        res.DepthImage = Image2ByteString(capture.Depth);
                    }
                    await responseStream.WriteAsync(res);
                    cur ++;
                }
            }
            catch (Exception e)
            {
                Program.logger.Debug(e.ToString());
            }
            finally
            {
                Program.logger.Debug("Video stream is off");
                UnregisterCaptureQueue(queue);
            }
        }
        #endregion
        
        #region Yolo
        private int detectionFreq = 5;

        private void YoloDetect(CancellationToken token, BlockingCollection<Capture> input_que,
            BlockingCollection<YoloResult> output_que)
        {
            (int width, int height) shape = AzureUtils.GetWidthHeightByResolution(ColorResolution.R720p);
            Rectangle lockRec = new Rectangle(0, 0, shape.width, shape.height);
            int yoloCount = -1;
            // var queue = new BlockingCollection<Capture>(1);
            // RegisterCaptureQueue(queue);
            using (var bitmap = new Bitmap(shape.width, shape.height, PixelFormat.Format32bppArgb))
            {
                using (var scorer = new YoloScorer<YoloCocoP5Model>("Assets/Weights/yolov5s.onnx"))
                {
                    while (!token.IsCancellationRequested)
                    {
                        var capture = input_que.Take(token);
                        yoloCount++;
                        var totalMilliseconds = 0.0;

                        using (capture)
                        {
                            if (yoloCount % detectionFreq != 0)
                                continue;

                            try
                            {
                                var colorImage = capture.Color;
                                // copy image buffer
                                unsafe
                                {
                                    using (var pin = colorImage.Memory.Pin())
                                    {
                                        var data = bitmap.LockBits(lockRec, ImageLockMode.WriteOnly,
                                            PixelFormat.Format32bppArgb);
                                        Buffer.MemoryCopy(pin.Pointer, data.Scan0.ToPointer(),
                                            colorImage.Memory.Length, colorImage.Memory.Length);
                                        bitmap.UnlockBits(data);
                                    }
                                }

                                totalMilliseconds = Math.Max(colorImage.DeviceTimestamp.TotalMilliseconds,
                                    capture.Depth.DeviceTimestamp.TotalMilliseconds);
                            }
                            catch (Exception e)
                            {
                                Program.logger.Debug(e.Message);
                            }
                        }

                        var predictions = scorer.Predict(bitmap);
                        output_que.Add(new YoloResult(predictions, totalMilliseconds), token);
                    }


                    //////// debug draw
                    // using var graphics = Graphics.FromImage(bitmap);
                    // foreach (var prediction in predictions) // iterate predictions to draw results
                    // {
                    //     Program.logger.Debug(prediction.Label.Name);
                    //
                    //     double score = Math.Round(prediction.Score, 2);
                    //         
                    //     graphics.DrawRectangles(new Pen(prediction.Label.Color, 1),
                    //         new[] { prediction.Rectangle });
                    //         
                    //     var (x, y) = (prediction.Rectangle.X - 3, prediction.Rectangle.Y - 23);
                    //     graphics.DrawString($"{prediction.Label.Name} ({score})",
                    //         new Font("Arial", 16, GraphicsUnit.Pixel), new SolidBrush(prediction.Label.Color),
                    //         new PointF(x, y));
                    // }
                    // bitmap.Save(count + ".jpg");
                    // count++;
                }
            }
        }

        public override async Task GetYoloResponse(YoloRequest request,
            IServerStreamWriter<YoloResponse> responseStream, ServerCallContext context)
        {
            var targetIds = new HashSet<int>();
            #region init target id filter
            if (request.CocoIds.Count == 0) // add all
            {
                foreach (var entry in CommUtils.CocoNames80)
                {
                    targetIds.Add(entry.Key);
                }
            }
            else
            {
                foreach (var id in request.CocoIds)
                {
                    targetIds.Add(id);
                }
            }
            #endregion
            
            var src_que = new BlockingCollection<Capture>(1);
            var res_que = new BlockingCollection<YoloResult>(5);
            var tmp = Task.Run(() => YoloDetect(context.CancellationToken, src_que, res_que));
            
            try
            {
                RegisterCaptureQueue(src_que);

                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var yoloResult = res_que.Take(context.CancellationToken);
                    var res = new YoloResponse();
                    res.CapturedEpochTimeMilliSeconds = _initTimeEpochMillsecond + 
                                                        yoloResult.totalMilliseconds +
                                                        _deviceTimeOffsetMillsecond;
                    
                    foreach (var prediction in yoloResult.predictions)
                    {
                        if (!targetIds.Contains(prediction.Label.Id))
                            continue;
                        
                        var obj = new CocoObject();
                        obj.Id = prediction.Label.Id;
                        obj.Score = prediction.Score;
                        obj.X = prediction.Rectangle.X;
                        obj.Y = prediction.Rectangle.Y;
                        obj.Width = prediction.Rectangle.Width;
                        obj.Height = prediction.Rectangle.Height;
                        res.Predictions.Add(obj);
                    }
                    await responseStream.WriteAsync(res);                        
                }

            }
            catch (Exception e)
            {
                Program.logger.Debug(e.ToString());
            }
            finally
            {
                Program.logger.Debug("Detection stream is off");
                UnregisterCaptureQueue(src_que);
            }
        }

        #endregion

        #region body


        
        private void FullProssCapture(CancellationToken token, BlockingCollection<Capture> queue,
            BlockingCollection<Frame> readyBodyFrames)
        {
            var config = TrackerConfiguration.Default;
            config.ProcessingMode = TrackerProcessingMode.Cuda;
            config.ModelPath = "dnn_model_2_0_op11.onnx";
            var tracker = Tracker.Create(_calibration, config);
            
            //// blocking version
            while (!token.IsCancellationRequested)
            {
                var capture = queue.Take(token);
                using (capture)
                {
                    //// single thread ~19-20 FPS
                    tracker.EnqueueCapture(capture.Reference());
                    var frame = tracker.PopResult();
                    
                    using (frame)
                    {
                        var bak = frame.Reference();
                        try
                        {
                            readyBodyFrames.Add(bak, token);
                        }
                        catch (OperationCanceledException)
                        {
                            bak.Dispose();
                        }
                    }
                }
            }
        }
        
        public override async Task GetBodyResponse(EmptyRequest request,
            IServerStreamWriter<BodyResponse> responseStream, ServerCallContext context)
        {

            var queue = new BlockingCollection<Capture>(1);
            var readyBodyFrames = new BlockingCollection<Frame>(5);
            var tmp = Task.Run(() => FullProssCapture(context.CancellationToken, queue, readyBodyFrames));
            
            Program.regularReporter.Elapsed += PrintFPS;
            try
            {
                RegisterCaptureQueue(queue);
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var frame = readyBodyFrames.Take(context.CancellationToken);
                    using (frame)
                    {
                        var res = new BodyResponse();
                        res.CapturedEpochTimeMilliSeconds = _initTimeEpochMillsecond + 
                                                            frame.DeviceTimestamp.TotalMilliseconds +
                                                            _deviceTimeOffsetMillsecond; 
                        
                        for (uint i = 0; i < frame.NumberOfBodies; i++)
                        {
                            var body = new Body();
                            body.Id = (int) frame.GetBodyId(i);
                            
                            //// unsafe version but seems not faster 
                            unsafe
                            {
                                var data = CommUtils.GetBytes(frame.GetBodySkeleton(i));
                                fixed(byte* ptr = data)
                                {
                                    var readStream = new UnmanagedMemoryStream(ptr,
                                        data.Length, data.Length, FileAccess.Read);
                                    body.Skeleton = ByteString.FromStream(readStream);
                                }
                            }
                            
                            //// safe version
                            // body.Skeleton = ByteString.CopyFrom( CommUtils.GetBytes(frame.GetBodySkeleton(i)));
                            
                            res.Bodys.Add(body);
                        }

                        await responseStream.WriteAsync(res);
                    }
                    count += 1;
                }
            }
            catch (Exception e)
            {
                Program.logger.Debug(e.ToString());
            }
            finally
            {
                Program.logger.Debug("Body stream is off");
                UnregisterCaptureQueue(queue);
                Program.regularReporter.Elapsed -= PrintFPS;
            }
        }
        
        #endregion
        
    

        #region other grpc
        
        public override Task<ImuSampleResponse> GetImuSample(EmptyRequest request, ServerCallContext context)
        {
            Program.logger.Info("Get ImuSample called");
            Initialize();
            
            _device.StartImu();
            var imuSample = _device.GetImuSample();
            _device.StopImu();

            var imuSampleResponse = new ImuSampleResponse();
            imuSampleResponse.ImuSample = ByteString.CopyFrom(CommUtils.GetBytes(imuSample));
            
            return Task.FromResult(imuSampleResponse);
        }

        public override Task<DeviceConfigResponse> GetDeviceConfig(EmptyRequest request, ServerCallContext context)
        {
            Program.logger.Debug("get device config");
            Initialize();
           
            var deviceConfigResponse = new DeviceConfigResponse();
            deviceConfigResponse.Calibration = ByteString.CopyFrom(CommUtils.GetBytes(_calibration));
            
            // temporary made for compatibility with K4AdotNet version in Unity 
            var config = new  K4AdotNet.Sensor.DeviceConfiguration
            {
                ColorResolution = K4AdotNet.Sensor.ColorResolution.R720p,
                ColorFormat = K4AdotNet.Sensor.ImageFormat.ColorBgra32,
                DepthMode = K4AdotNet.Sensor.DepthMode.NarrowViewUnbinned,
                CameraFps = K4AdotNet.Sensor.FrameRate.Thirty,
                SynchronizedImagesOnly = true
            };
            
            deviceConfigResponse.DeviceConfiguration = ByteString.CopyFrom(CommUtils.GetBytes(config));
            return Task.FromResult(deviceConfigResponse);
        }
        
        #endregion
    }
}