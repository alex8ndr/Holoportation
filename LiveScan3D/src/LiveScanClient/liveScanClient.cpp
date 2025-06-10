//   Copyright (C) 2015  Marek Kowalski (M.Kowalski@ire.pw.edu.pl), Jacek Naruniec (J.Naruniec@ire.pw.edu.pl)
//   License: MIT Software License   See LICENSE.txt for the full license.

//   If you use this software in your research, then please use the following citation:

//    Kowalski, M.; Naruniec, J.; Daniluk, M.: "LiveScan3D: A Fast and Inexpensive 3D Data
//    Acquisition System for Multiple Kinect v2 Sensors". in 3D Vision (3DV), 2015 International Conference on, Lyon, France, 2015

//    @INPROCEEDINGS{Kowalski15,
//        author={Kowalski, M. and Naruniec, J. and Daniluk, M.},
//        booktitle={3D Vision (3DV), 2015 International Conference on},
//        title={LiveScan3D: A Fast and Inexpensive 3D Data Acquisition System for Multiple Kinect v2 Sensors},
//        year={2015},
//    }
#include "stdafx.h"
#include "resource.h"
#include "LiveScanClient.h"
#include "filter.h"
#include "objectUtils.h"
#include <chrono>
#include <strsafe.h>
#include <fstream>
#include "zstd.h"
#include <shellapi.h>

#include <iostream>


std::mutex clientThreadMutex;

std::function<void(const std::string&)> LiveScanClient::GetLogger() {
	return [this](const std::string& msg) { this->Log(msg); };
}

void LiveScanClient::SetupLogging(int clientIndex)
{
	wchar_t buffer[MAX_PATH];
	GetModuleFileNameW(NULL, buffer, MAX_PATH);
	std::wstring path(buffer);
	std::wstring dir = path.substr(0, path.find_last_of(L"\\/")) + L"\\Log";

	CreateDirectoryW(dir.c_str(), NULL);

	std::wstring logPath = dir + L"\\app_log_client_" + std::to_wstring(clientIndex) + L".txt";
	m_logFile.open(logPath, std::ios::out | std::ios::app);

	if (!m_logFile.is_open())
	{
		OutputDebugStringW(L"Failed to open log file.\n");
		return;
	}

	Log("==== Application Started (Client " + std::to_string(clientIndex) + ") ====");
}

void LiveScanClient::Log(const std::string& message)
{
	if (m_logFile.is_open())
	{
		m_logFile << message << std::endl;
		m_logFile.flush();
	}
	else
	{
		OutputDebugStringA((message + "\n").c_str()); // fallback to debugger output
	}
}

LiveScanClient::LiveScanClient(int index) :
	m_nClientIndex(index),
	m_pCameraSpaceCoordinates(NULL),
	m_bCalibrate(false),
	m_bFilter(false),
	m_bStreamOnlyBodies(false),
	m_bCaptureFrame(false),
	m_bConfirmCaptured(false),
	m_bConfirmCalibrated(false),
	m_bConfirmRestartAsMaster(false),
	isClientThreadRunning(true),
	m_bFrameCompression(true),
	m_iCompressionLevel(2),
	m_nFilterNeighbors(10),
	m_fFilterThreshold(0.01f),
	m_bRestartingCamera(false),
	m_bAutoExposureEnabled(true), // Which state the Auto Exposure should be set to
	m_nExposureSteps(-5)
{
	pCapture = new AzureKinectCapture(m_nClientIndex);
	pCapture->SetLogger(GetLogger());

	m_vBounds.push_back(-0.5);
	m_vBounds.push_back(-0.5);
	m_vBounds.push_back(-0.5);
	m_vBounds.push_back(0.5);
	m_vBounds.push_back(0.5);
	m_vBounds.push_back(0.5);
}

LiveScanClient::~LiveScanClient()
{
	if (pCapture)
	{
		delete pCapture;
		pCapture = NULL;
	}

	if (m_pCameraSpaceCoordinates)
	{
		delete[] m_pCameraSpaceCoordinates;
		m_pCameraSpaceCoordinates = NULL;
	}
}

