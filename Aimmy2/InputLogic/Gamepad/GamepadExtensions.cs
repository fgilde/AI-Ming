﻿using System.Collections.Concurrent;
using SharpDX.XInput;
using System.Runtime.InteropServices;
using Aimmy2.InputLogic.Contracts;
using InputLogic;

namespace Aimmy2.InputLogic
{
    public static class GamepadExtensions
    {
        private static ConcurrentDictionary<Controller, string> _cache = new();

        public static void Send(this IGamepadSender sender, GamepadEventArgs args, KeyPressState? pressState = null)
        {
            var syncState = GamepadSyncState.Paused;
            if (pressState != null)
            {
                args.IsPressed = pressState is KeyPressState.Down or KeyPressState.DownAndUp;
                args.Value = pressState is KeyPressState.Down or KeyPressState.DownAndUp ? 1f : 0f;
                if(pressState == KeyPressState.Up)
                    syncState = GamepadSyncState.Resume;
            }

            if(args.GamepadButton is not null)
            {
                sender.SetButtonState(args.GamepadButton.Value, args.IsPressed ?? false, syncState);
            }
            else if(args.GamepadSlider is not null)
            {
                var argsValue = args.Value * 255f ?? 0;
                sender.SetSliderValue(args.GamepadSlider.Value, (byte)argsValue, syncState);
            }
            else if(args.GamepadAxis is not null)
            {
                sender.SetAxisValue(args.GamepadAxis.Value, (short)(args.Value ?? 0), syncState);
            }

            if (pressState == null || pressState == KeyPressState.DownAndUp)
            {
                Task.Delay(MouseManager.GetRandomDelay()).ContinueWith(task => { sender.ResumeSync(); });
            }
        }

        public static string GetControllerId(this Controller controller)
        {
            if (_cache.TryGetValue(controller, out var res))
                return res;

            Guid xInputDevice = new Guid("EC87F1E3-C13B-4100-B5F7-8B84D54260CB");
            string deviceId = null;

            IntPtr deviceInfoSet = SetupDiGetClassDevs(ref xInputDevice, null, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
            if (deviceInfoSet == IntPtr.Zero)
                return null;

            try
            {
                SP_DEVINFO_DATA devInfoData = new SP_DEVINFO_DATA();
                devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

                for (uint i = 0; SetupDiEnumDeviceInfo(deviceInfoSet, i, ref devInfoData); i++)
                {
                    string deviceInstanceId = GetDeviceInstanceId(deviceInfoSet, ref devInfoData);
                    if (!string.IsNullOrEmpty(deviceInstanceId) && deviceInstanceId.Contains("VID") && deviceInstanceId.Contains("PID"))
                    {
                        deviceId = deviceInstanceId;
                        break;
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
            _cache.TryAdd(controller, deviceId);
            return deviceId;
        }

        private static string GetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA devInfoData)
        {
            IntPtr buffer = Marshal.AllocHGlobal(512);
            try
            {
                if (SetupDiGetDeviceInstanceId(deviceInfoSet, ref devInfoData, buffer, 512, out _))
                {
                    return Marshal.PtrToStringAuto(buffer);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return null;
        }


        // P/Invoke Definitions bleiben unverändert
        private const int DIGCF_PRESENT = 0x00000002;
        private const int DIGCF_DEVICEINTERFACE = 0x00000010;

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, string Enumerator, IntPtr hwndParent, int Flags);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, IntPtr DeviceInstanceId, uint DeviceInstanceIdSize, out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);
    }
}
