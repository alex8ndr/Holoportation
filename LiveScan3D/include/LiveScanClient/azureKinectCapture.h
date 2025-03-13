#pragma once

#include "stdafx.h"
#include "ICapture.h"
#include <k4a/k4a.h>
#include <opencv2/opencv.hpp>
#include "utils.h"
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <thread>
#include <atomic>
#include <queue>
#include <mutex>
#include <condition_variable>

class AzureKinectCapture : public ICapture
{
public:
    AzureKinectCapture();
    ~AzureKinectCapture();

    bool Initialize(SYNC_STATE state, int syncOffset);
    bool AcquireFrame();
    bool Close();
    void MapDepthFrameToCameraSpace(Point3f* pCameraSpacePoints);
    void MapColorFrameToCameraSpace(Point3f* pCameraSpacePoints);
    void MapDepthFrameToColorSpace(UINT16* pDepthInColorSpace);
    void MapColorFrameToDepthSpace(RGB* pColorInDepthSpace);
    int GetSyncJackState();
    uint64_t GetTimeStamp();
    int GetDeviceIndex();
    void SetExposureState(bool enableAutoExposure, int exposureStep);

private:
    k4a_device_t kinectSensor = NULL;
    int32_t captureTimeoutMs = 1000;
    k4a_image_t colorImage = NULL;
    k4a_image_t depthImage = NULL;
    k4a_image_t pointCloudImage = NULL;
    k4a_image_t transformedDepthImage = NULL;
    k4a_image_t colorImageInDepth = NULL;
    k4a_image_t depthImageInColor = NULL;
    k4a_image_t colorImageDownscaled = NULL;
    k4a_transformation_t transformationColorDownscaled = NULL;
    k4a_transformation_t transformation = NULL;

    int colorImageDownscaledWidth;
    int colorImageDownscaledHeight;

    bool syncInConnected = false;
    bool syncOutConnected = false;
    uint64_t currentTimeStamp = 0;
    SYNC_STATE syncState = Standalone;
    int deviceIDForRestart = -1;
    int restartAttempts = 0;
    bool autoExposureEnabled = true;
    int exposureTimeStep = 0;

    SOCKET clientSocket = INVALID_SOCKET;
    struct sockaddr_in serverAddr;

    std::thread sendingThread;
    std::atomic<bool> stopSending;
    std::queue<cv::Mat> frameQueue;
    std::mutex queueMutex;
    std::condition_variable queueCondition;

    std::chrono::milliseconds lastFrameTime; // Add this line

    void UpdateDepthPointCloud();
    void UpdateDepthPointCloudForColorFrame();
    void SendFrameViaTCP(const cv::Mat& frame);
    void SendFrameWorker();
};

