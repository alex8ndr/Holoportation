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

	wrapper->thread = std::thread([=]() {
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