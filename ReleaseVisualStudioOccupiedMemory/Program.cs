using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ReleaseVisualStudioOccupiedMemory
{
    class Program
    {
        static uint TH32CS_SNAPPROCESS = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESSENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID;
            public uint cntThreads;
            public uint th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        };

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll")]
        static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        [DllImport("kernel32.dll")]
        static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        static List<ProcessInfo> list = new List<ProcessInfo>();

        static void Main(string[] args)
        {
            string[] flags = ConfigurationManager.AppSettings["ProcFlags"].Split(';');

            Process currentProcess = Process.GetCurrentProcess();
            int? currentParentId = default(int?);

            foreach (var proc in Process.GetProcesses())
            {
                string name = proc.ProcessName;

                if (flags.Any(item => name.ToLower().StartsWith(item.ToLower())))
                {
                    try
                    {
                        var procInfo = new ProcessInfo() { Id = proc.Id, Name = name, ParentId = GetParentProcess(proc.Id)?.Id, Process = proc };
                        list.Add(procInfo);

                        if (proc.Id == currentProcess.Id)
                        {
                            currentParentId = procInfo.ParentId;
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }           

            var vsProcesses = list.Where(item => item.Name == "devenv" && item.Id != currentParentId);

            foreach (var proc in vsProcesses)
            {
                KillSubProcesses(proc);
            }           
        }

        static void KillSubProcesses(ProcessInfo proc)
        {
            var children = list.Where(item => item.ParentId == proc.Id);

            foreach (var child in children)
            {
                try
                {                    
                    child.Process.Kill();
                }
                catch
                {
                }
            }
        }

        static Process GetParentProcess(int procId)
        {
            int iParentPid = 0;

            IntPtr oHnd = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);

            if (oHnd == IntPtr.Zero)
                return null;

            PROCESSENTRY32 oProcInfo = new PROCESSENTRY32();

            oProcInfo.dwSize =
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(PROCESSENTRY32));

            if (Process32First(oHnd, ref oProcInfo) == false)
                return null;

            do
            {
                if (procId == oProcInfo.th32ProcessID)
                    iParentPid = (int)oProcInfo.th32ParentProcessID;
            }
            while (iParentPid == 0 && Process32Next(oHnd, ref oProcInfo));

            if (iParentPid > 0)
                return Process.GetProcessById(iParentPid);
            else
                return null;
        }
    }

    class WinWrapper : System.Windows.Forms.IWin32Window
    {
        public WinWrapper(IntPtr oHandle)
        {
            _oHwnd = oHandle;
        }

        public IntPtr Handle
        {
            get { return _oHwnd; }
        }

        private IntPtr _oHwnd;
    }

    class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentId { get; set; }
        public Process Process { get; set; }
    }
}
