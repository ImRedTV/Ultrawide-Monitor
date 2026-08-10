using System;
using System.Runtime.InteropServices;

namespace UltrawideToys.Core;

internal static class DisplayTopologyInspector
{
	private static class Native
	{
		public struct LUID
		{
			public uint LowPart;

			public int HighPart;
		}

		public struct RATIONAL
		{
			public uint Numerator;

			public uint Denominator;
		}

		public struct POINTL
		{
			public int x;

			public int y;
		}

		public struct DISPLAYCONFIG_PATH_SOURCE_INFO
		{
			public LUID adapterId;

			public uint id;

			public uint modeInfoIdx;

			public uint statusFlags;
		}

		public struct DISPLAYCONFIG_PATH_TARGET_INFO
		{
			public LUID adapterId;

			public uint id;

			public uint modeInfoIdx;

			public uint outputTechnology;

			public uint rotation;

			public uint scaling;

			public RATIONAL refreshRate;

			public uint scanLineOrdering;

			public int targetAvailable;

			public uint statusFlags;
		}

		public struct DISPLAYCONFIG_PATH_INFO
		{
			public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;

			public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;

			public uint flags;
		}

		public struct DISPLAYCONFIG_2DREGION
		{
			public uint cx;

			public uint cy;
		}

		public struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
		{
			public ulong pixelRate;

			public RATIONAL hSyncFreq;

			public RATIONAL vSyncFreq;

			public DISPLAYCONFIG_2DREGION activeSize;

			public DISPLAYCONFIG_2DREGION totalSize;

			public uint videoStandard;

			public uint scanLineOrdering;
		}

		public struct DISPLAYCONFIG_TARGET_MODE
		{
			public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
		}

		public struct DISPLAYCONFIG_SOURCE_MODE
		{
			public uint width;

			public uint height;

			public uint pixelFormat;

			public POINTL position;
		}

		[StructLayout(LayoutKind.Explicit, Size = 64)]
		public struct DISPLAYCONFIG_MODE_INFO_UNION
		{
			[FieldOffset(0)]
			public DISPLAYCONFIG_TARGET_MODE targetMode;

			[FieldOffset(0)]
			public DISPLAYCONFIG_SOURCE_MODE sourceMode;
		}

		public struct DISPLAYCONFIG_MODE_INFO
		{
			public uint infoType;

			public uint id;

			public LUID adapterId;

			public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
		}

		public struct DISPLAYCONFIG_DEVICE_INFO_HEADER
		{
			public uint type;

			public uint size;

			public LUID adapterId;

			public uint id;
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
		{
			public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

			public uint flags;

			public uint outputTechnology;

			public ushort edidManufactureId;

			public ushort edidProductCodeId;

			public uint connectorInstance;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
			public string monitorFriendlyDeviceName;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string monitorDevicePath;
		}

		[DllImport("user32.dll")]
		public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

		[DllImport("user32.dll")]
		public static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathInfo, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfo, nint currentTopologyId);

		[DllImport("user32.dll")]
		public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_DEVICE_INFO_HEADER requestPacket);
	}

	public static DisplayIdentity? TryGetTargetIdentity(RectModel bounds)
	{
		try
		{
			if (Native.GetDisplayConfigBufferSizes(2u, out var pathCount, out var modeCount) != 0 || pathCount == 0)
			{
				return null;
			}
			Native.DISPLAYCONFIG_PATH_INFO[] paths = new Native.DISPLAYCONFIG_PATH_INFO[pathCount];
			Native.DISPLAYCONFIG_MODE_INFO[] modes = new Native.DISPLAYCONFIG_MODE_INFO[modeCount];
			if (Native.QueryDisplayConfig(18u, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) != 0)
			{
				return null;
			}
			Native.DISPLAYCONFIG_PATH_INFO[] array = paths;
			for (int i = 0; i < array.Length; i++)
			{
				Native.DISPLAYCONFIG_PATH_INFO path = array[i];
				if (path.sourceInfo.modeInfoIdx >= modeCount)
				{
					continue;
				}
				Native.DISPLAYCONFIG_MODE_INFO source = modes[path.sourceInfo.modeInfoIdx];
				if (source.infoType == 1 && source.modeInfo.sourceMode.position.x == bounds.X && source.modeInfo.sourceMode.position.y == bounds.Y)
				{
					Native.DISPLAYCONFIG_TARGET_DEVICE_NAME target = new Native.DISPLAYCONFIG_TARGET_DEVICE_NAME
					{
						header = new Native.DISPLAYCONFIG_DEVICE_INFO_HEADER
						{
							size = (uint)Marshal.SizeOf<Native.DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
							adapterId = path.targetInfo.adapterId,
							id = path.targetInfo.id,
							type = 2u
						}
					};
					if (Native.DisplayConfigGetDeviceInfo(ref target.header) == 0)
					{
						string name = target.monitorFriendlyDeviceName?.Trim(new char[2] { '\0', ' ' });
						string pathName = target.monitorDevicePath?.Trim(new char[2] { '\0', ' ' }) ?? string.Empty;
						return new DisplayIdentity(name, pathName, target.edidManufactureId, target.edidProductCodeId, target.connectorInstance);
					}
				}
			}
		}
		catch
		{
		}
		return null;
	}
}

