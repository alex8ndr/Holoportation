#pragma once
#include <calibration.h>

struct KinectSettings
{
    float minBounds[3];
    float maxBounds[3];

    bool filter;
    int filterNeighbors;
    float filterThreshold;

    MarkerPose* markerPoses;
    int numMarkers;

    bool streamOnlyBodies;
    int compressionLevel;
    bool autoExposureEnabled;
    int exposureStep;
};

struct AffineTransform
{
    float R[3][3];
    float t[3];
};