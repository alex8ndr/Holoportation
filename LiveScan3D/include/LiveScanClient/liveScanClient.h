#pragma once


#define WIN32_LEAN_AND_MEAN
#define _WINSOCK_DEPRECATED_NO_WARNINGS
#define _WINSOCKAPI_

#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>

#include "resource.h"
#include "ImageRenderer.h"
#include "SocketCS.h"
#include "calibration.h"
#include "azureKinectCapture.h"
#include "frameFileWriterReader.h"
#include <thread>
#include <mutex>
#include <functional>

class LiveScanClient
{
public:
    LiveScanClient(int index);
    ~LiveScanClient();

    void Run(HINSTANCE hInstance, int nCmdShow, bool headless = false, bool autoconnect = false, std::wstring serverAddress = L"");
    void RequestExit();
    std::function<void(const std::string&)> GetLogger();

    bool m_bSocketThread;

private:
    std::ofstream m_logFile;

    int m_index = 0;

    Calibration calibration;

    bool m_bCalibrate;
    bool m_bFilter;
    bool m_bStreamOnlyBodies;

    bool m_bIsMaster;
    bool m_bIsSubOrdinate;
    bool m_bRestartingCamera;

    ICapture* pCapture;

    int m_nFilterNeighbors;
    float m_fFilterThreshold;

    bool m_bCaptureFrame;
    bool m_bConnected;
    bool m_bConfirmCaptured;
    bool m_bConfirmTempSyncState;
    bool m_bConfirmSubOrdinateStarted;
    bool m_bConfirmRestartAsMaster;
    bool m_bConfirmCalibrated;
    bool m_bShowDepth;
    bool m_bFrameCompression;
    int m_iCompressionLevel;
    bool m_bAutoExposureEnabled;
    int m_nExposureStep;

    volatile bool m_bExitRequested = false;

    enum tempSyncConfig { MASTER, SUBORDINATE, STANDALONE };
    tempSyncConfig currentTempSyncState;

    FrameFileWriterReader m_framesFileWriterReader;

    SocketClient* m_pClientSocket;
    std::vector<float> m_vBounds;

    std::vector<Point3s> m_vLastFrameVertices;
    std::vector<RGB> m_vLastFrameRGB;
    std::vector<Body> m_vLastFrameBody;

    HWND m_hWnd;
    INT64 m_nLastCounter;
    double m_fFreq;
    INT64 m_nNextStatusTime;
    DWORD m_nFramesSinceUpdate;
    int frameRecordCounter;

    Point3f* m_pCameraSpaceCoordinates;
    RGB* m_pColorInColorSpace;
    UINT16* m_pDepthInColorSpace;

    // Direct2D
    ImageRenderer* m_pDrawColor;
    ID2D1Factory* m_pD2DFactory;
    RGB* m_pDepthRGBX;

    void UpdateFrame();

    void HandleSocket();
    void SendFrame(vector<Point3s> vertices, vector<RGB> RGB, vector<Body> body);

    void SocketThreadFunction();
    void ProcessFrame(Point3f* vertices, RGB* colorInDepth, vector<Body>& bodies, BYTE* bodyIndex);
    void SetupLogging(int clientIndex);
    void Log(const std::string& message);
};
