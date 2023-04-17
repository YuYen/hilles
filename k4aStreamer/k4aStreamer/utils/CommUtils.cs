using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace k4aStreamer.utils
{
    public class CommUtils
    {
        #region coco names 80
        public static readonly Dictionary<int, string> CocoNames80 = new Dictionary<int, string>()
        {
            {1, "person"}, {2, "bicycle"}, {3, "car"}, {4, "motorcycle"}, {5, "airplane"},
            {6, "bus"}, {7, "train"}, {8, "truck"}, {9, "boat"}, {10, "traffic light"},
            {11, "fire hydrant"}, {12, "stop sign"}, {13, "parking meter"}, {14, "bench"}, {15, "bird"},
            {16, "cat"}, {17, "dog"}, {18, "horse"}, {19, "sheep"}, {20, "cow"},
            {21, "elephant"}, {22, "bear"}, {23, "zebra"}, {24, "giraffe"}, {25, "backpack"},
            {26, "umbrella"}, {27, "handbag"}, {28, "tie"}, {29, "suitcase"}, {30, "frisbee"},
            {31, "skis"}, {32, "snowboard"}, {33, "sports ball"}, {34, "kite"}, {35, "baseball bat"},
            {36, "baseball glove"}, {37, "skateboard"}, {38, "surfboard"}, {39, "tennis racket"}, {40, "bottle"},
            {41, "wine glass"}, {42, "cup"}, {43, "fork"}, {44, "knife"}, {45, "spoon"},
            {46, "bowl"}, {47, "banana"}, {48, "apple"}, {49, "sandwich"}, {50, "orange"},
            {51, "broccoli"}, {52, "carrot"}, {53, "hot dog"}, {54, "pizza"}, {55, "donut"},
            {56, "cake"}, {57, "chair"}, {58, "couch"}, {59, "potted plant"}, {60, "bed"},
            {61, "dining table"}, {62, "toilet"}, {63, "tv"}, {64, "laptop"}, {65, "mouse"},
            {66, "remote"}, {67, "keyboard"}, {68, "cell phone"}, {69, "microwave"}, {70, "oven"},
            {71, "toaster"}, {72, "sink"}, {73, "refrigerator"}, {74, "book"}, {75, "clock"},
            {76, "vase"}, {77, "scissors"}, {78, "teddy bear"}, {79, "hair drier"}, {80, "toothbrush"}
        };
        #endregion
        
        
        public static readonly DateTime EpochUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public const int HILLES_SERV_CHECK_PORT = 38763;
        public static readonly byte[] HILLES_CONF_TOKEN = { 72, 73, 76, 76, 69, 83}; // ASCII: HILLES
        public static readonly byte[] HILLES_QUERY_TOKEN = { 72, 73, 76, 76, 69, 83, 63}; // ASCII: HILLES?
        
        public static IPAddress GetDefaultGateway()
        {
            var gateway_address = NetworkInterface.GetAllNetworkInterfaces()
                .Where(e => e.OperationalStatus == OperationalStatus.Up)
                .SelectMany(e => e.GetIPProperties().GatewayAddresses)
                .FirstOrDefault();
            
            if (gateway_address == null) return null;
            return gateway_address.Address;
        }
        
        public static byte[] GetBytes<T>(T str)
        {
            int size = Marshal.SizeOf(str);

            byte[] arr = new byte[size];

            GCHandle h = default(GCHandle);

            try
            {
                h = GCHandle.Alloc(arr, GCHandleType.Pinned);

                Marshal.StructureToPtr<T>(str, h.AddrOfPinnedObject(), false);
            }
            finally
            {
                if (h.IsAllocated)
                {
                    h.Free();
                }
            }

            return arr;
        }

        public static T FromBytes<T>(byte[] arr) where T : struct
        {
            T str = default(T);

            GCHandle h = default(GCHandle);

            try
            {
                h = GCHandle.Alloc(arr, GCHandleType.Pinned);

                str = Marshal.PtrToStructure<T>(h.AddrOfPinnedObject());

            }
            finally
            {
                if (h.IsAllocated)
                {
                    h.Free();
                }
            }

            return str;
        }

    }
}