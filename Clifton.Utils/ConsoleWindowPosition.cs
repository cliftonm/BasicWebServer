using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Clifton.Utils
{
	public static class ConsoleWindowPosition
	{
		const int SWP_NOSIZE = 0x0001;

		[System.Runtime.InteropServices.DllImport("kernel32.dll")]
		private static extern bool AllocConsole();

		[DllImport("user32.dll", EntryPoint = "SetWindowPos")]
		public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr GetConsoleWindow();

		// http://stackoverflow.com/questions/1548838/setting-position-of-a-console-window-opened-in-a-winforms-app/1548881#1548881
		public static void SetConsoleWindowPosition(int x, int y)
		{
			AllocConsole();
			IntPtr MyConsole = GetConsoleWindow();
			SetWindowPos(MyConsole, 0, x, y, 0, 0, SWP_NOSIZE);
		}
	}
}


