import sys
import os
import json
import subprocess
from PyQt5.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                             QTabWidget, QScrollArea, QSystemTrayIcon, QMenu, QAction, 
                             QMessageBox, QLabel, QInputDialog, QStyle, QLayout, QSizePolicy)
from PyQt5.QtGui import QIcon, QPixmap, QFont
from PyQt5.QtCore import Qt, QSize, QRect, QPoint, QFileInfo, QEvent

import win32api
import win32gui
import win32con
from win32com.shell import shell, shellcon

# Configuration file path
CONFIG_FILE = "config.json"

class AppConfig:
    def __init__(self):
        self.width = 200
        self.height = 300
        self.x = 100
        self.y = 100
        self.default_width = 200
        self.default_height = 300
        self.default_x = 100
        self.default_y = 100
        self.minimize_instead_of_close = True
        self.minimize_to = 'tray' # 'tray' or 'taskbar'
        self.tabs = [{"name": "Main", "items": []}]

    def load(self):
        if os.path.exists(CONFIG_FILE):
            try:
                with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                    self.width = data.get("width", 200)
                    self.height = data.get("height", 300)
                    self.x = data.get("x", 100)
                    self.y = data.get("y", 100)
                    self.default_width = data.get("default_width", 200)
                    self.default_height = data.get("default_height", 300)
                    self.default_x = data.get("default_x", 100)
                    self.default_y = data.get("default_y", 100)
                    # Migrate old settings
                    if "minimize_to_tray" in data:
                        self.minimize_instead_of_close = data["minimize_to_tray"]
                        self.minimize_to = 'tray' if data["minimize_to_tray"] else 'taskbar'
                    else:
                        self.minimize_instead_of_close = data.get("minimize_instead_of_close", True)
                        self.minimize_to = data.get("minimize_to", 'tray')
                    self.tabs = data.get("tabs", [{"name": "Main", "items": []}])
            except:
                pass

    def save(self):
        with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
            json.dump({
                "width": self.width,
                "height": self.height,
                "x": self.x,
                "y": self.y,
                "default_width": self.default_width,
                "default_height": self.default_height,
                "default_x": self.default_x,
                "default_y": self.default_y,
                "minimize_instead_of_close": self.minimize_instead_of_close,
                "minimize_to": self.minimize_to,
                "tabs": self.tabs
            }, f, indent=4)

config = AppConfig()
config.load()

class FlowLayout(QLayout):
    def __init__(self, parent=None, margin=0, spacing=-1):
        super(FlowLayout, self).__init__(parent)
        self.setContentsMargins(margin, margin, margin, margin)
        self.setSpacing(spacing)
        self.itemList = []

    def __del__(self):
        item = self.takeAt(0)
        while item:
            item = self.takeAt(0)

    def addItem(self, item):
        self.itemList.append(item)

    def count(self):
        return len(self.itemList)

    def itemAt(self, index):
        if index >= 0 and index < len(self.itemList):
            return self.itemList[index]
        return None

    def takeAt(self, index):
        if index >= 0 and index < len(self.itemList):
            return self.itemList.pop(index)
        return None

    def expandingDirections(self):
        return Qt.Orientations(Qt.Orientation(0))

    def hasHeightForWidth(self):
        return True

    def heightForWidth(self, width):
        height = self.doLayout(QRect(0, 0, width, 0), True)
        return height

    def setGeometry(self, rect):
        super(FlowLayout, self).setGeometry(rect)
        self.doLayout(rect, False)

    def sizeHint(self):
        return self.minimumSize()

    def minimumSize(self):
        size = QSize()
        for item in self.itemList:
            size = size.expandedTo(item.minimumSize())
        margin, _, _, _ = self.getContentsMargins()
        size += QSize(2 * margin, 2 * margin)
        return size

    def doLayout(self, rect, testOnly):
        x = rect.x()
        y = rect.y()
        lineHeight = 0
        spacing = self.spacing()

        for item in self.itemList:
            wid = item.widget()
            spaceX = spacing
            spaceY = spacing
            nextX = x + item.sizeHint().width() + spaceX
            if nextX - spaceX > rect.right() and lineHeight > 0:
                x = rect.x()
                y = y + lineHeight + spaceY
                nextX = x + item.sizeHint().width() + spaceX
                lineHeight = 0

            if not testOnly:
                item.setGeometry(QRect(QPoint(x, y), item.sizeHint()))

            x = nextX
            lineHeight = max(lineHeight, item.sizeHint().height())

        return y + lineHeight - rect.y()

