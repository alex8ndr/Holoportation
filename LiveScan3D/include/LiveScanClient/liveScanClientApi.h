#pragma once

#ifdef LIVESCANCLIENT_EXPORTS
#define LIVESCAN_API __declspec(dllexport)
#else
#define LIVESCAN_API __declspec(dllimport)
#endif

#include "objectUtils.h"

extern "C" {

	typedef void* LiveScanClientHandle;

	// Server to client (inbound) calls
	LIVESCAN_API LiveScanClientHandle CreateClient(int index);
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
	LIVESCAN_API void EnableTemporalSync(LiveScanClientHandle handle, int tempSyncState, int syncOffset);
	LIVESCAN_API void DisableTemporalSync(LiveScanClientHandle handle);
	LIVESCAN_API void StartMaster(LiveScanClientHandle handle);

	// Client to server (outbound) calls
	LIVESCAN_API void SetSendSerialNumberCallback(LiveScanClientHandle handle, SendSerialNumberCallback cb);
	LIVESCAN_API void SetConfirmCapturedCallback(LiveScanClientHandle handle, ConfirmCapturedCallback cb);
	LIVESCAN_API void SetConfirmCalibratedCallback(LiveScanClientHandle handle, ConfirmCalibratedCallback cb);
	LIVESCAN_API void SetSendLatestFrameCallback(LiveScanClientHandle handle, SendLatestFrameCallback cb);
	LIVESCAN_API void SetSendStoredFrameCallback(LiveScanClientHandle handle, SendStoredFrameCallback cb);
	LIVESCAN_API void SetConfirmTempSyncStateCallback(LiveScanClientHandle handle, ConfirmTempSyncStateCallback cb);
	LIVESCAN_API void SetConfirmMasterRestartCallback(LiveScanClientHandle handle, ConfirmMasterRestartCallback cb);
}