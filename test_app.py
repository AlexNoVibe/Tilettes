import unittest
import os
import json
from main import AppConfig, CONFIG_FILE

class TestAppConfig(unittest.TestCase):
    def setUp(self):
        if os.path.exists(CONFIG_FILE):
            os.remove(CONFIG_FILE)

    def tearDown(self):
        if os.path.exists(CONFIG_FILE):
            os.remove(CONFIG_FILE)

    def test_default_values(self):
        config = AppConfig()
        self.assertEqual(config.width, 200)
        self.assertEqual(config.height, 300)
        self.assertTrue(config.minimize_instead_of_close)
        self.assertEqual(config.minimize_to, 'tray')
        self.assertEqual(len(config.tabs), 1)

    def test_save_and_load(self):
        config = AppConfig()
        config.width = 400
        config.minimize_instead_of_close = False
        config.tabs = [{"name": "TestTab", "items": [{"path": "C:\\test", "size": 2}]}]
        config.save()

        config2 = AppConfig()
        config2.load()
        self.assertEqual(config2.width, 400)
        self.assertFalse(config2.minimize_instead_of_close)
        self.assertEqual(config2.tabs[0]["name"], "TestTab")
        self.assertEqual(config2.tabs[0]["items"][0]["size"], 2)

    def test_corrupted_config(self):
        with open(CONFIG_FILE, 'w', encoding='utf-8') as f:
            f.write("{ invalid json")
        
        config = AppConfig()
        config.load()
        self.assertEqual(config.width, 200) # Should fallback to default

if __name__ == '__main__':
    unittest.main()
