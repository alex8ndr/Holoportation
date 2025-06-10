#include "LiveScanClient.h"
#include "LiveScanClientApi.h"
#include <thread>
#include <memory>
#include <map>
#include <locale>
#include <codecvt> 

/*
* Server to client (inbound) calls
*/
LiveScanClientHandle CreateClient(int index, const char* serverIP)
{
	auto* wrapper = new LiveScanClientWrapper();

	// Convert const char* to std::wstring
	std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
	wrapper->serverIP = converter.from_bytes(serverIP);

	wrapper->client = std::make_unique<LiveScanClient>(index);
	wrapper->client->m_pWrapper = wrapper;
	return wrapper;
}

void StartClient(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->thread = std::thread([wrapper]() {
		wrapper->client->Run(wrapper->serverIP);
		});
}

void StopClient(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->RequestExit();

	if (wrapper->thread.joinable())
		wrapper->thread.join();
}

void DestroyClient(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	delete wrapper;
}

void StartFrameCapture(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->StartFrameCapture();
}

void Calibrate(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->Calibrate();
}

void SetSettings(LiveScanClientHandle handle, const KinectSettings* settings)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper || !settings) return;

	wrapper->client->SetSettings(*settings);
}

void RequestStoredFrame(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->RequestStoredFrame();
}

void RequestLastFrame(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->RequestLastFrame();
}

void ReceiveCalibration(LiveScanClientHandle handle, const AffineTransform* transform)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper || !wrapper->client || !transform)
		return;

	wrapper->client->ReceiveCalibration(*transform);
}

void ClearStoredFrames(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->ClearStoredFrames();
}

void EnableTemporalSync(LiveScanClientHandle handle, int syncOffset)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->EnableTemporalSync(syncOffset);
}

void DisableTemporalSync(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->DisableTemporalSync();
}

void StartMaster(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->StartMaster();
}

void RequestSyncJackState(LiveScanClientHandle handle)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (!wrapper) return;

	wrapper->client->RequestSyncJackState();
}

/*
* Client to server (outbound) calls
*/
void SetConfirmCapturedCallback(LiveScanClientHandle handle, ConfirmCapturedCallback cb)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (wrapper)
		wrapper->confirmCapturedCallback = cb;
}

void SetConfirmCalibratedCallback(LiveScanClientHandle handle, ConfirmCalibratedCallback cb)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (wrapper)
		wrapper->confirmCalibratedCallback = cb;
}

void SetSendLatestFrameCallback(LiveScanClientHandle handle, SendLatestFrameCallback cb)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (wrapper)
		wrapper->sendLatestFrameCallback = cb;
}

void SetSendStoredFrameCallback(LiveScanClientHandle handle, SendStoredFrameCallback cb)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (wrapper)
		wrapper->sendStoredFrameCallback = cb;
}

void SetConfirmTempSyncStateCallback(LiveScanClientHandle handle, ConfirmTempSyncStateCallback cb)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (wrapper)
		wrapper->confirmTempSyncStateCallback = cb;
}

void SetConfirmMasterRestartCallback(LiveScanClientHandle handle, ConfirmMasterRestartCallback cb)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (wrapper)
		wrapper->confirmMasterRestartCallback = cb;
}

void SetSendDeviceSyncStateCallback(LiveScanClientHandle handle, SendDeviceSyncStateCallback cb)
{
	auto* wrapper = static_cast<LiveScanClientWrapper*>(handle);
	if (wrapper)
		wrapper->sendDeviceSyncStateCallback = cb;
}
