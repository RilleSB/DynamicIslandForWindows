using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace DynamicIslandPC
{
    public static class RefreshRateHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public short dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);
        private const int ENUM_CURRENT_SETTINGS = -1;

        public static int DetectedRefreshRate { get; private set; } = 144;

        public static void InitializeHighRefreshRate()
        {
            try
            {
                var dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm) && dm.dmDisplayFrequency > 30)
                {
                    DetectedRefreshRate = dm.dmDisplayFrequency;
                }
                else
                {
                    DetectedRefreshRate = 144;
                }

                Timeline.DesiredFrameRateProperty.OverrideMetadata(
                    typeof(Timeline),
                    new FrameworkPropertyMetadata { DefaultValue = (int?)DetectedRefreshRate }
                );

                Logger.Log($"High refresh rate animation clock initialized: {DetectedRefreshRate} Hz");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to configure high refresh rate for animations", ex);
            }
        }
    }
}
