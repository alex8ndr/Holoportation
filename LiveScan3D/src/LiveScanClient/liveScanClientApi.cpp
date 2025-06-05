#include "LiveScanClient.h"
#include "LiveScanClientApi.h"
#include <thread>
#include <memory>
#include <map>

struct LiveScanClientWrapper {
	std::unique_ptr<LiveScanClient> client;
	std::thread thread;
	std::wstring serverIP;
	int index;

	LiveScanClientWrapper(int idx, const std::string& ip)
		: index(idx)
	{
		client = std::make_unique<LiveScanClient>(idx);
		serverIP = std::wstring(ip.begin(), ip.end());
	}
};

LiveScanClientHandle CreateClient(int index, const char* serverIP)
{
	auto* wrapper = new LiveScanClientWrapper(index, serverIP);
	return static_cast<LiveScanClientHandle>(wrapper);
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