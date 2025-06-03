#pragma once

#ifdef LIVESCANCLIENT_EXPORTS
#define LIVESCAN_API __declspec(dllexport)
#else
#define LIVESCAN_API __declspec(dllimport)
#endif

extern "C" {

	typedef void* LiveScanClientHandle;

	LIVESCAN_API LiveScanClientHandle CreateClient(int index, const char* serverIP, bool headless);
	LIVESCAN_API void StartClient(LiveScanClientHandle handle);
	LIVESCAN_API void StopClient(LiveScanClientHandle handle);
	LIVESCAN_API void DestroyClient(LiveScanClientHandle handle);

}