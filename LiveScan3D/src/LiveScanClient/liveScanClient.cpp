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


std::mutex m_mSocketThreadMutex;

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
	m_index(index),
	m_pCameraSpaceCoordinates(NULL),
	m_bCalibrate(false),
	m_bFilter(false),
	m_bStreamOnlyBodies(false),
	m_bCaptureFrame(false),
	m_bConnected(false),
	m_bConfirmCaptured(false),
	m_bConfirmCalibrated(false),
	m_bConfirmRestartAsMaster(false),
	m_bSocketThread(true),
	m_bFrameCompression(true),
	m_iCompressionLevel(2),
	m_pClientSocket(NULL),
	m_nFilterNeighbors(10),
	m_fFilterThreshold(0.01f),
	m_bRestartingCamera(false),
	m_bAutoExposureEnabled(true), // Which state the Auto Exposure should be set to
	m_nExposureSteps(-5)
{
	pCapture = new AzureKinectCapture(index);
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

	if (m_pClientSocket)
	{
		delete m_pClientSocket;
		m_pClientSocket = NULL;
	}
}

void LiveScanClient::Run(std::wstring serverAddress)
{
	SetupLogging(m_index);

	{
		std::lock_guard<std::mutex> lock(m_mSocketThreadMutex);
		while (!m_bConnected)
		{
			try
			{
				m_pClientSocket = new SocketClient(std::string(serverAddress.begin(), serverAddress.end()), 48001);
				m_bConnected = true;
				if (calibration.bCalibrated)
					m_bConfirmCalibrated = true;
			}
			catch (...)
			{
				Log("Failed to connect. Retrying...");
				std::this_thread::sleep_for(std::chrono::seconds(1)); // Wait for 1 second before retrying
			}
		}
	}

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

	std::thread t1(&LiveScanClient::SocketThreadFunction, this);
	// Main message loop
	while (!m_bExitRequested)
	{
		UpdateFrame();
	}

	m_bSocketThread = false;
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
		std::lock_guard<std::mutex> lock(m_mSocketThreadMutex);
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
		std::lock_guard<std::mutex> lock(m_mSocketThreadMutex);
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
	char byteToSend = MSG_STORED_FRAME;
	m_pClientSocket->SendBytes(&byteToSend, 1);

	vector<Point3s> points;
	vector<RGB> colors;
	bool res = m_framesFileWriterReader.readFrame(points, colors);
	if (res == false)
	{
		int size = -1;
		m_pClientSocket->SendBytes((char*)&size, 4);
	}
	else
		SendFrame(points, colors, m_vLastFrameBody);
}

void LiveScanClient::RequestLastFrame()
{
	char byteToSend = MSG_LAST_FRAME;
	m_pClientSocket->SendBytes(&byteToSend, 1);

	SendFrame(m_vLastFrameVertices, m_vLastFrameRGB, m_vLastFrameBody);
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
	int size = 3;
	char* buffer = new char[size];
	buffer[0] = MSG_SYNC_JACK_STATE;

	buffer[1] = pCapture->GetSyncJackState() + 1; // Gets the current Sync Jack State and adds + 1, as we can't send a negative value

	m_pClientSocket->SendBytes(buffer, size);
	m_bConfirmTempSyncState = false;
}

void LiveScanClient::SocketThreadFunction()
{
	while (m_bSocketThread)
	{
		std::this_thread::sleep_for(std::chrono::milliseconds(1));
		HandleSocket();
	}
}

