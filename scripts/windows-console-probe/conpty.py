"""A process in a Windows pseudo console (ConPTY), with its output as raw bytes.

The probe reads bytes rather than text on purpose. ConPTY writes UTF-8 to a
pipe in whatever pieces it likes, so a three-byte character can arrive in two
reads. A reader that decodes each read on its own, as pywinpty does, turns both
halves into replacement characters, and the screen it rebuilds is wider than
the one the program drew. This one hands back bytes, and the caller decodes
them with an incremental decoder that carries a split character over.
"""
import ctypes
import subprocess
from ctypes import wintypes

kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)

PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016
EXTENDED_STARTUPINFO_PRESENT = 0x00080000
CREATE_UNICODE_ENVIRONMENT = 0x00000400
WAIT_TIMEOUT = 0x00000102
STILL_ACTIVE = 259
HPCON = wintypes.HANDLE


class COORD(ctypes.Structure):
    _fields_ = [("X", wintypes.SHORT), ("Y", wintypes.SHORT)]


class STARTUPINFOW(ctypes.Structure):
    _fields_ = [
        ("cb", wintypes.DWORD), ("lpReserved", wintypes.LPWSTR), ("lpDesktop", wintypes.LPWSTR),
        ("lpTitle", wintypes.LPWSTR), ("dwX", wintypes.DWORD), ("dwY", wintypes.DWORD),
        ("dwXSize", wintypes.DWORD), ("dwYSize", wintypes.DWORD),
        ("dwXCountChars", wintypes.DWORD), ("dwYCountChars", wintypes.DWORD),
        ("dwFillAttribute", wintypes.DWORD), ("dwFlags", wintypes.DWORD),
        ("wShowWindow", wintypes.WORD), ("cbReserved2", wintypes.WORD),
        ("lpReserved2", ctypes.c_void_p), ("hStdInput", wintypes.HANDLE),
        ("hStdOutput", wintypes.HANDLE), ("hStdError", wintypes.HANDLE),
    ]


class STARTUPINFOEXW(ctypes.Structure):
    _fields_ = [("StartupInfo", STARTUPINFOW), ("lpAttributeList", ctypes.c_void_p)]


class PROCESS_INFORMATION(ctypes.Structure):
    _fields_ = [("hProcess", wintypes.HANDLE), ("hThread", wintypes.HANDLE),
                ("dwProcessId", wintypes.DWORD), ("dwThreadId", wintypes.DWORD)]


def _check(ok, what):
    if not ok:
        raise ctypes.WinError(ctypes.get_last_error(), f"{what} failed")


kernel32.CreatePipe.argtypes = [ctypes.POINTER(wintypes.HANDLE), ctypes.POINTER(wintypes.HANDLE),
                                ctypes.c_void_p, wintypes.DWORD]
kernel32.CreatePseudoConsole.argtypes = [COORD, wintypes.HANDLE, wintypes.HANDLE, wintypes.DWORD,
                                         ctypes.POINTER(HPCON)]
kernel32.CreatePseudoConsole.restype = ctypes.c_long
kernel32.ResizePseudoConsole.argtypes = [HPCON, COORD]
kernel32.ResizePseudoConsole.restype = ctypes.c_long
kernel32.ClosePseudoConsole.argtypes = [HPCON]
kernel32.InitializeProcThreadAttributeList.argtypes = [ctypes.c_void_p, wintypes.DWORD,
                                                       wintypes.DWORD, ctypes.POINTER(ctypes.c_size_t)]
kernel32.UpdateProcThreadAttribute.argtypes = [ctypes.c_void_p, wintypes.DWORD, ctypes.c_size_t,
                                               ctypes.c_void_p, ctypes.c_size_t, ctypes.c_void_p,
                                               ctypes.c_void_p]
kernel32.DeleteProcThreadAttributeList.argtypes = [ctypes.c_void_p]
kernel32.CreateProcessW.argtypes = [wintypes.LPCWSTR, wintypes.LPWSTR, ctypes.c_void_p,
                                    ctypes.c_void_p, wintypes.BOOL, wintypes.DWORD, ctypes.c_void_p,
                                    wintypes.LPCWSTR, ctypes.POINTER(STARTUPINFOEXW),
                                    ctypes.POINTER(PROCESS_INFORMATION)]
