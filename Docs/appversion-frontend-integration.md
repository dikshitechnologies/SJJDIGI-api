# App Version Check — Frontend Integration Guide

Base URL: `https://app.dikshitech.com/sjdigichit/API`

Version check endpoint is public — no JWT token required.

---

## Flow

```
App Startup (App.tsx)
        │
        ▼
GET /api/AppVersion/check?platform=android&versionCode=10
        │
        ▼
 updateAvailable?
   │
   ├── false → Normal Home Screen
   │
   └── true
         │
         ├── mandatory = false → Show popup with [Later] [Update App]
         │
         └── mandatory = true  → Show popup with [Update App] only
                                  (cannot dismiss)
```

---

## API — Check Version

**Endpoint:** `GET /api/AppVersion/check`

**Auth:** Not required (AllowAnonymous)

**Query params:**

| Param | Type | Example | Description |
|---|---|---|---|
| `platform` | string | `android` | `android` or `ios` |
| `versionCode` | int | `10` | Integer build number from your app |

**Example request:**
```
GET /api/AppVersion/check?platform=android&versionCode=10
```

**Response — update available:**
```json
{
  "updateAvailable": true,
  "latestVersion": "1.1.0",
  "latestVersionCode": 11,
  "mandatory": false,
  "message": "A new version is available. Please update the app.",
  "storeUrl": "https://play.google.com/store/apps/details?id=com.dikshitech.saroj"
}
```

**Response — already latest:**
```json
{
  "updateAvailable": false,
  "latestVersion": "1.1.0",
  "latestVersionCode": 11
}
```

---

## React Native Integration

### Step 1 — Get your app's versionCode

`react-native-device-info` package use pannu:

```bash
npm install react-native-device-info
```

```javascript
import DeviceInfo from 'react-native-device-info';

// Android versionCode / iOS CFBundleVersion — integer
const versionCode = DeviceInfo.getBuildNumber(); // returns string e.g. "10"
const versionCodeInt = parseInt(versionCode, 10);
```

---

### Step 2 — Version check function

```javascript
// services/versionService.js

import DeviceInfo from 'react-native-device-info';
import { Platform } from 'react-native';

const BASE_URL = 'https://app.dikshitech.com/sjdigichit/API';

export const checkAppVersion = async () => {
  try {
    const platform    = Platform.OS;                          // 'android' or 'ios'
    const versionCode = parseInt(DeviceInfo.getBuildNumber(), 10);

    const response = await fetch(
      `${BASE_URL}/api/AppVersion/check?platform=${platform}&versionCode=${versionCode}`
    );

    if (!response.ok) return null;

    return await response.json();
  } catch (error) {
    // Network error — silently ignore, don't block the user
    console.warn('Version check failed:', error);
    return null;
  }
};
```

---

### Step 3 — Update Modal component

```jsx
// components/UpdateModal.jsx

import React from 'react';
import {
  Modal,
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Linking,
  BackHandler,
} from 'react-native';

const UpdateModal = ({ visible, mandatory, message, storeUrl, onLater }) => {

  // Mandatory update — block Android hardware back button
  React.useEffect(() => {
    if (!visible || !mandatory) return;
    const sub = BackHandler.addEventListener('hardwareBackPress', () => true);
    return () => sub.remove();
  }, [visible, mandatory]);

  const openStore = () => {
    if (storeUrl) Linking.openURL(storeUrl);
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={() => { if (!mandatory) onLater(); }}
    >
      <View style={styles.overlay}>
        <View style={styles.card}>

          <Text style={styles.title}>
            {mandatory ? 'Update Required' : 'Update Available'}
          </Text>

          <Text style={styles.message}>
            {message || 'A new version is available. Please update the app.'}
          </Text>

          <TouchableOpacity style={styles.updateBtn} onPress={openStore}>
            <Text style={styles.updateBtnText}>Update App</Text>
          </TouchableOpacity>

          {/* Later button only shown for non-mandatory updates */}
          {!mandatory && (
            <TouchableOpacity style={styles.laterBtn} onPress={onLater}>
              <Text style={styles.laterBtnText}>Later</Text>
            </TouchableOpacity>
          )}

        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.5)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 24,
    width: '100%',
    maxWidth: 340,
    alignItems: 'center',
  },
  title: {
    fontSize: 20,
    fontWeight: '700',
    marginBottom: 12,
    color: '#1a1a1a',
    textAlign: 'center',
  },
  message: {
    fontSize: 15,
    color: '#555',
    textAlign: 'center',
    marginBottom: 24,
    lineHeight: 22,
  },
  updateBtn: {
    backgroundColor: '#F37254',
    borderRadius: 8,
    paddingVertical: 12,
    paddingHorizontal: 32,
    width: '100%',
    alignItems: 'center',
    marginBottom: 12,
  },
  updateBtnText: {
    color: '#fff',
    fontWeight: '700',
    fontSize: 16,
  },
  laterBtn: {
    paddingVertical: 8,
  },
  laterBtnText: {
    color: '#888',
    fontSize: 14,
  },
});

export default UpdateModal;
```