void LiveScanClient::HandleSocket()
{
	char byteToSend;
	std::lock_guard<std::mutex> lock(m_mSocketThreadMutex);

	if (!m_bConnected)
	{
		return;
	}

	string received = m_pClientSocket->ReceiveBytes();
	for (unsigned int i = 0; i < received.length(); i++)
	{
		//capture a frame
		if (received[i] == MSG_CAPTURE_FRAME)
			m_bCaptureFrame = true;
			
		//calibrate
		else if (received[i] == MSG_CALIBRATE)
			m_bCalibrate = true;
		
		//Enables Temporal sync on this client
		else if (received[i] == MSG_SET_TEMPSYNC_ON) {

			i++; //Get next byte (the sync Offset)
			int syncOffset = received[i];

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

		//Sets this device as Standalone
		else if (received[i] == MSG_SET_TEMPSYNC_OFF) {
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

		//Got confirmation from the server that all subs have started, and we can now start the master 
		else if (received[i] == MSG_START_MASTER) {
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

		//receive settings
		//TODO: what if packet is split?
		else if (received[i] == MSG_RECEIVE_SETTINGS)
		{
			vector<float> bounds(6);
			i++;
			int nBytes = *(int*)(received.c_str() + i);
			i += sizeof(int);

			for (int j = 0; j < 6; j++)
			{
				bounds[j] = *(float*)(received.c_str() + i);
				i += sizeof(float);
			}

			m_bFilter = (received[i]!=0);
			i++;

			m_nFilterNeighbors = *(int*)(received.c_str() + i);
			i += sizeof(int);

			m_fFilterThreshold = *(float*)(received.c_str() + i);
			i += sizeof(float);

			m_vBounds = bounds;

			int nMarkers = *(int*)(received.c_str() + i);
			i += sizeof(int);

			calibration.markerPoses.resize(nMarkers);

			for (int j = 0; j < nMarkers; j++)
			{
				for (int k = 0; k < 3; k++)
				{
					for (int l = 0; l < 3; l++)
					{
						calibration.markerPoses[j].R[k][l] = *(float*)(received.c_str() + i);
						i += sizeof(float);
					}
				}

				for (int k = 0; k < 3; k++)
				{
					calibration.markerPoses[j].t[k] = *(float*)(received.c_str() + i);
					i += sizeof(float);
				}

				calibration.markerPoses[j].markerId = *(int*)(received.c_str() + i);
				i += sizeof(int);
			}

			m_bStreamOnlyBodies = (received[i] != 0);
			i += 1;

			m_iCompressionLevel = *(int*)(received.c_str() + i);
			i += sizeof(int);
			if (m_iCompressionLevel > 0)
				m_bFrameCompression = true;
			else
				m_bFrameCompression = false;
			
			m_bAutoExposureEnabled = (received[i] != 0);
			i++;

			m_nExposureSteps = *(int*)(received.c_str() + i);
			i += sizeof(int);

			pCapture->SetExposureState(m_bAutoExposureEnabled, m_nExposureSteps);

			//so that we do not lose the next character in the stream
			i--;
		}
		//send stored frame
		else if (received[i] == MSG_REQUEST_STORED_FRAME)
		{
			byteToSend = MSG_STORED_FRAME;
			m_pClientSocket->SendBytes(&byteToSend, 1);

			vector<Point3s> points;
			vector<RGB> colors;
			bool res = m_framesFileWriterReader.readFrame(points, colors);
			if (res == false)
			{
				int size = -1;
				m_pClientSocket->SendBytes((char*)&size, 4);
			} else
				SendFrame(points, colors, m_vLastFrameBody);
		}
		//send last frame
		else if (received[i] == MSG_REQUEST_LAST_FRAME)
		{
			byteToSend = MSG_LAST_FRAME;
			m_pClientSocket->SendBytes(&byteToSend, 1);

			SendFrame(m_vLastFrameVertices, m_vLastFrameRGB, m_vLastFrameBody);
		}
		//receive calibration data
		else if (received[i] == MSG_RECEIVE_CALIBRATION)
		{
			i++;
			for (int j = 0; j < 3; j++)
			{
				for (int k = 0; k < 3; k++)
				{
					calibration.worldR[j][k] = *(float*)(received.c_str() + i);
					i += sizeof(float);
				}
			}
			for (int j = 0; j < 3; j++)
			{
				calibration.worldT[j] = *(float*)(received.c_str() + i);
				i += sizeof(float);
			}

			//so that we do not lose the next character in the stream
			i--;
		}
		else if (received[i] == MSG_CLEAR_STORED_FRAMES)
		{
			m_framesFileWriterReader.closeFileIfOpened();
		}

		else if (received[i] == MSG_REQUEST_SYNC_JACK_STATE) 
		{
			int size = 3;
			char* buffer = new char[size];
			buffer[0] = MSG_SYNC_JACK_STATE;

			buffer[1] = pCapture->GetSyncJackState() + 1; //Gets the current Sync Jack State and adds + 1, as we can't send a negative value

			m_pClientSocket->SendBytes(buffer, size);
			m_bConfirmTempSyncState = false;
		}
	}

	if (m_bConfirmCaptured)
	{
		byteToSend = MSG_CONFIRM_CAPTURED;
		m_pClientSocket->SendBytes(&byteToSend, 1);
		m_bConfirmCaptured = false;
	}

	//Send validation to the server that this device has been set to a specific Sync State 
	if (m_bConfirmTempSyncState)
	{
		int size = 3; //Somehow it doesn't work when sending only two bytes?? (So we send an extra one)
		char* buffer = new char[size];
		buffer[0] = MSG_CONFIRM_TEMP_SYNC_STATUS;

		switch (currentTempSyncState)
		{
		case SUBORDINATE: buffer[1] = 0; break;
		case MASTER: buffer[1] = 1; break;
		case STANDALONE: buffer[1] = 2; break;
		}

		m_pClientSocket->SendBytes(buffer, size);
		m_bConfirmTempSyncState = false;
	}

	//Send validation to the server that the Master camera has started recording again
	if (m_bConfirmRestartAsMaster) 
	{
		byteToSend = MSG_CONFIRM_MASTER_RESTART;
		m_pClientSocket->SendBytes(&byteToSend, 1);
		m_bConfirmRestartAsMaster = false;
	}


	if (m_bConfirmCalibrated)
	{
		int size = (9 + 3) * sizeof(float) + sizeof(int) + 1;
		char *buffer = new char[size];
		buffer[0] = MSG_CONFIRM_CALIBRATED;
		int i = 1;

		memcpy(buffer + i, &calibration.iUsedMarkerId, 1 * sizeof(int));
		i += 1 * sizeof(int);
		memcpy(buffer + i, calibration.worldR[0].data(), 3 * sizeof(float));
		i += 3 * sizeof(float);
		memcpy(buffer + i, calibration.worldR[1].data(), 3 * sizeof(float));
		i += 3 * sizeof(float);
		memcpy(buffer + i, calibration.worldR[2].data(), 3 * sizeof(float));
		i += 3 * sizeof(float);
		memcpy(buffer + i, calibration.worldT.data(), 3 * sizeof(float));
		i += 3 * sizeof(float);

		m_pClientSocket->SendBytes(buffer, size);
		m_bConfirmCalibrated = false;
	}
}

void LiveScanClient::SendFrame(vector<Point3s> vertices, vector<RGB> RGB, vector<Body> body)
{
	int size = RGB.size() * (3 + 3 * sizeof(short)) + sizeof(int);

	vector<char> buffer(size);
	char *ptr2 = (char*)vertices.data();
	int pos = 0;

	int nVertices = RGB.size();
	memcpy(buffer.data() + pos, &nVertices, sizeof(nVertices));
	pos += sizeof(nVertices);

	for (unsigned int i = 0; i < RGB.size(); i++)
	{
		buffer[pos++] = RGB[i].rgbRed;
		buffer[pos++] = RGB[i].rgbGreen;
		buffer[pos++] = RGB[i].rgbBlue;

		memcpy(buffer.data() + pos, ptr2, sizeof(short)* 3);
		ptr2 += sizeof(short) * 3;
		pos += sizeof(short) * 3;
	}

	int nBodies = body.size();
	size += sizeof(nBodies);
	for (int i = 0; i < nBodies; i++)
	{
		size += sizeof(body[i].bTracked);
		int nJoints = body[i].vJoints.size();
		size += sizeof(nJoints);
		size += nJoints * (3 * sizeof(float) + 2 * sizeof(int));
		size += nJoints * 2 * sizeof(float);
	}
	buffer.resize(size);

	memcpy(buffer.data() + pos, &nBodies, sizeof(nBodies));
	pos += sizeof(nBodies);

	for (int i = 0; i < nBodies; i++)
	{
		memcpy(buffer.data() + pos, &body[i].bTracked, sizeof(body[i].bTracked));
		pos += sizeof(body[i].bTracked);

		int nJoints = body[i].vJoints.size();
		memcpy(buffer.data() + pos, &nJoints, sizeof(nJoints));
		pos += sizeof(nJoints);

		for (int j = 0; j < nJoints; j++)
		{
			////Joint
			//memcpy(buffer.data() + pos, &body[i].vJoints[j].JointType, sizeof(JointType));
			//pos += sizeof(JointType);
			//memcpy(buffer.data() + pos, &body[i].vJoints[j].TrackingState, sizeof(TrackingState));
			//pos += sizeof(TrackingState);
			////Joint position
			//memcpy(buffer.data() + pos, &body[i].vJoints[j].Position.X, sizeof(float));
			//pos += sizeof(float);
			//memcpy(buffer.data() + pos, &body[i].vJoints[j].Position.Y, sizeof(float));
			//pos += sizeof(float);
			//memcpy(buffer.data() + pos, &body[i].vJoints[j].Position.Z, sizeof(float));
			//pos += sizeof(float);

			////JointInColorSpace
			//memcpy(buffer.data() + pos, &body[i].vJointsInColorSpace[j].X, sizeof(float));
			//pos += sizeof(float);
			//memcpy(buffer.data() + pos, &body[i].vJointsInColorSpace[j].Y, sizeof(float));
			//pos += sizeof(float);
		}
	}

	int iCompression = static_cast<int>(m_bFrameCompression);

	if (m_bFrameCompression)
	{
		// *2, because according to zstd documentation, increasing the size of the output buffer above a
		// bound should speed up the compression.
		int cBuffSize = ZSTD_compressBound(size) * 2;
		vector<char> compressedBuffer(cBuffSize);
		int cSize = ZSTD_compress(compressedBuffer.data(), cBuffSize, buffer.data(), size, m_iCompressionLevel);
		size = cSize;
		buffer = compressedBuffer;
	}
	char header[8];
	memcpy(header, (char*)&size, sizeof(size));
	memcpy(header + 4, (char*)&iCompression, sizeof(iCompression));

	m_pClientSocket->SendBytes((char*)&header, sizeof(int) * 2);
	m_pClientSocket->SendBytes(buffer.data(), size);
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