class ShortcutItem(QWidget):
    def __init__(self, item_data, parent_tab, app_window):
        super().__init__()
        self.item_data = item_data
        self.parent_tab = parent_tab
        self.app_window = app_window
        self.path = item_data.get("path", "")
        self.size_mode = item_data.get("size", 1) # 0: small, 1: medium, 2: large
        
        self.initUI()
        
    def initUI(self):
        self.layout = QVBoxLayout()
        self.layout.setContentsMargins(2,2,2,2)
        
        self.icon_label = QLabel()
        self.icon_label.setAlignment(Qt.AlignCenter)
        
        self.name_label = QLabel()
        self.name_label.setAlignment(Qt.AlignCenter)
        self.name_label.setWordWrap(True)
        
        self.layout.addWidget(self.icon_label)
        self.layout.addWidget(self.name_label)
        self.setLayout(self.layout)
        
        self.update_appearance()
        
    def update_appearance(self):
        if self.size_mode == 0:
            icon_size = QSize(16, 16)
            font_size = 7
            self.setFixedSize(50, 50)
        elif self.size_mode == 1:
            icon_size = QSize(32, 32)
            font_size = 8
            self.setFixedSize(80, 80)
        else:
            icon_size = QSize(64, 64)
            font_size = 9
            self.setFixedSize(120, 120)
            
        from PyQt5.QtWidgets import QFileIconProvider
        provider = QFileIconProvider()
        info = QFileInfo(self.path)
        icon = provider.icon(info)
        
        self.icon_label.setPixmap(icon.pixmap(icon_size))
        
        name = info.fileName()
        if not name:
            name = self.path
        self.name_label.setText(name)
        
        font = self.name_label.font()
        font.setPointSize(font_size)
        self.name_label.setFont(font)
        
    def mouseDoubleClickEvent(self, event):
        if event.button() == Qt.LeftButton:
            os.startfile(self.path)
            
    def mousePressEvent(self, event):
        if event.button() == Qt.RightButton:
            self.show_context_menu(event.globalPos())
            
    def show_context_menu(self, pos):
        hwnd = int(self.app_window.winId())
        
        try:
            pidl, flags = shell.SHILCreateFromPath(self.path, 0)
            desktop = shell.SHGetDesktopFolder()
            pidl_child = [pidl[-1]]
            
            if len(pidl) == 1:
                parent_folder = desktop
            else:
                pidl_parent = pidl[:-1]
                parent_folder = desktop.BindToObject(pidl_parent, None, shell.IID_IShellFolder)
                
            context_menu = parent_folder.GetUIObjectOf(hwnd, [pidl_child], shell.IID_IContextMenu, 0)[1]
            menu = win32gui.CreatePopupMenu()
            
            # 1 to 0x7FFF are IDs reserved for shell context menu
            context_menu.QueryContextMenu(menu, 0, 1, 0x7FFF, shellcon.CMF_NORMAL)
            
            # Append custom items at the end
            win32gui.AppendMenu(menu, win32con.MF_SEPARATOR, 0, '')
            win32gui.AppendMenu(menu, win32con.MF_STRING, 0x8001, 'Размер: Маленький')
            win32gui.AppendMenu(menu, win32con.MF_STRING, 0x8002, 'Размер: Средний')
            win32gui.AppendMenu(menu, win32con.MF_STRING, 0x8003, 'Размер: Большой')
            win32gui.AppendMenu(menu, win32con.MF_SEPARATOR, 0, '')
            win32gui.AppendMenu(menu, win32con.MF_STRING, 0x8004, 'Удалить слот')
            
            cmd = win32gui.TrackPopupMenu(menu, win32con.TPM_LEFTALIGN | win32con.TPM_RETURNCMD | win32con.TPM_RIGHTBUTTON,
                                          pos.x(), pos.y(), 0, hwnd, None)
            
            if cmd == 0x8001:
                self.set_size(0)
            elif cmd == 0x8002:
                self.set_size(1)
            elif cmd == 0x8003:
                self.set_size(2)
            elif cmd == 0x8004:
                self.parent_tab.remove_item(self)
            elif cmd > 0 and cmd <= 0x7FFF:
                info = (0, hwnd, cmd - 1, None, None, win32con.SW_SHOWNORMAL)
                context_menu.InvokeCommand(info)
                
            win32gui.DestroyMenu(menu)
            
        except Exception as e:
            QMessageBox.warning(self, "Ошибка", f"Не удалось открыть контекстное меню:\n{e}")

    def set_size(self, size):
        self.size_mode = size
        self.item_data["size"] = size
        config.save()
        self.update_appearance()
        self.parent_tab.refresh_layout()

