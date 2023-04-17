# HILLES

### How to run the camera service

#### Environment 
1. Nvidia CUDA v11.2
2. Nvidia cudnn v8.1.8
3. .Net >5.0
4. OS: window 10

#### Required hardware
1. Kinect Azure camera
2. Nvidia Graphic card


#### Steps
1. turn on NTP server (run runNtp.bat as administrator) 
2. build the .Net project - k4aStreamer
3. run the k4aStreamer service : the service will wait for connection from game engine.
4. ensure the headset is connected in the same local network or turn on mobile hotspot with 5G band to let headset connect directly


### How to run the Game
#### standalone version
1. download the build/hilles-mmsys.apk then install on a Meta Quest 2.
2. ensure the camera service has turned on
3. run the installed app: plp-game 


#### rift mode
1. download the build/rift_mode.zip then unzip
2. ensure the camera service has turned on
3. run the plp-games.exe

