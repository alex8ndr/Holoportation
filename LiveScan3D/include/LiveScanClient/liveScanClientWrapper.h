#pragma once

#include "utils.h"
#include <memory>
#include <functional>
#include <thread>
#include <string>

// Forward declaration
class LiveScanClient;

// Typedefs for the callback signatures
typedef void(*SendSerialNumberCallback)(int clientIndex, const char* serialNumber);
typedef void(*ConfirmCapturedCallback)(int clientIndex);
typedef void(*ConfirmCalibratedCallback)(int clientIndex, int markerId, const float* R, const float* t);
typedef void(*SendLatestFrameCallback)(int clientIndex, const Point3s* vertices, const RGB* colors, int count);
typedef void(*SendStoredFrameCallback)(int clientIndex, const Point3s* vertices, const RGB* colors, int count, bool noMoreFrames);
typedef void(*ConfirmTempSyncStateCallback)(int clientIndex, int tempSyncState);
typedef void(*ConfirmMasterRestartCallback)(int clientIndex);

struct LiveScanClientWrapper {
	std::unique_ptr<LiveScanClient> client;
	std::thread thread;

	SendSerialNumberCallback sendSerialNumberCallback = nullptr;
	ConfirmCapturedCallback confirmCapturedCallback = nullptr;
	ConfirmCalibratedCallback confirmCalibratedCallback = nullptr;
	SendLatestFrameCallback sendLatestFrameCallback = nullptr;
	SendStoredFrameCallback sendStoredFrameCallback = nullptr;
	ConfirmTempSyncStateCallback confirmTempSyncStateCallback = nullptr;
	ConfirmMasterRestartCallback confirmMasterRestartCallback = nullptr;
};