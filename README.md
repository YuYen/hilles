# HILLES

### How to run the camera service

#### Environment 
1. Nvidia CUDA v11.2
2. Nvidia cudnn v8.1.1
3. .Net 5.0
4. OS: window 10

#### Required hardware
1. Kinect Azure camera
2. Nvidia Graphic card


#### Steps
1. turn on NTP server (run runNtp.bat as administrator) 
2. download object detection model [yolov5s.onnx](https://github.com/ultralytics/yolov5/releases/download/v7.0/yolov5s.onnx) to k4aStreamer/Assets/Weights/
3. build the .Net project - k4aStreamer
4. connect the Kinect Azure camera to the laptop
5. run the k4aStreamer service : the service will wait for connection from game engine.
6. ensure the headset is connected in the same local network or turn on mobile hotspot with 5G band to let headset connect directly


### How to run the Game

pre-build game download link: https://utdallas.box.com/s/i2032f1no5xomgqx1uy48jmmoy6xzomc

#### distributed mode - HILLES
1. download the hilles-mmsys.apk then install on a Meta Quest 2.
2. ensure the camera service has turned on
3. run the installed app: plp-game 

#### rift mode
1. download the rift_mode.zip then unzip
2. ensure the camera service has turned on
3. run the plp-games.exe

