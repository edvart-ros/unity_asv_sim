# Unity ASV Simulator
This is a project meant to serve as a foundation for simulating unmanned/autonomous surface vessels in Unity with HDRP (High Definition Rendering Pipeline).

https://github.com/edvart-ros/unity_asv_sim/assets/94528774/35275add-04a7-4d5d-b1de-eafd5a51cb38

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

## Getting Started
### 1. Install Unity Hub & Editor
Follow the official instructions for installing the Unity Hub and Unity Editor on your machine (Windows, Mac or Linux): https://unity.com/download.

The project has been tested on and is currently configured for the 2023.2.20f1 Editor version, but it should also be compatible with newer versions of Unity, like Unity 6. 

If you install a version other than the one the repo is configured for, you may be given a warning about changing the editor version. We have changed editor version several times, and there are usually no issues with newer Unity versions. 

This project does not require a paid license, so just use the free one.

### 2. Clone the source code
Clone the source code of the Unity project in a suitable directory:

    cd <my_unity_projects_folder>
    git clone https://github.com/edvart-ros/unity_asv_sim.git

### 3. Add the project to Unity Hub
Open Unity Hub and click on "Add project from disk"

![image](https://github.com/edvart-ros/unity_asv_sim/assets/94528774/bb392f85-1678-42ac-b536-531f8b0050e6)


Locate the root directory of the project source code (e.g` <my_unity_projects_folder/unity_asv_sim/>`) and click "Open". This should add the project to your Projects list. 

### 4. Launch the project
You should now be able to launch the project and have a look around. To view a simple demo scene, open the scene located in `Assets/Scenes/MinimalScene/` by navigating through the project inspector in the editor.
![image](https://github.com/edvart-ros/unity_asv_sim/assets/94528774/c7bdcf44-9f95-4bbf-97d5-934bbbe6dd6f)

This scene is set up with a few floating objects, one of them being controllable via mouse + keyboard. Hit the play button, enter the Game window and try navigating the WAM-V around with your keyboard (WASD). The camera is configured to follow the WAM-V
and can be controlled via the mouse.

![image](https://github.com/edvart-ros/unity_asv_sim/assets/94528774/83a8bfac-f161-48b0-986c-15f184ecc1e5)


## 5. Familiarize yourself with the Unity environment
The scene consists of a set of "Game Object" which can be seen in the project hierarchy. Each game object has a "Transform" describing their pose, and "Components" are added to game objects to make them do things. 
You can also use game objects to structure your scene. In this scene, "Boats" is an empty game object which simply groups all the boats in the scene together. Further, the WAM-V vessel has several child objects which makes development and customization more convenient:

![image](https://github.com/edvart-ros/unity_asv_sim/assets/94528774/81155646-4244-45c2-9db4-652f86a41cec)

Select the WAM-V in the scene hierarchy and have a look at its components in the inspector:

![image](https://github.com/edvart-ros/unity_asv_sim/assets/94528774/5a493aa0-ebe5-416c-87a9-66db0edefa46)

As previously mentioned, the Tranform component simply holds the pose of the object, relative to the world origin or parent game object (the "Boats" game object in this case).
The Mesh filter, Mesh Renderer and Mesh collider are built-in Unity components that hold the object's 3D geometry, defines visual properties and enables collisions with other objects.
The Rigidbody component is another built-in component which defines the basic physical characteristics of the object, used by Unity's physics backend (PhysX).

The remaining components are custom scripts, written specifically for ASV simulation. `Submersion` and `Buoyancy` are required to make the WAM-V float, and `Ship Controller` enables keyboard controls of the WAM-V.

There are many more scripts set up in this scene, and in the project, and it's a good idea to have a look at what they do.




## Demonstration
https://github.com/edvart-ros/unity_asv_sim/assets/94528774/98dcea8f-6421-42f1-b26a-0a4edf289c9d

![bilde](https://github.com/edvart-ros/unity_asv_sim/assets/94528774/e7928355-eacf-41d1-8bfe-dcd7cc979207)

https://github.com/edvart-ros/unity_asv_sim/assets/94528774/1ded79fc-41ba-4e21-8802-9b5ca1700b31


https://github.com/edvart-ros/unity_asv_sim/assets/94528774/3e1fb73f-ca62-445d-928d-54aebd81ce08

![Rviz2](https://github.com/edvart-ros/unity_asv_sim/assets/94528774/64f98b1c-11b4-4faf-a298-57d26e832072)
