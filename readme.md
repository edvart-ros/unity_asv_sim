# Unity ASV Simulator
This is a project meant to serve as a foundation for simulating unmanned/autonomous surface vessels in Unity with HDRP (High Definition Rendering Pipeline).

## Features
- ROS/ROS2 integration with Unity ROS-TCP-Connector (https://github.com/Unity-Technologies/ROS-TCP-Connector)
- High quality water system through Unity's HDRP Water System (https://blog.unity.com/engine-platform/new-hdrp-water-system-in-2022-lts-and-2023-1)
- Physics support for floating bodies and vehicles (hydrostatics and hydrodynamics)
- Modular physics system with hot-swappable implementations
- Simulation of common USV sensors, including 2D/3D LiDAR, RGB+D Camera, IMU and Odometry
- Simple modular propulsion system
- All sensors publish data to the ROS network
- Thrusters subscribe to ROS topics for force and direction commands
- The joy and usability of Unity!

## System Requirements
For running the simulator itself, see the official Unity HDRP system requirements: https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@17.0/manual/System-Requirements.html

For ROS compatability, the system must (obviously) have some way of running ROS. Unity's ROS-TCP-Connector is used as the interface between ROS and Unity. See the official documentation and tutorials at https://github.com/Unity-Technologies/ROS-TCP-Connector for more information. ROS 2 Humble and ROS 2 Iron has been tested, but the simulator should be compatible with any ROS version supported by the ROS-TCP-Endpoint.


The simulator has been tested on Windows 11 (with Robostack or Docker), MacOS Sierra (not tested with ROS) and Ubuntu 24.04 (native). 



## Demonstration
https://github.com/edvart-ros/unity_asv_sim/assets/94528774/35275add-04a7-4d5d-b1de-eafd5a51cb38

https://github.com/edvart-ros/unity_asv_sim/assets/94528774/98dcea8f-6421-42f1-b26a-0a4edf289c9d

![bilde](https://github.com/edvart-ros/unity_asv_sim/assets/94528774/e7928355-eacf-41d1-8bfe-dcd7cc979207)

https://github.com/edvart-ros/unity_asv_sim/assets/94528774/1ded79fc-41ba-4e21-8802-9b5ca1700b31


https://github.com/edvart-ros/unity_asv_sim/assets/94528774/3e1fb73f-ca62-445d-928d-54aebd81ce08

![Rviz2](https://github.com/edvart-ros/unity_asv_sim/assets/94528774/64f98b1c-11b4-4faf-a298-57d26e832072)
