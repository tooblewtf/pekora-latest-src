## Why is Marine detected as malicious?

The Marine launcher is detected as malicious due to the executable being unsigned. Signed applications are (mostly) exempt from anti-virus detections. 

However, signed applications also cost a (frankly absurd) amount of money, **roughly $400+/year!**

Since Marine does not make any revenue in any way, we cannot afford to sign our launcher. Therefore, anti-viruses tend to be more sensitive towards our launcher.

### Pictures of Marine getting detected as malicious

#### Browsers
![Chrome detecting Marine launcher as dangerous](/img/instructions/mpc-hc64_7rruXWAmDd.png)
![Chrome detecting Marine launcher as dangerous](/img/instructions/3DlQ7incjh.png)
![Firefox detecting Marine launcher as dangerous](/img/instructions/firefox_VHDyU4CeYf.png)

#### All anti-viruses

![](/img/instructions/mpc-hc64_A6nhOC66qC.png)

#### Windows Defender

![](/img/instructions/mpc-hc64_lpo91sY2ap.png)

## Solutions

### Chrome

1. Go to any game on Marine and download the Marine launcher by clicking **"Play"** then **"Download and Install Marine"**
2. Chrome will tell you that ProjectXPlayerLauncher.exe is a dangerous download, and will block it. This is due to our launcher not being signed
   * ![Chrome detecting Marine launcher as dangerous](/img/instructions/mpc-hc64_7rruXWAmDd.png)
3. Go to your Chrome download tab by opening a new page and typing **chrome://download** and hitting enter
   * ![](/img/instructions/vmware_4TFVkEtdIs.png)
4. You should see the ProjectXPlayerLauncher.exe, and you should see that Chrome has blocked it due to the file being "dangerous"
   * ![](/img/instructions/3DlQ7incjh.png)
5. Click the three dots next to the X. Then click **"Download dangerous file"**
   * ![](/img/instructions/466JWvddy2.png)
6. A pop-up will appear saying "This file could harm your device." Click **"Download dangerous file"**
   * ![](/img/instructions/mpc-hc64_yfTUvicVYF.png)
7. The file should now download, and you can open it to install Marine.

### Firefox

1. Go to any game on Marine and download the Marine launcher by clicking **"Play"** then **"Download and Install Marine"**
2. Firefox will say the file contains virus or malware. This is, again, due to our launcher not being signed.
   * ![](/img/instructions/floorp_L7p2HadJgi.png)
3. Click "Show all downloads" The download window should appear. Right-click on ProjectXPlayerLauncher.exe, and click "Allow Download"
   * ![](/img/instructions/moJvVv3SHn.png)
4. A pop-up will appear, telling you the file contains a virus. Click "Allow Download" again
   * ![](/img/instructions/mpc-hc64_Oef8pMP4it.png)
5. The file should now download, and you can open it to install Marine.

### Windows

On Windows, the best solution is to disable your anti-virus while installing Marine, then set an exclusion path for the **%localappdata%/ProjectX** folder. You can then re-enable your anti-virus.

#### Microsoft Defender SmartScreen (Windows protected your PC)

No matter the anti-virus, due to the launcher not being signed, SmartScreen will alert you and tell you that it prevented an unrecognised app from starting. This is easy to solve:

1. Click the "More info" text
   * ![](/img/instructions/yEUKxZXlYg.png)
2. Click "Run anyway" at the bottom
   * ![](/img/instructions/rfZem3Lk2w.png)

#### Windows Security

1. Search for **Windows Security** and open it
   * ![](/img/instructions/vmware_jXRpAMx9Pk.png)
2. Once it opens, click **"Virus & threat protection"** on either the sidebar or one of the tiles
   * ![](/img/instructions/5vwfs7WVwS.png)
3. Under "Virus & threat protection settings", click **"Manage settings"**
   * ![](/img/instructions/r1AzSyBAcv.png)
4. Under "Real-time protection", click the **"On"** toggle to turn off your antivirus **temporarily.** Windows Security will automatically turn real-time protection back on after a short amount of time
   * ![](/img/instructions/1pp4kvjdrQ.png)
   * User Account Control will ask you if you want to allow the app to make changes. Click **"Yes"**
   * ![](/img/instructions/biykjU90CK.png)
5. Windows Security should now show the message "Real-time protection is off, leaving your device vulnerable."
   * ![](/img/instructions/mpc-hc64_s21Qbn8nfw.png)
6. Minimize Windows Security and go to any game page and download the Marine launcher by clicking **"Play"** then **"Download and Install Marine"**
7. Go back to Windows Security and scroll down until you find "Exclusions". Under "Exclusions", click **"Add or remove exclusions"**
   * ![](/img/instructions/pNIucxXFPR.png)
   * User Account Control will ask you if you want to allow the app to make changes. Click **"Yes"**
   * ![](/img/instructions/biykjU90CK.png)
8. Click on **"Add an exclusion"**, and then click on **"Folder"**
   * ![](/img/instructions/rj7LI67mUO.png)
9.  An explorer window will now open. In the address bar, type in **"%localappdata%/ProjectX"**, and press Enter
   * ![](/img/instructions/mpc-hc64_v9z03kuNrt.png)
10.  The Marine folder in Local AppData should now appear with a "Downloads" and "Versions" folder. If not, you need to install Marine. Then, click **"Select Folder"**
    * ![](/img/instructions/mpc-hc64_U4ADLQTCI0.png)
11.  A new folder exclusion entry should now appear with the directory "C:\Users\yourusername\AppData\Local\ProjectX"
    * ![](/img/instructions/mpc-hc64_GUsyxdat10.png)
12.  In the sidebar, click on the arrow in the top-left to go back a page.
    * ![](/img/instructions/Photos_L8eFKgsP4h.png)
13.  Turn your Real-time protection back on by clicking the **"Off"** toggle under Real-time protection.
    * ![](/img/instructions/Photos_kNhSohEwtn.png)
    *  User Account Control will ask you if you want to allow the app to make changes. Click **"Yes"**
    * ![](/img/instructions/biykjU90CK.png)
14. You can now play any game on Marine by clicking **"Play"** on a game page.