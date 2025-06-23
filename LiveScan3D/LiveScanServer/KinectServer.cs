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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using System.Net.Sockets;
using System.Net;
using System.ComponentModel;
using System.Windows.Forms;
using System.Diagnostics;
using static KinectServer.NativeLiveScanClient;

namespace KinectServer
{
    public delegate void ClientListChangedHandler(List<NativeLiveScanClient> list);
    public class KinectServer
    {
        bool bWaitForSubToStart = false;

        //This lock prevents the user from enabeling/disabling the Temp Sync State while the cameras are in transition to another state.
        //When starting the server, all devices are already initialized, as the LiveScanClient can only connect with an initialized device
        bool allDevicesInitialized = true; 

        KinectSettings oSettings;
        SettingsForm fSettingsForm;
        MainWindowForm fMainWindowForm;
        object oClientLock = new object();
        object oFrameRequestLock = new object();

        List<NativeLiveScanClient> liveScanClients = new List<NativeLiveScanClient>();

        public event ClientListChangedHandler clientListChanged;

        public int nClientCount
        {
            get
            {
                int nClients;
                lock (oClientLock)
                {
                    nClients = liveScanClients.Count;
                }
                return nClients;
            }
        }

        public List<AffineTransform> lCameraPoses
        {
            get 
            {
                List<AffineTransform> cameraPoses = new List<AffineTransform>();
                lock (oClientLock)
                {
                    foreach (var client in liveScanClients)
                    {
                        cameraPoses.Add(client.oCameraPose);
                    }                    
                }
                return cameraPoses;
            }
            set
            {
                lock (oClientLock)
                {
                    for (int i = 0; i < liveScanClients.Count; i++)
                    {
                        liveScanClients[i].oCameraPose = value[i];
                    }
                }
            }
        }

        public List<AffineTransform> lWorldTransforms
        {
            get
            {
                List<AffineTransform> worldTransforms = new List<AffineTransform>();
                lock (oClientLock)
                {
                    foreach (var client in liveScanClients)
                    {
                        worldTransforms.Add(client.oWorldTransform);
                    }
                }
                return worldTransforms;
            }

            set
            {
                lock (oClientLock)
                {
                    for (int i = 0; i < liveScanClients.Count; i++)
                    {
                        liveScanClients[i].oWorldTransform = value[i];
                    }
                }
            }
        }

        public bool bAllCalibrated
        {
            get
            {
                bool allCalibrated = true;
                lock (oClientLock)
                {
                    foreach (var client in liveScanClients)
                    {
                        if (!client.bCalibrated)
                        {
                            allCalibrated = false;
                            break;
                        }
                    }
                    
                }
                return allCalibrated;
            }
        }

        public KinectServer(KinectSettings settings)
        {
            this.oSettings = settings;
        }

        public void SetSettingsForm(SettingsForm settings)
        {
            fSettingsForm = settings;
        }

        public void SetMainWindowForm(MainWindowForm main)
        {
            fMainWindowForm = main;
        }

        public SettingsForm GetSettingsForm()
        {
            return fSettingsForm;
        }

        private void ClientListChanged()
        {
            if (clientListChanged != null)
            {
                clientListChanged(liveScanClients);
            }
        }

        public void LaunchClients(uint count)
        {
            // Start multiple instances of LiveScanClient
            for (int i = 0; i < count; i++)
            {
                var client = new NativeLiveScanClient(i);
                liveScanClients.Add(client);

                // Set callbacks so the client can call server methods directly
                client.SetSendSerialNumberCallback();
                client.SetConfirmCapturedCallback(OnConfirmCaptured);
                client.SetConfirmCalibratedCallback(OnConfirmCalibrated);
                client.SetSendLatestFrameCallback();
                client.SetSendStoredFrameCallback();
                client.SetConfirmTempSyncStateCallback(OnConfirmTempSyncState);
                client.SetConfirmMasterRestartCallback(OnConfirmMasterRestart);
                client.Start();
                
                // Send settings
                client.SetSettings(oSettings);
            }

            ClientListChanged();
        }

        public void StopServer()
        {
            // Ensure all LiveScanClients are terminated
            foreach (var client in liveScanClients)
            {
                client.Stop();
            }
        }

        public void CaptureSynchronizedFrame()
        {
            lock (oClientLock)
            {
                foreach (var client in liveScanClients)
                {
                    client.StartFrameCapture();
                }
            }

            //Wait till frames captured
            bool allGathered = false;
            while (!allGathered)
            {
                allGathered = true;

                lock (oClientLock)
                {
                    foreach (var client in liveScanClients)
                    {
                        if (!client.bFrameCaptured)
                        {
                            allGathered = false;
                            break;
                        }
                    }
                }
            }
        }

        public void Calibrate()
        {
            lock (oClientLock)
            {
                foreach (var client in liveScanClients)
                {
                    client.Calibrate();
                }
            }
        }

