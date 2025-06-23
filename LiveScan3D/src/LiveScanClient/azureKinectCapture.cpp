#include "azureKinectCapture.h"
#include <opencv2/core.hpp>
#include <opencv2/imgproc.hpp>
#include <opencv2/opencv.hpp>
#include <chrono>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <iostream>
#include <vector>
#include <thread>
#include <mutex>

#define SERVER_HOST "127.0.0.1"
#define SERVER_PORT_SEND 48003
#define FRAME_SEND_INTERVAL_MS 500

AzureKinectCapture::AzureKinectCapture(int deviceIndex) : m_deviceIndex(deviceIndex), stopSending(false)
{
    // Initialize Winsock
    WSADATA wsaData;
    int iResult = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (iResult != 0) {
        std::cerr << "WSAStartup failed: " << iResult << std::endl;
    }

    // Initialize the socket
    clientSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (clientSocket == INVALID_SOCKET) {
        std::cerr << "Error at socket(): " << WSAGetLastError() << std::endl;
        return;
    }

    // Setup the server address
    serverAddr.sin_family = AF_INET;
    serverAddr.sin_port = htons(SERVER_PORT_SEND);
    inet_pton(AF_INET, SERVER_HOST, &serverAddr.sin_addr);

    // Connect to server
    if (connect(clientSocket, (struct sockaddr*)&serverAddr, sizeof(serverAddr)) == SOCKET_ERROR) {
        std::cerr << "Failed to connect to server: " << WSAGetLastError() << std::endl;
        closesocket(clientSocket);
        clientSocket = INVALID_SOCKET;
    }

    // Start the TCP sending thread
    sendingThread = std::thread(&AzureKinectCapture::SendFrameWorker, this);
}

AzureKinectCapture::~AzureKinectCapture()
{
    // Call the Close() function to release the device and stop the pipeline
    Close();

    // Signal the sending thread to stop
    stopSending = true;
    queueCondition.notify_one();
    if (sendingThread.joinable()) {
        sendingThread.join();
    }

    // Close the socket
    if (clientSocket != INVALID_SOCKET) {
        closesocket(clientSocket);
    }

    // Cleanup Winsock
    WSACleanup();
}

void AzureKinectCapture::SetLogger(std::function<void(const std::string&)> loggerFunc) {
    m_logger = loggerFunc;
}

bool AzureKinectCapture::TryOpenDevice() 
{
    bool opened = false;

    // Find the requested device in the device list
    ob::Context ctx;
    ctx.setLoggerSeverity(OB_LOG_SEVERITY_DEBUG);

    auto devList = ctx.queryDeviceList();
    int count = static_cast<int>(devList->deviceCount());

    if (count < (m_deviceIndex - 1)) {
        if (m_logger) m_logger("Device not found!");
        return opened;
    }

    int deviceIdx = m_deviceIndex;

    // We save the deviceId of this Client.
    // When the cameras are reinitialized during runtime, we can then guarantee
    // that each LiveScan instance uses the same device as before (In case two or more cameras are connected to the same PC)
    // A device ID of -1 means that no camera has been successfully initialized yet (only happens when the Client starts)
    if (deviceIDForRestart != -1) {
        deviceIdx = deviceIDForRestart;
    }

    std::shared_ptr<ob::Device> obDevice;
    int restartAttempts = 0;

    while (true) {
        try {
            obDevice = devList->getDevice(deviceIdx);

            // Get device info to store serial number
            auto devInfo = obDevice->getDeviceInfo();
            serialNumber = devInfo->serialNumber();

            if (m_logger) {
                m_logger("Device opened successfully at index: " + std::to_string(deviceIdx));
            }

            opened = true;
            break;
        }
        catch (const ob::Error& e) {
            if (m_logger) {
                m_logger("Failed to open device at index: " + std::to_string(deviceIdx) +
                    " - Error: " + e.getMessage());
            }

            if (deviceIDForRestart == -1) {
                deviceIdx++;
                if (deviceIdx >= count) {
                    break;
                }
            }
            else {
                if (++restartAttempts > 2) {
                    break;
                }
                Sleep(200);
            }
        }
    }

    // Store device if opened
    if (opened) {
        m_obDevice = obDevice;
        deviceIDForRestart = deviceIdx;
        restartAttempts = 0;
    }

    return opened;
}

