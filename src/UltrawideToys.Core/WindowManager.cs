using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UltrawideToys.Core;

public sealed class WindowManager : IDisposable
{
	private sealed class ManagedWindow
	{
		public Native.WINDOWPLACEMENT OriginalPlacement;

		public string? MonitorId;

		public string? ZoneId;

		public DateTime AppliedAt;

		public bool RestoreRequested;
	}

	private sealed record PendingPlacement(Native.WINDOWPLACEMENT Placement, DateTime CapturedAt);

	private static class Native
	{
		public delegate void WinEventDelegate(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint eventThread, uint eventTime);

		public delegate nint LowLevelKeyboardProc(int code, nint message, nint data);

		public delegate nint LowLevelMouseProc(int code, nint message, nint data);

		public struct KBDLLHOOKSTRUCT
		{
			public uint vkCode;

			public uint scanCode;

			public uint flags;

			public uint time;

			public nint dwExtraInfo;
		}

		public struct MSLLHOOKSTRUCT
		{
			public POINT pt;

			public uint mouseData;

			public uint flags;

			public uint time;

			public nint dwExtraInfo;
		}

		public struct POINT
		{
			public int X;

			public int Y;
		}

		public struct RECT
		{
			public int Left;

			public int Top;

			public int Right;

			public int Bottom;
		}

		public struct POINTL
		{
			public int X;

			public int Y;
		}

		public struct WINDOWPLACEMENT
		{
			public int Length;

			public int flags;

			public int showCmd;

			public POINT ptMinPosition;

			public POINT ptMaxPosition;

			public RECT rcNormalPosition;
		}

		public struct MSG
		{
			public nint hwnd;

			public uint message;

			public nint wParam;

			public nint lParam;

			public uint time;

			public POINT pt;

			public uint lPrivate;
		}

		[DllImport("user32.dll")]
		public static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint hmod, WinEventDelegate callback, uint idProcess, uint idThread, uint flags);