        public void SendSettings()
        {
            lock (oClientLock)
            {
                foreach (var client in liveScanClients)
                {
                    client.SetSettings(oSettings);
                }
            }
        }

        public void SendCalibrationData()
        {
            lock (oClientLock)
            {
                foreach (var client in liveScanClients)
                {
                    client.ReceiveCalibration();
                }
            }
        }

        /// <summary>
        /// Enable temporal sync by assigning roles to each connected device
        /// </summary>
        public void EnableTemporalSync()
        {
            lock (oClientLock)
            {
                allDevicesInitialized = false;
                bWaitForSubToStart = true;

                // Sort clients by their serial numbers (ascending)
                List<NativeLiveScanClient> sortedClients = liveScanClients
                    .Where(c => !string.IsNullOrEmpty(c.serialNumber))
                    .OrderBy(c => c.serialNumber, StringComparer.Ordinal)
                    .ToList();

                if (sortedClients.Count == 0)
                    return;

                // First client becomes MASTER
                NativeLiveScanClient masterClient = sortedClients[0];
                masterClient.EnableTemporalSync((int)eTempSyncConfig.MASTER, 0);

                // All others become SUBORDINATE
                for (int i = 1; i < sortedClients.Count; i++)
                {
                    sortedClients[i].EnableTemporalSync((int)eTempSyncConfig.SUBORDINATE, i);
                }

                // Any clients not in sortedClients can be disabled or set to STANDALONE
                var standaloneClients = liveScanClients.Except(sortedClients);
                foreach (var c in standaloneClients)
                {
                    c.EnableTemporalSync((int)eTempSyncConfig.STANDALONE, 0);
                }
            }
        }

        /// <summary>
        /// When a client has send its Device Sync State, we check if we have the right number of each sync mode
        /// </summary>
        public void SendTemporalSyncData()
        {
            lock (oClientLock)
            {
                int masterCount = 0;
                int subordinateCount = 0;

                // First we check if we have recieved the Device Sync State of all Devices
                // If not, we return and the next client who confirms their state starts this function again
                foreach (var client in liveScanClients)
                {
                    if (client.currentDeviceTempSyncState == eTempSyncConfig.MASTER)
                    {
                        masterCount++;
                    }

                    if (client.currentDeviceTempSyncState == eTempSyncConfig.SUBORDINATE)
                    {
                        subordinateCount++;
                    }

                    if (client.currentDeviceTempSyncState == eTempSyncConfig.UNKNOWN)
                    {
                        return;
                    }
                }

                // Check if we have exactly one master and at least one subordinate
                if (masterCount != 1 || subordinateCount < 1)
                {
                    // If not, we show a error message and disable the temporal sync
                    fMainWindowForm?.SetStatusBarOnTimer("Temporal Sync cables not connected properly", 5000);
                    fSettingsForm?.ActivateTempSyncEnableButton();
                    return;
                }
            }
        }


        /// <summary>
        /// Sets all clients as standalone
        /// </summary>
        public void DisableTemporalSync()
        {
            allDevicesInitialized = false;

            lock (oClientLock)
            {
                foreach (var client in liveScanClients)
                {
                    client.DisableTemporalSync();
                }
            }
        }


        public void ConfirmTemporalSyncDisabled()
        {
            if (bWaitForSubToStart)
                return;

            lock (oClientLock)
            {
                foreach (var client in liveScanClients)
                {
                    if (client.currentClientTempSyncState != eTempSyncConfig.STANDALONE)
                    {
                        return;
                    }
                }
            }

            allDevicesInitialized = true;
        }

        /// <summary>
        /// Called when a sub client has started. This checks if all sub clients have already started. If yes, we initialize the master
        /// </summary>
        public void CheckForMasterStart()
        {
            if (!bWaitForSubToStart)
            {
                return;
            }

            bool allSubsStarted = true;

            lock (oClientLock)
            {
                foreach (var client in liveScanClients)
                {
                    if (!client.bSubStarted && client.currentClientTempSyncState == eTempSyncConfig.SUBORDINATE)
                    {
                        allSubsStarted = false;
                        break;
                    }
                }

                bWaitForSubToStart = false;

                if (allSubsStarted)
                {
                    foreach (var client in liveScanClients)
                    {
                        if(client.currentClientTempSyncState == eTempSyncConfig.MASTER)
                        {
                            client.StartMaster();
                            return;
                        }
                    }
                }
            }           
        }

        /// <summary>
        /// Tells the server that it is now ok to start recieving user changes again
        /// </summary>
        public void MasterSuccessfullyRestarted()
        {
            allDevicesInitialized = true; 
        }

        public bool GetAllDevicesInitialized()
        {
            return allDevicesInitialized;
        }

