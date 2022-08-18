using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Reflection;

namespace Program
{
    class Program
    {
        static void Main(string[] args)
        {
            RegistryKey rk = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (rk.GetValueNames().Contains("PicklePortsRenew") == false)
            {
                rk.SetValue("PicklePortsRenew", "\"" + AppDomain.CurrentDomain.BaseDirectory + "\\PicklePortsRenew.exe" + "\"");
            }
        }
    }
}
