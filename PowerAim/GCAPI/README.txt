GCAPI / Titan Two support
=========================

PowerAim can drive a ConsoleTuner **Titan Two** (and the same-API Titan One /
classic CronusMax) through ConsoleTuner's native GCAPI library. That library is
PROPRIETARY (Copyright ISystem Ltda) and is NOT — and legally cannot be —
shipped with PowerAim. You must supply your own copy from your ConsoleTuner /
Gtuner installation.

How to enable it
----------------
1. Locate the GCAPI DLL from your ConsoleTuner install. Depending on version it
   is named either:
       gcapi.dll
       gcdapi.dll   (ConsoleTuner's official name for the same classic API)
2. Drop that file into THIS folder (it is copied next to the app on build) OR
   directly into the GCAPI\ folder next to the running executable
   (SmartReticle.exe / AI-Machine.exe).
3. In PowerAim, set the Gamepad "Send mode" to "TitanTwo".

*** IMPORTANT: 64-BIT REQUIRED ***
PowerAim is a 64-bit (x64) application. Windows CANNOT load a 32-bit DLL into a
64-bit process — so a 32-bit GCAPI DLL will be detected but refuse to load, and
the Titan Two send mode will stay inactive (you'll see a "likely 32-bit" hint in
the gamepad diagnostics). The classic ConsoleTuner GCAPI DLL is 32-bit; a
reliable 64-bit build is not publicly distributed. If you only have a 32-bit
DLL, in-process support is not possible without a separate 32-bit bridge helper
(not yet shipped). Ask the maintainer if you need this.

Nothing is committed to the repository from this folder except this README —
the DLL is yours to provide and is git-ignored.