class TabPanel(QWidget):
    def __init__(self, tab_data, app_window):
        super().__init__()
        self.tab_data = tab_data
        self.app_window = app_window
        self.setAcceptDrops(True)
        self.initUI()
        
    def initUI(self):
        self.layout = QVBoxLayout()
        self.layout.setContentsMargins(0, 0, 0, 0)
        self.scroll = QScrollArea()
        self.scroll.setWidgetResizable(True)
        self.scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        
        self.container = QWidget()
        self.flow_layout = FlowLayout(margin=5, spacing=5)
        self.container.setLayout(self.flow_layout)
        
        self.scroll.setWidget(self.container)
        self.layout.addWidget(self.scroll)
        self.setLayout(self.layout)
        
        self.refresh_layout()
        
    def refresh_layout(self):
        # Clear flow layout
        while self.flow_layout.count():
            item = self.flow_layout.takeAt(0)
            widget = item.widget()
            if widget is not None:
                widget.setParent(None)
                
        # Re-add items
        for item_data in self.tab_data["items"]:
            item_widget = ShortcutItem(item_data, self, self.app_window)
            self.flow_layout.addWidget(item_widget)
            
    def dragEnterEvent(self, event):
        if event.mimeData().hasUrls():
            event.acceptProposedAction()
            
    def dropEvent(self, event):
        for url in event.mimeData().urls():
            path = url.toLocalFile()
            if os.path.exists(path):
                self.tab_data["items"].append({"path": path, "size": 1})
        config.save()
        self.refresh_layout()
        
    def remove_item(self, item_widget):
        self.tab_data["items"].remove(item_widget.item_data)
        config.save()
        self.refresh_layout()

from PyQt5.QtWidgets import QDialog, QFormLayout, QSpinBox, QCheckBox, QComboBox, QDialogButtonBox