		[DllImport("user32.dll")]
		public static extern bool UnhookWinEvent(nint hook);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, nint module, uint threadId);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc callback, nint module, uint threadId);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool UnhookWindowsHookEx(nint hook);

		[DllImport("user32.dll")]
		public static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

		[DllImport("user32.dll")]
		public static extern uint GetDoubleClickTime();

		[DllImport("user32.dll")]
		public static extern int GetMessage(out MSG msg, nint hwnd, uint min, uint max);

		[DllImport("user32.dll")]
		public static extern bool TranslateMessage(ref MSG msg);

		[DllImport("user32.dll")]
		public static extern nint DispatchMessage(ref MSG msg);

		[DllImport("user32.dll")]
		public static extern bool PostThreadMessage(uint idThread, uint msg, nint wParam, nint lParam);

		[DllImport("kernel32.dll")]
		public static extern uint GetCurrentThreadId();

		[DllImport("user32.dll")]
		public static extern bool IsWindow(nint hwnd);

		[DllImport("user32.dll")]
		public static extern bool IsWindowVisible(nint hwnd);

		[DllImport("user32.dll")]
		public static extern bool IsZoomed(nint hwnd);

		[DllImport("user32.dll")]
		public static extern nint GetForegroundWindow();

		[DllImport("user32.dll")]
		public static extern nint WindowFromPoint(POINT point);

		[DllImport("user32.dll")]
		public static extern nint GetAncestor(nint hwnd, uint flags);

		[DllImport("user32.dll")]
		public static extern nint GetWindow(nint hwnd, uint cmd);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern int GetClassName(nint hwnd, StringBuilder text, int max);

		[DllImport("user32.dll")]
		public static extern nint GetWindowLongPtr(nint hwnd, int index);

		[DllImport("user32.dll")]
		public static extern nint SetWindowLongPtr(nint hwnd, int index, nint value);

		[DllImport("user32.dll")]
		public static extern bool GetWindowRect(nint hwnd, out RECT rect);

		[DllImport("user32.dll")]
		public static extern bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

		[DllImport("user32.dll")]
		public static extern bool ShowWindow(nint hwnd, int command);

		[DllImport("user32.dll")]
		public static extern bool GetCursorPos(out POINT point);

		[DllImport("user32.dll")]
		public static extern short GetKeyState(int key);

		[DllImport("user32.dll")]
		public static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

		[DllImport("user32.dll")]
		public static extern bool GetWindowPlacement(nint hwnd, ref WINDOWPLACEMENT placement);

		[DllImport("user32.dll")]
		public static extern bool SetWindowPlacement(nint hwnd, ref WINDOWPLACEMENT placement);

		[DllImport("dwmapi.dll")]
		public static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out RECT rect, int size);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool OpenProcessToken(nint processHandle, uint access, out nint token);

		[DllImport("kernel32.dll")]
		public static extern bool CloseHandle(nint handle);

		[DllImport("kernel32.dll")]
		public static extern nint OpenProcess(uint access, bool inherit, uint processId);

		[DllImport("advapi32.dll", SetLastError = true)]
		public static extern bool GetTokenInformation(nint token, int informationClass, nint info, int length, out int returnLength);

		public static bool IsProcessElevated(uint pid)
		{
			nint process = OpenProcess(4096u, inherit: false, pid);
			if (process == IntPtr.Zero)
			{
				return false;
			}
			try
			{
				if (!OpenProcessToken(process, 8u, out var token))
				{
					return false;
				}
				try
				{
					int length = 0;
					GetTokenInformation(token, 25, IntPtr.Zero, 0, out length);
					if (length == 0)
					{
						return false;
					}
					nint buffer = Marshal.AllocHGlobal(length);
					try
					{
						if (!GetTokenInformation(token, 25, buffer, length, out var _))
						{
							return false;
						}
						nint sid = Marshal.ReadIntPtr(buffer);
						nint countPtr = GetSidSubAuthorityCount(sid);
						if (countPtr == IntPtr.Zero)
						{
							return false;
						}
						byte count = Marshal.ReadByte(countPtr);
						if (count == 0)
						{
							return false;
						}
						nint ridPtr = GetSidSubAuthority(sid, (uint)(count - 1));
						return ridPtr != IntPtr.Zero && Marshal.ReadInt32(ridPtr) >= 12288;
					}
					finally
					{
						Marshal.FreeHGlobal(buffer);
					}
				}
				finally
				{
					CloseHandle(token);
				}
			}
			finally
			{
				CloseHandle(process);
			}
		}

		[DllImport("advapi32.dll")]
		public static extern nint GetSidSubAuthorityCount(nint sid);

		[DllImport("advapi32.dll")]
		public static extern nint GetSidSubAuthority(nint sid, uint index);
	}

	private const uint EVENT_SYSTEM_MOVESIZESTART = 10u;

	private const uint EVENT_SYSTEM_MOVESIZEEND = 11u;

	private const uint EVENT_OBJECT_LOCATIONCHANGE = 32779u;

	private const uint WINEVENT_OUTOFCONTEXT = 0u;

	private const uint WINEVENT_SKIPOWNPROCESS = 2u;

	private const int SW_RESTORE = 9;

	private const int SW_SHOWNORMAL = 1;

	private const int WH_KEYBOARD_LL = 13;

	private const int WH_MOUSE_LL = 14;

	private const uint WM_KEYDOWN = 256u;

	private const uint WM_SYSKEYDOWN = 260u;

	private const uint WM_KEYUP = 257u;

	private const uint WM_SYSKEYUP = 261u;

	private const uint WM_LBUTTONDOWN = 513u;

	private const uint WM_LBUTTONUP = 514u;

	private const uint LLKHF_INJECTED = 16u;

	private const uint VK_LEFT = 37u;

	private const uint VK_UP = 38u;

	private const uint VK_RIGHT = 39u;

	private const uint VK_DOWN = 40u;

	private const uint VK_LWIN = 91u;

	private const uint VK_RWIN = 92u;

	private const uint VK_CONTROL = 17u;

	private const uint VK_MENU = 18u;

	private const uint GA_ROOT = 2u;

	private const int VK_SHIFT = 16;

	private const int GWL_STYLE = -16;

	private const long WS_MAXIMIZE = 16777216L;

	private const long WS_CHILD = 1073741824L;

	private const long WS_EX_TOOLWINDOW = 128L;

	private const long WS_EX_NOACTIVATE = 134217728L;

	private const uint SWP_NOZORDER = 4u;

	private const uint SWP_NOACTIVATE = 16u;

	private const uint SWP_FRAMECHANGED = 32u;

	private const uint MONITOR_DEFAULTTONEAREST = 2u;

	private const uint EVENT_SYSTEM_MINIMIZEEND = 23u;

	private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

	private readonly Func<AppSettings> _settings;

	private readonly Func<IReadOnlyList<MonitorProfile>> _monitors;

	private readonly WindowScope _scope;

	private readonly HashSet<nint> _processing = new HashSet<nint>();

	private readonly Dictionary<nint, ManagedWindow> _managed = new Dictionary<nint, ManagedWindow>();

	private readonly object _gate = new object();

	private Thread? _hookThread;

	private uint _hookThreadId;

	private nint _moveHook;

	private nint _locationHook;

	private nint _keyboardHook;

	private nint _mouseHook;

	private Native.WinEventDelegate? _callback;

	private Native.LowLevelKeyboardProc? _keyboardCallback;

	private Native.LowLevelMouseProc? _mouseCallback;

	private volatile bool _stopping;

	private uint _ownProcessId;

	private bool _leftWinDown;

	private bool _rightWinDown;

	private readonly HashSet<uint> _swallowedKeys = new HashSet<uint>();

	private readonly HashSet<nint> _deferred = new HashSet<nint>();

	private readonly Dictionary<nint, PendingPlacement> _pendingPlacements = new Dictionary<nint, PendingPlacement>();

	private nint _lastTitleClickWindow;

	private DateTime _lastTitleClickAt;

	private Native.POINT _lastTitleClickPoint;

	private bool _swallowedMouseUp;

	public WindowManager(Func<AppSettings> settings, Func<IReadOnlyList<MonitorProfile>> monitors, WindowScope scope = WindowScope.Normal)
	{
		_settings = settings;
		_monitors = monitors;
		_scope = scope;
		_ownProcessId = (uint)Environment.ProcessId;
	}

	public void Start()
	{
		if (_hookThread == null)
		{
			_hookThread = new Thread(HookLoop)
			{
				IsBackground = true,
				Name = "UltrawideToys Window Hook"
			};
			_hookThread.Start();
		}
	}

	public void Dispose()
	{
		_stopping = true;
		if (_hookThread == null)
		{
			return;
		}
		Native.PostThreadMessage(_hookThreadId, 18u, IntPtr.Zero, IntPtr.Zero);
		if (!_hookThread.Join(1500))
		{
			_hookThread = null;
		}
		lock (_gate)
		{
			_managed.Clear();
			_pendingPlacements.Clear();
			_deferred.Clear();
		}
	}

	private void HookLoop()
	{
		try
		{
			_hookThreadId = Native.GetCurrentThreadId();
			_callback = HandleWinEvent;
			_keyboardCallback = HandleKeyboard;
			_mouseCallback = HandleMouse;
			_moveHook = Native.SetWinEventHook(10u, 11u, IntPtr.Zero, _callback, 0u, 0u, 2u);
			_locationHook = Native.SetWinEventHook(32779u, 32779u, IntPtr.Zero, _callback, 0u, 0u, 2u);
			_keyboardHook = Native.SetWindowsHookEx(13, _keyboardCallback, IntPtr.Zero, 0u);
			_mouseHook = Native.SetWindowsHookEx(14, _mouseCallback, IntPtr.Zero, 0u);
			if (_moveHook == IntPtr.Zero || _locationHook == IntPtr.Zero)
			{
				LocalLog.Error($"Impossible d'installer le hook de fenêtres (erreur {Marshal.GetLastWin32Error()}).");
			}
			if (_keyboardHook == IntPtr.Zero)
			{
				LocalLog.Error($"Impossible d'installer le raccourci Win+flèche (erreur {Marshal.GetLastWin32Error()}).");
			}
			if (_mouseHook == IntPtr.Zero)
			{
				LocalLog.Error($"Impossible d'installer le hook de double-clic (erreur {Marshal.GetLastWin32Error()}).");
			}
			Native.MSG message;
			while (!_stopping && Native.GetMessage(out message, IntPtr.Zero, 0u, 0u) > 0)
			{
				Native.TranslateMessage(ref message);
				Native.DispatchMessage(ref message);
			}
		}
		catch (Exception exception)
		{
			LocalLog.Error("Le hook de fenêtres s’est arrêté", exception);
		}
		finally
		{
			if (_moveHook != IntPtr.Zero)
			{
				Native.UnhookWinEvent(_moveHook);
			}
			if (_locationHook != IntPtr.Zero)
			{
				Native.UnhookWinEvent(_locationHook);
			}
			if (_keyboardHook != IntPtr.Zero)
			{
				Native.UnhookWindowsHookEx(_keyboardHook);
			}
			if (_mouseHook != IntPtr.Zero)
			{
				Native.UnhookWindowsHookEx(_mouseHook);
			}
			_moveHook = IntPtr.Zero;
			_locationHook = IntPtr.Zero;
			_keyboardHook = IntPtr.Zero;
			_mouseHook = IntPtr.Zero;
		}
	}

	private void HandleWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
	{
		if (hwnd != IntPtr.Zero && idObject == 0 && idChild == 0 && !_stopping && ((eventType - 10 <= 1 || eventType == 23 || eventType == 32779) ? true : false))
		{
			ProcessWindow(hwnd, eventType);
		}
	}

	private nint HandleKeyboard(int code, nint message, nint dataPointer)
	{
		if (code >= 0 && dataPointer != IntPtr.Zero)
		{
			uint key = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(dataPointer).vkCode;
			bool isKeyDown = message == 256 || message == 260;
			bool isKeyUp = message == 257 || message == 261;
			if (key == 91)
			{
				_leftWinDown = isKeyDown || (!_leftWinDown && !isKeyUp);
			}
			if (key == 92)
			{
				_rightWinDown = isKeyDown || (!_rightWinDown && !isKeyUp);
			}
			if (isKeyUp && _swallowedKeys.Remove(key))
			{
				return 1;
			}
			if (isKeyDown && IsDirectionalKey(key) && IsWindowsKeyDown() && !IsModifierDown(16u) && !IsModifierDown(17u) && !IsModifierDown(18u) && TryHandleDirectionalKey(key))
			{
				_swallowedKeys.Add(key);
				return 1;
			}
		}
		return Native.CallNextHookEx(_keyboardHook, code, message, dataPointer);
	}

	private nint HandleMouse(int code, nint message, nint dataPointer)
	{
		if (code >= 0 && dataPointer != IntPtr.Zero)
		{
			Native.MSLLHOOKSTRUCT data = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(dataPointer);
			if (message == 514 && _swallowedMouseUp)
			{
				_swallowedMouseUp = false;
				return 1;
			}
			if (message == 513 && (data.flags & 0x10) == 0)
			{
				nint hwnd = FindTitleWindow(data.pt);
				DateTime now = DateTime.UtcNow;
				if (hwnd != IntPtr.Zero && hwnd == _lastTitleClickWindow && (now - _lastTitleClickAt).TotalMilliseconds <= (double)Native.GetDoubleClickTime() && Math.Abs(data.pt.X - _lastTitleClickPoint.X) <= 8 && Math.Abs(data.pt.Y - _lastTitleClickPoint.Y) <= 8)
				{
					_lastTitleClickWindow = IntPtr.Zero;
					if (TryHandleTitleDoubleClick(hwnd))
					{
						_swallowedMouseUp = true;
						return 1;
					}
				}
				_lastTitleClickWindow = hwnd;
				_lastTitleClickPoint = data.pt;
				_lastTitleClickAt = now;
			}
		}
		return Native.CallNextHookEx(_mouseHook, code, message, dataPointer);
	}

	private nint FindTitleWindow(Native.POINT point)
	{
		nint child = Native.WindowFromPoint(point);
		if (child == IntPtr.Zero)
		{
			return IntPtr.Zero;
		}
		nint hwnd = Native.GetAncestor(child, 2u);
		if (hwnd == IntPtr.Zero)
		{
			hwnd = child;
		}
		if (!Native.IsWindow(hwnd) || !Native.IsWindowVisible(hwnd) || !IsEligible(hwnd))
		{
			return IntPtr.Zero;
		}
		if (!Native.GetWindowRect(hwnd, out var raw))
		{
			return IntPtr.Zero;
		}
		if (point.X < raw.Left || point.X >= raw.Right || point.Y < raw.Top || point.Y > raw.Top + 48)
		{
			return IntPtr.Zero;
		}
		if (point.X >= raw.Right - 180)
		{
			return IntPtr.Zero;
		}
		return hwnd;
	}

	private bool TryHandleTitleDoubleClick(nint hwnd)
	{
		try
		{
			AppSettings settings = _settings();
			if (!settings.Enabled || !settings.MaximizeToZones || (settings.ShiftBypass && IsModifierDown(16u)) || !MatchesScope(hwnd))
			{
				return false;
			}
			ManagedWindow managed;
			ManagedWindow state = (_managed.TryGetValue(hwnd, out managed) ? managed : null);
			if (state != null && IsStillInManagedZone(hwnd, state))
			{
				RestoreOriginal(hwnd, state.OriginalPlacement);
				_managed.Remove(hwnd);
				return true;
			}
			_managed.Remove(hwnd);
			MonitorProfile monitor = FindMonitorForWindow(hwnd, preferCursor: true);
			if (monitor == null)
			{
				return false;
			}
			IReadOnlyList<ZoneRect> zones = LayoutEngine.Calculate(monitor.ActiveLayout.Root, monitor.WorkArea);
			if (zones.Count == 0)
			{
				return false;
			}
			ZoneRect zone = ChooseZone(hwnd, monitor, zones);
			if (!TryReadPlacement(hwnd, out var original))
			{
				return false;
			}
			original.Length = Marshal.SizeOf<Native.WINDOWPLACEMENT>();
			original.showCmd = 1;
			ManagedWindow next = new ManagedWindow
			{
				OriginalPlacement = original,
				ZoneId = zone.ZoneId,
				MonitorId = monitor.Id,
				AppliedAt = DateTime.UtcNow
			};
			ApplyZone(hwnd, zone.Rect);
			_managed[hwnd] = next;
			_pendingPlacements.Remove(hwnd);
			QueueDeferredReapply(hwnd);
			return true;
		}
		catch (Exception exception)
		{
			LocalLog.Error("Impossible d'appliquer le double-clic de barre de titre", exception);
			return false;
		}
	}

	private void ProcessWindow(nint hwnd, uint eventType)
	{
		if (!Native.IsWindow(hwnd) || !Native.IsWindowVisible(hwnd) || !IsEligible(hwnd))
		{
			return;
		}
		lock (_gate)
		{
			if (!_processing.Add(hwnd))
			{
				return;
			}
		}
		try
		{
			AppSettings settings = _settings();
			ManagedWindow known;
			ManagedWindow state = (_managed.TryGetValue(hwnd, out known) ? known : null);
			bool isZoomed = Native.IsZoomed(hwnd);
			if (eventType == 10)
			{
				Native.WINDOWPLACEMENT placement;
				if (settings.Enabled && settings.MaximizeToZones && !isZoomed && state != null && IsStillInManagedZone(hwnd, state))
				{
					state.RestoreRequested = true;
				}
				else if (settings.Enabled && settings.MaximizeToZones && !isZoomed && state == null && MatchesScope(hwnd) && TryReadPlacement(hwnd, out placement))
				{
					_pendingPlacements[hwnd] = new PendingPlacement(placement, DateTime.UtcNow);
				}
			}
			else if (!settings.Enabled)
			{
				_managed.Remove(hwnd);
				_pendingPlacements.Remove(hwnd);
			}
			else if (!isZoomed)
			{
				if (state != null)
				{
					if (IsStillInManagedZone(hwnd, state))
					{
						if (eventType == 11)
						{
							state.RestoreRequested = false;
						}
						return;
					}
					_managed.Remove(hwnd);
				}
				_pendingPlacements.Remove(hwnd);
				if (settings.SnapEnabled && eventType == 11)
				{
					SnapToEdges(hwnd, settings.SnapDistance);
				}
			}
			else if (!settings.MaximizeToZones || (settings.ShiftBypass && IsModifierDown(16u)))
			{
				_managed.Remove(hwnd);
				_pendingPlacements.Remove(hwnd);
			}
			else
			{
				if (!MatchesScope(hwnd))
				{
					return;
				}
				if (state != null && state.RestoreRequested)
				{
					RestoreOriginal(hwnd, state.OriginalPlacement);
					_managed.Remove(hwnd);
					return;
				}
				MonitorProfile monitor = FindMonitorForWindow(hwnd, state == null || eventType == 11);
				if (monitor == null)
				{
					return;
				}
				IReadOnlyList<ZoneRect> zones = LayoutEngine.Calculate(monitor.ActiveLayout.Root, monitor.WorkArea);
				if (zones.Count == 0)
				{
					return;
				}
				ZoneRect zone = ((state != null) ? (zones.FirstOrDefault((ZoneRect x) => string.Equals(x.ZoneId, state.ZoneId, StringComparison.OrdinalIgnoreCase)) ?? ChooseZone(hwnd, monitor, zones)) : ChooseZone(hwnd, monitor, zones));
				if (zone != null)
				{
					Native.WINDOWPLACEMENT original;
					PendingPlacement pending;
					if (state != null)
					{
						original = state.OriginalPlacement;
					}
					else if (_pendingPlacements.TryGetValue(hwnd, out pending) && (DateTime.UtcNow - pending.CapturedAt).TotalSeconds <= 3.0)
					{
						original = pending.Placement;
					}
					else if (!TryReadPlacement(hwnd, out original))
					{
						return;
					}
					original.Length = Marshal.SizeOf<Native.WINDOWPLACEMENT>();
					original.showCmd = 1;
					if (state == null)
					{
						state = new ManagedWindow
						{
							OriginalPlacement = original
						};
					}
					ApplyZone(hwnd, zone.Rect);
					state.ZoneId = zone.ZoneId;
					state.MonitorId = monitor.Id;
					state.AppliedAt = DateTime.UtcNow;
					_managed[hwnd] = state;
					_pendingPlacements.Remove(hwnd);
					QueueDeferredReapply(hwnd);
				}
			}
		}
		catch (Exception exception)
		{
			LocalLog.Error("Impossible d'appliquer une zone à la fenêtre", exception);
		}
		finally
		{
			lock (_gate)
			{
				_processing.Remove(hwnd);
			}
		}
	}

	private void ApplyZone(nint hwnd, RectModel rect)
	{
		Native.ShowWindow(hwnd, 9);
		Native.SetWindowPos(hwnd, IntPtr.Zero, rect.X, rect.Y, rect.Width, rect.Height, 52u);
		AlignVisibleFrame(hwnd, rect);
	}

	private void RestoreOriginal(nint hwnd, Native.WINDOWPLACEMENT placement)
	{
		placement.Length = Marshal.SizeOf<Native.WINDOWPLACEMENT>();
		placement.showCmd = 1;
		if (!Native.SetWindowPlacement(hwnd, ref placement))
		{
			Native.ShowWindow(hwnd, 9);
			Native.RECT rect = placement.rcNormalPosition;
			Native.SetWindowPos(hwnd, IntPtr.Zero, rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top, 52u);
		}
		else
		{
			Native.ShowWindow(hwnd, 9);
		}
	}

	private static void AlignVisibleFrame(nint hwnd, RectModel target)
	{
		if (!Native.GetWindowRect(hwnd, out var outer) || !TryGetExtendedFrameBounds(hwnd, out var visible))
		{
			return;
		}
		int outerWidth = outer.Right - outer.Left;
		int outerHeight = outer.Bottom - outer.Top;
		int visibleWidth = visible.Right - visible.Left;
		int visibleHeight = visible.Bottom - visible.Top;
		if (outerWidth > 0 && outerHeight > 0 && visibleWidth > 0 && visibleHeight > 0)
		{
			int x = outer.Left + target.X - visible.Left;
			int y = outer.Top + target.Y - visible.Top;
			int width = Math.Max(1, outerWidth + target.Width - visibleWidth);
			int height = Math.Max(1, outerHeight + target.Height - visibleHeight);
			if (x != outer.Left || y != outer.Top || width != outerWidth || height != outerHeight)
			{
				Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, width, height, 20u);
			}
		}
	}

	private static bool TryGetExtendedFrameBounds(nint hwnd, out Native.RECT rect)
	{
		rect = default(Native.RECT);
		return Native.DwmGetWindowAttribute(hwnd, 9u, out rect, Marshal.SizeOf<Native.RECT>()) == 0 && rect.Right > rect.Left && rect.Bottom > rect.Top;
	}

	private void QueueDeferredReapply(nint hwnd)
	{
		lock (_gate)
		{
			if (!_deferred.Add(hwnd))
			{
				return;
			}
		}
		Task.Run(async delegate
		{
			try
			{
				int[] array = new int[3] { 70, 220, 600 };
				foreach (int delay in array)
				{
					await Task.Delay(delay).ConfigureAwait(continueOnCapturedContext: false);
					if (_stopping)
					{
						break;
					}
					ProcessWindow(hwnd, 32779u);
				}
			}
			catch (Exception ex)
			{
				Exception exception = ex;
				LocalLog.Error("Erreur lors de la stabilisation d'une zone", exception);
			}
			finally
			{
				lock (_gate)
				{
					_deferred.Remove(hwnd);
				}
			}
		});
	}

	private bool TryHandleDirectionalKey(uint key)
	{
		try
		{
			AppSettings settings = _settings();
			if (!settings.Enabled || !settings.MaximizeToZones || (settings.ShiftBypass && IsModifierDown(16u)))
			{
				return false;
			}
			nint hwnd = Native.GetForegroundWindow();
			if (hwnd == IntPtr.Zero || !Native.IsWindowVisible(hwnd) || !IsEligible(hwnd) || !MatchesScope(hwnd))
			{
				return false;
			}
			MonitorProfile monitor = FindMonitorForWindow(hwnd, preferCursor: false);
			if (monitor == null)
			{
				return false;
			}
			IReadOnlyList<ZoneRect> zones = LayoutEngine.Calculate(monitor.ActiveLayout.Root, monitor.WorkArea);
			if (zones.Count == 0 || !Native.GetWindowRect(hwnd, out var raw))
			{
				return false;
			}
			RectModel window = RectModel.From(raw.Left, raw.Top, raw.Right - raw.Left, raw.Bottom - raw.Top);
			ManagedWindow managed;
			ManagedWindow state = (_managed.TryGetValue(hwnd, out managed) ? managed : null);
			ZoneRect current = ((state != null) ? zones.FirstOrDefault((ZoneRect x) => string.Equals(x.ZoneId, state.ZoneId, StringComparison.OrdinalIgnoreCase)) : ChooseZone(hwnd, monitor, zones));
			ZoneRect target = ChooseDirectionalZone(window, current, zones, key);
			if (target == null)
			{
				return false;
			}
			Native.WINDOWPLACEMENT original;
			if (state != null)
			{
				original = state.OriginalPlacement;
			}
			else if (!TryReadPlacement(hwnd, out original))
			{
				return false;
			}
			original.Length = Marshal.SizeOf<Native.WINDOWPLACEMENT>();
			original.showCmd = 1;
			if (state == null)
			{
				state = new ManagedWindow
				{
					OriginalPlacement = original
				};
			}
			ApplyZone(hwnd, target.Rect);
			state.ZoneId = target.ZoneId;
			state.MonitorId = monitor.Id;
			state.AppliedAt = DateTime.UtcNow;
			_managed[hwnd] = state;
			QueueDeferredReapply(hwnd);
			return true;
		}
		catch (Exception exception)
		{
			LocalLog.Error("Impossible d'appliquer la navigation Win+flèche", exception);
			return false;
		}
	}

	private static ZoneRect? ChooseDirectionalZone(RectModel window, ZoneRect? current, IReadOnlyList<ZoneRect> zones, uint key)
	{
		RectModel origin = current?.Rect ?? window;
		double originX = (double)origin.X + (double)origin.Width / 2.0;
		double originY = (double)origin.Y + (double)origin.Height / 2.0;
		bool flag = ((key == 37 || key == 39) ? true : false);
		bool horizontal = flag;
		flag = key - 39 <= 1;
		bool positive = flag;
		ZoneRect candidates = (from x in zones.Where((ZoneRect x) => current == null || !string.Equals(x.ZoneId, current.ZoneId, StringComparison.OrdinalIgnoreCase)).Select(delegate(ZoneRect x)
			{
				double num = (double)x.Rect.X + (double)x.Rect.Width / 2.0;
				double num2 = (double)x.Rect.Y + (double)x.Rect.Height / 2.0;
				double primary = (horizontal ? (num - originX) : (num2 - originY));
				double orthogonal = (horizontal ? Math.Abs(num2 - originY) : Math.Abs(num - originX));
				int overlap = (horizontal ? OverlapLength(x.Rect.Y, x.Rect.Bottom, origin.Y, origin.Bottom) : OverlapLength(x.Rect.X, x.Rect.Right, origin.X, origin.Right));
				return new
				{
					Zone = x,
					Primary = primary,
					Orthogonal = orthogonal,
					Overlap = overlap
				};
			})
			where positive ? (x.Primary > 1.0) : (x.Primary < -1.0)
			orderby x.Overlap > 0 descending, Math.Abs(x.Primary), x.Orthogonal
			select x.Zone).FirstOrDefault();
		return candidates ?? current;
	}

	private static int OverlapLength(int firstStart, int firstEnd, int secondStart, int secondEnd)
	{
		return Math.Max(0, Math.Min(firstEnd, secondEnd) - Math.Max(firstStart, secondStart));
	}

	private bool IsStillInManagedZone(nint hwnd, ManagedWindow state)
	{
		if (!Native.GetWindowRect(hwnd, out var raw))
		{
			return false;
		}
		MonitorProfile monitor = _monitors().FirstOrDefault((MonitorProfile x) => string.Equals(x.Id, state.MonitorId, StringComparison.OrdinalIgnoreCase));
		if (monitor == null)
		{
			return false;
		}
		ZoneRect zone = LayoutEngine.Calculate(monitor.ActiveLayout.Root, monitor.WorkArea).FirstOrDefault((ZoneRect x) => string.Equals(x.ZoneId, state.ZoneId, StringComparison.OrdinalIgnoreCase));
		if (zone == null)
		{
			return false;
		}
		Native.RECT visible;
		RectModel actual = (TryGetExtendedFrameBounds(hwnd, out visible) ? RectModel.From(visible.Left, visible.Top, visible.Right - visible.Left, visible.Bottom - visible.Top) : RectModel.From(raw.Left, raw.Top, raw.Right - raw.Left, raw.Bottom - raw.Top));
		return Math.Abs(actual.X - zone.Rect.X) <= 16 && Math.Abs(actual.Y - zone.Rect.Y) <= 16 && Math.Abs(actual.Width - zone.Rect.Width) <= 24 && Math.Abs(actual.Height - zone.Rect.Height) <= 24;
	}

	private static bool TryReadPlacement(nint hwnd, out Native.WINDOWPLACEMENT placement)
	{
		placement = new Native.WINDOWPLACEMENT
		{
			Length = Marshal.SizeOf<Native.WINDOWPLACEMENT>()
		};
		return Native.GetWindowPlacement(hwnd, ref placement);
	}

	private bool IsWindowsKeyDown()
	{
		return _leftWinDown || _rightWinDown || IsModifierDown(91u) || IsModifierDown(92u);
	}

	private static bool IsModifierDown(uint key)
	{
		return (Native.GetKeyState((int)key) & 0x8000) != 0;
	}

	private static bool IsDirectionalKey(uint key)
	{
		if (key - 37 <= 3)
		{
			return true;
		}
		return false;
	}

	private void SnapToEdges(nint hwnd, int distance)
	{
		if (!MatchesScope(hwnd) || !Native.GetWindowRect(hwnd, out var raw))
		{
			return;
		}
		RectModel window = RectModel.From(raw.Left, raw.Top, raw.Right - raw.Left, raw.Bottom - raw.Top);
		MonitorProfile monitor = DisplayService.FindForWindow(_monitors(), window);
		if (monitor == null)
		{
			return;
		}
		List<int> candidates = new List<int>
		{
			monitor.WorkArea.X,
			monitor.WorkArea.Right
		};
		candidates.AddRange(from splitRect in LayoutEngine.CalculateSplits(monitor.ActiveLayout.Root, monitor.WorkArea)
			select splitRect.Position);
		int x = window.X;
		int right = window.Right;
		foreach (int edge in candidates)
		{
			if (Math.Abs(x - edge) <= distance)
			{
				x = edge;
			}
			if (Math.Abs(right - edge) <= distance)
			{
				x = edge - window.Width;
			}
		}
		int y = window.Y;
		int bottom = window.Bottom;
		List<int> yEdges = new List<int>
		{
			monitor.WorkArea.Y,
			monitor.WorkArea.Bottom
		};
		yEdges.AddRange(from splitRect in LayoutEngine.CalculateSplits(monitor.ActiveLayout.Root, monitor.WorkArea)
			select splitRect.Position);
		foreach (int edge2 in yEdges)
		{
			if (Math.Abs(y - edge2) <= distance)
			{
				y = edge2;
			}
			if (Math.Abs(bottom - edge2) <= distance)
			{
				y = edge2 - window.Height;
			}
		}
		if (x != window.X || y != window.Y)
		{
			Native.SetWindowPos(hwnd, IntPtr.Zero, x, y, window.Width, window.Height, 20u);
		}
	}

	private ZoneRect ChooseZone(nint hwnd, MonitorProfile monitor, IReadOnlyList<ZoneRect> zones)
	{
		Native.GetCursorPos(out var cursor);
		ZoneRect atCursor = zones.FirstOrDefault((ZoneRect x) => x.Rect.Contains(cursor.X, cursor.Y));
		if (atCursor != null)
		{
			return atCursor;
		}
		if (!Native.GetWindowRect(hwnd, out var raw))
		{
			return zones[0];
		}
		RectModel window = RectModel.From(raw.Left, raw.Top, raw.Right - raw.Left, raw.Bottom - raw.Top);
		return zones.OrderByDescending((ZoneRect x) => IntersectionArea(x.Rect, window)).First();
	}

	private MonitorProfile? FindMonitorForWindow(nint hwnd, bool preferCursor)
	{
		if (!Native.GetWindowRect(hwnd, out var raw))
		{
			return null;
		}
		IReadOnlyList<MonitorProfile> monitors = _monitors();
		if (preferCursor && Native.GetCursorPos(out var cursor))
		{
			MonitorProfile underPointer = monitors.FirstOrDefault((MonitorProfile x) => x.Bounds.Contains(cursor.X, cursor.Y));
			if (underPointer != null)
			{
				return underPointer;
			}
		}
		return DisplayService.FindForWindow(monitors, RectModel.From(raw.Left, raw.Top, raw.Right - raw.Left, raw.Bottom - raw.Top));
	}

	private bool IsEligible(nint hwnd)
	{
		Native.GetWindowThreadProcessId(hwnd, out var pid);
		if (pid == _ownProcessId)
		{
			return false;
		}
		long style = ((IntPtr)Native.GetWindowLongPtr(hwnd, -16)).ToInt64();
		if ((style & 0x40000000) != 0L || Native.GetWindow(hwnd, 4u) != IntPtr.Zero)
		{
			return false;
		}
		long ex = ((IntPtr)Native.GetWindowLongPtr(hwnd, -20)).ToInt64();
		if ((ex & 0x8000080) != 0)
		{
			return false;
		}
		StringBuilder className = new StringBuilder(256);
		Native.GetClassName(hwnd, className, className.Capacity);
		bool flag;
		switch (className.ToString())
		{
		case "Shell_TrayWnd":
		case "Progman":
		case "WorkerW":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return false;
		}
		try
		{
			using Process process = Process.GetProcessById((int)pid);
			string name = process.ProcessName;
			if (_settings().ExcludedProcesses.Any((string x) => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
			{
				return false;
			}
		}
		catch (Exception exception)
		{
			LocalLog.Error("Impossible d’inspecter une fenêtre", exception);
			return false;
		}
		return true;
	}

	private bool MatchesScope(nint hwnd)
	{
		if (_scope == WindowScope.Any)
		{
			return true;
		}
		Native.GetWindowThreadProcessId(hwnd, out var pid);
		bool elevated = Native.IsProcessElevated(pid);
		return (_scope == WindowScope.ElevatedOnly) ? elevated : (!elevated);
	}

	private static long IntersectionArea(RectModel a, RectModel b)
	{
		int w = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X));
		int h = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y));
		return (long)w * (long)h;
	}
}