bool AzureKinectCapture::Initialize(SYNC_STATE state, int syncOffsetMultiplier)
{
    bool res = TryOpenDevice();

    if (!res)
    {
        bInitialized = false;
        return bInitialized;
    }

    // Set sync configuration on the device
    OBMultiDeviceSyncConfig syncConfig = m_obDevice->getMultiDeviceSyncConfig();

    if (state == Master) {
        syncConfig.syncMode = OB_MULTI_DEVICE_SYNC_MODE_PRIMARY;
    }
    else if (state == Subordinate) {
        syncConfig.syncMode = OB_MULTI_DEVICE_SYNC_MODE_SECONDARY;
        syncConfig.trigger2ImageDelayUs = 160 * syncOffsetMultiplier;
    }
    else {
        syncConfig.syncMode = OB_MULTI_DEVICE_SYNC_MODE_STANDALONE;
    }

    m_obDevice->setMultiDeviceSyncConfig(syncConfig);

    // Create a pipeline with the current device
    m_pipeline = std::make_shared<ob::Pipeline>(m_obDevice);

    // Create a configuration to set color and depth sensor parameters
    auto config = std::make_shared<ob::Config>();

    // Configure color stream
    auto colorProfiles = m_pipeline->getStreamProfileList(OB_SENSOR_COLOR);
    std::shared_ptr <ob::VideoStreamProfile> colorProfile;

    if (colorProfiles) {
        try {
            // Find the corresponding Profile according to the specified format
            colorProfile = colorProfiles->getVideoStreamProfile(1280, 720, OB_FORMAT_RGB888, 30);
        }
        catch (ob::Error& e) {
            // If the specified format is not found, select the first one (default stream profile)
            colorProfile = std::const_pointer_cast<ob::StreamProfile>(colorProfiles->getProfile(OB_PROFILE_DEFAULT))->as<ob::VideoStreamProfile>();
        }
    }

    config->enableStream(colorProfile);

    // Configure depth stream
    std::shared_ptr<ob::StreamProfileList> depthProfileList;
    OBAlignMode alignMode = ALIGN_DISABLE;

    if (colorProfile) {
        // Try find supported depth to color align hardware mode profile
        depthProfileList = m_pipeline->getD2CDepthProfileList(colorProfile, ALIGN_D2C_HW_MODE);
        if (depthProfileList->count() > 0) {
            alignMode = ALIGN_D2C_HW_MODE;
        }
        else {
            // Try find supported depth to color align software mode profile
            depthProfileList = m_pipeline->getD2CDepthProfileList(colorProfile, ALIGN_D2C_SW_MODE);
            if (depthProfileList->count() > 0) {
                alignMode = ALIGN_D2C_SW_MODE;
            }
        }
    }
    else {
        depthProfileList = m_pipeline->getStreamProfileList(OB_SENSOR_DEPTH);
    }

    if (depthProfileList->count() > 0) {
        std::shared_ptr<ob::StreamProfile> depthProfile;
        try {
            // Select the profile with the same frame rate as color and the specified parameters
            if (colorProfile) {
                depthProfile = depthProfileList->getVideoStreamProfile(640, 576, OB_FORMAT_Y16, colorProfile->fps());
            }
        }
        catch (...) {
            depthProfile = nullptr;
        }

        if (!depthProfile) {
            // If no matching profile is found, select the default profile
            depthProfile = depthProfileList->getProfile(OB_PROFILE_DEFAULT);
        }
        config->enableStream(depthProfile);
    }

    // Enable D2C alignment to generate RGBD point clouds
    config->setAlignMode(alignMode);

    // Start the pipeline with the new configuration
    try {
        m_pipeline->start(config);
        bInitialized = true;
    }
    catch (const ob::Error& e) {
        if (m_logger) m_logger("Failed to start pipeline: " + std::string(e.getMessage()));
        bInitialized = false;
    }

    if (autoExposureEnabled == false) {
        SetExposureState(false, exposureTimeStep);
    }

    // Check that the device is able to capture a frame in under 5 seconds
    // If this device is a subordinate, it is expected to start capturing at a later time (When the master has started), so we skip this check
    if (state != Subordinate) {
        auto start = std::chrono::system_clock::now();
        bool bTemp;
        do {
            bTemp = AcquireFrame();
            auto elapsedSeconds = std::chrono::duration<double>(std::chrono::system_clock::now() - start);
            if (elapsedSeconds.count() > 5.0) {
                bInitialized = false;
                break;
            }
        } while (!bTemp);
    }

    return bInitialized;
}

