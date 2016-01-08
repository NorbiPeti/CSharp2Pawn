using RGiesecke.DllExport;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using cell = System.Int32; //2015.06.22. - PAWN_CELL_SIZE alapján
using ucell = System.UInt32; //2015.06.22.

namespace CSharp2PawnLib
{
    public class PluginInterface
    { //2015.06.20.
        public const uint SampPluginVersion = 0x0200;
        public delegate void logprintf_t(string format, params object[] args); //2015.06.20.
        public static logprintf_t LogPrint;
        public static IntPtr pAMXFunctions;

        //----------------------------------------------------------
        // The Support() function indicates what possibilities this
        // plugin has. The SUPPORTS_VERSION flag is required to check
        // for compatibility with the server. 
        [DllExport] //2015.06.21.
        public static uint Supports()
        {
            return (uint)(SupportsEnum.Version | SupportsEnum.AmxNatives);
        }

        //----------------------------------------------------------
        // The Load() function gets passed on exported functions from
        // the SA-MP Server, like the AMX Functions and logprintf().
        // Should return true if loading the plugin has succeeded.
        
        //TODO: Valami megoldás a DllExport megfelelő használatára
        [DllExport]
        public static bool Load(IntPtr ppData)
        {
            IntPtr[] data = new IntPtr[100]; //0x13
            Marshal.Copy(ppData, data, 0, 100);
            pAMXFunctions = data[(int)PluginDataType.AmxExports];
            LogPrint = (logprintf_t)Marshal.GetDelegateForFunctionPointer(data[(int)PluginDataType.LogPrintF], typeof(logprintf_t));

            LogPrint("  Loading CSharp2Pawn made by NorbiPeti...");
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            { //2015.06.21.
                LogPrint("   Plugin error! " + e.ExceptionObject);
            };
            AMXFunctionsArray = new IntPtr[44]; //Az enum mérete
            Marshal.Copy(pAMXFunctions, AMXFunctionsArray, 0, 44); //2015.06.21.
            CompileNRun.Load(LogPrint, "   ");
            LogPrint("  Plugin CSharp2Pawn got loaded!");
            return true;
        }
        [DllExport]
        public static int AmxLoad(IntPtr ptr)
        { //2015.06.22.
            AMX amx = (AMX)Marshal.PtrToStructure((IntPtr)ptr, typeof(AMX)); //2015.07.04.
            LogPrint(" AmxLoad called. Registering natives..."); //2015.06.22.
            var HelloWorldNatives = new AMX_NATIVE_INFO[]{
	            new AMX_NATIVE_INFO{ name = "HelloWorld", func = n_HelloWorld }
            };
            return amx_Register(amx, ptr, HelloWorldNatives, -1); //2015.07.04.
        }

        //----------------------------------------------------------
        // When a gamemode is over or a filterscript gets unloaded, this
        // function gets called. No special actions needed in here.

        [DllExport]
        public static int AmxUnload(ref AMX amx)
        { //2015.07.04.
            return (int)AMX_ERR.NONE;
        }

        //----------------------------------------------------------
        // The Unload() function is called when the server shuts down,
        // meaning this plugin gets shut down with it.
        
        [DllExport]
        public static void Unload()
        {
            LogPrint("  Plugin CSharp2Pawn got unloaded!");
        }

//----------------------------------------------------------
// This is the sourcecode of the HelloWorld pawn native that we
// will be adding. "amx" is a pointer to the AMX-instance that's
// calling the function (e.g. a filterscript) and "params" is
// an array to the passed parameters. The first entry (params[0])
// is equal to  4 * PassedParameters, e.g. 16 for 4 parameters.

// native HelloWorld();
        [return: MarshalAs(UnmanagedType.I4)] //2015.07.04.
        public static cell n_HelloWorld(IntPtr amx, [MarshalAs(UnmanagedType.LPArray)]int[] _params) //<-- 2015.07.04.
        {
            LogPrint("Hello World, from the HelloWorld plugin!");
            return 1;
        }

        enum SupportsEnum : uint
        {
            Version = SampPluginVersion,
            VersionMask = 0xffff,
            AmxNatives = 0x10000
        }
        enum PluginDataType
        {
            // For some debugging
            LogPrintF = 0x00,	// void (*logprintf)(char* format, ...)