class SettingsDialog(QDialog):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("Настройки")
        
        layout = QFormLayout(self)
        
        self.w_spin = QSpinBox()
        self.w_spin.setRange(100, 2000)
        self.w_spin.setValue(config.default_width)
        layout.addRow("Ширина по умолчанию:", self.w_spin)
        
        self.h_spin = QSpinBox()
        self.h_spin.setRange(100, 2000)
        self.h_spin.setValue(config.default_height)
        layout.addRow("Высота по умолчанию:", self.h_spin)
        
        self.x_spin = QSpinBox()
        self.x_spin.setRange(0, 4000)
        self.x_spin.setValue(config.default_x)
        layout.addRow("Позиция X по умолчанию:", self.x_spin)
        
        self.y_spin = QSpinBox()
        self.y_spin.setRange(0, 4000)
        self.y_spin.setValue(config.default_y)
        layout.addRow("Позиция Y по умолчанию:", self.y_spin)
        
        self.minimize_close_chk = QCheckBox("Вместо закрытия сворачивать")
        self.minimize_close_chk.setChecked(config.minimize_instead_of_close)
        layout.addRow("", self.minimize_close_chk)
        
        self.minimize_to_combo = QComboBox()
        self.minimize_to_combo.addItems(["В трей", "В панель задач"])
        self.minimize_to_combo.setCurrentIndex(0 if config.minimize_to == 'tray' else 1)
        layout.addRow("Куда сворачивать:", self.minimize_to_combo)
        
        buttons = QDialogButtonBox(QDialogButtonBox.Ok | QDialogButtonBox.Cancel, self)
        buttons.accepted.connect(self.accept)
        buttons.rejected.connect(self.reject)
        layout.addRow(buttons)
        
    def accept(self):
        config.default_width = self.w_spin.value()
        config.default_height = self.h_spin.value()
        config.default_x = self.x_spin.value()
        config.default_y = self.y_spin.value()
        config.minimize_instead_of_close = self.minimize_close_chk.isChecked()
        config.minimize_to = 'tray' if self.minimize_to_combo.currentIndex() == 0 else 'taskbar'
        config.save()
        super().accept()

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.initUI()
        
    def initUI(self):
        self.setWindowTitle("WinPanel")
        
        # Use saved size/pos, but maybe provide an option to reset? 
        # Actually the prompt says "в настройки по дефолту размер окна при открытии, положение окна при открытии"
        # So maybe we should just use default_width/height/x/y upon opening? Or saved?
        # Let's just set default sizes if saved values are 0 or use defaults unconditionally? 
        # No, if it says "default size on open", we should use it on open.
        self.setGeometry(config.default_x, config.default_y, config.default_width, config.default_height)
        
        # Tabs
        self.tabs_widget = QTabWidget()
        self.setCentralWidget(self.tabs_widget)
        
        for tab_data in config.tabs:
            panel = TabPanel(tab_data, self)
            self.tabs_widget.addTab(panel, tab_data["name"])
            
        # Menu
        menubar = self.menuBar()
        settings_menu = menubar.addMenu('Настройки')
        
        add_tab_action = QAction('Добавить вкладку', self)
        add_tab_action.triggered.connect(self.add_tab)
        settings_menu.addAction(add_tab_action)
        
        open_settings_action = QAction('Открыть настройки', self)
        open_settings_action.triggered.connect(self.open_settings)
        settings_menu.addAction(open_settings_action)
        
        # Tray
        self.tray_icon = QSystemTrayIcon(self)
        self.tray_icon.setIcon(self.style().standardIcon(QStyle.SP_ComputerIcon))
        
        tray_menu = QMenu()
        show_action = QAction("Показать", self)
        show_action.triggered.connect(self.showNormal)
        quit_action = QAction("Выход", self)
        quit_action.triggered.connect(self.quit_app)
        
        tray_menu.addAction(show_action)
        tray_menu.addAction(quit_action)
        self.tray_icon.setContextMenu(tray_menu)
        self.tray_icon.activated.connect(self.tray_activated)
        
        self.update_tray_visibility()
            
    def quit_app(self):
        self.tray_icon.hide()
        QApplication.instance().quit()

    def open_settings(self):
        dlg = SettingsDialog(self)
        if dlg.exec_():
            self.update_tray_visibility()

    def update_tray_visibility(self):
        if config.minimize_instead_of_close and config.minimize_to == 'tray':
            self.tray_icon.show()
        else:
            self.tray_icon.hide()
            
    def add_tab(self):
        text, ok = QInputDialog.getText(self, 'Новая вкладка', 'Имя вкладки:')
        if ok and text:
            new_tab = {"name": text, "items": []}
            config.tabs.append(new_tab)
            config.save()
            panel = TabPanel(new_tab, self)
            self.tabs_widget.addTab(panel, text)
            
    def tray_activated(self, reason):
        if reason == QSystemTrayIcon.DoubleClick:
            self.showNormal()
            self.activateWindow()
            
    def changeEvent(self, event):
        if event.type() == QEvent.WindowStateChange:
            if self.isMinimized() and config.minimize_to == 'tray':
                self.hide()
                event.ignore()
                return
        super().changeEvent(event)

    def closeEvent(self, event):
        config.width = self.width()
        config.height = self.height()
        config.x = self.x()
        config.y = self.y()
        config.save()
        
        if config.minimize_instead_of_close:
            event.ignore()
            if config.minimize_to == 'tray':
                self.hide()
                self.tray_icon.showMessage(
                    "WinPanel",
                    "Приложение свернуто в трей",
                    QSystemTrayIcon.Information,
                    2000
                )
            else:
                self.showMinimized()
        else:
            self.tray_icon.hide()
            event.accept()
if __name__ == '__main__':
    app = QApplication(sys.argv)
    app.setQuitOnLastWindowClosed(False)
    
    ex = MainWindow()
    ex.show()
    
    sys.exit(app.exec_())
