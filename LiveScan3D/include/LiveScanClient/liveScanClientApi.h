#pragma once

#ifdef LIVESCANCLIENT_EXPORTS
#define LIVESCAN_API __declspec(dllexport)
#else
#define LIVESCAN_API __declspec(dllimport)
#endif

#include "objectUtils.h"

extern "C" {

	typedef void* LiveScanClientHandle;

	LIVESCAN_API LiveScanClientHandle CreateClient(int index, const char* serverIP);
	LIVESCAN_API void StartClient(LiveScanClientHandle handle);
	LIVESCAN_API void StopClient(LiveScanClientHandle handle);
	LIVESCAN_API void DestroyClient(LiveScanClientHandle handle);

	LIVESCAN_API void StartFrameCapture(LiveScanClientHandle handle);
	LIVESCAN_API void Calibrate(LiveScanClientHandle handle);
    LIVESCAN_API void SetSettings(LiveScanClientHandle handle, const KinectSettings* settings);
	LIVESCAN_API void RequestStoredFrame(LiveScanClientHandle handle);
	LIVESCAN_API void RequestLastFrame(LiveScanClientHandle handle);
	LIVESCAN_API void ReceiveCalibration(LiveScanClientHandle handle, const AffineTransform* transform);
	LIVESCAN_API void ClearStoredFrames(LiveScanClientHandle handle);
	LIVESCAN_API void EnableTemporalSync(LiveScanClientHandle handle, int syncOffset);
	LIVESCAN_API void DisableTemporalSync(LiveScanClientHandle handle);
	LIVESCAN_API void StartMaster(LiveScanClientHandle handle);
	LIVESCAN_API void RequestSyncJackState(LiveScanClientHandle handle);
}