            // AMX
            AmxExports = 0x10,	// void* AmxFunctionTable[]    (see PLUGIN_AMX_EXPORT)
            CallPublicFS = 0x11, // int (*AmxCallPublicFilterScript)(char *szFunctionName)
            CallPublicGM = 0x12, // int (*AmxCallPublicGameMode)(char *szFunctionName)

        };
        /* The AMX structure is the internal structure for many functions. Not all
         * fields are valid at all times; many fields are cached in local variables.
         */
        [StructLayout(LayoutKind.Sequential, Pack=1)] //2015.07.04.
        public struct AMX
        {
            [MarshalAs(UnmanagedType.ByValArray)]
            byte[] _base; /* points to the AMX header plus the code, optionally also the data */
            [MarshalAs(UnmanagedType.ByValArray)]
            byte[] data; /* points to separate data+stack+heap, may be NULL */
            //AMX_CALLBACK callback;
            IntPtr callback;
            //AMX_DEBUG debug; /* debug callback */
            IntPtr debug; /* debug callback */
            /* for external functions a few registers must be accessible from the outside */
            cell cip; /* instruction pointer: relative to base + amxhdr->cod */
            cell frm; /* stack frame base: relative to base + amxhdr->dat */
            cell hea; /* top of the heap: relative to base + amxhdr->dat */
            cell hlw; /* bottom of the heap: relative to base + amxhdr->dat */
            cell stk; /* stack pointer: relative to base + amxhdr->dat */
            cell stp; /* top of the stack: relative to base + amxhdr->dat */
            int flags; /* current status, see amx_Flags() */
            /* user data */
            //long[] usertags = new long[AMX_USERNUM];
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = AMX_USERNUM)]
            long[] usertags;
            //object[] userdata = new object[AMX_USERNUM];
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = AMX_USERNUM)]
            object[] userdata;
            /* native functions can raise an error */
            int error;
            /* passing parameters requires a "count" field */
            int paramcount;
            /* the sleep opcode needs to store the full AMX status */
            cell pri;
            cell alt;
            cell reset_stk;
            cell reset_hea;
            cell sysreq_d; /* relocated address/value for the SYSREQ.D opcode */
        }

        /* The AMX_HEADER structure is both the memory format as the file format. The
         * structure is used internaly.
         */
        struct AMX_HEADER
        {
            int size; /* size of the "file" */
            UInt16 magic; /* signature */
            char file_version; /* file format version */
            char amx_version; /* required version of the AMX */
            short flags;
            short defsize; /* size of a definition record */
            short cod; /* initial value of COD - code block */
            short dat; /* initial value of DAT - data block */
            short hea; /* initial value of HEA - start of the heap */
            short stp; /* initial value of STP - stack top */
            short cip; /* initial value of CIP - the instruction pointer */
            short publics; /* offset to the "public functions" table */
            short natives; /* offset to the "native functions" table */
            short libraries; /* offset to the table of libraries */
            short pubvars; /* the "public variables" table */
            short tags; /* the "public tagnames" table */
            short nametable; /* name table */
        }
        [StructLayout(LayoutKind.Sequential, Pack=1)] //<-- 2015.07.04.
        public struct AMX_NATIVE_INFO //--class: alapból pointer-t ad vissza
        {
            public string name;
            public AMX_NATIVE func;
            public AMX_NATIVE_INFO_int ToServer()
            { //2015.06.22.
                return new AMX_NATIVE_INFO_int { name = name, func = (func == null ? new IntPtr(0) : Marshal.GetFunctionPointerForDelegate(func)) };
            }
        }

        const int AMX_USERNUM = 4;
        const int sEXPMAX = 19;      /* maximum name length for file version <= 6 */
        const int sNAMEMAX = 31;     /* maximum name length of symbol name */

        class AMX_FUNCSTUB
        {
            ucell address;
            char[] name = new char[sEXPMAX + 1];
        }

        struct FUNCSTUBNT
        {
            ucell address;
            uint nameofs;
        }
        
        enum AMX_ERR
        {
            NONE,
            /* reserve the first 15 error codes for exit codes of the abstract machine */
            EXIT,         /* forced exit */
            ASSERT,       /* assertion failed */
            STACKERR,     /* stack/heap collision */
            BOUNDS,       /* index out of bounds */
            MEMACCESS,    /* invalid memory access */
            INVINSTR,     /* invalid instruction */
            STACKLOW,     /* stack underflow */
            HEAPLOW,      /* heap underflow */
            CALLBACK,     /* no callback, or invalid callback */
            NATIVE,       /* native function failed */
            DIVIDE,       /* divide by zero */
            SLEEP,        /* go into sleepmode - code can be restarted */
            INVSTATE,     /* invalid state for this access */

            MEMORY = 16,  /* out of memory */
            FORMAT,       /* invalid file format */
            VERSION,      /* file is for a newer version of the AMX */
            NOTFOUND,     /* function not found */
            INDEX,        /* invalid index parameter (bad entry point) */
            DEBUG,        /* debugger cannot run */
            INIT,         /* AMX not initialized (or doubly initialized) */
            USERDATA,     /* unable to set user data field (table full) */
            INIT_JIT,     /* cannot initialize the JIT */
            PARAMS,       /* parameter error */
            DOMAIN,       /* domain error, expression result does not fit in range */
            GENERAL,      /* general error (unknown or unspecific error) */
        };

        /*      AMX_FLAG_CHAR16   0x01     no longer used */
        const int AMX_FLAG_DEBUG = 0x02;  /* symbolic info. available */
        const int AMX_FLAG_COMPACT = 0x04;  /* compact encoding */
        const int AMX_FLAG_BYTEOPC = 0x08; /* opcode is a byte (not a cell) */
        const int AMX_FLAG_NOCHECKS = 0x10;/* no array bounds checking; no STMT opcode */
        const int AMX_FLAG_NTVREG = 0x1000; /* all native functions are registered */
        const int AMX_FLAG_JITC = 0x2000; /* abstract machine is JIT compiled */
        const int AMX_FLAG_BROWSE = 0x4000; /* busy browsing */
        const int AMX_FLAG_RELOC = 0x8000;  /* jump/call addresses relocated */

        const int AMX_EXEC_MAIN = -1;     /* start at program entry point */
        const int AMX_EXEC_CONT = -2;     /* continue from last address */

        private static int AMX_USERTAG(int a, int b, int c, int d)
        {
            return ((a) | ((b) << 8) | ((int)(c) << 16) | ((int)(d) << 24));
        }
        
        public delegate cell AMX_NATIVE(IntPtr amx, int[] _params); //2015.07.04.

        public delegate int AMX_CALLBACK(ref AMX amx, cell index, ref cell result, ref cell _params);
        public delegate int AMX_DEBUG(ref AMX amx);

        enum PLUGIN_AMX_EXPORT
        {
            Align16 = 0,
            Align32 = 1,
            Align64 = 2,
            Allot = 3,
            Callback = 4,
            Cleanup = 5,
            Clone = 6,
            Exec = 7,
            FindNative = 8,
            FindPublic = 9,
            FindPubVar = 10,
            FindTagId = 11,
            Flags = 12,
            GetAddr = 13,
            GetNative = 14,
            GetPublic = 15,
            GetPubVar = 16,
            GetString = 17,
            GetTag = 18,
            GetUserData = 19,
            Init = 20,
            InitJIT = 21,
            MemInfo = 22,
            NameLength = 23,
            NativeInfo = 24,
            NumNatives = 25,
            NumPublics = 26,
            NumPubVars = 27,
            NumTags = 28,
            Push = 29,
            PushArray = 30,
            PushString = 31,
            RaiseError = 32,
            Register = 33,
            Release = 34,
            SetCallback = 35,
            SetDebugHook = 36,
            SetString = 37,
            SetUserData = 38,
            StrLen = 39,
            UTF8Check = 40,
            UTF8Get = 41,
            UTF8Len = 42,
            UTF8Put = 43,
        };

        public static IntPtr[] AMXFunctionsArray;

        private static T GetAMXFunctionDelegate<T>(PLUGIN_AMX_EXPORT pae) where T : class
        {
            if (!typeof(T).IsSubclassOf(typeof(Delegate)))
            {
                throw new InvalidOperationException(typeof(T).Name + " is not a delegate type!");
            }
            return Marshal.GetDelegateForFunctionPointer(AMXFunctionsArray[(int)pae], typeof(T)) as T; //2015.07.04.
        }
        
        private delegate int amx_Register_t(IntPtr amx, AMX_NATIVE_INFO_int[] nativelist, int number);
        public static int amx_Register(AMX amx, IntPtr ptr, AMX_NATIVE_INFO[] nativelist, int number)
        { //2015.06.21.
            amx_Register_t fn = GetAMXFunctionDelegate<amx_Register_t>(PLUGIN_AMX_EXPORT.Register);
            var arr = nativelist.Select(entry => entry.ToServer()).ToArray(); //2015.07.04.
            return fn(ptr, arr, number); //2015.07.04.
        }

        [StructLayout(LayoutKind.Sequential)] //2015.07.04. + class-->struct
        public struct AMX_NATIVE_INFO_int
        {
            public string name;
            public IntPtr func;
        }

        private delegate UInt16 amx_Align16_t(UInt16 v);
        public static UInt16 amx_Align16(UInt16 v)
        {
            amx_Align16_t fn = GetAMXFunctionDelegate<amx_Align16_t>(PLUGIN_AMX_EXPORT.Align16);
            return fn(v);
        }

        private delegate uint amx_Align32_t(uint v);
        public static UInt32 amx_Align32(UInt32 v)
        {
            amx_Align32_t fn = GetAMXFunctionDelegate<amx_Align32_t>(PLUGIN_AMX_EXPORT.Align32);
            return fn(v);
        }

        private delegate int amx_Allot_t(ref AMX amx, int cells, ref cell amx_addr, ref cell[] phys_addr);
        public static int amx_Allot(ref AMX amx, int cells, ref cell amx_addr, ref cell[] phys_addr)
        {
            amx_Allot_t fn = GetAMXFunctionDelegate<amx_Allot_t>(PLUGIN_AMX_EXPORT.Allot);
            return fn(ref amx, cells, ref amx_addr, ref phys_addr);
        }

        private delegate int amx_Callback_t(ref AMX amx, cell index, ref cell result, ref cell _params);
        public static int amx_Allot(ref AMX amx, cell index, ref cell result, ref cell _params)
        {
            amx_Callback_t fn = GetAMXFunctionDelegate<amx_Callback_t>(PLUGIN_AMX_EXPORT.Callback);
            return fn(ref amx, index, ref result, ref _params);
        }

        private delegate int amx_Cleanup_t(ref AMX amx);
        public static int amx_Cleanup(ref AMX amx)
        {
            amx_Cleanup_t fn = GetAMXFunctionDelegate<amx_Cleanup_t>(PLUGIN_AMX_EXPORT.Cleanup);
            return fn(ref amx);
        }

        private delegate int amx_Clone_t(ref AMX amxClone, ref AMX amxSource, ref IntPtr data);
        public static int amx_Clone(ref AMX amxClone, ref AMX amxSource, ref IntPtr data)
        {
            var fn = GetAMXFunctionDelegate<amx_Clone_t>(PLUGIN_AMX_EXPORT.Clone);
            return fn(ref amxClone, ref amxSource, ref data);
        }

        private delegate int amx_Exec_t(ref AMX amx, ref cell retval, int index);
        public static int amx_Exec(ref AMX amx, ref cell retval, int index)
        {
            var fn = GetAMXFunctionDelegate<amx_Exec_t>(PLUGIN_AMX_EXPORT.Exec);
            return fn(ref amx, ref retval, index);
        }

        private delegate int amx_FindNative_t(ref AMX amx, string name, ref int index);
        public static int amx_FindNative(ref AMX amx, string name, ref int index)
        {
            amx_FindNative_t fn = GetAMXFunctionDelegate<amx_FindNative_t>(PLUGIN_AMX_EXPORT.FindNative);
            return fn(ref amx, name, ref index);
        }

        private delegate int amx_FindPublic_t(ref AMX amx, string funcname, ref int index);
        public static int amx_FindPublic(ref AMX amx, string funcname, ref int index)
        {
            amx_FindPublic_t fn = GetAMXFunctionDelegate<amx_FindPublic_t>(PLUGIN_AMX_EXPORT.FindPublic);
            return fn(ref amx, funcname, ref index);
        }

        private delegate int amx_FindPubVar_t(ref AMX amx, string varname, ref cell amx_addr);
        public static int amx_FindPubVar(ref AMX amx, string varname, ref cell amx_addr)
        {
            amx_FindPubVar_t fn = GetAMXFunctionDelegate<amx_FindPubVar_t>(PLUGIN_AMX_EXPORT.FindPubVar);
            return fn(ref amx, varname, ref amx_addr);
        }

        private delegate int amx_FindTagId_t(ref AMX amx, cell tag_id, string tagname);
        public static int amx_FindTagId(ref AMX amx, cell tag_id, string tagname)
        {
            amx_FindTagId_t fn = GetAMXFunctionDelegate<amx_FindTagId_t>(PLUGIN_AMX_EXPORT.FindTagId);
            return fn(ref amx, tag_id, tagname);
        }

        private delegate int amx_Flags_t(ref AMX amx, ref UInt16 flags);
        public static int amx_Flags(ref AMX amx, ref UInt16 flags)
        {
            amx_Flags_t fn = GetAMXFunctionDelegate<amx_Flags_t>(PLUGIN_AMX_EXPORT.Flags);
            return fn(ref amx, ref flags);
        }

        private delegate int amx_GetAddr_t(ref AMX amx, cell amx_addr, ref cell[] phys_addr);
        public static int amx_GetAddr(ref AMX amx, cell amx_addr, ref cell[] phys_addr)
        {
            amx_GetAddr_t fn = GetAMXFunctionDelegate<amx_GetAddr_t>(PLUGIN_AMX_EXPORT.GetAddr);
            return fn(ref amx, amx_addr, ref phys_addr);
        }

        private delegate int amx_GetNative_t(ref AMX amx, int index, string funcname);
        public static int amx_GetNative(ref AMX amx, int index, string funcname)
        {
            amx_GetNative_t fn = GetAMXFunctionDelegate<amx_GetNative_t>(PLUGIN_AMX_EXPORT.GetNative);
            return fn(ref amx, index, funcname);
        }

        private delegate int amx_GetPublic_t(ref AMX amx, int index, string funcname);
        public static int amx_GetPublic(ref AMX amx, int index, string funcname)
        {
            amx_GetPublic_t fn = GetAMXFunctionDelegate<amx_GetPublic_t>(PLUGIN_AMX_EXPORT.GetPublic);
            return fn(ref amx, index, funcname);
        }

        private delegate int amx_GetPubVar_t(ref AMX amx, int index, string varname, ref cell amx_addr);
        public static int amx_GetPubVar(ref AMX amx, int index, string varname, ref cell amx_addr)
        {
            amx_GetPubVar_t fn = GetAMXFunctionDelegate<amx_GetPubVar_t>(PLUGIN_AMX_EXPORT.GetPubVar);
            return fn(ref amx, index, varname, ref amx_addr);
        }

        private delegate int amx_GetString_t(ref string dest, ref cell source, int use_wchar, int size);
        public static int amx_GetString(ref string dest, ref cell source, int use_wchar, int size)
        {
            amx_GetString_t fn = GetAMXFunctionDelegate<amx_GetString_t>(PLUGIN_AMX_EXPORT.GetString);
            return fn(ref dest, ref source, use_wchar, size);
        }

        private delegate int amx_GetTag_t(ref AMX amx, int index, string tagname, ref cell tag_id);
        public static int amx_GetTag(ref AMX amx, int index, string tagname, ref cell tag_id)
        {
            amx_GetTag_t fn = GetAMXFunctionDelegate<amx_GetTag_t>(PLUGIN_AMX_EXPORT.GetTag);
            return fn(ref amx, index, tagname, ref tag_id);
        }

        private delegate int amx_GetUserData_t(ref AMX amx, long tag, IntPtr ptr);
        public static int amx_GetUserData(ref AMX amx, long tag, IntPtr ptr)
        {
            amx_GetUserData_t fn = GetAMXFunctionDelegate<amx_GetUserData_t>(PLUGIN_AMX_EXPORT.GetUserData);
            return fn(ref amx, tag, ptr);
        }

        private delegate int amx_Init_t(ref AMX amx, IntPtr program);
        public static int amx_Init(ref AMX amx, IntPtr program)
        {
            amx_Init_t fn = GetAMXFunctionDelegate<amx_Init_t>(PLUGIN_AMX_EXPORT.Init);
            return fn(ref amx, program);
        }

        private delegate int amx_InitJIT_t(ref AMX amx, IntPtr reloc_table, IntPtr native_code);
        public static int amx_InitJIT(ref AMX amx, IntPtr reloc_table, IntPtr native_code)
        {
            amx_InitJIT_t fn = GetAMXFunctionDelegate<amx_InitJIT_t>(PLUGIN_AMX_EXPORT.InitJIT);
            return fn(ref amx, reloc_table, native_code);
        }

        private delegate int amx_MemInfo_t(ref AMX amx, ref long codesize, ref long datasize, ref long stackheap);
        public static int amx_MemInfo(ref AMX amx, ref long codesize, ref long datasize, ref long stackheap)
        {
            amx_MemInfo_t fn = GetAMXFunctionDelegate<amx_MemInfo_t>(PLUGIN_AMX_EXPORT.MemInfo);
            return fn(ref amx, ref codesize, ref datasize, ref stackheap);
        }

        private delegate int amx_NameLength_t(ref AMX amx, ref int length);
        public static int amx_NameLength(ref AMX amx, ref int length)
        {
            amx_NameLength_t fn = GetAMXFunctionDelegate<amx_NameLength_t>(PLUGIN_AMX_EXPORT.NameLength);
            return fn(ref amx, ref length);
        }

        private delegate AMX_NATIVE_INFO amx_NativeInfo_t(string name, AMX_NATIVE func);
        AMX_NATIVE_INFO amx_NativeInfo(string name, AMX_NATIVE func)
        {
            amx_NativeInfo_t fn = GetAMXFunctionDelegate<amx_NativeInfo_t>(PLUGIN_AMX_EXPORT.NativeInfo);
            return fn(name, func);
        }

        private delegate int amx_NumNatives_t(ref AMX amx, ref int number);
        public static int amx_NumNatives(ref AMX amx, ref int number)
        {
            amx_NumNatives_t fn = GetAMXFunctionDelegate<amx_NumNatives_t>(PLUGIN_AMX_EXPORT.NumNatives);
            return fn(ref amx, ref number);
        }

        private delegate int amx_NumPublics_t(ref AMX amx, ref int number);
        public static int amx_NumPublics(ref AMX amx, ref int number)
        {
            amx_NumPublics_t fn = GetAMXFunctionDelegate<amx_NumPublics_t>(PLUGIN_AMX_EXPORT.NumPublics);
            return fn(ref amx, ref number);
        }

        private delegate int amx_NumPubVars_t(ref AMX amx, ref int number);
        public static int amx_NumPubVars(ref AMX amx, ref int number)
        {
            amx_NumPubVars_t fn = GetAMXFunctionDelegate<amx_NumPubVars_t>(PLUGIN_AMX_EXPORT.NumPubVars);
            return fn(ref amx, ref number);
        }

        private delegate int amx_NumTags_t(ref AMX amx, ref int number);
        public static int amx_NumTags(ref AMX amx, ref int number)
        {
            amx_NumTags_t fn = GetAMXFunctionDelegate<amx_NumTags_t>(PLUGIN_AMX_EXPORT.NumTags);
            return fn(ref amx, ref number);
        }

        private delegate int amx_Push_t(ref AMX amx, cell value);
        public static int amx_Push(ref AMX amx, cell value)
        {
            amx_Push_t fn = GetAMXFunctionDelegate<amx_Push_t>(PLUGIN_AMX_EXPORT.Push);
            return fn(ref amx, value);
        }

        private delegate int amx_PushArray_t(ref AMX amx, ref cell amx_addr, ref cell phys_addr, ref cell[] array, int numcells);
        public static int amx_PushArray(ref AMX amx, ref cell amx_addr, ref cell phys_addr, ref cell[] array, int numcells)
        {
            amx_PushArray_t fn = GetAMXFunctionDelegate<amx_PushArray_t>(PLUGIN_AMX_EXPORT.PushArray);
            return fn(ref amx, ref amx_addr, ref phys_addr, ref array, numcells);
        }

        private delegate int amx_PushString_t(ref AMX amx, ref cell amx_addr, ref cell phys_addr, string _string, int pack, int use_wchar);
        public static int amx_PushString(ref AMX amx, ref cell amx_addr, ref cell phys_addr, string _string, int pack, int use_wchar)
        {
            amx_PushString_t fn = GetAMXFunctionDelegate<amx_PushString_t>(PLUGIN_AMX_EXPORT.PushString);
            return fn(ref amx, ref amx_addr, ref phys_addr, _string, pack, use_wchar);
        }

        private delegate int amx_RaiseError_t(ref AMX amx, int error);
        public static int amx_RaiseError(ref AMX amx, int error)
        {
            amx_RaiseError_t fn = GetAMXFunctionDelegate<amx_RaiseError_t>(PLUGIN_AMX_EXPORT.RaiseError);
            return fn(ref amx, error);
        }

        private delegate int amx_Release_t(ref AMX amx, cell amx_addr);
        public static int amx_Release(ref AMX amx, cell amx_addr)
        {
            amx_Release_t fn = GetAMXFunctionDelegate<amx_Release_t>(PLUGIN_AMX_EXPORT.Release);
            return fn(ref amx, amx_addr);
        }

        private delegate int amx_SetCallback_t(ref AMX amx, AMX_CALLBACK _callback);
        public static int amx_SetCallback(ref AMX amx, AMX_CALLBACK _callback)
        {
            amx_SetCallback_t fn = GetAMXFunctionDelegate<amx_SetCallback_t>(PLUGIN_AMX_EXPORT.SetCallback);
            return fn(ref amx, _callback);
        }

        private delegate int amx_SetDebugHook_t(ref AMX amx, AMX_DEBUG debug);
        public static int amx_SetDebugHook(ref AMX amx, AMX_DEBUG debug)
        {
            amx_SetDebugHook_t fn = GetAMXFunctionDelegate<amx_SetDebugHook_t>(PLUGIN_AMX_EXPORT.SetDebugHook);
            return fn(ref amx, debug);
        }

        private delegate int amx_SetString_t(ref cell dest, string source, int pack, int use_wchar, int size);
        public static int amx_SetString(ref cell dest, string source, int pack, int use_wchar, int size)
        {
            amx_SetString_t fn = GetAMXFunctionDelegate<amx_SetString_t>(PLUGIN_AMX_EXPORT.SetString);
            return fn(ref dest, source, pack, use_wchar, size);
        }

        private delegate int amx_SetUserData_t(ref AMX amx, long tag, IntPtr ptr);
        public static int amx_SetUserData(ref AMX amx, long tag, IntPtr ptr)
        {
            amx_SetUserData_t fn = GetAMXFunctionDelegate<amx_SetUserData_t>(PLUGIN_AMX_EXPORT.SetUserData);
            return fn(ref amx, tag, ptr);
        }

        private delegate int amx_StrLen_t(ref cell cstring, ref int length);
        public static int amx_StrLen(ref cell cstring, ref int length)
        {
            amx_StrLen_t fn = GetAMXFunctionDelegate<amx_StrLen_t>(PLUGIN_AMX_EXPORT.StrLen);
            return fn(ref cstring, ref length);
        }

        private delegate int amx_UTF8Check_t(string _string, ref int length);
        public static int amx_UTF8Check(string _string, ref int length)
        {
            amx_UTF8Check_t fn = GetAMXFunctionDelegate<amx_UTF8Check_t>(PLUGIN_AMX_EXPORT.UTF8Check);
            return fn(_string, ref length);
        }

        private delegate int amx_UTF8Get_t(string _string, ref IntPtr endptr, ref cell value);
        public static int amx_UTF8Get(string _string, ref IntPtr endptr, ref cell value)
        {
            amx_UTF8Get_t fn = GetAMXFunctionDelegate<amx_UTF8Get_t>(PLUGIN_AMX_EXPORT.UTF8Get);
            return fn(_string, ref endptr, ref value);
        }

        private delegate int amx_UTF8Len_t(ref cell cstr, int length);
        public static int amx_UTF8Len(ref cell cstr, int length)
        {
            amx_UTF8Len_t fn = GetAMXFunctionDelegate<amx_UTF8Len_t>(PLUGIN_AMX_EXPORT.UTF8Len);
            return fn(ref cstr, length);
        }

        private delegate int amx_UTF8Put_t(string _string, ref IntPtr endptr, int maxchars, cell value);
        public static int amx_UTF8Put(string _string, ref IntPtr endptr, int maxchars, cell value)
        {
            amx_UTF8Put_t fn = GetAMXFunctionDelegate<amx_UTF8Put_t>(PLUGIN_AMX_EXPORT.UTF8Put);
            return fn(_string, ref endptr, maxchars, value);
        }
    }
}
