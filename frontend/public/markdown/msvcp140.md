When installing Marine, some users might not have a prerequisite redistributable to run the Marine launcher. As a result, when launching Marine, you may get one of the following errors:

![](/img/instructions/mpc-hc64_ILSeBrhVM8.png)

![](/img/instructions/mpc-hc64_rxnkQCPTN8.png)

This happens when **Visual C++ Redistributable 2015-2019 or above** (2015-2022 work too) are not installed on your computer. Solving this issue is easy, though.

Download the version of Visual C++ Redistributable that works for you:

| Windows Versions        | Version | Download Link                                                                                    |
|-------------------------|---------|--------------------------------------------------------------------------------------------------|
| Windows 7 and above     | Latest  | https://aka.ms/vs/17/release/vc_redist.x86.exe                                                   |
| Windows Vista and below | 14.27   | https://www.filehorse.com/download-microsoft-visual-c-redistributable-package-32/56166/download/ |

Then, run the installer. After it is installed, you should be able to run the Marine Player without getting that bug anymore.