# HOLOPORTATION: Live 3D Object Capture and Transmission for CSA’s AR Astronaut Training

**By: Roxanne Archambault, Alex Turianskyj, Aleksej Dejanov, and Anh Tu Nguyen**

This capstone project is an **open-source academic implementation** of real-time 3D point cloud reconstruction and transmission, inspired by Microsoft’s concept of “Holoportation.”

## Overview

Holoportation is a system that enables real-time 3D point cloud capture, transmission, and rendering of everything within a defined zone surrounded by RGBD cameras. This project enhances astronaut training using augmented reality (AR) by transmitting real-time point cloud data to Microsoft HoloLens 2 headsets. The system processes depth and color data from multiple Orbbec Femto Bolt cameras, and transmits it in real-time, allowing trainers to include physical objects in remote training without prior modelling.

## Features

- **Real-Time Point Cloud Transmission**: Transmits a live point cloud of everything within the camera-surrounded zone.
- **Document Detection**: Detects documents placed within the transmission zone and captures them in high resolution.
- **AR Integration**: Displays the transmitted point cloud in augmented reality environments using Microsoft HoloLens 2.
- **Open-Source**: Fully open-source for academic and research purposes.

## Technologies Used

- **Programming Languages**: Python, C#, C++
- **Frameworks and Libraries**:
  - OpenCV for image processing
  - OpenGL for 3D rendering
  - Unity for AR visualization
  - YOLO-World for document detection
  - MixedReality-WebRTC for HoloLens communication
- **Hardware**:
  - Orbbec Femto Bolt depth cameras
  - Microsoft HoloLens 2

## Getting Started

### Prerequisites

- **Hardware**:
  - Orbbec Femto Bolt depth camera(s)
  - Microsoft HoloLens 2 (optional for AR visualization)
- **Software**:
  - Windows 10 or later
  - Visual Studio 2019 or later
  - Unity 2022 or later
  - Python 3.8 or later
- **Dependencies**:
  - Install the required Python packages listed in `DocDetect/requirements.txt`
  - Install the Azure Kinect SDK

### Installation

1. **Clone the Repository**:
   ```bash
   git clone https://github.com/alex8ndr/Holoportation.git
   cd Holoportation
   ```

2. **Set Up Python Environment**:
   ```bash
   cd DocDetect
   python -m venv venv
   source venv/bin/activate  # On Windows: venv\Scripts\activate
   pip install -r requirements.txt
   ```

3. **Build LiveScan3D**:
   - Open `LiveScan3D/LiveScan.sln` in Visual Studio.
   - Build the solution in Release mode.

4. **Run the System**:
   - Start the LiveScan3D server by running `LiveScanServer.exe` from `LiveScan3D/bin/`.
   - Run the `DocDetect` Python script for document detection and transmission.
   - Launch the Unity project for AR visualization if using HoloLens 2.

5. **Test the Setup**:
   - Use the provided test files in `LiveScan3D/` to verify the system.

## Acknowledgments

[1] Kowalski, M.; Naruniec, J.; Daniluk, M., “LiveScan3D: A Fast and Inexpensive 3D Data Acquisition  System for Multiple Kinect v2 Sensors,” in 3D Vision (3DV), 2015 International Conference on, Lyon, France, 2015.

[2] Cheng, T.; Song, L.; Ge, Y.; Liu, W.; Wang, X.; Shan, Y., “YOLO- World: Real Time Open-Vocabulary Object Detection,” Proc. IEEE Conf. Computer Vision  and Pattern Recognition (CVPR), 2024.

[3] Microsoft, "MixedReality-WebRTC," GitHub repository, Sep. 2020. [Online]. Available: https://github.com/microsoft/MixedReality-WebRTC.

With thanks to the Shared Reality Lab for access to their facilities and HoloLens devices, and to the Canadian Space Agency for providing the Orbbec Femto Bolt cameras. We also thank Prof. Jeremy Cooperstock and Mr. Stéphane Rondeau for their guidance throughout the project.
