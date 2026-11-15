import os
import re
import subprocess
import sys
import time
from pathlib import Path

sdk = Path(r"C:/Program Files (x86)/Android/android-sdk")
adb = str(sdk / "platform-tools/adb.exe")
emulator = str(sdk / "emulator/emulator.exe")
dotnet = r"C:/Program Files/dotnet/dotnet.exe"
root = Path(__file__).resolve().parent.parent
admin = len(sys.argv) > 1 and sys.argv[1] == "Admin"
port, avd = (5556, "LimosDispatch_Admin_API30") if admin else (5554, "pixel_5_-_api_30_0")
serial = f"emulator-{port}"

def output(*args):
    return subprocess.run(args, capture_output=True, text=True, check=False, timeout=15).stdout.strip()

def device_status():
    return next((line.split()[1] for line in output(adb, "devices").splitlines()
                 if line.split() and line.split()[0] == serial and len(line.split()) > 1), None)

if device_status() is None:
    subprocess.Popen([emulator, "-avd", avd, "-port", str(port)],
                     creationflags=subprocess.DETACHED_PROCESS,
                     stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
deadline = time.monotonic() + 180
while not (device_status() == "device" and output(adb, "-s", serial, "shell", "getprop", "sys.boot_completed") == "1"):
    if time.monotonic() >= deadline:
        sys.exit(f"{avd} did not finish booting ({serial}).")
    time.sleep(2)
actual = output(adb, "-s", serial, "shell", "getprop", "ro.boot.qemu.avd_name")
if actual and actual != avd:
    sys.exit(f"{serial} belongs to {actual}, not {avd}.")
print(f"Building and installing on {avd} ({serial})...", flush=True)
project = root / "src/LimosDispatch.Mobile/LimosDispatch.Mobile.csproj"
env = {**os.environ, "ANDROID_SERIAL": serial}
subprocess.run([dotnet, "build", str(project), "-t:Build,Install", "-f", "net10.0-android",
                f"-p:AdbTarget=-s {serial}", "-p:MobileTargetFrameworks=net10.0-android",
                "-p:RuntimeIdentifier=android-x64", "-p:_XamarinBuildDownloadMasterDependsOnTargets=",
                "--no-restore", "-v:minimal"], cwd=root, env=env, check=True)
resolved = output(adb, "-s", serial, "shell", "cmd", "package", "resolve-activity", "--brief",
                  "-a", "android.intent.action.MAIN", "-c", "android.intent.category.LAUNCHER",
                  "com.limosdispatch.mobile").splitlines()
if not resolved or not re.fullmatch(r"com\.limosdispatch\.mobile/\S+", resolved[-1]):
    sys.exit(f"Android could not locate the installed app on {serial}.")
launch = subprocess.run([adb, "-s", serial, "shell", "am", "start", "-W", "-n", resolved[-1]],
                        capture_output=True, text=True, timeout=60)
print(launch.stdout, end="")
if launch.returncode or "Status: ok" not in launch.stdout:
    sys.exit(launch.stderr or f"Android could not launch LimosDispatch on {serial}.")
