#pragma once


#define WIN32_LEAN_AND_MEAN
#define _WINSOCK_DEPRECATED_NO_WARNINGS
#define _WINSOCKAPI_

#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>

#include "liveScanClientWrapper.h"
#include "resource.h"
#include "ImageRenderer.h"
#include "SocketCS.h"
#include "calibration.h"
#include "azureKinectCapture.h"
#include "frameFileWriterReader.h"
#include "objectUtils.h"
#include <thread>
#include <mutex>
#include <functional>

class LiveScanClient
{
public:
    LiveScanClient(int index);
    ~LiveScanClient();

    void Run(std::wstring serverAddress = L"");
    void StartFrameCapture();
    void Calibrate();
    void SetSettings(const KinectSettings& settings);
    void RequestStoredFrame();
    void RequestLastFrame();
    void ReceiveCalibration(const AffineTransform& transform);
    void ClearStoredFrames();
    void EnableTemporalSync(int syncOffset);
    void DisableTemporalSync();
    void StartMaster();
    void RequestSyncJackState();
    void RequestExit();

    std::function<void(const std::string&)> GetLogger();

    bool isClientThreadRunning;

    LiveScanClientWrapper* m_pWrapper = nullptr;
    int m_nClientIndex = -1;

private:
    std::ofstream m_logFile;

    Calibration calibration;

    atomic<bool> m_bCalibrate;
    bool m_bFilter;
    bool m_bStreamOnlyBodies;

    bool m_bRestartingCamera;

    ICapture* pCapture;

    int m_nFilterNeighbors;
    float m_fFilterThreshold;

    atomic<bool> m_bCaptureFrame;
    bool m_bConfirmCaptured;
    bool m_bConfirmTempSyncState;
    bool m_bConfirmRestartAsMaster;
    bool m_bConfirmCalibrated;
    bool m_bFrameCompression;
    int m_iCompressionLevel;
    bool m_bAutoExposureEnabled;
    int m_nExposureSteps;

    volatile bool m_bExitRequested = false;

    enum tempSyncConfig { MASTER, SUBORDINATE, STANDALONE };
    tempSyncConfig currentTempSyncState;

    FrameFileWriterReader m_framesFileWriterReader;

    std::vector<float> m_vBounds;

    std::vector<Point3s> m_vLastFrameVertices;
    std::vector<RGB> m_vLastFrameRGB;
    std::vector<Body> m_vLastFrameBody;

    Point3f* m_pCameraSpaceCoordinates;

    void UpdateFrame();

    void HandleClient();
    void ConfirmCaptured();
    void ConfirmCalibrated();
    void SendLatestFrame(vector<Point3s>& vertices, vector<RGB>& RGB);
    void SendStoredFrame(vector<Point3s>& vertices, vector<RGB>& RGB, bool noMoreFrames);
    void ConfirmTempSyncState();
    void ConfirmMasterRestart();
    void SendDeviceSyncState();

    void ClientThreadFunction();
    void ProcessFrame(Point3f* vertices, RGB* colorInDepth, vector<Body>& bodies, BYTE* bodyIndex);
    void SetupLogging(int clientIndex);
    void Log(const std::string& message);
};