void LiveScanClient::Run(std::wstring serverAddress)
{
	SetupLogging(m_nClientIndex);

	if (calibration.bCalibrated)
		m_bConfirmCalibrated = true;

	bool res = pCapture->Initialize(Standalone, 0);
	if (res)
	{
		calibration.LoadCalibration(pCapture->serialNumber);
		m_pCameraSpaceCoordinates = new Point3f[pCapture->nColorFrameWidth * pCapture->nColorFrameHeight];
		pCapture->SetExposureState(true, 0);
	}
	else
	{
		std::cerr << "[LiveScanClient] Failed to initialize capture device." << std::endl;
	}

	std::thread t1(&LiveScanClient::ClientThreadFunction, this);
	// Main message loop
	while (!m_bExitRequested)
	{
		UpdateFrame();
	}

	isClientThreadRunning = false;
	t1.join();
}

void LiveScanClient::UpdateFrame()
{
	if (!pCapture->bInitialized)
	{
		return;
	}

	bool bNewFrameAcquired = pCapture->AcquireFrame();

	if (!bNewFrameAcquired)
		return;

	pCapture->MapColorFrameToCameraSpace(m_pCameraSpaceCoordinates);

	{
		std::lock_guard<std::mutex> lock(clientThreadMutex);
		ProcessFrame(m_pCameraSpaceCoordinates, pCapture->pColorRGBX, pCapture->vBodies, pCapture->pBodyIndex);

		if (m_bCaptureFrame)
		{
			uint64_t timeStamp = pCapture->GetTimeStamp();
			m_framesFileWriterReader.writeFrame(m_vLastFrameVertices, m_vLastFrameRGB, timeStamp, pCapture->GetDeviceIndex());

			m_bConfirmCaptured = true;
			m_bCaptureFrame = false;
		}
	}

	if (m_bCalibrate)
	{
		std::lock_guard<std::mutex> lock(clientThreadMutex);
		Point3f* pCameraCoordinates = new Point3f[pCapture->nColorFrameWidth * pCapture->nColorFrameHeight];
		pCapture->MapColorFrameToCameraSpace(pCameraCoordinates);

		bool res = calibration.Calibrate(pCapture->pColorRGBX, pCameraCoordinates, pCapture->nColorFrameWidth, pCapture->nColorFrameHeight);

		delete[] pCameraCoordinates;

		if (res)
		{
			calibration.SaveCalibration(pCapture->serialNumber);
			m_bConfirmCalibrated = true;
			m_bCalibrate = false;
		}
	}
}

void LiveScanClient::StartFrameCapture()
{
	m_bCaptureFrame = true;
}

void LiveScanClient::Calibrate()
{
	m_bCalibrate = true;
}

void LiveScanClient::SetSettings(const KinectSettings& settings)
{
	m_vBounds = { settings.minBounds[0], settings.minBounds[1], settings.minBounds[2],
				  settings.maxBounds[0], settings.maxBounds[1], settings.maxBounds[2] };

	m_bFilter = settings.filter;
	m_nFilterNeighbors = settings.filterNeighbors;
	m_fFilterThreshold = settings.filterThreshold;

	calibration.markerPoses.resize(settings.numMarkers);
	for (int i = 0; i < settings.numMarkers; i++) {
		calibration.markerPoses[i].markerId = settings.markerPoses[i].markerId;
		memcpy(calibration.markerPoses[i].R, settings.markerPoses[i].R, sizeof(float) * 9);
		memcpy(calibration.markerPoses[i].t, settings.markerPoses[i].t, sizeof(float) * 3);
	}

	m_bStreamOnlyBodies = settings.streamOnlyBodies;
	m_iCompressionLevel = settings.compressionLevel;
	m_bFrameCompression = (settings.compressionLevel > 0);

	m_bAutoExposureEnabled = settings.autoExposureEnabled;
	m_nExposureSteps = settings.exposureStep;

	pCapture->SetExposureState(m_bAutoExposureEnabled, m_nExposureSteps);
}

void LiveScanClient::RequestStoredFrame()
{
	vector<Point3s> points;
	vector<RGB> colors;
	bool res = m_framesFileWriterReader.readFrame(points, colors);

	SendStoredFrame(points, colors, !res);
}