        public bool GetStoredFrame(ref List<List<byte>> lFramesRGB, ref List<List<float>> lFramesVerts)
        {
            bool bNoMoreStoredFrames;

            int count = lFramesRGB.Count;

            // Ensure list capacity
            if (lFramesVerts.Capacity < count)
                lFramesVerts.Capacity = count;
            if (lFramesRGB.Capacity < count)
                lFramesRGB.Capacity = count;

            lFramesRGB.Clear();
            lFramesVerts.Clear();
            
            lock (oFrameRequestLock)
            {
                //Request frames
                lock (oClientLock)
                {
                    foreach (var client in liveScanClients)
                    {
                        client.bStoredFrameReceived = false;
                        client.bNoMoreStoredFrames = false;
                        client.RequestStoredFrame();
                    }
                }

                //Wait till frames received
                bool allGathered = false;
                bNoMoreStoredFrames = false;
                while (!allGathered)
                {
                    allGathered = true;                
                    lock (oClientLock)
                    {
                        foreach (var client in liveScanClients)
                        {
                            if (!client.bStoredFrameReceived)
                            {
                                allGathered = false;
                                break;
                            }

                            if (client.bNoMoreStoredFrames)
                                bNoMoreStoredFrames = true;
                        }
                    }
                }

                //Store received frames
                lock (oClientLock)
                {
                    foreach (var client in liveScanClients)
                    {
                        lFramesRGB.Add(client.lFrameRGB);
                        lFramesVerts.Add(client.lFrameVerts);
                    }
                }
            }

            if (bNoMoreStoredFrames)
                return false;
            else
                return true;
        }

        public void GetLatestFrame(ref List<List<byte>> lFramesRGB, ref List<List<float>> lFramesVerts)
        {
            int count = lFramesRGB.Count;

            // Ensure list capacity
            if (lFramesVerts.Capacity < count)
                lFramesVerts.Capacity = count;
            if (lFramesRGB.Capacity < count)
                lFramesRGB.Capacity = count;

            lFramesRGB.Clear();
            lFramesVerts.Clear();

            lock (oFrameRequestLock)
            {
                //Request frames
                lock (oClientLock)
                {
                    foreach (var client in liveScanClients)
                    {
                        client.bLatestFrameReceived = false;
                        client.RequestLastFrame();
                    }
                }

                //Wait till frames received
                bool allGathered = false;

                while (!allGathered)
                {
                    allGathered = true;

                    lock (oClientLock)
                    {
                        foreach (var client in liveScanClients)
                        {
                            if (!client.bLatestFrameReceived)
                            {
                                allGathered = false;
                                break;
                            }
                        }
                    }
                }

                //Store received frames
                lock (oClientLock)
                {
                    foreach(var client in liveScanClients)
                    {
                        lFramesRGB.Add(client.lFrameRGB);
                        lFramesVerts.Add(client.lFrameVerts);
                    }
                }
            }
        }

        public void ClearStoredFrames()
        {
            lock (oClientLock)
            {
                foreach (var client in liveScanClients)
                {
                    client.ClearStoredFrames();
                }
            }
        }

        private void OnConfirmCaptured(int clientIndex)
        {
            liveScanClients[clientIndex].bFrameCaptured = true;
        }

        private void OnConfirmCalibrated(int clientIndex, int markerId, float[] R, float[] t)
        {
            NativeLiveScanClient client = liveScanClients[clientIndex];

            client.oWorldTransform = new AffineTransform
            {
                R = new float[3, 3],
                t = new float[3]
            };

            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    client.oWorldTransform.R[i, j] = R[i * 3 + j];

            for (int i = 0; i < 3; i++)
                client.oWorldTransform.t[i] = t[i];

            client.oCameraPose.R = client.oWorldTransform.R;
            for (int i = 0; i < 3; i++)
            {
                client.oCameraPose.t[i] = 0.0f;
                for (int j = 0; j < 3; j++)
                {
                    client.oCameraPose.t[i] += client.oWorldTransform.t[j] * client.oWorldTransform.R[i, j];
                }
            }

            client.bCalibrated = true;
            client.UpdateSocketState();
            ClientListChanged();
        }

        private void OnConfirmTempSyncState(int clientIndex, eTempSyncConfig state)
        {
            NativeLiveScanClient client = liveScanClients[clientIndex];
            client.currentClientTempSyncState = state;

            if (state == eTempSyncConfig.SUBORDINATE)
            {
                client.bSubStarted = true;
                client.bMasterStarted = false;
                CheckForMasterStart();
            }
            else if (state == eTempSyncConfig.MASTER)
            {
                client.bSubStarted = false;
            }
            else if (state == eTempSyncConfig.STANDALONE)
            {
                client.bSubStarted = false;
                client.bMasterStarted = false;
                ConfirmTemporalSyncDisabled();
            }

            client.UpdateSocketState();
        }

        private void OnConfirmMasterRestart(int clientIndex)
        {
            MasterSuccessfullyRestarted();
        }
    }
}
