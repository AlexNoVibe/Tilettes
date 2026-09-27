import win32api
import win32gui
import win32con
import pythoncom
from win32com.shell import shell, shellcon

def show_context_menu(filepath, hwnd=0):
    try:
        pidl, flags = shell.SHILCreateFromPath(filepath, 0)
        desktop = shell.SHGetDesktopFolder()
        
        # We need to get the parent folder and child pidl
        # Actually, we can get the parent folder from the full pidl
        pidl_child = [pidl[-1]]
        
        if len(pidl) == 1:
            parent_folder = desktop
        else:
            pidl_parent = pidl[:-1]
            parent_folder = desktop.BindToObject(pidl_parent, None, shell.IID_IShellFolder)
        
        try:
            context_menu = parent_folder.GetUIObjectOf(hwnd, pidl_child, shell.IID_IContextMenu, 0)[1]
        except Exception as e:
            print("GetUIObjectOf error:", e)
            return

        menu = win32gui.CreatePopupMenu()
        context_menu.QueryContextMenu(menu, 0, 1, 0x7FFF, shellcon.CMF_NORMAL)
        
        pos = win32gui.GetCursorPos()
        
        cmd = win32gui.TrackPopupMenu(menu, win32con.TPM_LEFTALIGN | win32con.TPM_RETURNCMD | win32con.TPM_RIGHTBUTTON,
                                      pos[0], pos[1], 0, hwnd, None)
        
        if cmd:
            info = (0, hwnd, cmd - 1, None, None, win32con.SW_SHOWNORMAL)
            context_menu.InvokeCommand(info)

        win32gui.DestroyMenu(menu)
    except Exception as e:
        print("Error showing menu:", e)

if __name__ == "__main__":
    import sys
    show_context_menu(sys.executable)
