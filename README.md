# VisionGuard — Intelligent Camera Human Detection

<p align="center">
  <img src="https://readme-typing-svg.demolab.com?font=JetBrains+Mono&weight=700&size=26&duration=2800&pause=900&color=00D4FF&center=true&vCenter=true&width=650&lines=VisionGuard;Real-Time+Camera+Monitoring;AI-Powered+Human+Detection" alt="VisionGuard animated heading" />
</p>

<p align="center">
  A Windows desktop application for IP camera monitoring and AI-assisted human detection, built with C# and .NET.
</p>

<p align="center">
  <a href="https://github.com/Abhishek400217/VisionGuard">
    <img src="https://img.shields.io/badge/Project-VisionGuard-00D4FF?style=for-the-badge&logo=shield&logoColor=white" alt="VisionGuard"/>
  </a>
  <img src="https://img.shields.io/badge/C%23-.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt="C# and .NET"/>
  <img src="https://img.shields.io/badge/AI-YOLO_ONNX-111827?style=for-the-badge&logo=onnx&logoColor=white" alt="YOLO ONNX"/>
  <img src="https://img.shields.io/badge/Platform-Windows-0078D4?style=for-the-badge&logo=windows&logoColor=white" alt="Windows"/>
</p>

<p align="center">
  <a href="#-overview">Overview</a> •
  <a href="#-features">Features</a> •
  <a href="#-technology-stack">Tech Stack</a> •
  <a href="#-getting-started">Getting Started</a> •
  <a href="#-roadmap">Roadmap</a>
</p>

---

## 🎯 Overview

**VisionGuard** explores the integration of IP camera streaming and computer vision in a Windows desktop application. It combines live camera footage with YOLO-based human detection to support real-time visual monitoring.

The project focuses on connecting camera-streaming infrastructure with AI inference in a desktop workflow, providing a foundation for further surveillance and monitoring research.

### Why VisionGuard?

- **Live monitoring:** Work with an IP camera stream using RTSP.
- **AI-assisted detection:** Use YOLO ONNX models for human detection.
- **Windows desktop experience:** Built with C# and .NET.
- **Extensible foundation:** Designed as a starting point for additional monitoring and detection capabilities.

## ✨ Features

| Feature | Description |
|---|---|
| 📹 IP camera streaming | Connect to a compatible camera through an RTSP stream. |
| 🧠 Human detection | Apply YOLO-based inference to identify human detections. |
| 🎯 Human-only filtering | Focus detection output on the human class. |
| 🖥️ Desktop interface | Windows Forms interface built with C#. |
| ⚡ Frame processing | Process camera frames for AI inference and visual output. |
| 🧩 ONNX model support | Include ONNX model files for the detection workflow. |

*Feature availability depends on the current application configuration, model, and connected camera.*

## 🛠️ Technology Stack

<p>
  <img src="https://img.shields.io/badge/C%23-68217A?style=flat-square&logo=csharp&logoColor=white" alt="C#"/>
  <img src="https://img.shields.io/badge/.NET-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET"/>
  <img src="https://img.shields.io/badge/Windows_Forms-0078D4?style=flat-square&logo=windows&logoColor=white" alt="Windows Forms"/>
  <img src="https://img.shields.io/badge/YOLO-111827?style=flat-square&logoColor=white" alt="YOLO"/>
  <img src="https://img.shields.io/badge/ONNX-005CED?style=flat-square&logo=onnx&logoColor=white" alt="ONNX"/>
  <img src="https://img.shields.io/badge/RTSP-Camera_Streaming-0F766E?style=flat-square" alt="RTSP"/>
  <img src="https://img.shields.io/badge/Git-GitHub-181717?style=flat-square&logo=github&logoColor=white" alt="Git and GitHub"/>
</p>

- **Language:** C#
- **Framework:** .NET 8 for Windows
- **User interface:** Windows Forms
- **Video input:** RTSP-compatible IP camera
- **Detection:** YOLO model inference using ONNX assets
- **Version control:** Git and GitHub

## 🏗️ Application Workflow