bool AzureKinectCapture::Close()
{
    if (!bInitialized)
        return false;

    try {
        if (m_pipeline) {
            m_pipeline->stop(); // Stop streaming
            m_pipeline.reset(); // Release the pipeline
        }

        if (m_obDevice) {
            m_obDevice.reset(); // Release the device
        }

        bInitialized = false;
        return true;
    }
    catch (const ob::Error& e) {
        if (m_logger)
            m_logger("Error during Close(): " + std::string(e.getMessage()));
        return false;
    }
}

bool AzureKinectCapture::AcquireFrame()
{
    if (!bInitialized || !m_pipeline) {
        return false;
    }

    try {
        // Wait for a frameset (color + depth)
        std::shared_ptr<ob::FrameSet> frameset = m_pipeline->waitForFrames(captureTimeoutMs);

        if (!frameset || !frameset->colorFrame() || !frameset->depthFrame() || (frameset->colorFrame()->globalTimeStampUs() != frameset->depthFrame()->globalTimeStampUs())) {
            if (m_logger) m_logger("Incomplete frame set");
            return false;
        }

        // Get color and depth frames
        auto colorFrame = frameset->colorFrame();
        auto depthFrame = frameset->depthFrame();

        // Retrieve image parameters
        int width = colorFrame->width();
        int height = colorFrame->height();
        int stride = colorFrame->pixelAvailableBitSize();

        // Store timestamp
        currentTimeStamp = colorFrame->globalTimeStampUs();

        // ==========================
        // Document Detection Branch
        // ==========================
        // Get the current time and check the interval
        auto now = std::chrono::steady_clock::now();
        auto nowMs = std::chrono::time_point_cast<std::chrono::milliseconds>(now).time_since_epoch().count();

        if (nowMs - lastFrameTime.count() >= FRAME_SEND_INTERVAL_MS)
        {
            // Convert color frame into an OpenCV mat
            cImg = cv::Mat(height, width, CV_8UC3, (void*)colorFrame->data(), width*3);

            // Allocate masked image if needed
            if (maskedImg.empty() || maskedImg.size() != cImg.size()) {
                maskedImg = cv::Mat(cImg.size(), CV_8UC3);
            }

            cImg.copyTo(maskedImg);

            // Convert depth frame into an OpenCV mat
            int depthWidth = depthFrame->width();
            int depthHeight = depthFrame->height();
            uint16_t* depthData = reinterpret_cast<uint16_t*>(depthFrame->data());

            cv::Mat depthMat(depthHeight, depthWidth, CV_16U, depthData);

            // Generate mask: depth == 0 or > 750mm
            cv::Mat mask = (depthMat == 0) | (depthMat > 750);

            // Apply mask: set color to black for masked pixels
            maskedImg.setTo(cv::Scalar(0, 0, 0), mask);

            // Push to queue for transmission
            {
                std::unique_lock<std::mutex> lock(queueMutex);
                while (!frameQueue.empty()) { frameQueue.pop(); }
                frameQueue.push(maskedImg.clone());
            }

            queueCondition.notify_one();
            lastFrameTime = std::chrono::milliseconds(nowMs);
        }

        // ==========================
        // Point Cloud Branch
        // ==========================

        // Resize color frame buffer if needed
        if (!pColorRGBX || nColorFrameWidth != width || nColorFrameHeight != height) {
            nColorFrameWidth = width;
            nColorFrameHeight = height;
            if (pColorRGBX) delete[] pColorRGBX;
            pColorRGBX = new RGB[width * height];
        }

        // Copy color frame data into color frame buffer
        if (colorFrame->format() != OB_FORMAT_RGB888) {
            m_logger("Warning: Expected RGB888 format but got " + std::to_string(colorFrame->format()));
        }

        const uint8_t* src = static_cast<const uint8_t*>(colorFrame->data());
        for (int i = 0; i < width * height; ++i) {
            pColorRGBX[i].rgbRed = src[i * 3 + 0];
            pColorRGBX[i].rgbGreen = src[i * 3 + 1];
            pColorRGBX[i].rgbBlue = src[i * 3 + 2];
            pColorRGBX[i].rgbReserved = 255;
        }

        // Generate point cloud directly
        UpdatePointCloud(frameset);

        return true;
    }
    catch (const ob::Error& e) {
        if (m_logger) m_logger("Failed to acquire frame: " + std::string(e.getMessage()));
        return false;
    };
}