void LiveScanClient::RequestLastFrame()
{
	SendLatestFrame(m_vLastFrameVertices, m_vLastFrameRGB);
}

void LiveScanClient::ReceiveCalibration(const AffineTransform& transform)
{
	for (int i = 0; i < 3; i++)
	{
		for (int j = 0; j < 3; j++)
			calibration.worldR[i][j] = transform.R[i][j];

		calibration.worldT[i] = transform.t[i];
	}
}

void LiveScanClient::ClearStoredFrames()
{
	m_framesFileWriterReader.closeFileIfOpened();
}

void LiveScanClient::EnableTemporalSync(int syncOffset)
{
	//Determine if this device is a subordinate, master, or standalone
	int jackState = pCapture->GetSyncJackState();

	bool res = false;

	switch (jackState)
	{
	case -1:
		currentTempSyncState = SUBORDINATE;

		//Restart this device as Subordinate, with a unique syncOffset (send by the server)
		m_bRestartingCamera = true;


		res = pCapture->Close();
		if (!res) {
			Log("Subordinate device failed to close! Restart Application!");
			return;
		}

		res = pCapture->Initialize(Subordinate, syncOffset);
		if (!res) {
			Log("Subordinate device failed to reinitialize! Restart Application!");
			return;
		}
		//Confirm to the server, that we set this device as subordinate
		m_bConfirmTempSyncState = true;
		m_bRestartingCamera = false;
		break;

	case 0:
		currentTempSyncState = MASTER;

		//Only Close this device, as it needs to wait for all subordinates to start, before starting itself
		m_bRestartingCamera = true;

		res = pCapture->Close();
		if (!res) {
			Log("Master device failed to close! Restart Application!");
			return;
		}

		m_bConfirmTempSyncState = true;
		break;

	case 1://Device is Standalone
		currentTempSyncState = STANDALONE;

		//Restart this device as Standalone
		m_bRestartingCamera = true;

		res = pCapture->Close();
		if (!res) {
			Log("Capture device failed to close! Restart Application!");
			return;
		}

		res = pCapture->Initialize(Standalone, 0);

		if (!res) {
			Log("Capture device failed to reinitialize! Restart Application!");
			return;
		}

		m_bConfirmTempSyncState = true;
		m_bRestartingCamera = false;
		break;
	default:
		break;
	}
}

void LiveScanClient::DisableTemporalSync()
{
	//Sets this device as Standalone
	currentTempSyncState = STANDALONE;
	m_bRestartingCamera = true;

	bool res;

	res = pCapture->Close();
	if (!res) {
		Log("Capture device failed to close! Restart Application!");
		return;
	}

	res = pCapture->Initialize(Standalone, 0);

	if (!res) {
		Log("Capture device failed to reinitialize! Restart Application!");
		return;
	}

	m_bConfirmTempSyncState = true;
	m_bRestartingCamera = false;
}

void LiveScanClient::StartMaster()
{
	//Got confirmation from the server that all subs have started, and we can now start the master 
	if (currentTempSyncState == MASTER)
	{
		bool res = pCapture->Initialize(Master, 0);
		if (!res) {
			Log("Master device failed to reinitialize! Restart Application!");
			return;
		}

		m_bConfirmRestartAsMaster = true;
		m_bRestartingCamera = false;
	}
}

void LiveScanClient::RequestSyncJackState()
{
	SendDeviceSyncState();
}

void LiveScanClient::ConfirmCaptured()
{
	if (m_pWrapper && m_pWrapper->confirmCapturedCallback)
		m_pWrapper->confirmCapturedCallback(m_nClientIndex);

	m_bConfirmCaptured = false;
}

void LiveScanClient::ConfirmCalibrated()
{
	if (m_pWrapper && m_pWrapper->confirmCalibratedCallback)
	{
		float* R = new float[9] {
			calibration.worldR[0][0], calibration.worldR[0][1], calibration.worldR[0][2],
			calibration.worldR[1][0], calibration.worldR[1][1], calibration.worldR[1][2],
			calibration.worldR[2][0], calibration.worldR[2][1], calibration.worldR[2][2]
		};

		float* t = calibration.worldT.data(); // already float[3]

		m_pWrapper->confirmCalibratedCallback(m_nClientIndex, calibration.iUsedMarkerId, R, t);
	}

	m_bConfirmCalibrated = false;
}