```mermaid
flowchart TD
    A[IP Camera] -->|RTSP Stream| B[Video Stream Processing]
    B --> C[Frame Processing]
    C --> D[YOLO ONNX Inference]
    D --> E[Human-Class Filtering]
    E --> F[Detection Visualization]
    F --> G[Windows Desktop Interface]

    style A fill:#172554,color:#ffffff,stroke:#38bdf8
    style D fill:#312e81,color:#ffffff,stroke:#a78bfa
    style G fill:#064e3b,color:#ffffff,stroke:#34d399
```

The workflow illustrates the intended processing pipeline. Actual performance depends on camera resolution, model selection, hardware, and implementation details.

## 📂 Project Structure

```text
VisionGuard/
├── .github/                    # Repository configuration
├── MainForm.cs                 # Application logic
├── MainForm.Designer.cs        # Windows Forms UI layout
├── MainForm.resx               # Form resources
├── Program.cs                  # Application entry point
├── VisionGuard.csproj          # Project configuration
├── VisionGuard.sln             # Visual Studio solution
├── yolo26n.onnx                # ONNX model asset
├── yolo26s.onnx                # ONNX model asset
├── yolo26s.pt                  # PyTorch model asset
├── .gitignore
└── README.md
```

*The model files shown above are the assets present in the project directory. Use the model required by your application configuration.*

## 🚀 Getting Started

### Prerequisites

- Windows 10 or Windows 11
- Visual Studio 2022 with the appropriate .NET desktop development components
- .NET 8 SDK
- A compatible IP camera or RTSP stream
- Required NuGet dependencies specified by the project
- The model files required by the application

### 1. Clone the repository

```bash
git clone https://github.com/Abhishek400217/VisionGuard.git
cd VisionGuard
```

### 2. Open the solution

Open `VisionGuard.sln` in Visual Studio 2022.

### 3. Restore dependencies

From the project directory, run:

```bash
dotnet restore
```

### 4. Configure the camera

Configure the RTSP stream using the application's existing camera configuration. Keep credentials private and avoid committing connection strings containing usernames or passwords.

### 5. Build and run

```bash
dotnet build VisionGuard.sln
```

Alternatively, select the appropriate startup project in Visual Studio and press **F5**.

> **Note:** Successful execution requires the correct model assets, camera configuration, and dependencies. The exact setup may vary with your camera and local environment.

## 🖼️ Screenshots & Demo

Screenshots and a demonstration GIF can be added here once captured from the running application.

<p align="center">
  <em>Planned showcase: live camera feed · human detection output · monitoring interface</em>
</p>

To keep the repository authentic, use actual application screenshots rather than illustrative mockups.

## 🗺️ Roadmap

- [x] Windows desktop application foundation
- [x] RTSP camera-streaming workflow
- [x] YOLO ONNX model assets
- [x] Human-class detection filtering
- [ ] Improved diagnostics and structured logging
- [ ] Detection confidence and performance metrics
- [ ] Configurable camera and model settings
- [ ] Event history and optional alert workflows
- [ ] Documentation and demonstration video

*Roadmap items are planned improvements, not claims of existing functionality.*

## 🔒 Security & Privacy

- Never commit camera credentials, API keys, or sensitive configuration.
- Use trusted camera networks and restrict access to camera streams.
- Follow applicable privacy requirements when monitoring people.
- Review model and dependency licenses before redistribution.

## 🤝 Contributing

Contributions, bug reports, and suggestions are welcome.

1. Fork the repository.
2. Create a feature branch.
3. Make and test your changes.
4. Submit a pull request with a clear explanation of the change.

For significant changes, open an issue first to discuss the proposed approach.

## 👨‍💻 Author

**Abhishek Karande**

Software development project exploring computer vision, IP camera streaming, and AI-assisted monitoring.

- GitHub: [@Abhishek400217](https://github.com/Abhishek400217)
- Project repository: [VisionGuard](https://github.com/Abhishek400217/VisionGuard)

## 📄 License

No license has been specified yet. Add a `LICENSE` file before permitting others to reuse or redistribute the project.

---

<p align="center">
  <strong>VisionGuard</strong><br/>
  <sub>Connecting camera streams with intelligent visual monitoring.</sub>
</p>
