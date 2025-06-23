#pragma once

#define WIN32_LEAN_AND_MEAN
#define _WINSOCK_DEPRECATED_NO_WARNINGS
#define _WINSOCKAPI_

#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>

#include "libobsensor/ObSensor.hpp"
#include "ICapture.h"
#include <opencv2/opencv.hpp>
#include "utils.h"
#include <functional>
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>
#include <thread>
#include <atomic>
#include <queue>
#include <mutex>
#include <condition_variable>

class AzureKinectCapture : public ICapture
{
public:
    AzureKinectCapture(int deviceIndex = 0);
    ~AzureKinectCapture();

    bool TryOpenDevice();
    bool Initialize(SYNC_STATE state, int syncOffset);
    bool AcquireFrame();
    void UpdatePointCloud(std::shared_ptr<ob::FrameSet> frameset);
    bool Close();
    uint64_t GetTimeStamp();
    int GetDeviceIndex();
    void SetExposureState(bool enableAutoExposure, int exposureStep);
    void SetLogger(std::function<void(const std::string&)> loggerFunc);

protected:
    std::function<void(const std::string&)> m_logger;

private:
    int m_deviceIndex = 0;
    std::shared_ptr<ob::Device> m_obDevice;
    std::shared_ptr<ob::Pipeline> m_pipeline;
    std::shared_ptr<ob::PointCloudFilter> pointCloudFilter;
    int32_t captureTimeoutMs = 1000;

    cv::Mat cImg;
    cv::Mat cImgResized;
    cv::Mat maskedImg;

    uint64_t currentTimeStamp = 0;
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

    std::chrono::milliseconds lastFrameTime;
    void SendFrameViaTCP(const cv::Mat& frame);
    void SendFrameWorker();
};