---

### Step 4 — Wire into App.tsx

```jsx
// App.tsx

import React, { useEffect, useState } from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { checkAppVersion } from './services/versionService';
import UpdateModal from './components/UpdateModal';

const App = () => {
  const [updateInfo, setUpdateInfo]     = useState(null);
  const [showUpdate, setShowUpdate]     = useState(false);

  useEffect(() => {
    runVersionCheck();
  }, []);

  const runVersionCheck = async () => {
    const result = await checkAppVersion();
    if (result?.updateAvailable) {
      setUpdateInfo(result);
      setShowUpdate(true);
    }
  };

  const handleLater = () => {
    // Only reachable for non-mandatory updates
    setShowUpdate(false);
  };

  return (
    <NavigationContainer>

      {/* Your normal app navigator here */}
      <MainNavigator />

      {/* Update modal sits on top of everything */}
      <UpdateModal
        visible={showUpdate}
        mandatory={updateInfo?.mandatory ?? false}
        message={updateInfo?.message}
        storeUrl={updateInfo?.storeUrl}
        onLater={handleLater}
      />

    </NavigationContainer>
  );
};

export default App;
```

---

## How versionCode comparison works

| Installed | Latest (DB) | Result |
|---|---|---|
| 10 | 11 | `updateAvailable: true` |
| 11 | 11 | `updateAvailable: false` |
| 12 | 11 | `updateAvailable: false` |

String comparison use பண்ணா `"1.9" > "1.10"` wrong result வரும் — integer VersionCode மட்டும் compare பண்றோம்.

---

## Admin — Release a new version

New build Play Store-la publish ஆனா, DB row update பண்ணு:

**Option 1 — SQL directly:**
```sql
UPDATE dbo.AppVersion
SET Version       = '1.1.0',
    VersionCode   = 11,
    IsMandatory   = 0,
    UpdateMessage = 'New features and performance improvements.',
    UpdatedAt     = SYSUTCDATETIME()
WHERE Platform = 'android';
```

**Option 2 — Admin API (JWT required):**
```
POST /api/AppVersion/update
Authorization: Bearer <admin_token>

{
  "platform":      "android",
  "version":       "1.1.0",
  "versionCode":   11,
  "isMandatory":   false,
  "updateMessage": "New features and performance improvements.",
  "storeUrl":      "https://play.google.com/store/apps/details?id=com.dikshitech.saroj"
}
```

**Mandatory update (critical fix):**
```sql
UPDATE dbo.AppVersion
SET Version       = '1.2.0',
    VersionCode   = 12,
    IsMandatory   = 1,
    UpdateMessage = 'Critical security update. Please update to continue.',
    UpdatedAt     = SYSUTCDATETIME()
WHERE Platform = 'android';
```
Users on versionCode < 12 will see the update popup with no "Later" button.

---

## Checklist

- [ ] `react-native-device-info` installed
- [ ] `getBuildNumber()` returns your actual build number
- [ ] Migration 003 run on production DB (SAROJCHIT)
- [ ] `AppVersion` table-la `android` row — correct `VersionCode` and `StoreUrl`
- [ ] App tested: old versionCode → popup shown, new versionCode → no popup
- [ ] Mandatory update tested: no "Later" button, back button blocked
