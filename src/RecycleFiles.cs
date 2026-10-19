using System;
using System.Runtime.InteropServices;

// Windows 10+: require recycling rather than accepting a permanent-delete fallback.
public static class RecycleFiles {
    [ComImport,Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface Operation {
        void Advise(IntPtr sink,out uint cookie);void Unadvise(uint cookie);void SetOperationFlags(uint flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)]string text);void SetProgressDialog(IntPtr dialog);void SetProperties(IntPtr properties);void SetOwnerWindow(uint window);
        void ApplyPropertiesToItem(IntPtr item);void ApplyPropertiesToItems(IntPtr items);
        void RenameItem(IntPtr item,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);void RenameItems(IntPtr items,[MarshalAs(UnmanagedType.LPWStr)]string name);
        void MoveItem(IntPtr item,IntPtr destination,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);void MoveItems(IntPtr items,IntPtr destination);
        void CopyItem(IntPtr item,IntPtr destination,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);void CopyItems(IntPtr items,IntPtr destination);
        void DeleteItem(IntPtr item,IntPtr sink);void DeleteItems(IntPtr items);
        void NewItem(IntPtr destination,uint attributes,[MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string template,IntPtr sink);
        void PerformOperations();void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)]out bool aborted);
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=false)]static extern void SHCreateItemFromParsingName(string path,IntPtr bind,ref Guid iid,out IntPtr item);
    public static bool Send(string path){
        var op=(Operation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09")));IntPtr item=IntPtr.Zero;
        try{var iid=new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out item);op.SetOperationFlags(0x00080000|0x00000040|0x00000400|0x00100000|0x00000004);op.DeleteItem(item,IntPtr.Zero);op.PerformOperations();bool aborted;op.GetAnyOperationsAborted(out aborted);return !aborted;}
        finally{if(item!=IntPtr.Zero)Marshal.Release(item);Marshal.FinalReleaseComObject(op);}
    }
}
