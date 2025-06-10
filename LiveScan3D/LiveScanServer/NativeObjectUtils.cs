using System.Runtime.InteropServices;
using System;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeMarkerPose
{
    public int markerId;
    public fixed float R[9];
    public fixed float t[3];
}

[StructLayout(LayoutKind.Sequential)]
public struct NativeKinectSettings
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] minBounds;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] maxBounds;

    [MarshalAs(UnmanagedType.I1)]
    public bool filter;
    public int filterNeighbors;
    public float filterThreshold;

    public IntPtr markerPoses;
    public int numMarkers;

    [MarshalAs(UnmanagedType.I1)]
    public bool streamOnlyBodies;
    public int compressionLevel;
    [MarshalAs(UnmanagedType.I1)]
    public bool autoExposureEnabled;
    public int exposureStep;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeAffineTransform
{
    public fixed float R[9]; // 3x3 matrix
    public fixed float t[3]; // translation vector
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Point3s
{
    public short X;
    public short Y;
    public short Z;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct RGB
{
    public byte rgbBlue;
    public byte rgbGreen;
    public byte rgbRed;
    public byte rgbReserved;
}