void AzureKinectCapture::UpdatePointCloud(std::shared_ptr<ob::FrameSet> frameset) {
    if (!pointCloudFilter) {
        pointCloudFilter = std::make_unique<ob::PointCloudFilter>();
        auto cameraParams = m_pipeline->getCameraParam();
        pointCloudFilter->setCameraParam(cameraParams);
    }

    pointCloudFilter->setCreatePointFormat(OB_FORMAT_RGB_POINT);

    try {
        auto pcFrame = pointCloudFilter->process(frameset);

        // Extract point cloud data
        OBColorPoint *point = (OBColorPoint*)(pcFrame->data());
        int pointCount = pcFrame->dataSize() / sizeof(OBColorPoint);

        lastFrameVertices.clear();
        lastFrameRGB.clear();

        for (int i = 0; i < pointCount; i++) {
            float x = point->x / 1000.0f;
            float y = point->y / 1000.0f;
            float z = point->z / 1000.0f;
            byte r = point->r;
            byte g = point->g;
            byte b = point->b;

            lastFrameVertices.emplace_back(x, y, z);
            lastFrameRGB.push_back({ b, g, r, 255 });
            point++;
        }
    }
    catch (std::exception& e) {
        if (m_logger) m_logger("Point cloud generation failed: " + std::string(e.what()));
    }
}

void AzureKinectCapture::SendFrameViaTCP(const cv::Mat& frame)
{
    if (clientSocket == INVALID_SOCKET) {
        return;
    }

    // Encode the frame as a png
    std::vector<uchar> buf;
    cv::imencode(".png", frame, buf);

    // Send the size of the image first
    uint32_t imageSize = buf.size();
    send(clientSocket, (const char*)&imageSize, sizeof(imageSize), 0);

    // Send the image data
    send(clientSocket, (const char*)buf.data(), buf.size(), 0);
}

void AzureKinectCapture::SendFrameWorker()
{
    while (true) {
        std::unique_lock<std::mutex> lock(queueMutex);
        queueCondition.wait(lock, [this] { return stopSending || !frameQueue.empty(); });
        if (stopSending && frameQueue.empty())
            break;
        cv::Mat frame = frameQueue.front();
        frameQueue.pop();
        lock.unlock();
        SendFrameViaTCP(frame);
    }
}

/// <summary>
/// Enables/Disables Auto Exposure and/or sets the exposure to a step value between 1 and 300
/// </summary>
/// <param name="exposureStep">The Exposure Step between 1 and 300</param>
void AzureKinectCapture::SetExposureState(bool enableAutoExposure, int exposureStep)
{
    if (!bInitialized || !m_obDevice) {
        return;
    }

    try {
        if (enableAutoExposure) {
            m_obDevice->setBoolProperty(OB_PROP_COLOR_AUTO_EXPOSURE_BOOL, true);
            autoExposureEnabled = true;
        }
        else {
            m_obDevice->setBoolProperty(OB_PROP_COLOR_AUTO_EXPOSURE_BOOL, false);
            m_obDevice->setIntProperty(OB_PROP_COLOR_EXPOSURE_INT, exposureStep);
            autoExposureEnabled = false;
            exposureTimeStep = exposureStep;
        }
    }
    catch (const ob::Error& e) {
        if (m_logger) m_logger("Failed to set exposure: " + std::string(e.getMessage()));
    }
}

uint64_t AzureKinectCapture::GetTimeStamp()
{
    return currentTimeStamp;
}

int AzureKinectCapture::GetDeviceIndex()
{
    return deviceIDForRestart;
}