void LiveScanClient::SendLatestFrame(std::vector<Point3s>& vertices, std::vector<RGB>& RGB)
{
	if (m_pWrapper && m_pWrapper->sendLatestFrameCallback)
	{
		int count = static_cast<int>(vertices.size());
		if (count != RGB.size())
		{
			Log("Warning: size mismatch! There were " + std::to_string(count) + " vertices and " + std::to_string(RGB.size()) + " colors. Sending smallest size.");

			if (count < RGB.size())
				count = RGB.size();
		}

		m_pWrapper->sendLatestFrameCallback(m_nClientIndex, vertices.data(), RGB.data(), count);
	}
}

void LiveScanClient::SendStoredFrame(std::vector<Point3s>& vertices, std::vector<RGB>& RGB, bool noMoreFrames)
{
	if (m_pWrapper && m_pWrapper->sendStoredFrameCallback)
	{
		int count = static_cast<int>(vertices.size());
		if (count != RGB.size())
		{
			Log("Warning: size mismatch! There were " + std::to_string(count) + " vertices and " + std::to_string(RGB.size()) + " colors. Sending smallest size.");

			if (count < RGB.size())
				count = RGB.size();
		}

		m_pWrapper->sendStoredFrameCallback(m_nClientIndex, vertices.data(), RGB.data(), count, noMoreFrames);
	}
}

void LiveScanClient::ConfirmTempSyncState()
{
	if (m_pWrapper && m_pWrapper->confirmTempSyncStateCallback)
	{
		int syncState = 2; // default: STANDALONE
		switch (currentTempSyncState)
		{
		case SUBORDINATE: syncState = 0; break;
		case MASTER:      syncState = 1; break;
		case STANDALONE:  syncState = 2; break;
		}

		m_pWrapper->confirmTempSyncStateCallback(m_nClientIndex, syncState);
	}

	m_bConfirmTempSyncState = false;
}

void LiveScanClient::ConfirmMasterRestart()
{
	if (m_pWrapper && m_pWrapper->confirmMasterRestartCallback)
	{
		m_pWrapper->confirmMasterRestartCallback(m_nClientIndex);
	}

	m_bConfirmRestartAsMaster = false;
}

void LiveScanClient::SendDeviceSyncState()
{
	SYNC_STATE deviceSyncState = pCapture->GetSyncJackState();
	m_bConfirmTempSyncState = false;

	if (m_pWrapper && m_pWrapper->sendDeviceSyncStateCallback)
	{
		int syncState = 2; // default: STANDALONE
		switch (deviceSyncState)
		{
		case SUBORDINATE: syncState = 0; break;
		case MASTER:      syncState = 1; break;
		case STANDALONE:  syncState = 2; break;
		}

		m_pWrapper->sendDeviceSyncStateCallback(m_nClientIndex, syncState);
	}
}

void LiveScanClient::ClientThreadFunction()
{
	while (isClientThreadRunning)
	{
		std::this_thread::sleep_for(std::chrono::milliseconds(1));
		HandleClient();
	}
}

void LiveScanClient::HandleClient()
{
	char byteToSend;
	std::lock_guard<std::mutex> lock(clientThreadMutex);

	if (m_bConfirmCaptured)
	{
		ConfirmCaptured();
	}


	if (m_bConfirmCalibrated)
	{
		ConfirmCalibrated();
	}

	// Send validation to the server that this device has been set to a specific Sync State 
	if (m_bConfirmTempSyncState)
	{
		ConfirmTempSyncState();
	}

	// Send validation to the server that the Master camera has started recording again
	if (m_bConfirmRestartAsMaster) 
	{
		ConfirmMasterRestart();
	}
}

