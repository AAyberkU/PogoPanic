AMS UI Soft Mask (c) Activity Media. All rights reserved.
https://ams.sorialexandre.tech/ui-soft-mask
	 
Description
  UI Soft Mask is a definitive solution to easily get soft masks for UI in Unity.
	
Features
  - Soft Mask capability;
  - Fall off & Opacity control;
  - Custom Shader Support (Hand-coded / Amplify Shader / Shader Graph);

Supported Platforms
  - All platforms 

Minimum Requirements
  - Unity 5+

Installation
  - Import ‘AMS UI Soft Mask’ through “Package Manager/My Assets”; or if you have a downloaded package import it as a custom package through “Assets/Import Package/Custom Package..”.
  - It’s required to have Unity’s TextMeshPro package imported into your project;
  - For TextMeshPro shaders support please import the package present in the plugin folder at path: “Assets/AMS/UISoftMask/Resources/Packages/TMP_SoftMaskSupport.unitypackage”.
  - Note: For different Unity versions it might be better to add TMP support manually. Please check "3.3 Hand-coded Shader" at documentation for more details.

+---- Important Note ---------------------------------------------------------------------------------------------------------------------------------------------------------------------+
|                                                                                                                                                                                         |
|  To ensure AMS UI Soft Mask shaders are included in build make sure to add them to the ‘Always Included Shaders’ list at menu “Project Settings > Graphics > Always Included Shaders”.  |
|  For your convenience, we’ve added a menu item at “Window/AMS/UISoftMask/Force Include Shaders (Project Settings)”.                                                                     |
|                                                                                                                                                                                         |
+-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+

Quick Guide
  1) Add a new "UI Soft Mask" component to your parent UI object;
  2) Hook your texture mask into "Mask" property;
  3) Make sure to mark the child UI objects as "maskable";
  4) If any object bellow the hierarchy uses a custom shader it is required to add UISoftMask support into that shader. Please check "Custom Shader Support" at documentation for more details.

Documentation
  https://ams.sorialexandre.tech/ui-soft-mask
	
Feedback & Support
  ams@sorialexandre.tech
  support@sorialexandre.tech (customers only)
  https://ams.sorialexandre.tech/contact