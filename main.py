import sys
import os
import json
import subprocess
from PyQt5.QtWidgets import (QApplication, QMainWindow, QWidget, QVBoxLayout, 
                             QTabWidget, QScrollArea, QSystemTrayIcon, QMenu, QAction, 
                             QMessageBox, QLabel, QInputDialog, QStyle, QLayout, QSizePolicy)
from PyQt5.QtGui import QIcon, QPixmap, QFont
from PyQt5.QtCore import Qt, QSize, QRect, QPoint, QFileInfo

# Configuration file path
CONFIG_FILE = "config.json"

class AppConfig:
    def __init__(self):
        self.width = 200
        self.height = 300
        self.x = 100
        self.y = 100
        self.minimize_to_tray = True
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
                    self.minimize_to_tray = data.get("minimize_to_tray", True)
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
                "minimize_to_tray": self.minimize_to_tray,
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
        menu = QMenu(self)
        
        size_menu = QMenu("Размер плитки", self)
        small_action = size_menu.addAction("Маленький")
        med_action = size_menu.addAction("Средний")
        large_action = size_menu.addAction("Большой")
        menu.addMenu(size_menu)
        
        remove_action = menu.addAction("Удалить слот")
        menu.addSeparator()
        
        explorer_menu_action = menu.addAction("Показать в папке")
        properties_action = menu.addAction("Свойства")
        
        action = menu.exec_(pos)
        
        if action == small_action:
            self.set_size(0)
        elif action == med_action:
            self.set_size(1)
        elif action == large_action:
            self.set_size(2)
        elif action == remove_action:
            self.parent_tab.remove_item(self)
        elif action == explorer_menu_action:
            subprocess.run(['explorer.exe', '/select,', self.path])
        elif action == properties_action:
            self.show_windows_properties()
            
    def set_size(self, size):
        self.size_mode = size
        self.item_data["size"] = size
        config.save()
        self.update_appearance()
        self.parent_tab.refresh_layout()
        
    def show_windows_properties(self):
        import win32api
        import win32con
        try:
            win32api.ShellExecute(0, "properties", self.path, None, None, win32con.SW_SHOW)
        except Exception as e:
            QMessageBox.warning(self, "Ошибка", f"Не удалось открыть свойства:\n{e}")

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

class MainWindow(QMainWindow):
    def __init__(self):
        super().__init__()
        self.initUI()
        
    def initUI(self):
        self.setWindowTitle("WinPanel")
        self.setGeometry(config.x, config.y, config.width, config.height)
        
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
        
        self.tray_action = QAction('Сворачивать в трей при закрытии', self, checkable=True)
        self.tray_action.setChecked(config.minimize_to_tray)
        self.tray_action.triggered.connect(self.toggle_tray_setting)
        settings_menu.addAction(self.tray_action)
        
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
        
        if config.minimize_to_tray:
            self.tray_icon.show()
            
    def quit_app(self):
        self.tray_icon.hide()
        QApplication.instance().quit()

    def toggle_tray_setting(self):
        config.minimize_to_tray = self.tray_action.isChecked()
        config.save()
        if config.minimize_to_tray:
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
            
    def closeEvent(self, event):
        config.width = self.width()
        config.height = self.height()
        config.x = self.x()
        config.y = self.y()
        config.save()
        
        if config.minimize_to_tray:
            event.ignore()
            self.hide()
            self.tray_icon.showMessage(
                "WinPanel",
                "Приложение свернуто в трей",
                QSystemTrayIcon.Information,
                2000
            )
        else:
            self.tray_icon.hide()
            event.accept()

if __name__ == '__main__':
    app = QApplication(sys.argv)
    app.setQuitOnLastWindowClosed(False)
    
    ex = MainWindow()
    ex.show()
    
    sys.exit(app.exec_())