void LiveScanClient::ProcessFrame(Point3f *vertices, RGB *colorInDepth, vector<Body> &bodies, BYTE* bodyIndex)
{
	unsigned int nVertices = pCapture->nColorFrameHeight * pCapture->nColorFrameWidth;

	//To save some processing cost, we allocate a full frame size (nVertices) of a Point3f Vector beforehand
	//instead of using push_back for each vertice. Even though we have to copy the vertices into a clean array
	//later and it uses a little bit more RAM, this gives us a nice speed increase for this function, around 25-50%
	Point3f invalidPoint = Point3f(0, 0, 0, true);
	vector<Point3f> AllVertices(nVertices);
	int goodVerticesCount = 0;

	for (unsigned int vertexIndex = 0; vertexIndex < nVertices; vertexIndex++)
	{
		if (m_bStreamOnlyBodies && bodyIndex[vertexIndex] >= bodies.size())
			continue;

		//As the resizing function doesn't return a valid RGB-Reserved value which indicates that this pixel is invalid,
		//we cut all vertices under a distance of 0.0001mm, as the invalid vertices always have a Z-Value of 0
		if (vertices[vertexIndex].Z >= 0.0001 && colorInDepth[vertexIndex].rgbReserved == 255)
		{
			Point3f temp = vertices[vertexIndex];
			RGB tempColor = colorInDepth[vertexIndex];
			if (calibration.bCalibrated)
			{
				temp.X += calibration.worldT[0];
				temp.Y += calibration.worldT[1];
				temp.Z += calibration.worldT[2];
				temp = RotatePoint(temp, calibration.worldR);

				if (temp.X < m_vBounds[0] || temp.X > m_vBounds[3]
					|| temp.Y < m_vBounds[1] || temp.Y > m_vBounds[4]
					|| temp.Z < m_vBounds[2] || temp.Z > m_vBounds[5]) 
				{
					AllVertices[vertexIndex] = invalidPoint;
					continue;
				}
					
			}

			AllVertices[vertexIndex] = temp;
			goodVerticesCount++;
		}

		else 
		{
			AllVertices[vertexIndex] = invalidPoint;
		}
	}

	vector<Body> tempBodies = bodies;

	//for (unsigned int i = 0; i < tempBodies.size(); i++)
	//{
	//	for (unsigned int j = 0; j < tempBodies[i].vJoints.size(); j++)
	//	{
	//		if (calibration.bCalibrated)
	//		{
	//			tempBodies[i].vJoints[j].Position.X += calibration.worldT[0];
	//			tempBodies[i].vJoints[j].Position.Y += calibration.worldT[1];
	//			tempBodies[i].vJoints[j].Position.Z += calibration.worldT[2];

	//			Point3f tempPoint(tempBodies[i].vJoints[j].Position.X, tempBodies[i].vJoints[j].Position.Y, tempBodies[i].vJoints[j].Position.Z);

	//			tempPoint = RotatePoint(tempPoint, calibration.worldR);

	//			tempBodies[i].vJoints[j].Position.X = tempPoint.X;
	//			tempBodies[i].vJoints[j].Position.Y = tempPoint.Y;
	//			tempBodies[i].vJoints[j].Position.Z = tempPoint.Z;
	//		}
	//	}
	//}

	vector<Point3f> goodVertices(goodVerticesCount);
	vector<RGB> goodColorPoints(goodVerticesCount);
	int goodVerticesShortCounter = 0;

	//Copy all valid vertices into a clean vector 
	for (unsigned int i = 0; i < AllVertices.size(); i++)
	{
		if (!AllVertices[i].Invalid) 
		{
			goodVertices[goodVerticesShortCounter] = AllVertices[i];
			goodColorPoints[goodVerticesShortCounter] = colorInDepth[i];
			goodVerticesShortCounter++;
		}
	}

	if (m_bFilter)
		filter(goodVertices, goodColorPoints, m_nFilterNeighbors, m_fFilterThreshold);


	vector<Point3s> goodVerticesShort(goodVertices.size());
	
	for (size_t i = 0; i < goodVertices.size(); i++)
	{
		goodVerticesShort[i] = goodVertices[i];
	}

	m_vLastFrameBody = tempBodies;
	m_vLastFrameVertices = goodVerticesShort;
	m_vLastFrameRGB = goodColorPoints;
}

void LiveScanClient::RequestExit()
{
	m_bExitRequested = true;

	// Optionally send WM_QUIT to the message loop
	PostThreadMessage(GetCurrentThreadId(), WM_QUIT, 0, 0);
}