kernel32.ReadFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                              ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
kernel32.WriteFile.argtypes = [wintypes.HANDLE, ctypes.c_void_p, wintypes.DWORD,
                               ctypes.POINTER(wintypes.DWORD), ctypes.c_void_p]
kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
kernel32.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
kernel32.WaitForSingleObject.restype = wintypes.DWORD
kernel32.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
kernel32.TerminateProcess.argtypes = [wintypes.HANDLE, wintypes.UINT]


class PseudoConsole:
    def __init__(self, argv, cwd, env, rows, cols):
        in_read, in_write = wintypes.HANDLE(), wintypes.HANDLE()
        out_read, out_write = wintypes.HANDLE(), wintypes.HANDLE()
        _check(kernel32.CreatePipe(ctypes.byref(in_read), ctypes.byref(in_write), None, 0), "CreatePipe")
        _check(kernel32.CreatePipe(ctypes.byref(out_read), ctypes.byref(out_write), None, 0), "CreatePipe")

        self._hpc = HPCON()
        hr = kernel32.CreatePseudoConsole(COORD(cols, rows), in_read, out_write, 0, ctypes.byref(self._hpc))
        if hr != 0:
            raise OSError(f"CreatePseudoConsole failed: 0x{hr & 0xFFFFFFFF:08x}")
        # THE PSEUDO CONSOLE HOLDS ITS OWN COPIES, so these ends are closed
        # here; keeping them open would stop the output pipe ever reporting
        # that the program has gone.
        kernel32.CloseHandle(in_read)
        kernel32.CloseHandle(out_write)
        self._in, self._out = in_write, out_read

        size = ctypes.c_size_t()
        kernel32.InitializeProcThreadAttributeList(None, 1, 0, ctypes.byref(size))
        self._attributes = (ctypes.c_byte * size.value)()
        _check(kernel32.InitializeProcThreadAttributeList(self._attributes, 1, 0, ctypes.byref(size)),
               "InitializeProcThreadAttributeList")
        _check(kernel32.UpdateProcThreadAttribute(
            self._attributes, 0, PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
            self._hpc, ctypes.sizeof(HPCON), None, None), "UpdateProcThreadAttribute")

        startup = STARTUPINFOEXW()
        startup.StartupInfo.cb = ctypes.sizeof(STARTUPINFOEXW)
        startup.lpAttributeList = ctypes.cast(self._attributes, ctypes.c_void_p)

        block = "".join(f"{k}={v}\0" for k, v in sorted(env.items())) + "\0"
        env_buffer = ctypes.create_unicode_buffer(block, len(block))
        command = ctypes.create_unicode_buffer(subprocess.list2cmdline(argv))
        self._process = PROCESS_INFORMATION()
        _check(kernel32.CreateProcessW(
            None, command, None, None, False,
            EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
            env_buffer, cwd, ctypes.byref(startup), ctypes.byref(self._process)), "CreateProcessW")
        kernel32.CloseHandle(self._process.hThread)

    def read(self, size=65536):
        """Raw bytes, blocking until there are some; b"" once the program has gone."""
        buffer = ctypes.create_string_buffer(size)
        count = wintypes.DWORD()
        if not kernel32.ReadFile(self._out, buffer, size, ctypes.byref(count), None):
            return b""
        return buffer.raw[:count.value]

    def write(self, text):
        data = text.encode("utf-8")
        count = wintypes.DWORD()
        _check(kernel32.WriteFile(self._in, data, len(data), ctypes.byref(count), None), "WriteFile")

    def resize(self, rows, cols):
        kernel32.ResizePseudoConsole(self._hpc, COORD(cols, rows))

    def alive(self):
        return kernel32.WaitForSingleObject(self._process.hProcess, 0) == WAIT_TIMEOUT

    def exit_code(self):
        code = wintypes.DWORD()
        kernel32.GetExitCodeProcess(self._process.hProcess, ctypes.byref(code))
        return None if code.value == STILL_ACTIVE else code.value

    def terminate(self):
        kernel32.TerminateProcess(self._process.hProcess, 1)

    def close(self):
        # CLOSING THE PSEUDO CONSOLE ENDS THE OUTPUT PIPE, which is what lets
        # the reader thread return. It can wait for that reader, so the caller
        # keeps reading until this returns.
        kernel32.ClosePseudoConsole(self._hpc)
        kernel32.CloseHandle(self._in)
        kernel32.DeleteProcThreadAttributeList(self._attributes)
        kernel32.CloseHandle(self._process.hProcess)
